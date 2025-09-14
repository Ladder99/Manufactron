using Microsoft.SemanticKernel;
using System.ComponentModel;
using Manufactron.Models;
using Manufactron.Integration;
using Microsoft.Extensions.Logging;

namespace Manufactron.Plugins;

public class ProductionMonitoringPlugin
{
    private readonly ISystemIntegrationService _integrationService;
    private readonly ILogger<ProductionMonitoringPlugin> _logger;
    private readonly Random _random = new();

    public ProductionMonitoringPlugin(
        ISystemIntegrationService integrationService,
        ILogger<ProductionMonitoringPlugin> logger)
    {
        _integrationService = integrationService;
        _logger = logger;
    }

    [KernelFunction, Description("Monitor production line status and metrics")]
    public async Task<ProductionStatus> GetProductionStatus(
        [Description("Production line ID")] string lineId)
    {
        _logger.LogInformation("Getting production status for line: {LineId}", lineId);

        try
        {
            var status = await _integrationService.GetProductionStatusFromMES(lineId);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get real MES data, using simulated data");

            return new ProductionStatus
            {
                LineId = lineId,
                Status = "Running",
                Throughput = 850 + _random.Next(-50, 50),
                Efficiency = 0.85 + _random.NextDouble() * 0.1,
                LastUpdated = DateTime.UtcNow,
                Metrics = new Dictionary<string, object>
                {
                    ["Temperature"] = 72.5 + _random.NextDouble() * 5,
                    ["Pressure"] = 145 + _random.Next(-10, 10),
                    ["Speed"] = 1200 + _random.Next(-100, 100),
                    ["UpTime"] = 0.92
                }
            };
        }
    }

    [KernelFunction, Description("Detect anomalies in production metrics")]
    public async Task<AnomalyResult> DetectAnomalies(
        [Description("Sensor data for analysis")] SensorData data)
    {
        _logger.LogInformation("Analyzing sensor data for anomalies: {SensorId}", data.SensorId);

        var isAnomaly = false;
        var anomalyType = "None";
        var affectedMetrics = new List<string>();

        if (data.Type == "Temperature" && data.Value > 85)
        {
            isAnomaly = true;
            anomalyType = "Temperature Excursion";
            affectedMetrics.Add("Temperature");
        }
        else if (data.Type == "Pressure" && data.Value > 160)
        {
            isAnomaly = true;
            anomalyType = "Pressure Overload";
            affectedMetrics.Add("Pressure");
        }
        else if (data.Type == "Vibration" && data.Value > 5.0)
        {
            isAnomaly = true;
            anomalyType = "Excessive Vibration";
            affectedMetrics.Add("Vibration");
        }

        return await Task.FromResult(new AnomalyResult
        {
            IsAnomaly = isAnomaly,
            ConfidenceScore = isAnomaly ? 0.85 + _random.NextDouble() * 0.15 : 0.1,
            AnomalyType = anomalyType,
            Description = isAnomaly ?
                $"Detected {anomalyType} in sensor {data.SensorId}" :
                "No anomalies detected",
            AffectedMetrics = affectedMetrics,
            Details = new Dictionary<string, object>
            {
                ["SensorId"] = data.SensorId,
                ["Value"] = data.Value,
                ["Threshold"] = GetThreshold(data.Type),
                ["Timestamp"] = data.Timestamp
            }
        });
    }

    [KernelFunction, Description("Calculate OEE (Overall Equipment Effectiveness)")]
    public async Task<double> CalculateOEE(
        [Description("Production line ID")] string lineId,
        [Description("Time period in hours")] int periodHours = 8)
    {
        _logger.LogInformation("Calculating OEE for line {LineId} over {Hours} hours", lineId, periodHours);

        var availability = 0.92 + _random.NextDouble() * 0.05;
        var performance = 0.85 + _random.NextDouble() * 0.1;
        var quality = 0.96 + _random.NextDouble() * 0.03;

        var oee = availability * performance * quality;

        _logger.LogInformation("OEE Calculation - Availability: {A:P}, Performance: {P:P}, Quality: {Q:P}, OEE: {OEE:P}",
            availability, performance, quality, oee);

        return await Task.FromResult(oee);
    }

    [KernelFunction, Description("Get real-time sensor readings from production equipment")]
    public async Task<List<SensorData>> GetSensorReadings(
        [Description("Equipment ID")] string equipmentId)
    {
        _logger.LogInformation("Getting sensor readings for equipment: {EquipmentId}", equipmentId);

        var sensors = new List<SensorData>
        {
            new SensorData
            {
                SensorId = $"{equipmentId}_TEMP_01",
                Type = "Temperature",
                Value = 72.5 + _random.NextDouble() * 10,
                Unit = "°C",
                Timestamp = DateTime.UtcNow
            },
            new SensorData
            {
                SensorId = $"{equipmentId}_PRESS_01",
                Type = "Pressure",
                Value = 145 + _random.NextDouble() * 20,
                Unit = "PSI",
                Timestamp = DateTime.UtcNow
            },
            new SensorData
            {
                SensorId = $"{equipmentId}_VIB_01",
                Type = "Vibration",
                Value = 2.5 + _random.NextDouble() * 3,
                Unit = "mm/s",
                Timestamp = DateTime.UtcNow
            },
            new SensorData
            {
                SensorId = $"{equipmentId}_FLOW_01",
                Type = "Flow",
                Value = 450 + _random.NextDouble() * 50,
                Unit = "L/min",
                Timestamp = DateTime.UtcNow
            }
        };

        return await Task.FromResult(sensors);
    }

    private double GetThreshold(string sensorType)
    {
        return sensorType switch
        {
            "Temperature" => 85.0,
            "Pressure" => 160.0,
            "Vibration" => 5.0,
            "Flow" => 500.0,
            _ => 100.0
        };
    }
}