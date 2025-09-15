using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Manufactron.I3X.Aggregator.Models;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Aggregator.Services
{
    public class ContextBuilderService
    {
        private readonly II3XDataAccess _dataAccess;
        private readonly GraphDiscoveryService _graphDiscovery;
        private readonly ILogger<ContextBuilderService> _logger;

        public ContextBuilderService(
            II3XDataAccess dataAccess,
            GraphDiscoveryService graphDiscovery,
            ILogger<ContextBuilderService> logger)
        {
            _dataAccess = dataAccess;
            _graphDiscovery = graphDiscovery;
            _logger = logger;
        }

        public async Task<ManufacturingContext> BuildContextAsync(string startElementId)
        {
            _logger.LogInformation("Building manufacturing context from element: {ElementId}", startElementId);

            // Discover or get cached graph
            var graph = await _graphDiscovery.DiscoverGraphAsync();

            // Initialize context
            var context = new ManufacturingContext();
            var populatedTypes = new HashSet<ManufacturingContextType>();

            // Get the starting node
            if (!graph.Nodes.TryGetValue(startElementId, out var startNode))
            {
                // Node not in cache, fetch it
                var instance = await _dataAccess.GetObjectAsync(startElementId, true);
                if (instance == null)
                {
                    _logger.LogWarning("Starting element not found: {ElementId}", startElementId);
                    return context;
                }

                // Add node to graph for future use
                startNode = CreateGraphNode(instance);
                graph.Nodes[startElementId] = startNode;
            }

            // Populate context with starting node if it matches a context type
            await PopulateContextField(context, startNode, populatedTypes);

            // Determine what context types we still need
            var targetTypes = GetRequiredContextTypes(populatedTypes);

            // Find paths to missing context types
            var paths = _graphDiscovery.FindPathsToContextTypes(graph, startElementId, targetTypes);

            // Traverse paths and populate context
            foreach (var path in paths.OrderBy(p => p.Cost))
            {
                await TraversePathAndPopulateContext(context, path, graph, populatedTypes);
            }

            // If still missing critical fields, do targeted searches
            await FillMissingContextFields(context, graph, startNode);

            // Aggregate all relationships
            AggregateAllRelationships(context);

            _logger.LogInformation("Context building complete. Populated fields: {Fields}",
                string.Join(", ", populatedTypes));

            return context;
        }

        private GraphNode CreateGraphNode(Instance instance)
        {
            var node = new GraphNode
            {
                ElementId = instance.ElementId,
                Name = instance.Name,
                TypeId = instance.TypeId,
                NamespaceUri = instance.NamespaceUri,
                Attributes = instance.Attributes ?? new Dictionary<string, object>(),
                Relationships = instance.Relationships ?? new Dictionary<string, List<string>>()
            };

            // Determine service source
            if (instance.Attributes?.TryGetValue("_source", out var source) == true)
            {
                node.ServiceSource = source.ToString();
            }

            return node;
        }

        private HashSet<ManufacturingContextType> GetRequiredContextTypes(HashSet<ManufacturingContextType> populatedTypes)
        {
            var required = new HashSet<ManufacturingContextType>
            {
                ManufacturingContextType.Order,
                ManufacturingContextType.Job,
                ManufacturingContextType.Line,
                ManufacturingContextType.Equipment,
                ManufacturingContextType.Operator,
                ManufacturingContextType.MaterialBatch
            };

            required.ExceptWith(populatedTypes);
            return required;
        }

        private async Task PopulateContextField(
            ManufacturingContext context,
            GraphNode node,
            HashSet<ManufacturingContextType> populatedTypes)
        {
            if (!node.ContextType.HasValue || populatedTypes.Contains(node.ContextType.Value))
                return;

            // Fetch full instance if we only have partial data
            Instance instance = null;
            if (node.Attributes == null || !node.Attributes.Any())
            {
                instance = await _dataAccess.GetObjectAsync(node.ElementId, true);
            }
            else
            {
                // Create instance from node data
                instance = new Instance
                {
                    ElementId = node.ElementId,
                    Name = node.Name,
                    TypeId = node.TypeId,
                    NamespaceUri = node.NamespaceUri,
                    Attributes = node.Attributes,
                    Relationships = node.Relationships
                };
            }

            if (instance == null) return;

            // Populate appropriate context field based on type
            switch (node.ContextType.Value)
            {
                case ManufacturingContextType.Order:
                    context.Order = instance;
                    populatedTypes.Add(ManufacturingContextType.Order);
                    break;
                case ManufacturingContextType.Job:
                    context.Job = instance;
                    populatedTypes.Add(ManufacturingContextType.Job);
                    break;
                case ManufacturingContextType.Line:
                    context.Line = instance;
                    populatedTypes.Add(ManufacturingContextType.Line);
                    break;
                case ManufacturingContextType.Equipment:
                    if (context.Equipment == null) // Primary equipment
                    {
                        context.Equipment = instance;
                        populatedTypes.Add(ManufacturingContextType.Equipment);
                    }
                    else // Additional equipment
                    {
                        context.UpstreamEquipment.Add(instance);
                    }
                    break;
                case ManufacturingContextType.Operator:
                    context.Operator = instance;
                    populatedTypes.Add(ManufacturingContextType.Operator);
                    break;
                case ManufacturingContextType.MaterialBatch:
                    context.MaterialBatch = instance;
                    populatedTypes.Add(ManufacturingContextType.MaterialBatch);
                    break;
            }
        }

        private async Task TraversePathAndPopulateContext(
            ManufacturingContext context,
            GraphPath path,
            ManufacturingGraph graph,
            HashSet<ManufacturingContextType> populatedTypes)
        {
            // Traverse the path and populate context fields
            foreach (var nodeId in path.NodeIds)
            {
                if (graph.Nodes.TryGetValue(nodeId, out var node))
                {
                    await PopulateContextField(context, node, populatedTypes);
                }
                else
                {
                    // Node not in graph, fetch it
                    var instance = await _dataAccess.GetObjectAsync(nodeId, true);
                    if (instance != null)
                    {
                        var newNode = CreateGraphNode(instance);
                        graph.Nodes[nodeId] = newNode;
                        await PopulateContextField(context, newNode, populatedTypes);
                    }
                }
            }
        }

        private async Task FillMissingContextFields(
            ManufacturingContext context,
            ManufacturingGraph graph,
            GraphNode startNode)
        {
            // Use relationship semantics to find missing fields
            if (startNode.Relationships != null)
            {
                // Look for order relationship
                if (context.Order == null && startNode.Relationships.TryGetValue("ForOrder", out var orderIds))
                {
                    if (orderIds?.Count > 0)
                    {
                        context.Order = await _dataAccess.GetObjectAsync(orderIds[0], true);
                    }
                }

                // Look for job relationship
                if (context.Job == null)
                {
                    if (startNode.Relationships.TryGetValue("ExecutingJob", out var jobIds) ||
                        startNode.Relationships.TryGetValue("CurrentJob", out jobIds))
                    {
                        if (jobIds?.Count > 0)
                        {
                            context.Job = await _dataAccess.GetObjectAsync(jobIds[0], true);
                        }
                    }
                }

                // Look for operator relationship
                if (context.Operator == null)
                {
                    if (startNode.Relationships.TryGetValue("ProducedBy", out var operatorIds) ||
                        startNode.Relationships.TryGetValue("OperatedBy", out operatorIds))
                    {
                        if (operatorIds?.Count > 0)
                        {
                            context.Operator = await _dataAccess.GetObjectAsync(operatorIds[0], true);
                        }
                    }
                }

                // Look for material batch relationship
                if (context.MaterialBatch == null && startNode.Relationships.TryGetValue("ConsumedMaterial", out var batchIds))
                {
                    if (batchIds?.Count > 0)
                    {
                        context.MaterialBatch = await _dataAccess.GetObjectAsync(batchIds[0], true);
                    }
                }

                // Look for equipment in children
                if (context.Equipment == null && startNode.Relationships.TryGetValue("HasChildren", out var childIds))
                {
                    foreach (var childId in childIds)
                    {
                        var child = await _dataAccess.GetObjectAsync(childId, true);
                        if (child?.TypeId?.Contains("equipment", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            // Prioritize filler for the scenario
                            if (context.Equipment == null ||
                                child.ElementId.Contains("filler", StringComparison.OrdinalIgnoreCase))
                            {
                                context.Equipment = child;
                            }
                            else
                            {
                                context.UpstreamEquipment.Add(child);
                            }
                        }
                    }
                }
            }

            // If we have a job but no line, check job's relationships
            if (context.Line == null && context.Job?.Relationships != null)
            {
                if (context.Job.Relationships.TryGetValue("ExecutedOn", out var lineIds) && lineIds?.Count > 0)
                {
                    context.Line = await _dataAccess.GetObjectAsync(lineIds[0], true);
                }
            }

            // If we have equipment but no line, get parent
            if (context.Line == null && context.Equipment != null)
            {
                context.Line = await _dataAccess.GetParentAsync(context.Equipment.ElementId, true);
            }
        }

        private void AggregateAllRelationships(ManufacturingContext context)
        {
            var allRelationships = new Dictionary<string, List<Relationship>>();

            // Collect relationships from all context entities
            var entities = new List<Instance>
            {
                context.Equipment,
                context.Line,
                context.Job,
                context.Order,
                context.MaterialBatch,
                context.Operator
            };

            entities.AddRange(context.UpstreamEquipment ?? new List<Instance>());
            entities.AddRange(context.DownstreamEquipment ?? new List<Instance>());

            foreach (var entity in entities.Where(e => e?.Relationships != null))
            {
                foreach (var rel in entity.Relationships)
                {
                    if (!allRelationships.ContainsKey(rel.Key))
                        allRelationships[rel.Key] = new List<Relationship>();

                    foreach (var targetId in rel.Value)
                    {
                        // Avoid duplicates
                        if (!allRelationships[rel.Key].Any(r =>
                            r.SubjectId == entity.ElementId && r.ObjectId == targetId))
                        {
                            allRelationships[rel.Key].Add(new Relationship
                            {
                                SubjectId = entity.ElementId,
                                PredicateType = rel.Key,
                                ObjectId = targetId
                            });
                        }
                    }
                }
            }

            context.AllRelationships = allRelationships;
        }
    }
}