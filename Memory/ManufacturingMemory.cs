#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Manufactron.Memory;

public class ManufacturingMemory : IManufacturingMemory
{
    private readonly ISemanticTextMemory _memory;
    private readonly ILogger<ManufacturingMemory> _logger;
    private readonly ConcurrentDictionary<string, List<MemoryItem>> _localStorage;

    public ManufacturingMemory(
        ISemanticTextMemory memory,
        ILogger<ManufacturingMemory> logger)
    {
        _memory = memory;
        _logger = logger;
        _localStorage = new ConcurrentDictionary<string, List<MemoryItem>>();
        InitializeDefaultKnowledge();
    }

    public async Task StoreKnowledgeAsync(string collection, string id, string text, string description)
    {
        _logger.LogInformation("Storing knowledge in {Collection}: {Id}", collection, id);

        try
        {
            await _memory.SaveInformationAsync(collection, text, id, description);

            var items = _localStorage.GetOrAdd(collection, _ => new List<MemoryItem>());
            lock (items)
            {
                items.Add(new MemoryItem
                {
                    Id = id,
                    Text = text,
                    Description = description,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing knowledge");

            var items = _localStorage.GetOrAdd(collection, _ => new List<MemoryItem>());
            lock (items)
            {
                items.Add(new MemoryItem
                {
                    Id = id,
                    Text = text,
                    Description = description,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    public async Task<List<MemoryResult>> QueryKnowledgeAsync(string query, int limit = 5)
    {
        _logger.LogInformation("Querying knowledge for: {Query}", query);

        var results = new List<MemoryResult>();

        try
        {
            var memoriesEnumerable = _memory.SearchAsync("manufacturing_procedures", query, limit);
            var memories = new List<MemoryQueryResult>();
            await foreach (var memory in memoriesEnumerable)
            {
                memories.Add(memory);
            }

            foreach (var memory in memories)
            {
                results.Add(new MemoryResult
                {
                    Id = memory.Metadata.Id,
                    Text = memory.Metadata.Text,
                    Description = memory.Metadata.Description,
                    Relevance = memory.Relevance
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying semantic memory, using local storage");

            if (_localStorage.TryGetValue("manufacturing_procedures", out var procedureItems))
            {
                List<MemoryItem> itemsCopy;
                lock (procedureItems)
                {
                    itemsCopy = procedureItems.ToList();
                }

                var items = itemsCopy
                    .Where(item => item.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                  item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .Select(item => new MemoryResult
                    {
                        Id = item.Id,
                        Text = item.Text,
                        Description = item.Description,
                        Relevance = 0.7
                    })
                    .ToList();

                results.AddRange(items);
            }
        }

        return results;
    }

    public async Task StoreIncidentResolutionAsync(string incidentId, string issue, string resolution)
    {
        _logger.LogInformation("Storing incident resolution: {IncidentId}", incidentId);

        var metadata = new Dictionary<string, object>
        {
            ["IncidentId"] = incidentId,
            ["Issue"] = issue,
            ["Resolution"] = resolution,
            ["Timestamp"] = DateTime.UtcNow
        };

        var text = $"Issue: {issue}\nResolution: {resolution}";

        await StoreKnowledgeAsync("incident_history", incidentId, text,
            $"Incident {incidentId}: {issue}");
    }

    public async Task<List<MemoryResult>> GetSimilarIncidentsAsync(string issue, int limit = 3)
    {
        _logger.LogInformation("Finding similar incidents for: {Issue}", issue);

        try
        {
            var memoriesEnumerable = _memory.SearchAsync("incident_history", issue, limit);
            var memories = new List<MemoryQueryResult>();
            await foreach (var memory in memoriesEnumerable)
            {
                memories.Add(memory);
            }

            return memories.Select(m => new MemoryResult
            {
                Id = m.Metadata.Id,
                Text = m.Metadata.Text,
                Description = m.Metadata.Description,
                Relevance = m.Relevance
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching incidents, using local storage");

            if (_localStorage.TryGetValue("incident_history", out var historyItems))
            {
                List<MemoryItem> itemsCopy;
                lock (historyItems)
                {
                    itemsCopy = historyItems.ToList();
                }

                return itemsCopy
                    .Where(item => item.Text.Contains(issue, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .Select(item => new MemoryResult
                    {
                        Id = item.Id,
                        Text = item.Text,
                        Description = item.Description,
                        Relevance = 0.6
                    })
                    .ToList();
            }

            return new List<MemoryResult>();
        }
    }

    private void InitializeDefaultKnowledge()
    {
        _logger.LogInformation("Initializing default manufacturing knowledge");

        var procedures = new[]
        {
            ("sop_line_startup", "Line Startup Procedure",
                @"1. Verify all safety systems are operational
                2. Check material availability
                3. Warm up equipment to operating temperature
                4. Run calibration checks
                5. Start production at reduced speed
                6. Monitor initial output quality
                7. Ramp up to full production speed"),

            ("sop_quality_check", "Quality Control Procedure",
                @"1. Sample products at regular intervals
                2. Measure critical dimensions
                3. Perform visual inspection
                4. Test functional requirements
                5. Record measurements in quality system
                6. Flag non-conforming products
                7. Initiate corrective action if needed"),

            ("sop_changeover", "Production Changeover Procedure",
                @"1. Complete current production run
                2. Stop equipment safely
                3. Clean and sanitize equipment
                4. Change tooling/dies as required
                5. Adjust equipment settings for new product
                6. Run test batch
                7. Verify quality before full production"),

            ("troubleshooting_temp", "Temperature Variance Troubleshooting",
                @"Common causes:
                - Heating element failure
                - Thermocouple drift
                - Cooling system malfunction
                - Insulation degradation
                Resolution steps:
                1. Verify sensor readings
                2. Check heating/cooling systems
                3. Calibrate temperature controllers
                4. Replace faulty components"),

            ("maintenance_preventive", "Preventive Maintenance Schedule",
                @"Daily: Visual inspection, lubrication check
                Weekly: Clean filters, check belt tension
                Monthly: Calibrate instruments, test safety systems
                Quarterly: Replace wear parts, alignment checks
                Annually: Major overhaul, motor testing")
        };

        foreach (var (id, description, text) in procedures)
        {
            Task.Run(async () => await StoreKnowledgeAsync("manufacturing_procedures", id, text, description));
        }
    }

    private class MemoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
#pragma warning restore SKEXP0001