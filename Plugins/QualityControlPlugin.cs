using Microsoft.SemanticKernel;
using System.ComponentModel;
using Manufactron.Models;
using Microsoft.Extensions.Logging;

namespace Manufactron.Plugins;

public class QualityControlPlugin
{
    private readonly ILogger<QualityControlPlugin> _logger;
    private readonly Random _random = new();

    public QualityControlPlugin(ILogger<QualityControlPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction, Description("Analyze product quality metrics for a batch")]
    public async Task<QualityReport> AnalyzeQuality(
        [Description("Batch ID")] string batchId)
    {
        _logger.LogInformation("Analyzing quality for batch: {BatchId}", batchId);

        var metrics = new List<QualityMetric>
        {
            new QualityMetric
            {
                Name = "Dimensional Accuracy",
                Value = 0.002 + _random.NextDouble() * 0.003,
                Target = 0.003,
                Tolerance = 0.001,
                InSpec = true
            },
            new QualityMetric
            {
                Name = "Surface Finish",
                Value = 1.2 + _random.NextDouble() * 0.4,
                Target = 1.5,
                Tolerance = 0.3,
                InSpec = true
            },
            new QualityMetric
            {
                Name = "Weight Variance",
                Value = 0.5 + _random.NextDouble() * 1.0,
                Target = 1.0,
                Tolerance = 0.5,
                InSpec = true
            },
            new QualityMetric
            {
                Name = "Tensile Strength",
                Value = 450 + _random.NextDouble() * 50,
                Target = 475,
                Tolerance = 25,
                InSpec = true
            }
        };

        var defects = new List<string>();
        var qualityScore = 0.95 + _random.NextDouble() * 0.04;

        if (_random.NextDouble() < 0.1)
        {
            defects.Add("Minor surface imperfection detected");
            qualityScore -= 0.02;
        }

        if (_random.NextDouble() < 0.05)
        {
            defects.Add("Slight dimensional variance on edge");
            qualityScore -= 0.01;
        }

        return await Task.FromResult(new QualityReport
        {
            BatchId = batchId,
            QualityScore = qualityScore,
            PassedQualityCheck = qualityScore >= 0.90,
            Metrics = metrics,
            Defects = defects,
            InspectionDate = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Predict quality issues based on process parameters")]
    public async Task<Dictionary<string, double>> PredictQualityIssues(
        [Description("Process temperature")] double temperature,
        [Description("Process pressure")] double pressure,
        [Description("Process speed")] double speed)
    {
        _logger.LogInformation("Predicting quality issues for T:{Temp}, P:{Pressure}, S:{Speed}",
            temperature, pressure, speed);

        var predictions = new Dictionary<string, double>();

        if (temperature > 85)
        {
            predictions["Thermal Deformation"] = 0.15 + (temperature - 85) * 0.02;
        }

        if (pressure > 150)
        {
            predictions["Stress Cracking"] = 0.10 + (pressure - 150) * 0.01;
        }

        if (speed > 1500)
        {
            predictions["Surface Defects"] = 0.08 + (speed - 1500) * 0.001;
        }

        if (temperature < 65)
        {
            predictions["Incomplete Cure"] = 0.20 + (65 - temperature) * 0.03;
        }

        if (predictions.Count == 0)
        {
            predictions["No Issues"] = 0.95;
        }

        return await Task.FromResult(predictions);
    }

    [KernelFunction, Description("Generate quality improvement recommendations")]
    public async Task<List<string>> GenerateQualityRecommendations(
        [Description("Current quality score")] double qualityScore,
        [Description("List of defects")] List<string> defects)
    {
        _logger.LogInformation("Generating quality recommendations for score: {Score}", qualityScore);

        var recommendations = new List<string>();

        if (qualityScore < 0.95)
        {
            recommendations.Add("Increase inspection frequency during critical process steps");
            recommendations.Add("Review and update Standard Operating Procedures (SOPs)");
            recommendations.Add("Conduct additional operator training on quality standards");
        }

        if (defects.Any(d => d.Contains("surface", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Adjust surface treatment parameters");
            recommendations.Add("Check and calibrate finishing equipment");
            recommendations.Add("Review material handling procedures to prevent surface damage");
        }

        if (defects.Any(d => d.Contains("dimensional", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Recalibrate measurement equipment");
            recommendations.Add("Verify tooling wear and replace if necessary");
            recommendations.Add("Implement tighter process control on critical dimensions");
        }

        if (qualityScore >= 0.98)
        {
            recommendations.Add("Current quality levels are excellent - maintain current procedures");
            recommendations.Add("Consider documenting current best practices for knowledge sharing");
        }

        return await Task.FromResult(recommendations);
    }

    [KernelFunction, Description("Calculate First Pass Yield (FPY) for production line")]
    public async Task<double> CalculateFirstPassYield(
        [Description("Total units produced")] int totalUnits,
        [Description("Units requiring rework")] int reworkUnits,
        [Description("Units scrapped")] int scrappedUnits)
    {
        _logger.LogInformation("Calculating FPY - Total: {Total}, Rework: {Rework}, Scrapped: {Scrap}",
            totalUnits, reworkUnits, scrappedUnits);

        if (totalUnits == 0) return 0;

        var goodUnits = totalUnits - reworkUnits - scrappedUnits;
        var fpy = (double)goodUnits / totalUnits;

        _logger.LogInformation("First Pass Yield: {FPY:P}", fpy);

        return await Task.FromResult(fpy);
    }
}