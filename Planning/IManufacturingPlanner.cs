namespace Manufactron.Planning;

public interface IManufacturingPlanner
{
    Task<ProductionPlan> CreateProductionPlanAsync(string goal);
    Task<ProductionPlan> CreateMaintenancePlanAsync(string equipmentId, string issue);
    Task<ProductionPlan> CreateQualityImprovementPlanAsync(double currentQuality, double targetQuality);
    Task<object> ExecutePlanAsync(ProductionPlan plan);
}

public class ProductionPlan
{
    public string Goal { get; set; } = string.Empty;
    public List<PlanStep> Steps { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

public class PlanStep
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsCompleted { get; set; }
}