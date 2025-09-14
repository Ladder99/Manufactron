using System;
using System.Collections.Generic;

namespace Manufactron.I3X.Shared.Models.Manufacturing
{
    // Manufacturing domain models aligned with the beverage production example

    public class ProductionLine
    {
        public string LineId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; } // Running, Stopped, Maintenance
        public string CurrentJobId { get; set; }
        public decimal OEE { get; set; } // Overall Equipment Effectiveness
        public decimal Throughput { get; set; } // units/hour
        public List<string> EquipmentIds { get; set; } = new();
    }

    public class ProductionJob
    {
        public string JobId { get; set; }
        public string OrderId { get; set; }
        public string Product { get; set; }
        public int PlannedQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime ExpectedEndTime { get; set; }
        public string Status { get; set; } // Scheduled, Running, Complete
        public string LineId { get; set; }
        public string MaterialBatchId { get; set; }
        public string OperatorId { get; set; }
    }

    public class Equipment
    {
        public string EquipmentId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // Mixer, Filler, Capper, Labeler, Palletizer
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string State { get; set; } // Running, Idle, Faulted
        public int ProductCount { get; set; }
        public int RejectCount { get; set; }
        public decimal Efficiency { get; set; }
        public DateTime LastCalibration { get; set; }
        public string ParentLineId { get; set; }
    }

    public class MaterialBatch
    {
        public string BatchId { get; set; }
        public string Material { get; set; }
        public string Supplier { get; set; }
        public decimal Quantity { get; set; }
        public string QualityCertificate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime ReceivedDate { get; set; }
    }

    public class CustomerOrder
    {
        public string OrderId { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public DateTime DueDate { get; set; }
        public string Priority { get; set; } // High, Medium, Low
        public string Status { get; set; } // Pending, InProduction, Completed, Shipped
    }

    public class Operator
    {
        public string OperatorId { get; set; }
        public string Name { get; set; }
        public string Shift { get; set; }
        public List<string> Certifications { get; set; } = new();
        public string TeamId { get; set; }
    }

    public class EnvironmentalConditions
    {
        public string LocationId { get; set; }
        public decimal AmbientTemperature { get; set; }
        public decimal Humidity { get; set; }
        public string CleanRoomStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class QualityMetrics
    {
        public string JobId { get; set; }
        public int ExpectedYield { get; set; }
        public int ActualYield { get; set; }
        public int WasteCount { get; set; }
        public decimal WastePercentage { get; set; }
        public string RootCause { get; set; }
        public List<QualityIssue> Issues { get; set; } = new();
    }

    public class QualityIssue
    {
        public string IssueId { get; set; }
        public string EquipmentId { get; set; }
        public string Type { get; set; } // Reject, Defect, Contamination
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; }
        public string Severity { get; set; } // Critical, Major, Minor
        public string Resolution { get; set; }
    }

    public class MaintenanceEvent
    {
        public string EventId { get; set; }
        public string EquipmentId { get; set; }
        public string Type { get; set; } // Preventive, Corrective, Calibration
        public DateTime ScheduledDate { get; set; }
        public DateTime CompletedDate { get; set; }
        public string TechnicianId { get; set; }
        public string Notes { get; set; }
    }

    // Aggregated context models

    public class ProductionContext
    {
        public ProductionJob Job { get; set; }
        public ProductionLine Line { get; set; }
        public List<Equipment> Equipment { get; set; } = new();
        public MaterialBatch MaterialBatch { get; set; }
        public CustomerOrder Order { get; set; }
        public Operator Operator { get; set; }
        public EnvironmentalConditions Environment { get; set; }
        public QualityMetrics Quality { get; set; }
        public Dictionary<string, List<string>> Relationships { get; set; } = new();
    }

    public class WasteAnalysis
    {
        public string JobId { get; set; }
        public decimal WastePercentage { get; set; }
        public Dictionary<string, int> RejectsByEquipment { get; set; } = new();
        public string PrimaryRootCause { get; set; }
        public List<string> ContributingFactors { get; set; } = new();
        public List<RecommendedAction> RecommendedActions { get; set; } = new();
    }

    public class RecommendedAction
    {
        public string ActionType { get; set; } // Calibration, Maintenance, Training
        public string TargetId { get; set; } // Equipment, Operator, etc.
        public string Description { get; set; }
        public string Priority { get; set; }
        public decimal EstimatedImpact { get; set; } // Expected reduction in waste %
    }

    public class MaterialTraceability
    {
        public MaterialBatch Batch { get; set; }
        public string Supplier { get; set; }
        public List<ProductionJob> JobsUsed { get; set; } = new();
        public List<QualityIssue> RelatedIssues { get; set; } = new();
        public Dictionary<string, object> QualityTestResults { get; set; } = new();
    }

    public class PerformanceAnalysis
    {
        public string LineId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<string, ShiftPerformance> PerformanceByShift { get; set; } = new();
        public Dictionary<string, OperatorPerformance> PerformanceByOperator { get; set; } = new();
        public List<PerformanceTrend> Trends { get; set; } = new();
    }

    public class ShiftPerformance
    {
        public string Shift { get; set; }
        public decimal AverageOEE { get; set; }
        public int TotalProduction { get; set; }
        public int TotalWaste { get; set; }
        public decimal Efficiency { get; set; }
    }

    public class OperatorPerformance
    {
        public string OperatorId { get; set; }
        public string OperatorName { get; set; }
        public int JobsCompleted { get; set; }
        public decimal AverageEfficiency { get; set; }
        public int TotalRejects { get; set; }
    }

    public class PerformanceTrend
    {
        public string Metric { get; set; }
        public string Direction { get; set; } // Improving, Declining, Stable
        public decimal ChangePercentage { get; set; }
        public string Timeframe { get; set; }
    }
}