using Manufactron.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;

namespace Manufactron.Integration;

public class SystemIntegrationService : ISystemIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemIntegrationService> _logger;
    private readonly Random _random = new();

    public SystemIntegrationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SystemIntegrationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    // ERP Integration Methods
    public async Task<InventoryLevel> GetInventoryFromERP(string materialId)
    {
        _logger.LogInformation("Getting inventory for material: {MaterialId}", materialId);

        try
        {
            var endpoint = _configuration["Manufacturing:SystemIntegration:ERPEndpoint"];
            if (!string.IsNullOrEmpty(endpoint) && endpoint != "https://your-erp-system.com/api")
            {
                var response = await _httpClient.GetAsync($"{endpoint}/inventory/{materialId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<InventoryLevel>(json) ?? CreateMockInventory(materialId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to ERP, using mock data");
        }

        return CreateMockInventory(materialId);
    }

    public async Task<ProductionOrder> GetProductionOrderFromERP(string orderId)
    {
        _logger.LogInformation("Getting production order: {OrderId}", orderId);

        return await Task.FromResult(new ProductionOrder
        {
            OrderId = orderId,
            ProductId = $"PROD-{_random.Next(1000, 9999)}",
            ProductName = "Manufacturing Component A",
            Quantity = 1000 + _random.Next(0, 500),
            DueDate = DateTime.UtcNow.AddDays(_random.Next(1, 30)),
            Status = "In Progress",
            CompletedQuantity = _random.Next(0, 500),
            RequiredMaterials = new List<string> { "MAT-001", "MAT-002", "MAT-003" }
        });
    }

    public async Task UpdateERPInventory(string materialId, double quantity)
    {
        _logger.LogInformation("Updating ERP inventory for {MaterialId}: {Quantity}",
            materialId, quantity);

        try
        {
            var endpoint = _configuration["Manufacturing:SystemIntegration:ERPEndpoint"];
            if (!string.IsNullOrEmpty(endpoint) && endpoint != "https://your-erp-system.com/api")
            {
                var content = JsonSerializer.Serialize(new { materialId, quantity });
                var response = await _httpClient.PostAsync(
                    $"{endpoint}/inventory/update",
                    new StringContent(content, System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update ERP inventory");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating ERP inventory");
        }
    }

    // MES Integration Methods
    public async Task<ProductionStatus> GetProductionStatusFromMES(string lineId)
    {
        _logger.LogInformation("Getting MES status for line: {LineId}", lineId);

        return await Task.FromResult(new ProductionStatus
        {
            LineId = lineId,
            Status = _random.NextDouble() > 0.1 ? "Running" : "Idle",
            Throughput = 800 + _random.Next(-100, 100),
            Efficiency = 0.80 + _random.NextDouble() * 0.15,
            LastUpdated = DateTime.UtcNow,
            Metrics = new Dictionary<string, object>
            {
                ["CycleTime"] = 45 + _random.Next(-5, 5),
                ["GoodParts"] = 950 + _random.Next(-50, 50),
                ["RejectedParts"] = _random.Next(0, 20),
                ["DowntimeMinutes"] = _random.Next(0, 30)
            }
        });
    }

    public async Task<ProductionOrder> GetProductionOrderFromMES(string orderId)
    {
        return await GetProductionOrderFromERP(orderId);
    }

    public async Task UpdateMESProductionStatus(string lineId, string status)
    {
        _logger.LogInformation("Updating MES status for line {LineId}: {Status}",
            lineId, status);

        await Task.Delay(100);
    }

    // SCADA Integration Methods
    public async Task<SensorData> GetRealtimeDataFromSCADA(string tagName)
    {
        _logger.LogInformation("Reading SCADA tag: {TagName}", tagName);

        var sensorType = DetermineSensorType(tagName);

        return await Task.FromResult(new SensorData
        {
            SensorId = tagName,
            Type = sensorType,
            Value = GenerateSensorValue(sensorType),
            Unit = GetSensorUnit(sensorType),
            Timestamp = DateTime.UtcNow,
            AdditionalReadings = new Dictionary<string, double>
            {
                ["Min"] = GenerateSensorValue(sensorType) * 0.9,
                ["Max"] = GenerateSensorValue(sensorType) * 1.1,
                ["Avg"] = GenerateSensorValue(sensorType)
            }
        });
    }

    public async Task<List<SensorData>> GetMultipleSCADAPoints(List<string> tagNames)
    {
        _logger.LogInformation("Reading {Count} SCADA tags", tagNames.Count);

        var results = new List<SensorData>();

        foreach (var tag in tagNames)
        {
            results.Add(await GetRealtimeDataFromSCADA(tag));
        }

        return results;
    }

    public async Task WriteSCADAValue(string tagName, double value)
    {
        _logger.LogInformation("Writing SCADA tag {TagName}: {Value}", tagName, value);

        await Task.Delay(50);
    }

    // IoT Platform Integration Methods
    public async Task<DeviceTelemetry> GetIoTData(string deviceId)
    {
        _logger.LogInformation("Getting IoT data for device: {DeviceId}", deviceId);

        return await Task.FromResult(new DeviceTelemetry
        {
            DeviceId = deviceId,
            DeviceType = "Industrial Sensor",
            Metrics = new Dictionary<string, double>
            {
                ["Temperature"] = 22.5 + _random.NextDouble() * 5,
                ["Humidity"] = 45 + _random.NextDouble() * 10,
                ["BatteryLevel"] = 85 + _random.NextDouble() * 10,
                ["SignalStrength"] = -60 + _random.Next(-10, 10)
            },
            Timestamp = DateTime.UtcNow,
            Status = "Online"
        });
    }

    public async Task SendIoTCommand(string deviceId, string command, Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Sending IoT command {Command} to device {DeviceId}",
            command, deviceId);

        await Task.Delay(100);
    }

    // Helper Methods
    private InventoryLevel CreateMockInventory(string materialId)
    {
        var onHand = 1000 + _random.Next(0, 2000);
        var allocated = _random.Next(0, onHand / 2);

        return new InventoryLevel
        {
            MaterialId = materialId,
            MaterialName = $"Material {materialId}",
            QuantityOnHand = onHand,
            QuantityAllocated = allocated,
            QuantityAvailable = onHand - allocated,
            Unit = "units",
            LastUpdated = DateTime.UtcNow
        };
    }

    private string DetermineSensorType(string tagName)
    {
        if (tagName.Contains("TEMP", StringComparison.OrdinalIgnoreCase))
            return "Temperature";
        if (tagName.Contains("PRESS", StringComparison.OrdinalIgnoreCase))
            return "Pressure";
        if (tagName.Contains("FLOW", StringComparison.OrdinalIgnoreCase))
            return "Flow";
        if (tagName.Contains("VIB", StringComparison.OrdinalIgnoreCase))
            return "Vibration";
        if (tagName.Contains("SPEED", StringComparison.OrdinalIgnoreCase))
            return "Speed";

        return "Generic";
    }

    private double GenerateSensorValue(string sensorType)
    {
        return sensorType switch
        {
            "Temperature" => 70 + _random.NextDouble() * 20,
            "Pressure" => 140 + _random.NextDouble() * 30,
            "Flow" => 400 + _random.NextDouble() * 100,
            "Vibration" => 2 + _random.NextDouble() * 3,
            "Speed" => 1000 + _random.NextDouble() * 500,
            _ => _random.NextDouble() * 100
        };
    }

    private string GetSensorUnit(string sensorType)
    {
        return sensorType switch
        {
            "Temperature" => "°C",
            "Pressure" => "PSI",
            "Flow" => "L/min",
            "Vibration" => "mm/s",
            "Speed" => "RPM",
            _ => "units"
        };
    }
}