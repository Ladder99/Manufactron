using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.I3X.Aggregator.Services;

public class I3XAggregatorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<I3XAggregatorService> _logger;
    private readonly Dictionary<string, string> _serviceUrls;

    public I3XAggregatorService(HttpClient httpClient, ILogger<I3XAggregatorService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure service URLs
        _serviceUrls = new Dictionary<string, string>
        {
            ["ERP"] = "http://localhost:7001",
            ["MES"] = "http://localhost:7002",
            ["SCADA"] = "http://localhost:7003"
        };
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
        var context = new ManufacturingContext();

        // Determine starting point and build context
        var instance = await GetObjectAsync(elementId, true);
        if (instance == null) return context;

        // Build context based on element type
        if (instance.TypeId?.Contains("order") == true)
        {
            context.Order = instance;
            await BuildContextFromOrder(context, elementId);
        }
        else if (instance.TypeId?.Contains("job") == true)
        {
            context.Job = instance;
            await BuildContextFromJob(context, elementId);
        }
        else if (instance.TypeId?.Contains("equipment") == true)
        {
            context.Equipment = instance;
            await BuildContextFromEquipment(context, elementId);
        }

        return context;
    }

    private async Task BuildContextFromOrder(ManufacturingContext context, string orderId)
    {
        // Get job from MES
        var jobs = await GetRelationshipsAsync(orderId, "HasJobs");
        if (jobs?.Count > 0)
        {
            context.Job = jobs[0];
            await BuildContextFromJob(context, jobs[0].ElementId);
        }

        // Get material from ERP
        var materials = await GetRelationshipsAsync(orderId, "RequiresMaterial");
        if (materials?.Count > 0)
        {
            context.MaterialBatch = materials[0];
        }
    }

    private async Task BuildContextFromJob(ManufacturingContext context, string jobId)
    {
        // Get the parent order ID from job's relationships
        if (context.Job?.Relationships?.TryGetValue("ForOrder", out var orderIds) == true && orderIds?.Count > 0)
        {
            // Fetch the full order object from ERP
            context.Order = await GetObjectAsync(orderIds[0], true);
        }

        // Get production line from MES
        var lines = await GetRelationshipsAsync(jobId, "ExecutedOn");
        if (lines?.Count > 0)
        {
            context.Line = lines[0];

            // Get equipment from line's children or relationships
            if (context.Line?.Relationships?.TryGetValue("HasChildren", out var equipmentIds) == true && equipmentIds?.Count > 0)
            {
                // Fetch the first equipment (or the filler for this scenario)
                // In the real scenario, Filler-001 is the problematic equipment
                var fillerId = equipmentIds.FirstOrDefault(id => id.Contains("filler")) ?? equipmentIds[0];
                context.Equipment = await GetObjectAsync(fillerId, true);
            }
            else if (context.Line?.Relationships?.TryGetValue("HasEquipment", out var hasEquipmentIds) == true && hasEquipmentIds?.Count > 0)
            {
                // Alternative relationship name
                context.Equipment = await GetObjectAsync(hasEquipmentIds[0], true);
            }
            else
            {
                // Fallback: try getting children through the children endpoint
                var equipment = await GetChildrenAsync(context.Line?.ElementId ?? lines[0].ElementId, true);
                context.Equipment = equipment?.Count > 0 ? equipment[0] : null;
            }
        }

        // Get operator from MES
        var operators = await GetRelationshipsAsync(jobId, "ProducedBy");
        if (operators?.Count > 0)
        {
            context.Operator = operators[0];
        }

        // Get material batch ID from job's relationships
        if (context.Job?.Relationships?.TryGetValue("ConsumedMaterial", out var batchIds) == true && batchIds?.Count > 0)
        {
            // Fetch the full material batch object from ERP
            context.MaterialBatch = await GetObjectAsync(batchIds[0], true);
        }
    }

    private async Task BuildContextFromEquipment(ManufacturingContext context, string equipmentId)
    {
        // Get parent line from SCADA
        context.Line = await GetParentAsync(equipmentId, true);

        if (context.Line != null)
        {
            // Get current job from line
            var currentJob = context.Line.Attributes?.GetValueOrDefault("currentJob")?.ToString();
            if (!string.IsNullOrEmpty(currentJob))
            {
                context.Job = await GetObjectAsync(currentJob, true);
            }
        }

        // Get upstream/downstream equipment
        var upstreamEquipment = await GetRelationshipsAsync(equipmentId, "UpstreamFrom");
        context.UpstreamEquipment = upstreamEquipment ?? new List<Instance>();

        var downstreamEquipment = await GetRelationshipsAsync(equipmentId, "DownstreamTo");
        context.DownstreamEquipment = downstreamEquipment ?? new List<Instance>();
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