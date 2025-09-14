namespace Manufactron.Models;

public class ProductionStatus
{
    public string LineId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Throughput { get; set; }
    public double Efficiency { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class SensorData
{
    public string SensorId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> AdditionalReadings { get; set; } = new();
}

public class AnomalyResult
{
    public bool IsAnomaly { get; set; }
    public double ConfidenceScore { get; set; }
    public string AnomalyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedMetrics { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
}

public class QualityReport
{
    public string BatchId { get; set; } = string.Empty;
    public double QualityScore { get; set; }
    public bool PassedQualityCheck { get; set; }
    public List<QualityMetric> Metrics { get; set; } = new();
    public List<string> Defects { get; set; } = new();
    public DateTime InspectionDate { get; set; }
}

public class QualityMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Target { get; set; }
    public double Tolerance { get; set; }
    public bool InSpec { get; set; }
}

public class MaintenancePrediction
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public DateTime PredictedFailureDate { get; set; }
    public double FailureProbability { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public List<string> RiskFactors { get; set; } = new();
    public double EstimatedDowntime { get; set; }
    public double EstimatedCost { get; set; }
}

public class ManufacturingScenario
{
    public double ProductionTarget { get; set; }
    public double CurrentOutput { get; set; }
    public string EquipmentStatus { get; set; } = string.Empty;
    public Dictionary<string, double> QualityMetrics { get; set; } = new();
    public List<string> ActiveAlerts { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class Recommendation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
    public double ExpectedImprovement { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class ManufacturingEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}

public class OrchestrationResult
{
    public bool Success { get; set; }
    public List<string> ActionsTaken { get; set; } = new();
    public Dictionary<string, object> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}