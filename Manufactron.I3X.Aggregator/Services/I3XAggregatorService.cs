using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.I3X.Aggregator.Services;

public class I3XAggregatorService : II3XDataAccess
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<I3XAggregatorService> _logger;
    private readonly Dictionary<string, string> _serviceUrls;
    private readonly IServiceProvider _serviceProvider;

    public I3XAggregatorService(
        HttpClient httpClient,
        ILogger<I3XAggregatorService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Load service URLs from configuration
        _serviceUrls = new Dictionary<string, string>();
        var i3xConfig = configuration.GetSection("I3XServices");

        _serviceUrls["ERP"] = i3xConfig["ERP"] ?? "http://localhost:7001";
        _serviceUrls["MES"] = i3xConfig["MES"] ?? "http://localhost:7002";
        _serviceUrls["SCADA"] = i3xConfig["SCADA"] ?? "http://localhost:7003";
    }

    public async Task<List<Namespace>> GetNamespacesAsync()
    {
        var namespaces = new List<Namespace>();

        foreach (var service in _serviceUrls)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{service.Value}/api/i3x/namespaces");
                if (response.IsSuccessStatusCode)
                {
                    var serviceNamespaces = await response.Content.ReadFromJsonAsync<List<Namespace>>();
                    if (serviceNamespaces != null)
                    {
                        // Tag each namespace with its source service
                        foreach (var ns in serviceNamespaces)
                        {
                            ns.Description = $"[{service.Key}] {ns.Description}";
                        }
                        namespaces.AddRange(serviceNamespaces);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching namespaces from {Service}", service.Key);
            }
        }

        return namespaces;
    }

    public async Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null)
    {
        var objectTypes = new List<ObjectType>();

        // Determine which service to query based on namespace
        var targetServices = DetermineTargetServices(namespaceUri);

        foreach (var service in targetServices)
        {
            try
            {
                var url = $"{service.Value}/api/i3x/types";
                if (!string.IsNullOrEmpty(namespaceUri))
                    url += $"?namespaceUri={Uri.EscapeDataString(namespaceUri)}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var types = await response.Content.ReadFromJsonAsync<List<ObjectType>>();
                    if (types != null)
                        objectTypes.AddRange(types);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching object types from {Service}", service.Key);
            }
        }

        return objectTypes;
    }

    public async Task<ObjectType> GetObjectTypeAsync(string elementId)
    {
        // Try each service until we find the type
        foreach (var service in _serviceUrls)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{service.Value}/api/i3x/types/{elementId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ObjectType>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Type {ElementId} not found in {Service}", elementId, service.Key);
            }
        }

        return null;
    }

    public async Task<List<Instance>> GetObjectsAsync(string typeId = null, bool includeMetadata = false)
    {
        var objects = new List<Instance>();

        // Determine which services to query based on type
        var targetServices = DetermineTargetServicesByType(typeId);

        foreach (var service in targetServices)
        {
            try
            {
                var url = $"{service.Value}/api/i3x/objects?includeMetadata={includeMetadata}";
                if (!string.IsNullOrEmpty(typeId))
                    url += $"&typeId={Uri.EscapeDataString(typeId)}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var serviceObjects = await response.Content.ReadFromJsonAsync<List<Instance>>();
                    if (serviceObjects != null)
                    {
                        // Add service source to attributes
                        foreach (var obj in serviceObjects)
                        {
                            obj.Attributes["_source"] = service.Key;
                        }
                        objects.AddRange(serviceObjects);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching objects from {Service}", service.Key);
            }
        }

        return objects;
    }

    public async Task<Instance> GetObjectAsync(string elementId, bool includeMetadata = false)
    {
        // Try each service until we find the object
        foreach (var service in _serviceUrls)
        {
            try
            {
                var url = $"{service.Value}/api/i3x/objects/{elementId}?includeMetadata={includeMetadata}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var obj = await response.Content.ReadFromJsonAsync<Instance>();
                    if (obj != null)
                    {
                        obj.Attributes["_source"] = service.Key;
                        return obj;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Object {ElementId} not found in {Service}", elementId, service.Key);
            }
        }

        return null;
    }

    public async Task<List<Instance>> GetRelationshipsAsync(string elementId, string relationshipType)
    {
        var relationships = new List<Instance>();

        // Get relationships from all services
        foreach (var service in _serviceUrls)
        {
            try
            {
                var url = $"{service.Value}/api/i3x/relationships/{elementId}/{relationshipType}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var serviceRels = await response.Content.ReadFromJsonAsync<List<Instance>>();
                    if (serviceRels != null)
                        relationships.AddRange(serviceRels);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Relationships for {ElementId} not found in {Service}", elementId, service.Key);
            }
        }

        return relationships;
    }

    public async Task<List<Instance>> GetChildrenAsync(string elementId, bool includeMetadata = false)
    {
        var children = new List<Instance>();

        // Try to find the parent in any service, then get its children
        var parent = await GetObjectAsync(elementId, false);
        if (parent != null && parent.Attributes.TryGetValue("_source", out var sourceService))
        {
            try
            {
                var url = $"{_serviceUrls[sourceService.ToString()]}/api/i3x/objects/{elementId}/children?includeMetadata={includeMetadata}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    children = await response.Content.ReadFromJsonAsync<List<Instance>>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching children for {ElementId}", elementId);
            }
        }

        return children ?? new List<Instance>();
    }

    public async Task<Instance> GetParentAsync(string elementId, bool includeMetadata = false)
    {
        // Try each service to find the parent
        foreach (var service in _serviceUrls)
        {
            try
            {
                var url = $"{service.Value}/api/i3x/objects/{elementId}/parent?includeMetadata={includeMetadata}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Instance>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parent for {ElementId} not found in {Service}", elementId, service.Key);
            }
        }

        return null;
    }

    public async Task<Dictionary<string, object>> GetValueAsync(string elementId)
    {
        // Find which service has this element
        var obj = await GetObjectAsync(elementId, false);
        if (obj != null && obj.Attributes.TryGetValue("_source", out var sourceService))
        {
            try
            {
                var url = $"{_serviceUrls[sourceService.ToString()]}/api/i3x/value/{elementId}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching value for {ElementId}", elementId);
            }
        }

        return new Dictionary<string, object>();
    }

    public async Task<List<HistoricalValue>> GetHistoryAsync(
        string elementId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? maxPoints = null)
    {
        // Find which service has this element
        var obj = await GetObjectAsync(elementId, false);
        if (obj != null && obj.Attributes.TryGetValue("_source", out var sourceService))
        {
            try
            {
                var url = $"{_serviceUrls[sourceService.ToString()]}/api/i3x/history/{elementId}";
                var parameters = new List<string>();

                if (startTime.HasValue)
                    parameters.Add($"startTime={startTime.Value:yyyy-MM-ddTHH:mm:ssZ}");
                if (endTime.HasValue)
                    parameters.Add($"endTime={endTime.Value:yyyy-MM-ddTHH:mm:ssZ}");
                if (maxPoints.HasValue)
                    parameters.Add($"maxPoints={maxPoints.Value}");

                if (parameters.Any())
                    url += "?" + string.Join("&", parameters);

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<HistoricalValue>>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history for {ElementId}", elementId);
            }
        }

        return new List<HistoricalValue>();
    }

    public async Task<bool> UpdateValueAsync(string elementId, Dictionary<string, object> values)
    {
        // Find which service owns this element
        var obj = await GetObjectAsync(elementId, false);
        if (obj != null && obj.Attributes.TryGetValue("_source", out var sourceService))
        {
            try
            {
                var url = $"{_serviceUrls[sourceService.ToString()]}/api/i3x/value/{elementId}";
                var response = await _httpClient.PutAsJsonAsync(url, values);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating value for {ElementId}", elementId);
            }
        }

        return false;
    }

    public async Task<ManufacturingContext> GetManufacturingContextAsync(string elementId)
    {
        // Try to get ContextBuilderService from service provider
        var contextBuilder = _serviceProvider.GetService<ContextBuilderService>();

        if (contextBuilder != null)
        {
            // Use the dynamic graph-based context building
            _logger.LogInformation("Using ContextBuilderService for dynamic context building");
            return await contextBuilder.BuildContextAsync(elementId);
        }

        // Fallback to simplified context building if ContextBuilderService not available
        _logger.LogWarning("ContextBuilderService not available, using simplified context building");
        var context = new ManufacturingContext();

        // Get the starting entity
        var instance = await GetObjectAsync(elementId, true);
        if (instance == null)
        {
            _logger.LogWarning("Element not found: {ElementId}", elementId);
            return context;
        }

        // Populate based on what we found
        if (instance.TypeId?.Contains("equipment") == true)
        {
            context.Equipment = instance;

            // Get parent line
            context.Line = await GetParentAsync(elementId, true);

            // Get job from line if available
            if (context.Line?.Attributes?.TryGetValue("currentJob", out var jobId) == true)
            {
                context.Job = await GetObjectAsync(jobId.ToString(), true);

                // Get order, operator, material from job relationships
                if (context.Job?.Relationships != null)
                {
                    if (context.Job.Relationships.TryGetValue("ForOrder", out var orderIds) && orderIds?.Count > 0)
                        context.Order = await GetObjectAsync(orderIds[0], true);

                    if (context.Job.Relationships.TryGetValue("ProducedBy", out var operatorIds) && operatorIds?.Count > 0)
                        context.Operator = await GetObjectAsync(operatorIds[0], true);

                    if (context.Job.Relationships.TryGetValue("ConsumedMaterial", out var batchIds) && batchIds?.Count > 0)
                        context.MaterialBatch = await GetObjectAsync(batchIds[0], true);
                }
            }

            // Get related equipment
            var upstreamEquipment = await GetRelationshipsAsync(elementId, "UpstreamFrom");
            context.UpstreamEquipment = upstreamEquipment ?? new List<Instance>();

            var downstreamEquipment = await GetRelationshipsAsync(elementId, "DownstreamTo");
            context.DownstreamEquipment = downstreamEquipment ?? new List<Instance>();
        }
        else if (instance.TypeId?.Contains("production-line") == true || instance.TypeId?.Contains("line") == true)
        {
            context.Line = instance;

            // Get current job
            if (instance.Attributes?.TryGetValue("currentJob", out var jobId) == true)
            {
                context.Job = await GetObjectAsync(jobId.ToString(), true);

                // Get related entities from job
                if (context.Job?.Relationships != null)
                {
                    if (context.Job.Relationships.TryGetValue("ForOrder", out var orderIds) && orderIds?.Count > 0)
                        context.Order = await GetObjectAsync(orderIds[0], true);

                    if (context.Job.Relationships.TryGetValue("ProducedBy", out var operatorIds) && operatorIds?.Count > 0)
                        context.Operator = await GetObjectAsync(operatorIds[0], true);

                    if (context.Job.Relationships.TryGetValue("ConsumedMaterial", out var batchIds) && batchIds?.Count > 0)
                        context.MaterialBatch = await GetObjectAsync(batchIds[0], true);
                }
            }

            // Get equipment from line's children
            if (instance.Relationships?.TryGetValue("HasChildren", out var equipmentIds) == true && equipmentIds?.Count > 0)
            {
                var fillerId = equipmentIds.FirstOrDefault(id => id.Contains("filler", StringComparison.OrdinalIgnoreCase)) ?? equipmentIds[0];
                context.Equipment = await GetObjectAsync(fillerId, true);
            }
        }
        else if (instance.TypeId?.Contains("job") == true)
        {
            context.Job = instance;

            // Get related entities from relationships
            if (instance.Relationships != null)
            {
                if (instance.Relationships.TryGetValue("ForOrder", out var orderIds) && orderIds?.Count > 0)
                    context.Order = await GetObjectAsync(orderIds[0], true);

                if (instance.Relationships.TryGetValue("ExecutedOn", out var lineIds) && lineIds?.Count > 0)
                {
                    context.Line = await GetObjectAsync(lineIds[0], true);

                    // Get equipment from line
                    if (context.Line?.Relationships?.TryGetValue("HasChildren", out var equipmentIds) == true && equipmentIds?.Count > 0)
                    {
                        var fillerId = equipmentIds.FirstOrDefault(id => id.Contains("filler", StringComparison.OrdinalIgnoreCase)) ?? equipmentIds[0];
                        context.Equipment = await GetObjectAsync(fillerId, true);
                    }
                }

                if (instance.Relationships.TryGetValue("ProducedBy", out var operatorIds) && operatorIds?.Count > 0)
                    context.Operator = await GetObjectAsync(operatorIds[0], true);

                if (instance.Relationships.TryGetValue("ConsumedMaterial", out var batchIds) && batchIds?.Count > 0)
                    context.MaterialBatch = await GetObjectAsync(batchIds[0], true);
            }
        }
        else if (instance.TypeId?.Contains("order") == true)
        {
            context.Order = instance;

            // Get job from order relationships
            if (instance.Relationships?.TryGetValue("HasJobs", out var jobIds) == true && jobIds?.Count > 0)
            {
                context.Job = await GetObjectAsync(jobIds[0], true);

                // Get line, equipment, operator from job
                if (context.Job?.Relationships != null)
                {
                    if (context.Job.Relationships.TryGetValue("ExecutedOn", out var lineIds) && lineIds?.Count > 0)
                    {
                        context.Line = await GetObjectAsync(lineIds[0], true);

                        // Get equipment from line
                        if (context.Line?.Relationships?.TryGetValue("HasChildren", out var equipmentIds) == true && equipmentIds?.Count > 0)
                        {
                            var fillerId = equipmentIds.FirstOrDefault(id => id.Contains("filler", StringComparison.OrdinalIgnoreCase)) ?? equipmentIds[0];
                            context.Equipment = await GetObjectAsync(fillerId, true);
                        }
                    }

                    if (context.Job.Relationships.TryGetValue("ProducedBy", out var operatorIds) && operatorIds?.Count > 0)
                        context.Operator = await GetObjectAsync(operatorIds[0], true);

                    if (context.Job.Relationships.TryGetValue("ConsumedMaterial", out var batchIds) && batchIds?.Count > 0)
                        context.MaterialBatch = await GetObjectAsync(batchIds[0], true);
                }
            }
        }

        _logger.LogInformation("Built context for {ElementId}: Order={Order}, Job={Job}, Line={Line}, Equipment={Equipment}",
            elementId,
            context.Order?.ElementId ?? "null",
            context.Job?.ElementId ?? "null",
            context.Line?.ElementId ?? "null",
            context.Equipment?.ElementId ?? "null");

        return context;
    }

    // Helper methods
    private Dictionary<string, string> DetermineTargetServices(string namespaceUri)
    {
        if (string.IsNullOrEmpty(namespaceUri))
            return _serviceUrls;

        // Route based on namespace
        if (namespaceUri.Contains("business") || namespaceUri.Contains("erp"))
            return new Dictionary<string, string> { ["ERP"] = _serviceUrls["ERP"] };
        if (namespaceUri.Contains("production") || namespaceUri.Contains("mes"))
            return new Dictionary<string, string> { ["MES"] = _serviceUrls["MES"] };
        if (namespaceUri.Contains("equipment") || namespaceUri.Contains("scada"))
            return new Dictionary<string, string> { ["SCADA"] = _serviceUrls["SCADA"] };

        return _serviceUrls;
    }

    private Dictionary<string, string> DetermineTargetServicesByType(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return _serviceUrls;

        // Route based on type
        if (typeId.Contains("order") || typeId.Contains("customer") || typeId.Contains("material"))
            return new Dictionary<string, string> { ["ERP"] = _serviceUrls["ERP"] };
        if (typeId.Contains("job") || typeId.Contains("line") || typeId.Contains("operator"))
            return new Dictionary<string, string> { ["MES"] = _serviceUrls["MES"] };
        if (typeId.Contains("equipment") || typeId.Contains("sensor"))
            return new Dictionary<string, string> { ["SCADA"] = _serviceUrls["SCADA"] };

        return _serviceUrls;
    }
}