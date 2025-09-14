using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.I3X.ERP.Data
{
    public class ERPMockDataSource : II3XDataSource
    {
        private readonly Dictionary<string, Instance> _instances = new();
        private readonly Dictionary<string, ObjectType> _objectTypes = new();
        private readonly Dictionary<string, List<Relationship>> _relationships = new();
        private Func<I3XUpdate, Task> _updateCallback;
        private readonly Random _random = new();

        public ERPMockDataSource()
        {
            InitializeERPData();
        }

        private void InitializeERPData()
        {
            // Define ERP namespace
            var erpNamespace = new Namespace
            {
                Uri = "http://i3x.manufactron/erp",
                Name = "ERP",
                Description = "Enterprise Resource Planning domain",
                Version = "1.0.0"
            };

            // Define object types
            var customerOrderType = new ObjectType
            {
                ElementId = "customer-order-type",
                Name = "CustomerOrder",
                NamespaceUri = erpNamespace.Uri,
                Description = "Customer order in ERP system",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "orderId", DataType = "string", IsRequired = true },
                    new() { Name = "customerId", DataType = "string", IsRequired = true },
                    new() { Name = "customerName", DataType = "string", IsRequired = true },
                    new() { Name = "product", DataType = "string", IsRequired = true },
                    new() { Name = "quantity", DataType = "number", IsRequired = true },
                    new() { Name = "dueDate", DataType = "string", IsRequired = true },
                    new() { Name = "priority", DataType = "string", IsRequired = true },
                    new() { Name = "status", DataType = "string", IsRequired = true }
                },
                AllowedRelationships = new List<string> { "ForCustomer", "HasJobs", "RequiresMaterial" }
            };

            var customerType = new ObjectType
            {
                ElementId = "customer-type",
                Name = "Customer",
                NamespaceUri = erpNamespace.Uri,
                Description = "Customer entity",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "customerId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "segment", DataType = "string" },
                    new() { Name = "creditLimit", DataType = "number" },
                    new() { Name = "paymentTerms", DataType = "string" }
                }
            };

            var materialType = new ObjectType
            {
                ElementId = "material-batch-type",
                Name = "MaterialBatch",
                NamespaceUri = erpNamespace.Uri,
                Description = "Material batch with supplier info",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "batchId", DataType = "string", IsRequired = true },
                    new() { Name = "material", DataType = "string", IsRequired = true },
                    new() { Name = "supplier", DataType = "string", IsRequired = true },
                    new() { Name = "quantity", DataType = "number", IsRequired = true },
                    new() { Name = "qualityCertificate", DataType = "string" },
                    new() { Name = "expirationDate", DataType = "string" },
                    new() { Name = "cost", DataType = "number" }
                }
            };

            var supplierType = new ObjectType
            {
                ElementId = "supplier-type",
                Name = "Supplier",
                NamespaceUri = erpNamespace.Uri,
                Description = "Supplier entity",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "supplierId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "rating", DataType = "number" },
                    new() { Name = "leadTime", DataType = "number", EngUnit = "days" },
                    new() { Name = "certifications", DataType = "string" }
                }
            };

            _objectTypes["customer-order-type"] = customerOrderType;
            _objectTypes["customer-type"] = customerType;
            _objectTypes["material-batch-type"] = materialType;
            _objectTypes["supplier-type"] = supplierType;

            // Create instances matching the beverage production example

            // Customer: Walmart
            var walmart = new Instance
            {
                ElementId = "customer-walmart",
                Name = "Walmart",
                TypeId = "customer-type",
                NamespaceUri = erpNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["customerId"] = "CUST-001",
                    ["name"] = "Walmart Inc.",
                    ["segment"] = "Retail",
                    ["creditLimit"] = 5000000,
                    ["paymentTerms"] = "Net 30"
                },
                LastUpdated = DateTime.UtcNow
            };

            // Order #12345
            var order12345 = new Instance
            {
                ElementId = "ORD-12345",
                Name = "Premium Cola Order #12345",
                TypeId = "customer-order-type",
                NamespaceUri = erpNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["orderId"] = "ORD-12345",
                    ["customerId"] = "CUST-001",
                    ["customerName"] = "Walmart Inc.",
                    ["product"] = "Premium Cola 500ml",
                    ["quantity"] = 10000,
                    ["dueDate"] = DateTime.UtcNow.AddDays(7).ToString("O"),
                    ["priority"] = "High",
                    ["status"] = "InProduction"
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["ForCustomer"] = new List<string> { "customer-walmart" },
                    ["HasJobs"] = new List<string> { "job-J-2025-001" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Supplier: SweetCo Inc
            var sweetco = new Instance
            {
                ElementId = "supplier-sweetco",
                Name = "SweetCo Inc",
                TypeId = "supplier-type",
                NamespaceUri = erpNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["supplierId"] = "SUPP-001",
                    ["name"] = "SweetCo Inc.",
                    ["rating"] = 4.8,
                    ["leadTime"] = 3,
                    ["certifications"] = "ISO 9001, FSSC 22000"
                },
                LastUpdated = DateTime.UtcNow
            };

            // Material Batch MB-2025-0142
            var batch0142 = new Instance
            {
                ElementId = "batch-MB-2025-0142",
                Name = "Sugar Syrup Batch MB-2025-0142",
                TypeId = "material-batch-type",
                NamespaceUri = erpNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["batchId"] = "MB-2025-0142",
                    ["material"] = "Sugar Syrup",
                    ["supplier"] = "SweetCo Inc.",
                    ["quantity"] = 5000,
                    ["qualityCertificate"] = "QC-2025-0142",
                    ["expirationDate"] = DateTime.UtcNow.AddMonths(6).ToString("O"),
                    ["cost"] = 2500.00
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["SuppliedBy"] = new List<string> { "supplier-sweetco" },
                    ["UsedInJobs"] = new List<string> { "job-J-2025-001" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Additional customers
            var targetCustomer = new Instance
            {
                ElementId = "customer-target",
                Name = "Target Corporation",
                TypeId = "customer-type",
                NamespaceUri = erpNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["customerId"] = "CUST-002",
                    ["name"] = "Target Corporation",
                    ["segment"] = "Retail",
                    ["creditLimit"] = 3000000,
                    ["paymentTerms"] = "Net 45"
                },
                LastUpdated = DateTime.UtcNow
            };

            // Store instances
            _instances["customer-walmart"] = walmart;
            _instances["ORD-12345"] = order12345;
            _instances["supplier-sweetco"] = sweetco;
            _instances["batch-MB-2025-0142"] = batch0142;
            _instances["customer-target"] = targetCustomer;

            // Create relationships
            CreateRelationship("ORD-12345", "ForCustomer", "customer-walmart");
            CreateRelationship("batch-MB-2025-0142", "SuppliedBy", "supplier-sweetco");
            CreateRelationship("ORD-12345", "RequiresMaterial", "batch-MB-2025-0142");
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

        public Task StartAsync(Func<I3XUpdate, Task> updateCallback = null)
        {
            _updateCallback = updateCallback;
            // Start simulating occasional updates
            _ = Task.Run(async () => await SimulateUpdatesAsync());
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _updateCallback = null;
            return Task.CompletedTask;
        }

        private async Task SimulateUpdatesAsync()
        {
            while (_updateCallback != null)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                // Simulate order status changes
                var order = _instances["ORD-12345"];
                if (order != null && _updateCallback != null)
                {
                    var update = new I3XUpdate
                    {
                        ElementId = order.ElementId,
                        Attributes = order.Attributes,
                        Timestamp = DateTime.UtcNow,
                        UpdateType = "value"
                    };

                    await _updateCallback(update);
                }
            }
        }

        public Task<List<Namespace>> GetNamespacesAsync()
        {
            return Task.FromResult(new List<Namespace>
            {
                new Namespace
                {
                    Uri = "http://i3x.manufactron/erp",
                    Name = "ERP",
                    Description = "Enterprise Resource Planning domain",
                    Version = "1.0.0"
                }
            });
        }

        public Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null)
        {
            var types = _objectTypes.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(namespaceUri))
                types = types.Where(t => t.NamespaceUri == namespaceUri);

            return Task.FromResult(types.ToList());
        }

        public Task<ObjectType> GetObjectTypeByIdAsync(string elementId)
        {
            _objectTypes.TryGetValue(elementId, out var type);
            return Task.FromResult(type);
        }

        public Task<List<Instance>> GetInstancesAsync(string typeId = null, int? limit = null, int? offset = null)
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
            return Task.FromResult(instance);
        }

        public Task<List<Instance>> GetRelatedInstancesAsync(string elementId, string relationshipType)
        {
            var related = new List<Instance>();

            if (_relationships.ContainsKey(elementId))
            {
                var rels = _relationships[elementId]
                    .Where(r => r.PredicateType == relationshipType)
                    .Select(r => r.ObjectId);

                foreach (var id in rels)
                {
                    if (_instances.TryGetValue(id, out var instance))
                        related.Add(instance);
                }
            }

            return Task.FromResult(related);
        }

        public Task<List<string>> GetHierarchicalRelationshipsAsync()
        {
            return Task.FromResult(new List<string> { "PartOf", "HasChildren" });
        }

        public Task<List<string>> GetNonHierarchicalRelationshipsAsync()
        {
            return Task.FromResult(new List<string>
            {
                "ForCustomer", "SuppliedBy", "RequiresMaterial", "HasJobs", "UsedInJobs"
            });
        }

        public Task<List<Instance>> GetChildrenAsync(string elementId)
        {
            // ERP doesn't have hierarchical equipment relationships
            return Task.FromResult(new List<Instance>());
        }

        public Task<Instance> GetParentAsync(string elementId)
        {
            // ERP doesn't have hierarchical equipment relationships
            return Task.FromResult<Instance>(null);
        }

        public Task<Dictionary<string, object>> GetValueAsync(string elementId)
        {
            if (_instances.TryGetValue(elementId, out var instance))
                return Task.FromResult(instance.Attributes);

            return Task.FromResult(new Dictionary<string, object>());
        }

        public Task<List<HistoricalValue>> GetHistoryAsync(string elementId, DateTime startTime, DateTime endTime, int? maxPoints = null)
        {
            // Generate mock historical data for orders
            var history = new List<HistoricalValue>();

            if (_instances.TryGetValue(elementId, out var instance))
            {
                var current = startTime;
                while (current <= endTime)
                {
                    history.Add(new HistoricalValue
                    {
                        ElementId = elementId,
                        Timestamp = current,
                        Values = new Dictionary<string, object>(instance.Attributes),
                        Quality = "Good"
                    });

                    current = current.AddHours(1);

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
                        Source = "ERPSystem"
                    });
                }
            }

            return Task.FromResult(updates);
        }

        public Task<List<Relationship>> GetRelationshipsAsync(string elementId, string predicateType = null)
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

            // Build context from ERP perspective
            if (_instances.TryGetValue(elementId, out var instance))
            {
                if (instance.TypeId == "customer-order-type")
                {
                    context.Order = instance;

                    // Get customer
                    var customerRels = await GetRelatedInstancesAsync(elementId, "ForCustomer");
                    if (customerRels.Any())
                        context.AllRelationships["Customer"] = customerRels.Select(c => new Relationship
                        {
                            SubjectId = elementId,
                            PredicateType = "ForCustomer",
                            ObjectId = c.ElementId
                        }).ToList();

                    // Get material
                    var materialRels = await GetRelatedInstancesAsync(elementId, "RequiresMaterial");
                    if (materialRels.Any())
                        context.MaterialBatch = materialRels.First();
                }
                else if (instance.TypeId == "material-batch-type")
                {
                    context.MaterialBatch = instance;

                    // Get supplier
                    var supplierRels = await GetRelatedInstancesAsync(elementId, "SuppliedBy");
                    if (supplierRels.Any())
                        context.AllRelationships["Supplier"] = supplierRels.Select(s => new Relationship
                        {
                            SubjectId = elementId,
                            PredicateType = "SuppliedBy",
                            ObjectId = s.ElementId
                        }).ToList();
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
                    if (_relationships.ContainsKey(id))
                    {
                        var rels = _relationships[id]
                            .Where(r => r.PredicateType == step.RelationshipType);

                        if (step.Direction == "reverse")
                        {
                            // Find relationships where current is object
                            foreach (var allRels in _relationships)
                            {
                                var reverseRels = allRels.Value
                                    .Where(r => r.ObjectId == id && r.PredicateType == step.RelationshipType)
                                    .Select(r => r.SubjectId);
                                nextIds.AddRange(reverseRels);
                            }
                        }
                        else
                        {
                            nextIds.AddRange(rels.Select(r => r.ObjectId));
                        }
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
            // Store subscription details if needed
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
                await Task.Delay(TimeSpan.FromSeconds(10));

                foreach (var id in elementIds)
                {
                    if (_instances.TryGetValue(id, out var instance))
                    {
                        // Simulate occasional updates
                        if (_random.Next(100) < 10) // 10% chance
                        {
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