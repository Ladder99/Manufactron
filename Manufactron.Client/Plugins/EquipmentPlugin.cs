using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.Client.Plugins;

public class EquipmentPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EquipmentPlugin> _logger;
    private readonly string _aggregatorUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public EquipmentPlugin(HttpClient httpClient, ILogger<EquipmentPlugin> logger, string aggregatorUrl)
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

    [KernelFunction("GetEquipmentStatus")]
    [Description("Get the current status and state of equipment by ID")]
    public async Task<string> GetEquipmentStatusAsync(
        [Description("The equipment ID to query")] string equipmentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects/{equipmentId}?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to retrieve equipment {equipmentId}. Status: {response.StatusCode}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var equipment = JsonSerializer.Deserialize<Instance>(json, _jsonOptions);

            if (equipment == null)
            {
                return $"Equipment {equipmentId} not found.";
            }

            var status = new
            {
                Id = equipment.ElementId,
                Name = equipment.Name,
                Type = equipment.TypeId,
                State = equipment.Attributes?.GetValueOrDefault("state", "Unknown"),
                OEE = equipment.Attributes?.GetValueOrDefault("OEE", "N/A"),
                Temperature = equipment.Attributes?.GetValueOrDefault("temperature", "N/A"),
                Speed = equipment.Attributes?.GetValueOrDefault("speed", "N/A"),
                LastMaintenance = equipment.Attributes?.GetValueOrDefault("lastMaintenance", "N/A"),
                ParentLine = equipment.ParentId
            };

            return JsonSerializer.Serialize(status, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting equipment status for {EquipmentId}", equipmentId);
            return $"Error retrieving equipment status: {ex.Message}";
        }
    }

    [KernelFunction("GetEquipmentPerformance")]
    [Description("Get performance metrics (OEE, availability, performance, quality) for equipment")]
    public async Task<string> GetEquipmentPerformanceAsync(
        [Description("The equipment ID to analyze")] string equipmentId,
        [Description("Time range in hours (default 24)")] int hoursBack = 24)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hoursBack);

            var response = await _httpClient.GetAsync(
                $"{_aggregatorUrl}/api/i3x/history/{equipmentId}?startTime={startTime:yyyy-MM-ddTHH:mm:ss}Z&endTime={endTime:yyyy-MM-ddTHH:mm:ss}Z");

            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to retrieve performance data for {equipmentId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var history = JsonSerializer.Deserialize<List<HistoricalValue>>(json, _jsonOptions);

            if (history == null || !history.Any())
            {
                return $"No performance data available for {equipmentId} in the last {hoursBack} hours";
            }

            var oeeValues = history
                .Where(h => h.Values.ContainsKey("OEE"))
                .Select(h => Convert.ToDouble(h.Values["OEE"]))
                .ToList();

            var performance = new
            {
                EquipmentId = equipmentId,
                TimeRange = $"Last {hoursBack} hours",
                AverageOEE = oeeValues.Any() ? oeeValues.Average() : 0,
                MinOEE = oeeValues.Any() ? oeeValues.Min() : 0,
                MaxOEE = oeeValues.Any() ? oeeValues.Max() : 0,
                DataPoints = oeeValues.Count,
                CurrentOEE = oeeValues.LastOrDefault()
            };

            return JsonSerializer.Serialize(performance, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting equipment performance for {EquipmentId}", equipmentId);
            return $"Error retrieving performance metrics: {ex.Message}";
        }
    }

    [KernelFunction("ListEquipmentByLine")]
    [Description("List all equipment on a specific production line")]
    public async Task<string> ListEquipmentByLineAsync(
        [Description("The production line ID")] string lineId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects/{lineId}/children");
            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to retrieve equipment for line {lineId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var equipment = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (equipment == null || !equipment.Any())
            {
                return $"No equipment found on line {lineId}";
            }

            var equipmentList = equipment.Select(e => new
            {
                Id = e.ElementId,
                Name = e.Name,
                Type = e.TypeId,
                State = e.Attributes?.GetValueOrDefault("state", "Unknown"),
                OEE = e.Attributes?.GetValueOrDefault("OEE", "N/A")
            });

            return JsonSerializer.Serialize(equipmentList, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing equipment for line {LineId}", lineId);
            return $"Error retrieving equipment list: {ex.Message}";
        }
    }

    [KernelFunction("PredictEquipmentFailure")]
    [Description("Predict potential equipment failures based on current metrics and historical patterns")]
    public async Task<string> PredictEquipmentFailureAsync(
        [Description("The equipment ID to analyze")] string equipmentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects/{equipmentId}?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to analyze equipment {equipmentId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var equipment = JsonSerializer.Deserialize<Instance>(json, _jsonOptions);

            if (equipment?.Attributes == null)
            {
                return $"No data available for equipment {equipmentId}";
            }

            var temperature = Convert.ToDouble(equipment.Attributes.GetValueOrDefault("temperature", 0)?.ToString() ?? "0");
            var vibration = Convert.ToDouble(equipment.Attributes.GetValueOrDefault("vibration", 0)?.ToString() ?? "0");
            var runtime = Convert.ToDouble(equipment.Attributes.GetValueOrDefault("runtimeHours", 0)?.ToString() ?? "0");
            var lastMaintenanceStr = equipment.Attributes.GetValueOrDefault("lastMaintenance", DateTime.MinValue.ToString())?.ToString() ?? DateTime.MinValue.ToString();

            var daysSinceLastMaintenance = DateTime.TryParse(lastMaintenanceStr, out var lastMaintenanceDate)
                ? (DateTime.UtcNow - lastMaintenanceDate).TotalDays
                : 365; // Default to a year if no maintenance date

            var riskFactors = new List<string>();
            var riskLevel = "Low";
            var failureProbability = 0.1;

            if (temperature > 85)
            {
                riskFactors.Add($"High temperature: {temperature}°C");
                failureProbability += 0.2;
            }

            if (vibration > 5.0)
            {
                riskFactors.Add($"Excessive vibration: {vibration} mm/s");
                failureProbability += 0.25;
            }

            if (runtime > 8000)
            {
                riskFactors.Add($"High runtime hours: {runtime}");
                failureProbability += 0.15;
            }

            if (daysSinceLastMaintenance > 30)
            {
                riskFactors.Add($"Overdue maintenance: {daysSinceLastMaintenance:F0} days");
                failureProbability += 0.2;
            }

            if (failureProbability > 0.6) riskLevel = "High";
            else if (failureProbability > 0.3) riskLevel = "Medium";

            var prediction = new
            {
                EquipmentId = equipmentId,
                RiskLevel = riskLevel,
                FailureProbability = Math.Min(failureProbability, 0.95),
                RiskFactors = riskFactors,
                RecommendedActions = GetRecommendedActions(riskLevel, riskFactors),
                EstimatedTimeToFailure = EstimateTimeToFailure(failureProbability)
            };

            return JsonSerializer.Serialize(prediction, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting failure for {EquipmentId}", equipmentId);
            return $"Error analyzing equipment failure risk: {ex.Message}";
        }
    }

    private List<string> GetRecommendedActions(string riskLevel, List<string> riskFactors)
    {
        var actions = new List<string>();

        if (riskLevel == "High")
        {
            actions.Add("Schedule immediate maintenance inspection");
            actions.Add("Prepare replacement parts");
            actions.Add("Consider reducing equipment load");
        }
        else if (riskLevel == "Medium")
        {
            actions.Add("Schedule maintenance within next week");
            actions.Add("Increase monitoring frequency");
            actions.Add("Review maintenance procedures");
        }

        if (riskFactors.Any(r => r.Contains("temperature")))
        {
            actions.Add("Check cooling system");
            actions.Add("Verify thermal sensors calibration");
        }

        if (riskFactors.Any(r => r.Contains("vibration")))
        {
            actions.Add("Check alignment and balance");
            actions.Add("Inspect bearings and mounts");
        }

        return actions;
    }

    private string EstimateTimeToFailure(double failureProbability)
    {
        if (failureProbability > 0.8) return "Within 24-48 hours";
        if (failureProbability > 0.6) return "Within 3-7 days";
        if (failureProbability > 0.4) return "Within 2-4 weeks";
        if (failureProbability > 0.2) return "Within 1-2 months";
        return "No immediate concern";
    }
}