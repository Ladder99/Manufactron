using Microsoft.SemanticKernel;
using System.ComponentModel;
using Manufactron.Models;
using Microsoft.Extensions.Logging;

namespace Manufactron.Plugins;

public class MaintenancePlugin
{
    private readonly ILogger<MaintenancePlugin> _logger;
    private readonly Random _random = new();

    public MaintenancePlugin(ILogger<MaintenancePlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction, Description("Predict equipment maintenance needs using ML models")]
    public async Task<MaintenancePrediction> PredictMaintenance(
        [Description("Equipment ID")] string equipmentId)
    {
        _logger.LogInformation("Predicting maintenance for equipment: {EquipmentId}", equipmentId);

        var hoursToFailure = 500 + _random.Next(0, 1000);
        var failureProbability = CalculateFailureProbability(hoursToFailure);

        var riskFactors = new List<string>();

        if (failureProbability > 0.7)
        {
            riskFactors.Add("High vibration levels detected");
            riskFactors.Add("Temperature trending above normal");
        }
        else if (failureProbability > 0.4)
        {
            riskFactors.Add("Moderate wear indicators");
            riskFactors.Add("Lubrication schedule overdue");
        }
        else
        {
            riskFactors.Add("Normal operating parameters");
        }

        var recommendedAction = failureProbability switch
        {
            > 0.8 => "Schedule immediate maintenance - critical failure risk",
            > 0.6 => "Plan maintenance within next week",
            > 0.4 => "Monitor closely and schedule preventive maintenance",
            _ => "Continue normal monitoring schedule"
        };

        return await Task.FromResult(new MaintenancePrediction
        {
            EquipmentId = equipmentId,
            EquipmentName = $"Production Equipment {equipmentId}",
            PredictedFailureDate = DateTime.UtcNow.AddHours(hoursToFailure),
            FailureProbability = failureProbability,
            RecommendedAction = recommendedAction,
            RiskFactors = riskFactors,
            EstimatedDowntime = failureProbability > 0.6 ? 4.0 + _random.NextDouble() * 4 : 2.0,
            EstimatedCost = failureProbability > 0.6 ? 5000 + _random.Next(0, 10000) : 1000
        });
    }

    [KernelFunction, Description("Schedule maintenance tasks based on predictions")]
    public async Task<Dictionary<string, object>> ScheduleMaintenance(
        [Description("Equipment ID")] string equipmentId,
        [Description("Maintenance type")] string maintenanceType,
        [Description("Preferred date")] DateTime preferredDate)
    {
        _logger.LogInformation("Scheduling {Type} maintenance for {EquipmentId} on {Date}",
            maintenanceType, equipmentId, preferredDate);

        var schedule = new Dictionary<string, object>
        {
            ["ScheduleId"] = Guid.NewGuid().ToString(),
            ["EquipmentId"] = equipmentId,
            ["MaintenanceType"] = maintenanceType,
            ["ScheduledDate"] = preferredDate,
            ["EstimatedDuration"] = maintenanceType == "Preventive" ? 2.0 : 4.0,
            ["TechnicianRequired"] = maintenanceType == "Corrective" ? 2 : 1,
            ["PartsRequired"] = GeneratePartslist(maintenanceType),
            ["Status"] = "Scheduled"
        };

        return await Task.FromResult(schedule);
    }

    [KernelFunction, Description("Calculate Mean Time Between Failures (MTBF)")]
    public async Task<double> CalculateMTBF(
        [Description("Equipment ID")] string equipmentId,
        [Description("Period in days")] int periodDays = 365)
    {
        _logger.LogInformation("Calculating MTBF for {EquipmentId} over {Days} days",
            equipmentId, periodDays);

        var totalOperatingHours = periodDays * 20;
        var numberOfFailures = _random.Next(2, 8);

        var mtbf = numberOfFailures > 0 ? totalOperatingHours / (double)numberOfFailures : totalOperatingHours;

        _logger.LogInformation("MTBF: {MTBF} hours", mtbf);

        return await Task.FromResult(mtbf);
    }

    [KernelFunction, Description("Get maintenance history for equipment")]
    public async Task<List<Dictionary<string, object>>> GetMaintenanceHistory(
        [Description("Equipment ID")] string equipmentId,
        [Description("Number of records")] int limit = 10)
    {
        _logger.LogInformation("Getting maintenance history for {EquipmentId}", equipmentId);

        var history = new List<Dictionary<string, object>>();

        for (int i = 0; i < limit; i++)
        {
            var daysAgo = i * 30 + _random.Next(0, 30);
            history.Add(new Dictionary<string, object>
            {
                ["Date"] = DateTime.UtcNow.AddDays(-daysAgo),
                ["Type"] = i % 3 == 0 ? "Corrective" : "Preventive",
                ["Duration"] = 2.0 + _random.NextDouble() * 4,
                ["Cost"] = 1000 + _random.Next(0, 5000),
                ["Description"] = i % 3 == 0 ? "Replaced worn bearing" : "Routine maintenance and inspection",
                ["Technician"] = $"Tech-{_random.Next(100, 200)}"
            });
        }

        return await Task.FromResult(history);
    }

    [KernelFunction, Description("Generate maintenance cost optimization recommendations")]
    public async Task<List<string>> OptimizeMaintenanceCosts(
        [Description("Current maintenance budget")] double budget,
        [Description("Number of equipment units")] int equipmentCount)
    {
        _logger.LogInformation("Optimizing maintenance costs for budget: {Budget}, Equipment: {Count}",
            budget, equipmentCount);

        var recommendations = new List<string>
        {
            "Implement predictive maintenance to reduce unplanned downtime by 30-50%",
            "Standardize spare parts inventory to reduce carrying costs",
            "Train operators in basic maintenance tasks to reduce service calls",
            $"Current budget per equipment: ${budget / equipmentCount:F2}"
        };

        if (budget / equipmentCount < 5000)
        {
            recommendations.Add("Budget appears low - consider increasing to prevent costly failures");
            recommendations.Add("Focus on critical equipment first with available budget");
        }
        else
        {
            recommendations.Add("Budget is adequate - consider investing in condition monitoring systems");
            recommendations.Add("Explore maintenance contracts for non-critical equipment");
        }

        recommendations.Add("Implement a Computerized Maintenance Management System (CMMS)");
        recommendations.Add("Review and optimize maintenance schedules based on actual usage patterns");

        return await Task.FromResult(recommendations);
    }

    private double CalculateFailureProbability(int hoursToFailure)
    {
        if (hoursToFailure < 100) return 0.95;
        if (hoursToFailure < 200) return 0.80;
        if (hoursToFailure < 400) return 0.60;
        if (hoursToFailure < 600) return 0.40;
        if (hoursToFailure < 1000) return 0.20;
        return 0.10;
    }

    private List<string> GeneratePartslist(string maintenanceType)
    {
        var parts = new List<string>();

        if (maintenanceType == "Preventive")
        {
            parts.Add("Lubricants");
            parts.Add("Filters");
            parts.Add("Belts");
        }
        else if (maintenanceType == "Corrective")
        {
            parts.Add("Bearing assembly");
            parts.Add("Motor coupling");
            parts.Add("Seals and gaskets");
            parts.Add("Electrical components");
        }

        return parts;
    }
}