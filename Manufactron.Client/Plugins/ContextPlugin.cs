using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.Client.Plugins;

public class ContextPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContextPlugin> _logger;
    private readonly string _aggregatorUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContextPlugin(HttpClient httpClient, ILogger<ContextPlugin> logger, string aggregatorUrl)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aggregatorUrl = aggregatorUrl;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [KernelFunction("BuildManufacturingContext")]
    [Description("Build complete manufacturing context from any element (equipment, job, order, etc.)")]
    public async Task<string> BuildManufacturingContextAsync(
        [Description("The element ID to build context from")] string elementId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/context/{elementId}");
            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to build context for {elementId}. Status: {response.StatusCode}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<ManufacturingContext>(json, _jsonOptions);

            if (context == null)
            {
                return $"No context available for {elementId}";
            }

            var summary = new
            {
                StartingElement = elementId,
                Equipment = context.Equipment != null ? new
                {
                    Id = context.Equipment.ElementId,
                    Name = context.Equipment.Name,
                    State = context.Equipment.Attributes?.GetValueOrDefault("state", "Unknown"),
                    OEE = context.Equipment.Attributes?.GetValueOrDefault("OEE", "N/A")
                } : null,
                Line = context.Line != null ? new
                {
                    Id = context.Line.ElementId,
                    Name = context.Line.Name,
                    Status = context.Line.Attributes?.GetValueOrDefault("status", "Unknown")
                } : null,
                Job = context.Job != null ? new
                {
                    Id = context.Job.ElementId,
                    Product = context.Job.Attributes?.GetValueOrDefault("product", "Unknown"),
                    PlannedQuantity = context.Job.Attributes?.GetValueOrDefault("plannedQuantity", "0"),
                    ActualQuantity = context.Job.Attributes?.GetValueOrDefault("actualQuantity", "0"),
                    Status = context.Job.Attributes?.GetValueOrDefault("status", "Unknown")
                } : null,
                Order = context.Order != null ? new
                {
                    Id = context.Order.ElementId,
                    Customer = context.Order.Attributes?.GetValueOrDefault("customerName", "Unknown"),
                    Quantity = context.Order.Attributes?.GetValueOrDefault("quantity", "0"),
                    Priority = context.Order.Attributes?.GetValueOrDefault("priority", "Normal")
                } : null,
                MaterialBatch = context.MaterialBatch != null ? new
                {
                    Id = context.MaterialBatch.ElementId,
                    Material = context.MaterialBatch.Attributes?.GetValueOrDefault("material", "Unknown"),
                    Supplier = context.MaterialBatch.Attributes?.GetValueOrDefault("supplier", "Unknown"),
                    ExpiryDate = context.MaterialBatch.Attributes?.GetValueOrDefault("expiryDate", "N/A")
                } : null,
                Operator = context.Operator != null ? new
                {
                    Id = context.Operator.ElementId,
                    Name = context.Operator.Name,
                    Shift = context.Operator.Attributes?.GetValueOrDefault("shift", "Unknown")
                } : null,
                RelationshipCount = context.AllRelationships?.Count ?? 0
            };

            return JsonSerializer.Serialize(summary, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building context for {ElementId}", elementId);
            return $"Error building manufacturing context: {ex.Message}";
        }
    }

    [KernelFunction("SearchObjects")]
    [Description("Search for objects by name, type, or attributes")]
    public async Task<string> SearchObjectsAsync(
        [Description("Search term to find objects")] string searchTerm,
        [Description("Optional: specific type to filter (e.g., 'equipment', 'job', 'order')")] string? typeFilter = null)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return "Unable to retrieve objects for search";
            }

            var json = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (objects == null || !objects.Any())
            {
                return "No objects available to search";
            }

            var searchLower = searchTerm.ToLower();
            var results = objects.Where(o =>
                (o.ElementId?.ToLower().Contains(searchLower) ?? false) ||
                (o.Name?.ToLower().Contains(searchLower) ?? false) ||
                (o.TypeId?.ToLower().Contains(searchLower) ?? false) ||
                (o.Attributes?.Any(a =>
                    a.Key.ToLower().Contains(searchLower) ||
                    (a.Value?.ToString()?.ToLower().Contains(searchLower) ?? false)) ?? false)
            );

            if (!string.IsNullOrEmpty(typeFilter))
            {
                results = results.Where(o => o.TypeId?.ToLower().Contains(typeFilter.ToLower()) ?? false);
            }

            var resultList = results.Take(10).Select(o => new
            {
                Id = o.ElementId,
                Name = o.Name,
                Type = o.TypeId,
                MatchedIn = GetMatchLocation(o, searchLower)
            }).ToList();

            if (!resultList.Any())
            {
                return $"No objects found matching '{searchTerm}'";
            }

            return JsonSerializer.Serialize(new
            {
                SearchTerm = searchTerm,
                TypeFilter = typeFilter,
                ResultCount = resultList.Count(),
                Results = resultList
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for objects with term {SearchTerm}", searchTerm);
            return $"Error searching objects: {ex.Message}";
        }
    }

    [KernelFunction("GetObjectRelationships")]
    [Description("Get all relationships for a specific object")]
    public async Task<string> GetObjectRelationshipsAsync(
        [Description("The object ID to get relationships for")] string objectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects/{objectId}?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to retrieve object {objectId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var obj = JsonSerializer.Deserialize<Instance>(json, _jsonOptions);

            if (obj?.Relationships == null || !obj.Relationships.Any())
            {
                return $"No relationships found for {objectId}";
            }

            var relationships = new List<object>();
            foreach (var rel in obj.Relationships)
            {
                var relResponse = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects/{objectId}/relationships/{rel.Key}");
                if (relResponse.IsSuccessStatusCode)
                {
                    var relJson = await relResponse.Content.ReadAsStringAsync();
                    var relatedObjects = JsonSerializer.Deserialize<List<Instance>>(relJson, _jsonOptions);

                    relationships.Add(new
                    {
                        Type = rel.Key,
                        Direction = rel.Value,
                        RelatedObjects = relatedObjects?.Select(r => new
                        {
                            Id = r.ElementId,
                            Name = r.Name,
                            Type = r.TypeId
                        })
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                ObjectId = objectId,
                ObjectName = obj.Name,
                ObjectType = obj.TypeId,
                Relationships = relationships
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting relationships for {ObjectId}", objectId);
            return $"Error retrieving relationships: {ex.Message}";
        }
    }

    [KernelFunction("GetProductionHierarchy")]
    [Description("Get the complete production hierarchy (line -> equipment -> sensors)")]
    public async Task<string> GetProductionHierarchyAsync(
        [Description("Optional: specific line ID to start from")] string? lineId = null)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return "Unable to retrieve production hierarchy";
            }

            var json = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (objects == null || !objects.Any())
            {
                return "No objects available";
            }

            var lines = objects.Where(o => o.TypeId?.Contains("line", StringComparison.OrdinalIgnoreCase) ?? false);

            if (!string.IsNullOrEmpty(lineId))
            {
                lines = lines.Where(l => l.ElementId == lineId);
            }

            var hierarchy = new List<object>();
            foreach (var line in lines)
            {
                var equipment = objects.Where(o => o.ParentId == line.ElementId).ToList();

                hierarchy.Add(new
                {
                    Line = new
                    {
                        Id = line.ElementId,
                        Name = line.Name,
                        Status = line.Attributes?.GetValueOrDefault("status", "Unknown"),
                        OEE = line.Attributes?.GetValueOrDefault("OEE", "N/A")
                    },
                    Equipment = equipment.Select(e => new
                    {
                        Id = e.ElementId,
                        Name = e.Name,
                        Type = e.TypeId,
                        State = e.Attributes?.GetValueOrDefault("state", "Unknown"),
                        Sensors = objects.Where(s => s.ParentId == e.ElementId).Select(s => new
                        {
                            Id = s.ElementId,
                            Name = s.Name,
                            Type = s.TypeId,
                            Value = s.Attributes?.GetValueOrDefault("value", "N/A"),
                            Unit = s.Attributes?.GetValueOrDefault("unit", "")
                        })
                    })
                });
            }

            return JsonSerializer.Serialize(new
            {
                TotalLines = hierarchy.Count,
                Hierarchy = hierarchy
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting production hierarchy");
            return $"Error retrieving hierarchy: {ex.Message}";
        }
    }

    private string GetMatchLocation(Instance obj, string searchTerm)
    {
        if (obj.ElementId?.ToLower().Contains(searchTerm) ?? false) return "ID";
        if (obj.Name?.ToLower().Contains(searchTerm) ?? false) return "Name";
        if (obj.TypeId?.ToLower().Contains(searchTerm) ?? false) return "Type";

        var matchedAttr = obj.Attributes?.FirstOrDefault(a =>
            a.Key.ToLower().Contains(searchTerm) ||
            (a.Value?.ToString()?.ToLower().Contains(searchTerm) ?? false));

        if (matchedAttr.HasValue)
        {
            return $"Attribute: {matchedAttr.Value.Key}";
        }

        return "Unknown";
    }
}