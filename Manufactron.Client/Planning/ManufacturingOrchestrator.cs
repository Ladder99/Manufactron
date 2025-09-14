using Manufactron.Models;
using Microsoft.Extensions.Logging;

namespace Manufactron.Planning;

public interface IManufacturingAgent
{
    Task<object> ProcessEvent(ManufacturingEvent evt);
    string AgentType { get; }
}

public class ManufacturingOrchestrator
{
    private readonly Dictionary<string, IManufacturingAgent> _agents;
    private readonly ILogger<ManufacturingOrchestrator> _logger;

    public ManufacturingOrchestrator(ILogger<ManufacturingOrchestrator> logger)
    {
        _logger = logger;
        _agents = new Dictionary<string, IManufacturingAgent>();
    }

    public void RegisterAgent(string name, IManufacturingAgent agent)
    {
        _agents[name] = agent;
        _logger.LogInformation("Registered agent: {Name} of type {Type}", name, agent.AgentType);
    }

    public async Task<OrchestrationResult> HandleManufacturingEvent(ManufacturingEvent evt)
    {
        _logger.LogInformation("Handling event {EventId} of type {EventType}",
            evt.EventId, evt.EventType);

        var relevantAgents = DetermineRelevantAgents(evt);
        var results = new Dictionary<string, object>();
        var actionsTaken = new List<string>();
        var errors = new List<string>();

        var tasks = relevantAgents.Select(async agent =>
        {
            try
            {
                var agentName = _agents.FirstOrDefault(x => x.Value == agent).Key;
                _logger.LogInformation("Dispatching event to agent: {AgentName}", agentName);

                var result = await agent.ProcessEvent(evt);

                lock (results)
                {
                    results[agentName] = result;
                    actionsTaken.Add($"{agentName} processed {evt.EventType}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in agent processing");
                lock (errors)
                {
                    errors.Add($"Agent error: {ex.Message}");
                }
                return null;
            }
        });

        await Task.WhenAll(tasks);

        var orchestrationResult = await SynthesizeResponse(results, evt);
        orchestrationResult.ActionsTaken.AddRange(actionsTaken);
        orchestrationResult.Errors.AddRange(errors);

        return orchestrationResult;
    }

    private List<IManufacturingAgent> DetermineRelevantAgents(ManufacturingEvent evt)
    {
        var relevantAgents = new List<IManufacturingAgent>();

        switch (evt.EventType)
        {
            case "ProductionAlert":
            case "EfficiencyDrop":
                if (_agents.ContainsKey("production"))
                    relevantAgents.Add(_agents["production"]);
                if (_agents.ContainsKey("quality"))
                    relevantAgents.Add(_agents["quality"]);
                break;

            case "QualityIssue":
            case "DefectDetected":
                if (_agents.ContainsKey("quality"))
                    relevantAgents.Add(_agents["quality"]);
                if (_agents.ContainsKey("production"))
                    relevantAgents.Add(_agents["production"]);
                break;

            case "EquipmentFailure":
            case "MaintenanceRequired":
                if (_agents.ContainsKey("maintenance"))
                    relevantAgents.Add(_agents["maintenance"]);
                if (_agents.ContainsKey("production"))
                    relevantAgents.Add(_agents["production"]);
                break;

            case "SupplyChainDisruption":
                if (_agents.ContainsKey("supply"))
                    relevantAgents.Add(_agents["supply"]);
                if (_agents.ContainsKey("production"))
                    relevantAgents.Add(_agents["production"]);
                break;

            default:
                relevantAgents.AddRange(_agents.Values);
                break;
        }

        _logger.LogInformation("Selected {Count} agents for event type {EventType}",
            relevantAgents.Count, evt.EventType);

        return relevantAgents;
    }

    private async Task<OrchestrationResult> SynthesizeResponse(
        Dictionary<string, object> agentResponses,
        ManufacturingEvent evt)
    {
        _logger.LogInformation("Synthesizing response from {Count} agent responses",
            agentResponses.Count);

        var result = new OrchestrationResult
        {
            Success = agentResponses.Count > 0 && !agentResponses.Values.Any(v => v == null),
            Results = agentResponses,
            ActionsTaken = new List<string>(),
            Errors = new List<string>()
        };

        if (evt.Severity == "Critical" && result.Success)
        {
            result.ActionsTaken.Add("Critical event handled - escalation triggered");
            result.ActionsTaken.Add("Notifications sent to management team");
        }

        return await Task.FromResult(result);
    }

    public async Task<Dictionary<string, object>> GetSystemStatus()
    {
        var status = new Dictionary<string, object>
        {
            ["RegisteredAgents"] = _agents.Count,
            ["AgentTypes"] = _agents.Values.Select(a => a.AgentType).Distinct().ToList(),
            ["Status"] = "Operational",
            ["LastUpdated"] = DateTime.UtcNow
        };

        return await Task.FromResult(status);
    }
}