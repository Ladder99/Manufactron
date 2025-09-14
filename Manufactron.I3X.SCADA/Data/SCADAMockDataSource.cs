using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.I3X.SCADA.Data
{
    public class SCADAMockDataSource : II3XDataSource
    {
        private readonly Dictionary<string, Instance> _instances = new();
        private readonly Dictionary<string, ObjectType> _objectTypes = new();
        private readonly Dictionary<string, List<Relationship>> _relationships = new();
        private Func<I3XUpdate, Task>? _updateCallback;
        private readonly Random _random = new();
        private System.Threading.Timer? _simulationTimer;

        public SCADAMockDataSource()
        {
            InitializeSCADAData();
        }

        private void InitializeSCADAData()
        {
            // Define SCADA namespace
            var scadaNamespace = new Namespace
            {
                Uri = "http://i3x.manufactron/scada",
                Name = "SCADA",
                Description = "Supervisory Control and Data Acquisition domain",
                Version = "1.0.0"
            };

            // Define object types
            var equipmentType = new ObjectType
            {
                ElementId = "equipment-type",
                Name = "Equipment",
                NamespaceUri = scadaNamespace.Uri,
                Description = "Manufacturing equipment with sensor data",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "equipmentId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "type", DataType = "string", IsRequired = true },
                    new() { Name = "model", DataType = "string", IsRequired = true },
                    new() { Name = "serialNumber", DataType = "string", IsRequired = true },
                    new() { Name = "state", DataType = "string", IsRequired = true },
                    new() { Name = "productCount", DataType = "number", IsRequired = true },
                    new() { Name = "rejectCount", DataType = "number", IsRequired = true },
                    new() { Name = "efficiency", DataType = "number", EngUnit = "%", IsRequired = true },
                    new() { Name = "temperature", DataType = "number", EngUnit = "°C" },
                    new() { Name = "pressure", DataType = "number", EngUnit = "bar" },
                    new() { Name = "speed", DataType = "number", EngUnit = "rpm" },
                    new() { Name = "lastCalibration", DataType = "string" },
                    new() { Name = "maintenanceHours", DataType = "number", EngUnit = "hours" }
                },
                AllowedRelationships = new List<string>
                {
                    "PartOf", "UpstreamFrom", "DownstreamTo", "MaintainedBy", "CalibratedBy"
                }
            };

            var sensorType = new ObjectType
            {
                ElementId = "sensor-type",
                Name = "Sensor",
                NamespaceUri = scadaNamespace.Uri,
                Description = "Industrial sensor",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "sensorId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "type", DataType = "string", IsRequired = true },
                    new() { Name = "value", DataType = "number", IsRequired = true },
                    new() { Name = "unit", DataType = "string", IsRequired = true },
                    new() { Name = "status", DataType = "string", IsRequired = true },
                    new() { Name = "lastCalibration", DataType = "string" }
                }
            };

            var utilityType = new ObjectType
            {
                ElementId = "utility-type",
                Name = "Utility",
                NamespaceUri = scadaNamespace.Uri,
                Description = "Utility supply (WAGES)",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "utilityId", DataType = "string", IsRequired = true },
                    new() { Name = "name", DataType = "string", IsRequired = true },
                    new() { Name = "type", DataType = "string", IsRequired = true }, // Water, Air, Gas, Electric, Steam
                    new() { Name = "consumption", DataType = "number", IsRequired = true },
                    new() { Name = "unit", DataType = "string", IsRequired = true },
                    new() { Name = "pressure", DataType = "number", EngUnit = "bar" },
                    new() { Name = "temperature", DataType = "number", EngUnit = "°C" },
                    new() { Name = "flowRate", DataType = "number" }
                }
            };

            var environmentType = new ObjectType
            {
                ElementId = "environment-type",
                Name = "Environment",
                NamespaceUri = scadaNamespace.Uri,
                Description = "Environmental conditions",
                Attributes = new List<AttributeDefinition>
                {
                    new() { Name = "locationId", DataType = "string", IsRequired = true },
                    new() { Name = "ambientTemperature", DataType = "number", EngUnit = "°C" },
                    new() { Name = "humidity", DataType = "number", EngUnit = "%" },
                    new() { Name = "cleanRoomStatus", DataType = "string" },
                    new() { Name = "airQuality", DataType = "string" }
                }
            };

            _objectTypes["equipment-type"] = equipmentType;
            _objectTypes["sensor-type"] = sensorType;
            _objectTypes["utility-type"] = utilityType;
            _objectTypes["environment-type"] = environmentType;

            // Create equipment instances for beverage production line

            // Mixer-001
            var mixer001 = new Instance
            {
                ElementId = "mixer-001",
                Name = "Rotary Mixer 1",
                TypeId = "equipment-type",
                ParentId = "line-1",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["equipmentId"] = "EQ-MIX-001",
                    ["name"] = "Rotary Mixer 1",
                    ["type"] = "Mixer",
                    ["model"] = "RM-3000",
                    ["serialNumber"] = "RM3K-2023-0089",
                    ["state"] = "Running",
                    ["productCount"] = 9850,
                    ["rejectCount"] = 0,
                    ["efficiency"] = 99.5,
                    ["temperature"] = 22.5,
                    ["pressure"] = 1.2,
                    ["speed"] = 150,
                    ["lastCalibration"] = DateTime.UtcNow.AddDays(-7).ToString("O"),
                    ["maintenanceHours"] = 4320
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["PartOf"] = new List<string> { "line-1" },
                    ["DownstreamTo"] = new List<string> { "filler-001" },
                    ["MaintainedBy"] = new List<string> { "maintenance-team-a" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Filler-001 (The problematic equipment with calibration drift)
            var filler001 = new Instance
            {
                ElementId = "filler-001",
                Name = "Rotary Filler 1",
                TypeId = "equipment-type",
                ParentId = "line-1",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["equipmentId"] = "EQ-FIL-001",
                    ["name"] = "Rotary Filler 1",
                    ["type"] = "Filler",
                    ["model"] = "RF-5000",
                    ["serialNumber"] = "RF5K-2023-0142",
                    ["state"] = "Running",
                    ["productCount"] = 9850,
                    ["rejectCount"] = 150,  // This is causing the 1.5% waste!
                    ["efficiency"] = 96.5,  // Degrading efficiency
                    ["temperature"] = 23.1,
                    ["pressure"] = 2.1,
                    ["speed"] = 200,
                    ["lastCalibration"] = DateTime.UtcNow.AddDays(-13).ToString("O"), // 13 days ago - needs recalibration!
                    ["maintenanceHours"] = 5280
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["PartOf"] = new List<string> { "line-1" },
                    ["UpstreamFrom"] = new List<string> { "mixer-001" },
                    ["DownstreamTo"] = new List<string> { "capper-001" },
                    ["MaintainedBy"] = new List<string> { "maintenance-team-a" },
                    ["CalibratedBy"] = new List<string> { "quality-team" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Capper-001
            var capper001 = new Instance
            {
                ElementId = "capper-001",
                Name = "Rotary Capper 1",
                TypeId = "equipment-type",
                ParentId = "line-1",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["equipmentId"] = "EQ-CAP-001",
                    ["name"] = "Rotary Capper 1",
                    ["type"] = "Capper",
                    ["model"] = "RC-2000",
                    ["serialNumber"] = "RC2K-2023-0067",
                    ["state"] = "Running",
                    ["productCount"] = 9700,
                    ["rejectCount"] = 5,
                    ["efficiency"] = 99.2,
                    ["temperature"] = 22.8,
                    ["pressure"] = 1.8,
                    ["speed"] = 180,
                    ["lastCalibration"] = DateTime.UtcNow.AddDays(-3).ToString("O"),
                    ["maintenanceHours"] = 3960
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["PartOf"] = new List<string> { "line-1" },
                    ["UpstreamFrom"] = new List<string> { "filler-001" },
                    ["DownstreamTo"] = new List<string> { "labeler-001" },
                    ["MaintainedBy"] = new List<string> { "maintenance-team-a" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Labeler-001
            var labeler001 = new Instance
            {
                ElementId = "labeler-001",
                Name = "High-Speed Labeler 1",
                TypeId = "equipment-type",
                ParentId = "line-1",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["equipmentId"] = "EQ-LAB-001",
                    ["name"] = "High-Speed Labeler 1",
                    ["type"] = "Labeler",
                    ["model"] = "HSL-1500",
                    ["serialNumber"] = "HSL-2023-0034",
                    ["state"] = "Running",
                    ["productCount"] = 9695,
                    ["rejectCount"] = 2,
                    ["efficiency"] = 99.8,
                    ["temperature"] = 22.3,
                    ["pressure"] = 1.0,
                    ["speed"] = 250,
                    ["lastCalibration"] = DateTime.UtcNow.AddDays(-5).ToString("O"),
                    ["maintenanceHours"] = 2880
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["PartOf"] = new List<string> { "line-1" },
                    ["UpstreamFrom"] = new List<string> { "capper-001" },
                    ["DownstreamTo"] = new List<string> { "palletizer-001" },
                    ["MaintainedBy"] = new List<string> { "maintenance-team-a" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Palletizer-001
            var palletizer001 = new Instance
            {
                ElementId = "palletizer-001",
                Name = "Robotic Palletizer 1",
                TypeId = "equipment-type",
                ParentId = "line-1",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["equipmentId"] = "EQ-PAL-001",
                    ["name"] = "Robotic Palletizer 1",
                    ["type"] = "Palletizer",
                    ["model"] = "RP-800",
                    ["serialNumber"] = "RP-2023-0012",
                    ["state"] = "Running",
                    ["productCount"] = 9693,
                    ["rejectCount"] = 0,
                    ["efficiency"] = 99.9,
                    ["temperature"] = 21.5,
                    ["pressure"] = 0,
                    ["speed"] = 20,
                    ["lastCalibration"] = DateTime.UtcNow.AddDays(-10).ToString("O"),
                    ["maintenanceHours"] = 6720
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["PartOf"] = new List<string> { "line-1" },
                    ["UpstreamFrom"] = new List<string> { "labeler-001" },
                    ["MaintainedBy"] = new List<string> { "maintenance-team-a" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Environmental sensors
            var envSensor = new Instance
            {
                ElementId = "env-prod-floor",
                Name = "Production Floor Environment",
                TypeId = "environment-type",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["locationId"] = "PROD-FLOOR-1",
                    ["ambientTemperature"] = 22.0,
                    ["humidity"] = 45.0,
                    ["cleanRoomStatus"] = "Grade B",
                    ["airQuality"] = "Good"
                },
                LastUpdated = DateTime.UtcNow
            };

            // Utility supplies
            var powerSupply = new Instance
            {
                ElementId = "power-main",
                Name = "Main Power Supply",
                TypeId = "utility-type",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["utilityId"] = "UTL-PWR-001",
                    ["name"] = "Main Power Supply",
                    ["type"] = "Electric",
                    ["consumption"] = 125.5,
                    ["unit"] = "kWh",
                    ["flowRate"] = 0
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["Supplies"] = new List<string> { "line-1" }
                },
                LastUpdated = DateTime.UtcNow
            };

            var compressedAir = new Instance
            {
                ElementId = "compressed-air-001",
                Name = "Compressed Air System",
                TypeId = "utility-type",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["utilityId"] = "UTL-AIR-001",
                    ["name"] = "Compressed Air System",
                    ["type"] = "Air",
                    ["consumption"] = 850,
                    ["unit"] = "m³/h",
                    ["pressure"] = 6.5,
                    ["temperature"] = 20,
                    ["flowRate"] = 850
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["Supplies"] = new List<string> { "line-1" }
                },
                LastUpdated = DateTime.UtcNow
            };

            var waterSupply = new Instance
            {
                ElementId = "water-supply-001",
                Name = "Process Water Supply",
                TypeId = "utility-type",
                NamespaceUri = scadaNamespace.Uri,
                Attributes = new Dictionary<string, object>
                {
                    ["utilityId"] = "UTL-WTR-001",
                    ["name"] = "Process Water Supply",
                    ["type"] = "Water",
                    ["consumption"] = 15.2,
                    ["unit"] = "m³/h",
                    ["pressure"] = 3.2,
                    ["temperature"] = 18,
                    ["flowRate"] = 15.2
                },
                Relationships = new Dictionary<string, List<string>>
                {
                    ["Supplies"] = new List<string> { "line-1" }
                },
                LastUpdated = DateTime.UtcNow
            };

            // Store instances
            _instances["mixer-001"] = mixer001;
            _instances["filler-001"] = filler001;
            _instances["capper-001"] = capper001;
            _instances["labeler-001"] = labeler001;
            _instances["palletizer-001"] = palletizer001;
            _instances["env-prod-floor"] = envSensor;
            _instances["power-main"] = powerSupply;
            _instances["compressed-air-001"] = compressedAir;
            _instances["water-supply-001"] = waterSupply;

            // Create relationships
            CreateRelationships();
        }

        private void CreateRelationships()
        {
            // Equipment flow relationships
            CreateRelationship("mixer-001", "DownstreamTo", "filler-001");
            CreateRelationship("filler-001", "UpstreamFrom", "mixer-001");
            CreateRelationship("filler-001", "DownstreamTo", "capper-001");
            CreateRelationship("capper-001", "UpstreamFrom", "filler-001");
            CreateRelationship("capper-001", "DownstreamTo", "labeler-001");
            CreateRelationship("labeler-001", "UpstreamFrom", "capper-001");
            CreateRelationship("labeler-001", "DownstreamTo", "palletizer-001");
            CreateRelationship("palletizer-001", "UpstreamFrom", "labeler-001");

            // Utility relationships
            CreateRelationship("power-main", "Supplies", "line-1");
            CreateRelationship("compressed-air-001", "Supplies", "line-1");
            CreateRelationship("water-supply-001", "Supplies", "line-1");
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

            // Start simulating equipment updates
            _simulationTimer = new System.Threading.Timer(
                async _ => await SimulateEquipmentUpdates(),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15));

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _simulationTimer?.Dispose();
            _updateCallback = null;
            return Task.CompletedTask;
        }

        private async Task SimulateEquipmentUpdates()
        {
            if (_updateCallback == null) return;

            // Simulate filler efficiency degradation (the root cause of waste)
            if (_instances.TryGetValue("filler-001", out var filler))
            {
                var efficiency = Convert.ToDouble(filler.Attributes["efficiency"]);

                // Gradual degradation
                efficiency -= _random.NextDouble() * 0.2; // Degrade by up to 0.2%
                efficiency = Math.Max(94.0, efficiency); // Don't go below 94%

                filler.Attributes["efficiency"] = Math.Round(efficiency, 1);
                filler.Attributes["rejectCount"] = Convert.ToInt32(filler.Attributes["rejectCount"]) + _random.Next(0, 3);
                filler.LastUpdated = DateTime.UtcNow;

                await _updateCallback(new I3XUpdate
                {
                    ElementId = filler.ElementId,
                    Attributes = filler.Attributes,
                    Timestamp = DateTime.UtcNow,
                    UpdateType = "value"
                });
            }

            // Simulate other equipment normal variations
            foreach (var equipment in _instances.Values.Where(i => i.TypeId == "equipment-type" && i.ElementId != "filler-001"))
            {
                if (_random.Next(100) < 30) // 30% chance of update
                {
                    // Small variations in temperature and speed
                    if (equipment.Attributes.ContainsKey("temperature"))
                    {
                        var temp = Convert.ToDouble(equipment.Attributes["temperature"]);
                        equipment.Attributes["temperature"] = Math.Round(temp + (_random.NextDouble() - 0.5) * 0.5, 1);
                    }

                    if (equipment.Attributes.ContainsKey("speed"))
                    {
                        var speed = Convert.ToDouble(equipment.Attributes["speed"]);
                        equipment.Attributes["speed"] = Math.Round(speed + (_random.NextDouble() - 0.5) * 2, 0);
                    }

                    equipment.Attributes["productCount"] = Convert.ToInt32(equipment.Attributes["productCount"]) + _random.Next(5, 15);
                    equipment.LastUpdated = DateTime.UtcNow;

                    await _updateCallback(new I3XUpdate
                    {
                        ElementId = equipment.ElementId,
                        Attributes = equipment.Attributes,
                        Timestamp = DateTime.UtcNow,
                        UpdateType = "value"
                    });
                }
            }

            // Update environmental conditions
            if (_instances.TryGetValue("env-prod-floor", out var env))
            {
                var temp = Convert.ToDouble(env.Attributes["ambientTemperature"]);
                var humidity = Convert.ToDouble(env.Attributes["humidity"]);

                env.Attributes["ambientTemperature"] = Math.Round(temp + (_random.NextDouble() - 0.5) * 0.3, 1);
                env.Attributes["humidity"] = Math.Round(humidity + (_random.NextDouble() - 0.5) * 2, 1);
                env.LastUpdated = DateTime.UtcNow;

                await _updateCallback(new I3XUpdate
                {
                    ElementId = env.ElementId,
                    Attributes = env.Attributes,
                    Timestamp = DateTime.UtcNow,
                    UpdateType = "value"
                });
            }
        }

        // Implement remaining II3XDataSource interface methods...
        // (Similar pattern to ERP and MES services)

        public Task<List<Namespace>> GetNamespacesAsync()
        {
            return Task.FromResult(new List<Namespace>
            {
                new Namespace
                {
                    Uri = "http://i3x.manufactron/scada",
                    Name = "SCADA",
                    Description = "Supervisory Control and Data Acquisition domain",
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
                "UpstreamFrom", "DownstreamTo", "MaintainedBy", "CalibratedBy", "Supplies"
            });
        }

        public Task<List<Instance>> GetChildrenAsync(string elementId)
        {
            // Return equipment that are part of a line
            var children = _instances.Values
                .Where(i => i.ParentId == elementId)
                .ToList();

            return Task.FromResult(children);
        }

        public Task<Instance> GetParentAsync(string elementId)
        {
            if (_instances.TryGetValue(elementId, out var instance) && !string.IsNullOrEmpty(instance.ParentId))
            {
                _instances.TryGetValue(instance.ParentId, out var parent);
                return Task.FromResult(parent!);
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
                var interval = TimeSpan.FromMinutes(5);

                while (current <= endTime)
                {
                    var historicalValues = new Dictionary<string, object>(instance.Attributes);

                    // Simulate historical data for equipment
                    if (instance.ElementId == "filler-001")
                    {
                        // Show degradation over time
                        var hoursFromStart = (endTime - current).TotalHours;
                        var efficiency = 99.0 - (hoursFromStart * 0.1); // Degrade over time
                        historicalValues["efficiency"] = Math.Max(94.0, efficiency);
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
                        Source = "SCADASystem"
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
                if (instance.TypeId == "equipment-type")
                {
                    context.Equipment = instance;

                    // Get parent line
                    if (!string.IsNullOrEmpty(instance.ParentId))
                    {
                        _instances.TryGetValue(instance.ParentId, out var line);
                        context.Line = line;
                    }

                    // Get upstream/downstream equipment
                    context.UpstreamEquipment = await GetRelatedInstancesAsync(elementId, "UpstreamFrom");
                    context.DownstreamEquipment = await GetRelatedInstancesAsync(elementId, "DownstreamTo");
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
                            .Where(r => r.PredicateType == step.RelationshipType)
                            .Select(r => r.ObjectId);

                        nextIds.AddRange(rels);
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
                await Task.Delay(TimeSpan.FromSeconds(3));

                foreach (var id in elementIds)
                {
                    if (_instances.TryGetValue(id, out var instance))
                    {
                        if (instance.TypeId == "equipment-type" && _random.Next(100) < 40)
                        {
                            // Simulate real-time sensor updates
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