using Microsoft.AspNetCore.Mvc;
using Manufactron.I3X.Aggregator.Services;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Aggregator.Controllers;

[ApiController]
[Route("api/i3x")]
public class I3XAggregatorController : ControllerBase
{
    private readonly I3XAggregatorService _aggregator;
    private readonly ILogger<I3XAggregatorController> _logger;

    public I3XAggregatorController(
        I3XAggregatorService aggregator,
        ILogger<I3XAggregatorController> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Get all namespaces from all I3X services
    /// </summary>
    [HttpGet("namespaces")]
    public async Task<ActionResult<List<Namespace>>> GetNamespaces()
    {
        _logger.LogInformation("Aggregating namespaces from all services");
        var namespaces = await _aggregator.GetNamespacesAsync();
        return Ok(namespaces);
    }

    /// <summary>
    /// Get object types, optionally filtered by namespace
    /// </summary>
    [HttpGet("types")]
    public async Task<ActionResult<List<ObjectType>>> GetObjectTypes(
        [FromQuery] string? namespaceUri = null)
    {
        _logger.LogInformation("Aggregating object types, namespace: {Namespace}", namespaceUri);
        var types = await _aggregator.GetObjectTypesAsync(namespaceUri);
        return Ok(types);
    }

    /// <summary>
    /// Get a specific object type by ID
    /// </summary>
    [HttpGet("types/{elementId}")]
    public async Task<ActionResult<ObjectType>> GetObjectType(string elementId)
    {
        _logger.LogInformation("Looking up object type: {ElementId}", elementId);
        var type = await _aggregator.GetObjectTypeAsync(elementId);

        if (type == null)
        {
            _logger.LogWarning("Object type not found: {ElementId}", elementId);
            return NotFound($"Object type '{elementId}' not found in any service");
        }

        return Ok(type);
    }

    /// <summary>
    /// Get all objects, optionally filtered by type
    /// </summary>
    [HttpGet("objects")]
    public async Task<ActionResult<List<Instance>>> GetObjects(
        [FromQuery] string? typeId = null,
        [FromQuery] bool includeMetadata = false)
    {
        _logger.LogInformation("Aggregating objects, type: {TypeId}, metadata: {Metadata}",
            typeId, includeMetadata);
        var objects = await _aggregator.GetObjectsAsync(typeId, includeMetadata);
        return Ok(objects);
    }

    /// <summary>
    /// Get a specific object by ID
    /// </summary>
    [HttpGet("objects/{elementId}")]
    public async Task<ActionResult<Instance>> GetObject(
        string elementId,
        [FromQuery] bool includeMetadata = false)
    {
        _logger.LogInformation("Looking up object: {ElementId}, metadata: {Metadata}",
            elementId, includeMetadata);
        var obj = await _aggregator.GetObjectAsync(elementId, includeMetadata);

        if (obj == null)
        {
            _logger.LogWarning("Object not found: {ElementId}", elementId);
            return NotFound($"Object '{elementId}' not found in any service");
        }

        return Ok(obj);
    }

    /// <summary>
    /// Get relationships for an object
    /// </summary>
    [HttpGet("relationships/{elementId}/{relationshipType}")]
    public async Task<ActionResult<List<Instance>>> GetRelationships(
        string elementId,
        string relationshipType)
    {
        _logger.LogInformation("Getting relationships for {ElementId}, type: {Type}",
            elementId, relationshipType);
        var relationships = await _aggregator.GetRelationshipsAsync(elementId, relationshipType);
        return Ok(relationships);
    }

    /// <summary>
    /// Get children of an object
    /// </summary>
    [HttpGet("objects/{elementId}/children")]
    public async Task<ActionResult<List<Instance>>> GetChildren(
        string elementId,
        [FromQuery] bool includeMetadata = false)
    {
        _logger.LogInformation("Getting children for {ElementId}, metadata: {Metadata}",
            elementId, includeMetadata);
        var children = await _aggregator.GetChildrenAsync(elementId, includeMetadata);
        return Ok(children);
    }

    /// <summary>
    /// Get parent of an object
    /// </summary>
    [HttpGet("objects/{elementId}/parent")]
    public async Task<ActionResult<Instance>> GetParent(
        string elementId,
        [FromQuery] bool includeMetadata = false)
    {
        _logger.LogInformation("Getting parent for {ElementId}, metadata: {Metadata}",
            elementId, includeMetadata);
        var parent = await _aggregator.GetParentAsync(elementId, includeMetadata);

        if (parent == null)
        {
            _logger.LogWarning("Parent not found for: {ElementId}", elementId);
            return NotFound($"Parent not found for '{elementId}'");
        }

        return Ok(parent);
    }

    /// <summary>
    /// Get current values for an object
    /// </summary>
    [HttpGet("value/{elementId}")]
    public async Task<ActionResult<Dictionary<string, object>>> GetValue(string elementId)
    {
        _logger.LogInformation("Getting current value for {ElementId}", elementId);
        var values = await _aggregator.GetValueAsync(elementId);

        if (values == null || !values.Any())
        {
            _logger.LogWarning("No values found for: {ElementId}", elementId);
            return NotFound($"No values found for '{elementId}'");
        }

        return Ok(values);
    }

    /// <summary>
    /// Get historical values for an object
    /// </summary>
    [HttpGet("history/{elementId}")]
    public async Task<ActionResult<List<HistoricalValue>>> GetHistory(
        string elementId,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int? maxPoints = null)
    {
        _logger.LogInformation("Getting history for {ElementId}, start: {Start}, end: {End}, max: {Max}",
            elementId, startTime, endTime, maxPoints);

        var history = await _aggregator.GetHistoryAsync(elementId, startTime, endTime, maxPoints);
        return Ok(history);
    }

    /// <summary>
    /// Update values for an object
    /// </summary>
    [HttpPut("value/{elementId}")]
    public async Task<ActionResult> UpdateValue(
        string elementId,
        [FromBody] Dictionary<string, object> values)
    {
        _logger.LogInformation("Updating value for {ElementId}", elementId);
        var success = await _aggregator.UpdateValueAsync(elementId, values);

        if (!success)
        {
            _logger.LogWarning("Failed to update value for: {ElementId}", elementId);
            return BadRequest($"Failed to update value for '{elementId}'");
        }

        return Ok();
    }


    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> GetHealth()
    {
        var health = new
        {
            Status = "Healthy",
            Service = "I3X Aggregator",
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, string>()
        };

        // Check each service
        var serviceUrls = new Dictionary<string, string>
        {
            ["ERP"] = "http://localhost:7001",
            ["MES"] = "http://localhost:7002",
            ["SCADA"] = "http://localhost:7003"
        };

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        foreach (var service in serviceUrls)
        {
            try
            {
                var response = await httpClient.GetAsync($"{service.Value}/api/i3x/namespaces");
                health.Services[service.Key] = response.IsSuccessStatusCode ? "Available" : "Unavailable";
            }
            catch
            {
                health.Services[service.Key] = "Unreachable";
            }
        }

        // Set overall status
        if (health.Services.All(s => s.Value == "Available"))
        {
            health = health with { Status = "Healthy" };
        }
        else if (health.Services.Any(s => s.Value == "Available"))
        {
            health = health with { Status = "Degraded" };
        }
        else
        {
            health = health with { Status = "Unhealthy" };
        }

        return Ok(health);
    }
}