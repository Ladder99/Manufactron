using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.I3X.MES.Data
{
    public class MESMockDataSource : II3XDataSource
    {
        private readonly Dictionary<string, Instance> _instances = new();
        private readonly Dictionary<string, ObjectType> _objectTypes = new();
        private readonly Dictionary<string, List<Relationship>> _relationships = new();
        private Func<I3XUpdate, Task>? _updateCallback;
        private readonly Random _random = new();
        private System.Threading.Timer? _simulationTimer;

        public MESMockDataSource()
        {
            InitializeMESData();
        }

        private void InitializeMESData()
        {
            // Define MES namespace
            var mesNamespace = new Namespace
            {
                Uri = "http://i3x.manufactron/mes",
                Name = "MES",
                Description = "Manufacturing Execution System domain",
                Version = "1.0.0"
            };

            // Define object types
            var productionJobType = new ObjectType
            {
                ElementId = "production-job-type",
                Name = "ProductionJob",
                NamespaceUri = mesNamespace.Uri,
                Description = "Production job execution",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "jobId", DataType = "string", IsRequired = true },
                    new() { Name = "orderId", DataType = "string", IsRequired = true },
                    new() { Name = "product", DataType = "string", IsRequired = true },
                    new() { Name = "plannedQuantity", DataType = "number", IsRequired = true },
                    new() { Name = "actualQuantity", DataType = "number", IsRequired = true },
                    new() { Name = "startTime", DataType = "string", IsRequired = true },
                    new() { Name = "expectedEndTime", DataType = "string", IsRequired = true },
                    new() { Name = "status", DataType = "string", IsRequired = true }
                },
                AllowedRelationships = new List<string>
                {
                    "ExecutedOn", "ConsumedMaterial", "ProducedBy", "ForOrder"
                }
            };

            var productionLineType = new ObjectType
            {
                ElementId = "production-line-type",
                Name = "ProductionLine",
                NamespaceUri = mesNamespace.Uri,
                Description = "Production line with OEE metrics",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "lineId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "status", DataType = "string", IsRequired = true },
                    new() { Name = "currentJob", DataType = "string" },
                    new() { Name = "OEE", DataType = "number", EngUnit = "%" },
                    new() { Name = "availability", DataType = "number", EngUnit = "%" },
                    new() { Name = "performance", DataType = "number", EngUnit = "%" },
                    new() { Name = "quality", DataType = "number", EngUnit = "%" },
                    new() { Name = "throughput", DataType = "number", EngUnit = "units/hour" }
                },
                AllowedRelationships = new List<string>
                {
                    "HasChildren", "ExecutingJob", "OperatedBy"
                }
            };

            var operatorType = new ObjectType
            {
                ElementId = "operator-type",
                Name = "Operator",
                NamespaceUri = mesNamespace.Uri,
                Description = "Production operator",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "operatorId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "shift", DataType = "string", IsRequired = true },
                    new() { Name = "certifications", DataType = "string" },
                    new() { Name = "teamId", DataType = "string" },
                    new() { Name = "experience", DataType = "number", EngUnit = "years" }
                }
            };

            var shiftType = new ObjectType
            {
                ElementId = "shift-type",
                Name = "Shift",
                NamespaceUri = mesNamespace.Uri,
                Description = "Production shift",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "shiftId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "startTime", DataType = "string", IsRequired = true },
                    new() { Name = "endTime", DataType = "string", IsRequired = true },
                    new() { Name = "operators", DataType = "number" }
                }
            };

            _objectTypes["production-job-type"] = productionJobType;
            _objectTypes["production-line-type"] = productionLineType;
            _objectTypes["operator-type"] = operatorType;
            _objectTypes["shift-type"] = shiftType;

            // Create instances matching the beverage production example

            // Production Line "Line-1"
            var line1 = new Instance
            {
                ElementId = "line-1",
                Name = "Beverage_Line_1",
                TypeId = "production-line-type",
                NamespaceUri = mesNamespace.Uri,
                HasChildren = true,
                Attributes = new Dictionary<string, object>
                {
                    ["lineId"] = "LINE-001",
                    ["name"] = "Beverage Production Line 1",
                    ["status"] = "Running",
                    ["currentJob"] = "job-J-2025-001",
                    ["OEE"] = 82.5,
                    ["availability"] = 95.0,
                    ["performance"] = 90.0,
                    ["quality"] = 96.5,
                    ["throughput"] = 600
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["ExecutingJob"] = new List<string> { "job-J-2025-001" },
                    ["OperatedBy"] = new List<string> { "operator-john-smith" },
                    ["HasChildren"] = new List<string>
                    {
                        "mixer-001", "filler-001", "capper-001",
                        "labeler-001", "palletizer-001"
                    }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Production Job J-2025-001
            var jobJ2025001 = new Instance
            {
                ElementId = "job-J-2025-001",
                Name = "Premium_Soda_Production",
                TypeId = "production-job-type",
                NamespaceUri = mesNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["jobId"] = "J-2025-001",
                    ["orderId"] = "ORD-12345",
                    ["product"] = "Premium Cola 500ml",
                    ["plannedQuantity"] = 10000,
                    ["actualQuantity"] = 9850,
                    ["startTime"] = DateTime.UtcNow.Date.AddHours(8).ToString("O"),
                    ["expectedEndTime"] = DateTime.UtcNow.Date.AddHours(16).ToString("O"),
                    ["status"] = "Running"
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["ExecutedOn"] = new List<string> { "line-1" },
                    ["ConsumedMaterial"] = new List<string> { "batch-MB-2025-0142" },
                    ["ProducedBy"] = new List<string> { "operator-john-smith" },
                    ["ForOrder"] = new List<string> { "ORD-12345" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Operator John Smith
            var johnSmith = new Instance
            {
                ElementId = "operator-john-smith",
                Name = "John Smith",
                TypeId = "operator-type",
                NamespaceUri = mesNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["operatorId"] = "OP-001",
                    ["name"] = "John Smith",
                    ["shift"] = "Day Shift A",
                    ["certifications"] = "Line-1 Certified, Filler Specialist",
                    ["teamId"] = "TEAM-A",
                    ["experience"] = 5
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["Operating"] = new List<string> { "line-1" },
                    ["AssignedToJobs"] = new List<string> { "job-J-2025-001" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Day Shift A
            var dayShiftA = new Instance
            {
                ElementId = "shift-day-a",
                Name = "Day Shift A",
                TypeId = "shift-type",
                NamespaceUri = mesNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["shiftId"] = "SHIFT-A",
                    ["name"] = "Day Shift A",
                    ["startTime"] = "08:00:00",
                    ["endTime"] = "16:00:00",
                    ["operators"] = 12
                },
                LastUpdated = DateTime.UtcNow
            };

            // Additional operators
            var janeOperator = new Instance
            {
                ElementId = "operator-jane-doe",
                Name = "Jane Doe",
                TypeId = "operator-type",
                NamespaceUri = mesNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["operatorId"] = "OP-002",
                    ["name"] = "Jane Doe",
                    ["shift"] = "Night Shift B",
                    ["certifications"] = "Line-1 Certified, Quality Inspector",
                    ["teamId"] = "TEAM-B",
                    ["experience"] = 8
                },
                LastUpdated = DateTime.UtcNow
            };

            // Additional job for history
            var jobJ2025002 = new Instance
            {
                ElementId = "job-J-2025-002",
                Name = "Premium_Soda_Production_Batch_2",
                TypeId = "production-job-type",
                NamespaceUri = mesNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["jobId"] = "J-2025-002",
                    ["orderId"] = "ORD-12346",
                    ["product"] = "Premium Cola 500ml",
                    ["plannedQuantity"] = 8000,
                    ["actualQuantity"] = 7950,
                    ["startTime"] = DateTime.UtcNow.Date.AddDays(-1).AddHours(8).ToString("O"),
                    ["expectedEndTime"] = DateTime.UtcNow.Date.AddDays(-1).AddHours(14).ToString("O"),
                    ["status"] = "Completed"
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["ExecutedOn"] = new List<string> { "line-1" },
                    ["ConsumedMaterial"] = new List<string> { "batch-MB-2025-0142" },
                    ["ProducedBy"] = new List<string> { "operator-jane-doe" }
                },
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            };

            // Store instances
            _instances["line-1"] = line1;
            _instances["job-J-2025-001"] = jobJ2025001;
            _instances["operator-john-smith"] = johnSmith;
            _instances["shift-day-a"] = dayShiftA;
            _instances["operator-jane-doe"] = janeOperator;
            _instances["job-J-2025-002"] = jobJ2025002;

            // Create relationships
            CreateRelationship("job-J-2025-001", "ExecutedOn", "line-1");
            CreateRelationship("job-J-2025-001", "ProducedBy", "operator-john-smith");
            CreateRelationship("line-1", "ExecutingJob", "job-J-2025-001");
            CreateRelationship("line-1", "OperatedBy", "operator-john-smith");
        }

        private void CreateRelationship(string subjectId, string predicateType, string objectId)
        {
            var relationship = new Relationship
            {
                SubjectId = subjectId,
                PredicateType = predicateType,
                ObjectId = objectId,
                EstablishedAt = DateTime.UtcNow
            };

            if (!_relationships.ContainsKey(subjectId))
                _relationships[subjectId] = new List<Relationship>();

            _relationships[subjectId].Add(relationship);
        }

        public Task StartAsync(Func<I3XUpdate, Task>? updateCallback = null)
        {
            _updateCallback = updateCallback;

            // Start simulating production updates
            _simulationTimer = new System.Threading.Timer(
                async _ => await SimulateProductionUpdates(),
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _simulationTimer?.Dispose();
            _updateCallback = null;
            return Task.CompletedTask;
        }

        private async Task SimulateProductionUpdates()
        {
            if (_updateCallback == null) return;

            // Simulate job progress
            if (_instances.TryGetValue("job-J-2025-001", out var job))
            {
                var actualQty = Convert.ToInt32(job.Attributes["actualQuantity"]);
                if (actualQty < 10000)
                {
                    actualQty += _random.Next(50, 150);
                    job.Attributes["actualQuantity"] = Math.Min(actualQty, 10000);
                    job.LastUpdated = DateTime.UtcNow;

                    await _updateCallback(new I3XUpdate
                    {
                        ElementId = job.ElementId,
                        Attributes = job.Attributes,
                        Timestamp = DateTime.UtcNow,
                        UpdateType = "value"
                    });
                }
            }

            // Simulate OEE fluctuation
            if (_instances.TryGetValue("line-1", out var line))
            {
                var oee = Convert.ToDouble(line.Attributes["OEE"]);
                oee += (_random.NextDouble() - 0.5) * 2; // +/- 1%
                oee = Math.Max(75, Math.Min(95, oee));
                line.Attributes["OEE"] = Math.Round(oee, 1);
                line.LastUpdated = DateTime.UtcNow;

                await _updateCallback(new I3XUpdate
                {
                    ElementId = line.ElementId,
                    Attributes = line.Attributes,
                    Timestamp = DateTime.UtcNow,
                    UpdateType = "value"
                });
            }
        }

        public Task<List<Namespace>> GetNamespacesAsync()
        {
            return Task.FromResult(new List<Namespace>
            {
                new Namespace
                {
                    Uri = "http://i3x.manufactron/mes",
                    Name = "MES",
                    Description = "Manufacturing Execution System domain",
                    Version = "1.0.0"
                }
            });
        }

        public Task<List<ObjectType>> GetObjectTypesAsync(string? namespaceUri = null)
        {
            var types = _objectTypes.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(namespaceUri))
                types = types.Where(t => t.NamespaceUri == namespaceUri);

            return Task.FromResult(types.ToList());
        }

        public Task<ObjectType> GetObjectTypeByIdAsync(string elementId)
        {
            _objectTypes.TryGetValue(elementId, out var type);
            return Task.FromResult(type!);
        }

        public Task<List<Instance>> GetInstancesAsync(string? typeId = null, int? limit = null, int? offset = null)
        {
            var instances = _instances.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(typeId))
                instances = instances.Where(i => i.TypeId == typeId);

            if (offset.HasValue)
                instances = instances.Skip(offset.Value);

            if (limit.HasValue)
                instances = instances.Take(limit.Value);

            return Task.FromResult(instances.ToList());
        }

        public Task<Instance> GetInstanceByIdAsync(string elementId)
        {
            _instances.TryGetValue(elementId, out var instance);
            return Task.FromResult(instance!);
        }

        public Task<List<Instance>> GetRelatedInstancesAsync(string elementId, string relationshipType)
        {
            var related = new List<Instance>();

            if (_instances.TryGetValue(elementId, out var instance) &&
                instance.Relationships.ContainsKey(relationshipType))
            {
                foreach (var relatedId in instance.Relationships[relationshipType])
                {
                    if (_instances.TryGetValue(relatedId, out var relatedInstance))
                        related.Add(relatedInstance);
                }
            }

            return Task.FromResult(related);
        }

        public Task<List<string>> GetHierarchicalRelationshipsAsync()
        {
            return Task.FromResult(new List<string> { "HasChildren", "PartOf" });
        }

        public Task<List<string>> GetNonHierarchicalRelationshipsAsync()
        {
            return Task.FromResult(new List<string>
            {
                "ExecutedOn", "ExecutingJob", "ProducedBy", "OperatedBy",
                "ConsumedMaterial", "ForOrder", "Operating", "AssignedToJobs"
            });
        }

        public Task<List<Instance>> GetChildrenAsync(string elementId)
        {
            var children = new List<Instance>();

            if (_instances.TryGetValue(elementId, out var instance) &&
                instance.Relationships.ContainsKey("HasChildren"))
            {
                foreach (var childId in instance.Relationships["HasChildren"])
                {
                    if (_instances.TryGetValue(childId, out var child))
                        children.Add(child);
                }
            }

            return Task.FromResult(children);
        }

        public Task<Instance> GetParentAsync(string elementId)
        {
            // Find parent by checking which instance has this element as a child
            foreach (var instance in _instances.Values)
            {
                if (instance.Relationships.ContainsKey("HasChildren") &&
                    instance.Relationships["HasChildren"].Contains(elementId))
                {
                    return Task.FromResult(instance);
                }
            }

            return Task.FromResult<Instance>(null!);
        }

        public Task<Dictionary<string, object>> GetValueAsync(string elementId)
        {
            if (_instances.TryGetValue(elementId, out var instance))
                return Task.FromResult(instance.Attributes);

            return Task.FromResult(new Dictionary<string, object>());
        }

        public Task<List<HistoricalValue>> GetHistoryAsync(string elementId, DateTime startTime, DateTime endTime, int? maxPoints = null)
        {
            var history = new List<HistoricalValue>();

            if (_instances.TryGetValue(elementId, out var instance))
            {
                var current = startTime;
                var interval = TimeSpan.FromMinutes(15);

                while (current <= endTime)
                {
                    var historicalValues = new Dictionary<string, object>(instance.Attributes);

                    // Simulate historical variations
                    if (instance.TypeId == "production-job-type" &&
                        historicalValues.ContainsKey("actualQuantity"))
                    {
                        var qty = Convert.ToInt32(historicalValues["actualQuantity"]);
                        var hoursFromStart = (current - startTime).TotalHours;
                        historicalValues["actualQuantity"] = Math.Min(qty, (int)(hoursFromStart * 1200));
                    }

                    if (instance.TypeId == "production-line-type" &&
                        historicalValues.ContainsKey("OEE"))
                    {
                        var oee = Convert.ToDouble(historicalValues["OEE"]);
                        historicalValues["OEE"] = oee + (_random.NextDouble() - 0.5) * 5;
                    }

                    history.Add(new HistoricalValue
                    {
                        ElementId = elementId,
                        Timestamp = current,
                        Values = historicalValues,
                        Quality = "Good"
                    });

                    current = current.Add(interval);

                    if (maxPoints.HasValue && history.Count >= maxPoints.Value)
                        break;
                }
            }

            return Task.FromResult(history);
        }

        public Task<List<ValueUpdate>> UpdateInstanceValuesAsync(List<string> elementIds, List<Dictionary<string, object>> values)
        {
            var updates = new List<ValueUpdate>();

            for (int i = 0; i < elementIds.Count && i < values.Count; i++)
            {
                if (_instances.TryGetValue(elementIds[i], out var instance))
                {
                    foreach (var kvp in values[i])
                    {
                        instance.Attributes[kvp.Key] = kvp.Value;
                    }

                    instance.LastUpdated = DateTime.UtcNow;

                    updates.Add(new ValueUpdate
                    {
                        ElementId = elementIds[i],
                        Values = values[i],
                        Timestamp = DateTime.UtcNow,
                        Source = "MESSystem"
                    });
                }
            }

            return Task.FromResult(updates);
        }

        public Task<List<Relationship>> GetRelationshipsAsync(string elementId, string? predicateType = null)
        {
            if (!_relationships.ContainsKey(elementId))
                return Task.FromResult(new List<Relationship>());

            var rels = _relationships[elementId].AsEnumerable();
            if (!string.IsNullOrEmpty(predicateType))
                rels = rels.Where(r => r.PredicateType == predicateType);

            return Task.FromResult(rels.ToList());
        }

        public Task<Relationship> CreateRelationshipAsync(string subjectId, string predicateType, string objectId)
        {
            var relationship = new Relationship
            {
                SubjectId = subjectId,
                PredicateType = predicateType,
                ObjectId = objectId,
                EstablishedAt = DateTime.UtcNow
            };

            if (!_relationships.ContainsKey(subjectId))
                _relationships[subjectId] = new List<Relationship>();

            _relationships[subjectId].Add(relationship);

            return Task.FromResult(relationship);
        }

        public Task<bool> DeleteRelationshipAsync(string subjectId, string predicateType, string objectId)
        {
            if (!_relationships.ContainsKey(subjectId))
                return Task.FromResult(false);

            var removed = _relationships[subjectId]
                .RemoveAll(r => r.PredicateType == predicateType && r.ObjectId == objectId);

            return Task.FromResult(removed > 0);
        }

        public async Task<ManufacturingContext> BuildManufacturingContextAsync(string elementId)
        {
            var context = new ManufacturingContext();

            if (_instances.TryGetValue(elementId, out var instance))
            {
                if (instance.TypeId == "production-job-type")
                {
                    context.Job = instance;

                    // Get line
                    var lineRels = await GetRelatedInstancesAsync(elementId, "ExecutedOn");
                    if (lineRels.Any())
                        context.Line = lineRels.First();

                    // Get operator
                    var operatorRels = await GetRelatedInstancesAsync(elementId, "ProducedBy");
                    if (operatorRels.Any())
                        context.Operator = operatorRels.First();
                }
                else if (instance.TypeId == "production-line-type")
                {
                    context.Line = instance;

                    // Get current job
                    var jobRels = await GetRelatedInstancesAsync(elementId, "ExecutingJob");
                    if (jobRels.Any())
                        context.Job = jobRels.First();

                    // Get equipment
                    context.Equipment = (await GetChildrenAsync(elementId)).FirstOrDefault();
                }
                else if (instance.TypeId == "operator-type")
                {
                    context.Operator = instance;

                    // Get assigned jobs
                    var jobRels = await GetRelatedInstancesAsync(elementId, "AssignedToJobs");
                    if (jobRels.Any())
                        context.Job = jobRels.First();
                }
            }

            return context;
        }

        public Task<List<Instance>> TraverseGraphAsync(I3XQueryPath queryPath)
        {
            var results = new List<Instance>();
            var currentIds = new List<string> { queryPath.StartElementId };

            foreach (var step in queryPath.Steps)
            {
                var nextIds = new List<string>();

                foreach (var id in currentIds)
                {
                    if (_instances.TryGetValue(id, out var instance) &&
                        instance.Relationships.ContainsKey(step.RelationshipType))
                    {
                        nextIds.AddRange(instance.Relationships[step.RelationshipType]);
                    }
                }

                currentIds = nextIds.Distinct().ToList();
            }

            foreach (var id in currentIds)
            {
                if (_instances.TryGetValue(id, out var instance))
                    results.Add(instance);
            }

            return Task.FromResult(results);
        }

        public Task<string> CreateSubscriptionAsync(List<string> elementIds, bool includeMetadata = false)
        {
            var subscriptionId = Guid.NewGuid().ToString();
            return Task.FromResult(subscriptionId);
        }

        public Task<bool> DeleteSubscriptionAsync(string subscriptionId)
        {
            return Task.FromResult(true);
        }

        public async IAsyncEnumerable<I3XUpdate> SubscribeToUpdatesAsync(List<string> elementIds)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));

                foreach (var id in elementIds)
                {
                    if (_instances.TryGetValue(id, out var instance))
                    {
                        // Simulate production updates
                        if (instance.TypeId == "production-job-type" && _random.Next(100) < 20)
                        {
                            var qty = Convert.ToInt32(instance.Attributes["actualQuantity"]);
                            instance.Attributes["actualQuantity"] = qty + _random.Next(10, 50);

                            yield return new I3XUpdate
                            {
                                ElementId = id,
                                Attributes = instance.Attributes,
                                Timestamp = DateTime.UtcNow,
                                UpdateType = "value"
                            };
                        }
                        else if (instance.TypeId == "production-line-type" && _random.Next(100) < 30)
                        {
                            var oee = Convert.ToDouble(instance.Attributes["OEE"]);
                            instance.Attributes["OEE"] = Math.Round(oee + (_random.NextDouble() - 0.5), 1);

                            yield return new I3XUpdate
                            {
                                ElementId = id,
                                Attributes = instance.Attributes,
                                Timestamp = DateTime.UtcNow,
                                UpdateType = "value"
                            };
                        }
                    }
                }
            }
        }
    }
}