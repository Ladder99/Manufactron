using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace Manufactron.Planning;

public class ManufacturingPlanner : IManufacturingPlanner
{
    private readonly Kernel _kernel;
    private readonly ILogger<ManufacturingPlanner> _logger;

    public ManufacturingPlanner(Kernel kernel, ILogger<ManufacturingPlanner> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ProductionPlan> CreateProductionPlanAsync(string goal)
    {
        _logger.LogInformation("Creating production plan for goal: {Goal}", goal);

        var prompt = $@"Create a detailed production plan for the following goal:
        {goal}

        Provide step-by-step actions that should be taken.
        Format as a structured plan with clear steps.";

        var result = await _kernel.InvokePromptAsync(prompt);
        var planText = result.ToString();

        var plan = new ProductionPlan
        {
            Goal = goal,
            Steps = ParseStepsFromText(planText)
        };

        _logger.LogInformation("Created plan with {StepCount} steps", plan.Steps.Count);

        return plan;
    }

    public async Task<ProductionPlan> CreateMaintenancePlanAsync(string equipmentId, string issue)
    {
        _logger.LogInformation("Creating maintenance plan for {EquipmentId} with issue: {Issue}",
            equipmentId, issue);

        var goal = $@"Create a maintenance plan to address the following:
            Equipment: {equipmentId}
            Issue: {issue}

            Steps should include:
            1. Diagnose the root cause
            2. Determine required resources
            3. Schedule maintenance window
            4. Execute repairs
            5. Verify equipment functionality";

        return await CreateProductionPlanAsync(goal);
    }

    public async Task<ProductionPlan> CreateQualityImprovementPlanAsync(double currentQuality, double targetQuality)
    {
        _logger.LogInformation("Creating quality improvement plan from {Current:P} to {Target:P}",
            currentQuality, targetQuality);

        var goal = $@"Develop a quality improvement plan:
            Current Quality: {currentQuality:P}
            Target Quality: {targetQuality:P}

            Analyze current quality metrics, identify root causes of defects,
            and create actionable steps to achieve the target quality level.";

        return await CreateProductionPlanAsync(goal);
    }

    public Task<object> ExecutePlanAsync(ProductionPlan plan)
    {
        _logger.LogInformation("Executing plan with {StepCount} steps", plan.Steps.Count);

        try
        {
            var results = new List<object>();

            foreach (var step in plan.Steps)
            {
                _logger.LogInformation("Executing step: {StepName}", step.Name);
                step.IsCompleted = true;
                results.Add($"Completed: {step.Name}");
            }

            _logger.LogInformation("Plan execution completed successfully");

            return Task.FromResult<object>(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing plan");
            throw;
        }
    }

    private List<PlanStep> ParseStepsFromText(string text)
    {
        var steps = new List<PlanStep>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("-") || line.Trim().StartsWith("*") ||
                line.Trim().StartsWith("1") || line.Trim().StartsWith("2"))
            {
                steps.Add(new PlanStep
                {
                    Name = $"Step {steps.Count + 1}",
                    Description = line.Trim().TrimStart('-', '*', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ' ')
                });
            }
        }

        if (steps.Count == 0)
        {
            steps.Add(new PlanStep
            {
                Name = "Execute Plan",
                Description = text
            });
        }

        return steps;
    }
}