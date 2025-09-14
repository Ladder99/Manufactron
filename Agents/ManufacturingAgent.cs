using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Manufactron.Plugins;
using Manufactron.Memory;
using Manufactron.Planning;

namespace Manufactron.Agents;

public class ManufacturingAgent
{
    private readonly Kernel _kernel;
    private readonly IManufacturingPlanner _planner;
    private readonly IManufacturingMemory _memoryStore;
    private readonly ILogger<ManufacturingAgent> _logger;

    public ManufacturingAgent(
        Kernel kernel,
        IManufacturingPlanner planner,
        IManufacturingMemory memoryStore,
        ILogger<ManufacturingAgent> logger)
    {
        _kernel = kernel;
        _planner = planner;
        _memoryStore = memoryStore;
        _logger = logger;
    }

    public async Task<string> ProcessQueryAsync(string query)
    {
        _logger.LogInformation("Processing query: {Query}", query);

        var relevantMemories = await _memoryStore.QueryKnowledgeAsync(query, limit: 5);

        var context = string.Join("\n", relevantMemories.Select(m => m.Text));

        var prompt = $@"
            Context from manufacturing knowledge base:
            {context}

            User Query: {query}

            Please provide a comprehensive response based on the context and your knowledge of manufacturing processes.
            Focus on practical, actionable insights.";

        var result = await _kernel.InvokePromptAsync(prompt);

        return result.ToString();
    }

    public async Task<Models.Recommendation> AnalyzeScenarioAsync(Models.ManufacturingScenario scenario)
    {
        _logger.LogInformation("Analyzing manufacturing scenario");

        var prompt = $@"
            Analyze the following manufacturing scenario and provide recommendations:

            Production Target: {scenario.ProductionTarget} units/hour
            Current Output: {scenario.CurrentOutput} units/hour
            Equipment Status: {scenario.EquipmentStatus}
            Quality Metrics: {string.Join(", ", scenario.QualityMetrics.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}
            Active Alerts: {string.Join(", ", scenario.ActiveAlerts)}

            Based on this information:
            1. Identify the main issues affecting production
            2. Recommend specific actions to improve performance
            3. Estimate the potential improvement from these actions
            4. Prioritize actions by impact and feasibility

            Format the response as a structured recommendation.";

        var result = await _kernel.InvokePromptAsync(prompt);

        return ParseRecommendation(result.ToString());
    }

    public async Task MonitorProductionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting production monitoring");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var productionPlugin = _kernel.Plugins["ProductionMonitoring"];
                var qualityPlugin = _kernel.Plugins["QualityControl"];

                var statusFunction = productionPlugin["GetProductionStatus"];
                var qualityFunction = qualityPlugin["AnalyzeQuality"];

                var productionStatus = await _kernel.InvokeAsync(statusFunction, new() { ["lineId"] = "LINE-001" });

                _logger.LogInformation("Production status: {Status}", productionStatus);

                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during production monitoring");
            }
        }
    }

    private Models.Recommendation ParseRecommendation(string aiResponse)
    {
        var recommendation = new Models.Recommendation
        {
            Type = "Optimization",
            Priority = "High",
            Description = aiResponse,
            Actions = new List<string>(),
            ExpectedImprovement = 0.0
        };

        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                recommendation.Actions.Add(line.Substring(2).Trim());
            }
        }

        return recommendation;
    }
}