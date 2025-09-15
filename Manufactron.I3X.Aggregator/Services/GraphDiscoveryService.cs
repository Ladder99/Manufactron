using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Manufactron.I3X.Aggregator.Models;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Aggregator.Services
{
    public class GraphDiscoveryService
    {
        private readonly II3XDataAccess _dataAccess;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GraphDiscoveryService> _logger;
        private const string GRAPH_CACHE_KEY = "manufacturing_graph";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

        // Type patterns for intelligent type recognition
        private readonly List<TypePattern> _typePatterns = new()
        {
            // Orders
            new() { Pattern = "order", ContextType = ManufacturingContextType.Order, Priority = 1 },
            new() { Pattern = "purchase", ContextType = ManufacturingContextType.Order, Priority = 2 },

            // Jobs
            new() { Pattern = "job", ContextType = ManufacturingContextType.Job, Priority = 1 },
            new() { Pattern = "production-job", ContextType = ManufacturingContextType.Job, Priority = 0 },
            new() { Pattern = "work-order", ContextType = ManufacturingContextType.Job, Priority = 2 },

            // Lines
            new() { Pattern = "line", ContextType = ManufacturingContextType.Line, Priority = 1 },
            new() { Pattern = "production-line", ContextType = ManufacturingContextType.Line, Priority = 0 },

            // Equipment
            new() { Pattern = "equipment", ContextType = ManufacturingContextType.Equipment, Priority = 1 },
            new() { Pattern = "machine", ContextType = ManufacturingContextType.Equipment, Priority = 2 },
            new() { Pattern = "mixer", ContextType = ManufacturingContextType.Equipment, Priority = 3 },
            new() { Pattern = "filler", ContextType = ManufacturingContextType.Equipment, Priority = 3 },
            new() { Pattern = "capper", ContextType = ManufacturingContextType.Equipment, Priority = 3 },

            // Operators
            new() { Pattern = "operator", ContextType = ManufacturingContextType.Operator, Priority = 1 },
            new() { Pattern = "worker", ContextType = ManufacturingContextType.Operator, Priority = 2 },
            new() { Pattern = "user", ContextType = ManufacturingContextType.Operator, Priority = 3 },

            // Material
            new() { Pattern = "material", ContextType = ManufacturingContextType.MaterialBatch, Priority = 1 },
            new() { Pattern = "batch", ContextType = ManufacturingContextType.MaterialBatch, Priority = 1 },
            new() { Pattern = "ingredient", ContextType = ManufacturingContextType.MaterialBatch, Priority = 2 },

            // Customers
            new() { Pattern = "customer", ContextType = ManufacturingContextType.Customer, Priority = 1 },
            new() { Pattern = "client", ContextType = ManufacturingContextType.Customer, Priority = 2 },

            // Maintenance
            new() { Pattern = "maintenance", ContextType = ManufacturingContextType.MaintenanceTeam, Priority = 1 },
            new() { Pattern = "technician", ContextType = ManufacturingContextType.MaintenanceTeam, Priority = 2 },

            // Quality
            new() { Pattern = "quality", ContextType = ManufacturingContextType.QualityInspector, Priority = 1 },
            new() { Pattern = "inspector", ContextType = ManufacturingContextType.QualityInspector, Priority = 1 },
        };

        public GraphDiscoveryService(
            II3XDataAccess dataAccess,
            IMemoryCache cache,
            ILogger<GraphDiscoveryService> logger)
        {
            _dataAccess = dataAccess;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ManufacturingGraph> DiscoverGraphAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _cache.TryGetValue(GRAPH_CACHE_KEY, out ManufacturingGraph cachedGraph))
            {
                _logger.LogDebug("Returning cached manufacturing graph");
                return cachedGraph;
            }

            _logger.LogInformation("Discovering manufacturing graph structure");
            var graph = new ManufacturingGraph();

            // Step 1: Discover all types in the system
            var types = await _dataAccess.GetObjectTypesAsync();
            _logger.LogInformation("Discovered {Count} object types", types.Count);

            // Step 2: Discover all objects (instances)
            var objects = await _dataAccess.GetObjectsAsync(includeMetadata: true);
            _logger.LogInformation("Discovered {Count} objects", objects.Count);

            // Step 3: Build nodes from objects
            foreach (var obj in objects)
            {
                var node = CreateGraphNode(obj);
                graph.Nodes[node.ElementId] = node;

                // Initialize adjacency list for this node
                if (!graph.AdjacencyList.ContainsKey(node.ElementId))
                    graph.AdjacencyList[node.ElementId] = new List<string>();
            }

            // Step 4: Build edges from relationships
            foreach (var obj in objects)
            {
                if (obj.Relationships != null)
                {
                    foreach (var relationship in obj.Relationships)
                    {
                        foreach (var targetId in relationship.Value)
                        {
                            var edge = new GraphEdge
                            {
                                FromNodeId = obj.ElementId,
                                ToNodeId = targetId,
                                RelationshipType = relationship.Key,
                                Direction = DetermineEdgeDirection(relationship.Key)
                            };
                            graph.Edges.Add(edge);

                            // Update adjacency list
                            if (!graph.AdjacencyList[obj.ElementId].Contains(targetId))
                                graph.AdjacencyList[obj.ElementId].Add(targetId);

                            // Add reverse edge for bidirectional relationships
                            if (edge.Direction == EdgeDirection.Bidirectional)
                            {
                                if (!graph.AdjacencyList.ContainsKey(targetId))
                                    graph.AdjacencyList[targetId] = new List<string>();

                                if (!graph.AdjacencyList[targetId].Contains(obj.ElementId))
                                    graph.AdjacencyList[targetId].Add(obj.ElementId);
                            }
                        }
                    }
                }
            }

            graph.LastUpdated = DateTime.UtcNow;

            // Cache the graph
            _cache.Set(GRAPH_CACHE_KEY, graph, _cacheExpiration);

            _logger.LogInformation("Graph discovery complete. Nodes: {NodeCount}, Edges: {EdgeCount}",
                graph.Nodes.Count, graph.Edges.Count);

            return graph;
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

            // Determine service source from namespace or attributes
            if (instance.Attributes?.TryGetValue("_source", out var source) == true)
            {
                node.ServiceSource = source.ToString();
            }
            else if (!string.IsNullOrEmpty(instance.NamespaceUri))
            {
                if (instance.NamespaceUri.Contains("erp"))
                    node.ServiceSource = "ERP";
                else if (instance.NamespaceUri.Contains("mes"))
                    node.ServiceSource = "MES";
                else if (instance.NamespaceUri.Contains("scada"))
                    node.ServiceSource = "SCADA";
            }

            // Intelligently determine context type
            node.ContextType = DetermineContextType(instance);

            return node;
        }

        private ManufacturingContextType DetermineContextType(Instance instance)
        {
            // Check type ID and element ID against patterns
            var candidates = new List<(ManufacturingContextType type, int priority)>();

            foreach (var pattern in _typePatterns)
            {
                if (!string.IsNullOrEmpty(instance.TypeId) &&
                    instance.TypeId.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((pattern.ContextType, pattern.Priority));
                }
                else if (!string.IsNullOrEmpty(instance.ElementId) &&
                         instance.ElementId.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((pattern.ContextType, pattern.Priority + 10)); // Lower priority for ID matches
                }
                else if (!string.IsNullOrEmpty(instance.Name) &&
                         instance.Name.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((pattern.ContextType, pattern.Priority + 20)); // Even lower for name matches
                }
            }

            // Also check attributes for hints
            if (instance.Attributes != null)
            {
                if (instance.Attributes.ContainsKey("customerId") || instance.Attributes.ContainsKey("customerName"))
                    candidates.Add((ManufacturingContextType.Order, 0));
                if (instance.Attributes.ContainsKey("jobId") || instance.Attributes.ContainsKey("plannedQuantity"))
                    candidates.Add((ManufacturingContextType.Job, 0));
                if (instance.Attributes.ContainsKey("lineId") || instance.Attributes.ContainsKey("OEE"))
                    candidates.Add((ManufacturingContextType.Line, 0));
                if (instance.Attributes.ContainsKey("equipmentId") || instance.Attributes.ContainsKey("serialNumber"))
                    candidates.Add((ManufacturingContextType.Equipment, 0));
                if (instance.Attributes.ContainsKey("operatorId") || instance.Attributes.ContainsKey("shift"))
                    candidates.Add((ManufacturingContextType.Operator, 0));
                if (instance.Attributes.ContainsKey("batchId") || instance.Attributes.ContainsKey("material"))
                    candidates.Add((ManufacturingContextType.MaterialBatch, 0));
            }

            // Return the best match (lowest priority number)
            if (candidates.Any())
            {
                return candidates.OrderBy(c => c.priority).First().type;
            }

            return ManufacturingContextType.Unknown;
        }

        private EdgeDirection DetermineEdgeDirection(string relationshipType)
        {
            // Bidirectional relationships
            if (relationshipType.Contains("AssociatedWith") ||
                relationshipType.Contains("RelatedTo"))
                return EdgeDirection.Bidirectional;

            // Reverse relationships (child to parent)
            if (relationshipType.Contains("PartOf") ||
                relationshipType.Contains("BelongsTo") ||
                relationshipType.Contains("ChildOf"))
                return EdgeDirection.Reverse;

            // Default to forward (parent to child)
            return EdgeDirection.Forward;
        }

        public List<GraphPath> FindPathsToContextTypes(
            ManufacturingGraph graph,
            string startNodeId,
            HashSet<ManufacturingContextType> targetTypes)
        {
            var paths = new List<GraphPath>();
            var foundTypes = new HashSet<ManufacturingContextType>();

            // BFS to find shortest paths to each context type
            var queue = new Queue<(string nodeId, GraphPath path)>();
            var visited = new HashSet<string>();

            queue.Enqueue((startNodeId, new GraphPath { NodeIds = { startNodeId } }));
            visited.Add(startNodeId);

            while (queue.Count > 0 && foundTypes.Count < targetTypes.Count)
            {
                var (currentNodeId, currentPath) = queue.Dequeue();

                if (!graph.Nodes.TryGetValue(currentNodeId, out var currentNode))
                    continue;

                // Check if this node represents a target context type we haven't found yet
                if (currentNode.ContextType.HasValue &&
                    targetTypes.Contains(currentNode.ContextType.Value) &&
                    !foundTypes.Contains(currentNode.ContextType.Value))
                {
                    paths.Add(currentPath);
                    foundTypes.Add(currentNode.ContextType.Value);
                }

                // Explore neighbors
                if (graph.AdjacencyList.TryGetValue(currentNodeId, out var neighbors))
                {
                    foreach (var neighborId in neighbors)
                    {
                        if (!visited.Contains(neighborId))
                        {
                            visited.Add(neighborId);
                            var newPath = new GraphPath
                            {
                                NodeIds = new List<string>(currentPath.NodeIds) { neighborId },
                                Cost = currentPath.Cost + 1
                            };

                            // Track the relationship type used
                            var edge = graph.Edges.FirstOrDefault(e =>
                                e.FromNodeId == currentNodeId && e.ToNodeId == neighborId);
                            if (edge != null)
                            {
                                newPath.RelationshipTypes = new List<string>(currentPath.RelationshipTypes)
                                {
                                    edge.RelationshipType
                                };
                            }

                            queue.Enqueue((neighborId, newPath));
                        }
                    }
                }
            }

            return paths;
        }
    }
}