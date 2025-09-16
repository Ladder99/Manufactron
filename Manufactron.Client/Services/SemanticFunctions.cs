using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Manufactron.Client.Services;

public class SemanticFunctions
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticFunctions> _logger;
    private readonly Dictionary<string, KernelFunction> _functions;

    public SemanticFunctions(Kernel kernel, ILogger<SemanticFunctions> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _functions = new Dictionary<string, KernelFunction>();
        LoadSemanticFunctions();
    }

    private void LoadSemanticFunctions()
    {
        try
        {
            var promptsPath = Path.Combine(Directory.GetCurrentDirectory(), "Prompts");

            // Load Root Cause Analysis
            if (File.Exists(Path.Combine(promptsPath, "RootCauseAnalysis.txt")))
            {
                var rootCausePrompt = File.ReadAllText(Path.Combine(promptsPath, "RootCauseAnalysis.txt"));
                _functions["RootCauseAnalysis"] = _kernel.CreateFunctionFromPrompt(
                    rootCausePrompt,
                    functionName: "AnalyzeRootCause",
                    description: "Analyze manufacturing issues to identify root causes");
            }

            // Load Production Optimization
            if (File.Exists(Path.Combine(promptsPath, "ProductionOptimization.txt")))
            {
                var optimizationPrompt = File.ReadAllText(Path.Combine(promptsPath, "ProductionOptimization.txt"));
                _functions["ProductionOptimization"] = _kernel.CreateFunctionFromPrompt(
                    optimizationPrompt,
                    functionName: "OptimizeProduction",
                    description: "Optimize production schedules and resource allocation");
            }

            // Load Quality Assessment
            if (File.Exists(Path.Combine(promptsPath, "QualityAssessment.txt")))
            {
                var qualityPrompt = File.ReadAllText(Path.Combine(promptsPath, "QualityAssessment.txt"));
                _functions["QualityAssessment"] = _kernel.CreateFunctionFromPrompt(
                    qualityPrompt,
                    functionName: "AssessQuality",
                    description: "Perform comprehensive quality assessment and analysis");
            }

            _logger.LogInformation($"Loaded {_functions.Count} semantic functions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading semantic functions");
        }
    }

    public async Task<string> AnalyzeRootCauseAsync(
        string equipment,
        string status,
        object metrics,
        List<string> events,
        object patterns)
    {
        try
        {
            if (!_functions.ContainsKey("RootCauseAnalysis"))
            {
                return "Root cause analysis function not available";
            }

            var arguments = new KernelArguments
            {
                ["equipment"] = equipment,
                ["status"] = status,
                ["metrics"] = JsonSerializer.Serialize(metrics),
                ["events"] = string.Join("\n", events),
                ["patterns"] = JsonSerializer.Serialize(patterns)
            };

            var result = await _functions["RootCauseAnalysis"].InvokeAsync(_kernel, arguments);
            return result?.ToString() ?? "No analysis available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing root cause analysis");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> OptimizeProductionAsync(
        object lines,
        object jobs,
        object equipment,
        object materials,
        object orders,
        int availableHours = 24)
    {
        try
        {
            if (!_functions.ContainsKey("ProductionOptimization"))
            {
                return "Production optimization function not available";
            }

            var arguments = new KernelArguments
            {
                ["lines"] = JsonSerializer.Serialize(lines),
                ["jobs"] = JsonSerializer.Serialize(jobs),
                ["equipment"] = JsonSerializer.Serialize(equipment),
                ["materials"] = JsonSerializer.Serialize(materials),
                ["orders"] = JsonSerializer.Serialize(orders),
                ["availableHours"] = availableHours.ToString(),
                ["changeoverTimes"] = "30-60 minutes typical",
                ["qualityRequirements"] = ">98% quality rate",
                ["maintenanceWindows"] = "Daily: 2 hours, Weekly: 8 hours"
            };

            var result = await _functions["ProductionOptimization"].InvokeAsync(_kernel, arguments);
            return result?.ToString() ?? "No optimization available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing production");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> AssessQualityAsync(
        string product,
        object batch,
        object testResults,
        object defectRates,
        string equipment,
        string operatorName)
    {
        try
        {
            if (!_functions.ContainsKey("QualityAssessment"))
            {
                return "Quality assessment function not available";
            }

            // Calculate statistical measures
            var defectRatesList = defectRates as List<double> ?? new List<double>();
            var mean = defectRatesList.Any() ? defectRatesList.Average() : 0;
            var stdDev = CalculateStandardDeviation(defectRatesList);

            var arguments = new KernelArguments
            {
                ["product"] = product,
                ["batch"] = JsonSerializer.Serialize(batch),
                ["testResults"] = JsonSerializer.Serialize(testResults),
                ["defectRates"] = JsonSerializer.Serialize(defectRates),
                ["complaints"] = "None reported",
                ["mean"] = mean.ToString("F2"),
                ["stdDev"] = stdDev.ToString("F2"),
                ["processCapability"] = "Cp: 1.33, Cpk: 1.25",
                ["controlLimits"] = $"UCL: {mean + 3 * stdDev:F2}, LCL: {Math.Max(0, mean - 3 * stdDev):F2}",
                ["equipment"] = equipment,
                ["operator"] = operatorName,
                ["environment"] = "Temperature: 22°C, Humidity: 45%",
                ["materialSource"] = "Primary Supplier"
            };

            var result = await _functions["QualityAssessment"].InvokeAsync(_kernel, arguments);
            return result?.ToString() ?? "No assessment available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing quality");
            return $"Error: {ex.Message}";
        }
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (!values.Any()) return 0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    public async Task<string> GenerateInsightAsync(string context, string data, string objective)
    {
        var prompt = $@"
As a manufacturing intelligence expert, analyze the following data and provide actionable insights.

Context: {context}
Data: {data}
Objective: {objective}

Provide:
1. Key observations
2. Patterns or anomalies detected
3. Risk assessment
4. Specific recommendations
5. Expected outcomes if recommendations are followed

Be concise, specific, and focus on actionable intelligence.";

        var function = _kernel.CreateFunctionFromPrompt(prompt);
        var result = await function.InvokeAsync(_kernel);
        return result?.ToString() ?? "No insights generated";
    }

    public List<string> GetAvailableFunctions()
    {
        return _functions.Keys.ToList();
    }
}