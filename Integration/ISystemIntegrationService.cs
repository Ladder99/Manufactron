using Manufactron.Models;

namespace Manufactron.Integration;

public interface ISystemIntegrationService
{
    // ERP Integration
    Task<InventoryLevel> GetInventoryFromERP(string materialId);
    Task<ProductionOrder> GetProductionOrderFromERP(string orderId);
    Task UpdateERPInventory(string materialId, double quantity);

    // MES Integration
    Task<ProductionStatus> GetProductionStatusFromMES(string lineId);
    Task<ProductionOrder> GetProductionOrderFromMES(string orderId);
    Task UpdateMESProductionStatus(string lineId, string status);

    // SCADA Integration
    Task<SensorData> GetRealtimeDataFromSCADA(string tagName);
    Task<List<SensorData>> GetMultipleSCADAPoints(List<string> tagNames);
    Task WriteSCADAValue(string tagName, double value);

    // IoT Platform Integration
    Task<DeviceTelemetry> GetIoTData(string deviceId);
    Task SendIoTCommand(string deviceId, string command, Dictionary<string, object> parameters);
}

public class InventoryLevel
{
    public string MaterialId { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public double QuantityOnHand { get; set; }
    public double QuantityAllocated { get; set; }
    public double QuantityAvailable { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class ProductionOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public double CompletedQuantity { get; set; }
    public List<string> RequiredMaterials { get; set; } = new();
}

public class DeviceTelemetry
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
}