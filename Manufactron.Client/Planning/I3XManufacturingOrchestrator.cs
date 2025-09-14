using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;
using Manufactron.Models;
using Manufactron.Services.I3X;

namespace Manufactron.Planning
{
    /// <summary>
    /// I3X-aware orchestrator that coordinates manufacturing decisions
    /// by aggregating context from ERP, MES, and SCADA services
    /// </summary>
    public class I3XManufacturingOrchestrator
    {
        private readonly II3XClient _i3xClient;
        private readonly Kernel _kernel;
        private readonly ILogger<I3XManufacturingOrchestrator> _logger;
        private readonly Dictionary<string, IManufacturingAgent> _agents;

        public I3XManufacturingOrchestrator(
            II3XClient i3xClient,
            Kernel kernel,
            ILogger<I3XManufacturingOrchestrator> logger)
        {
            _i3xClient = i3xClient;
            _kernel = kernel;
            _logger = logger;
            _agents = new Dictionary<string, IManufacturingAgent>();
        }

        public void RegisterAgent(string name, IManufacturingAgent agent)
        {
            _agents[name] = agent;
            _logger.LogInformation("Registered I3X-aware agent: {Name} of type {Type}", name, agent.AgentType);
        }

        /// <summary>
        /// Handles manufacturing events by gathering I3X context and orchestrating agents
        /// </summary>
        public async Task<OrchestrationResult> HandleManufacturingEventAsync(ManufacturingEvent evt)
        {
            _logger.LogInformation("Handling event {EventId} of type {EventType} with I3X context",
                evt.EventId, evt.EventType);

            // Step 1: Build comprehensive context from I3X services
            var context = await BuildI3XContextAsync(evt);

            // Step 2: Use AI to determine optimal response strategy
            var strategy = await DetermineStrategyWithAIAsync(evt, context);

            // Step 3: Execute strategy with relevant agents
            var results = await ExecuteStrategyAsync(strategy, evt, context);

            // Step 4: Monitor and adjust if needed
            await MonitorOutcomeAsync(results, context);

            return results;
        }

        /// <summary>
        /// Builds comprehensive manufacturing context from all I3X services
        /// </summary>
        private async Task<ManufacturingContext> BuildI3XContextAsync(ManufacturingEvent evt)
        {
            _logger.LogInformation("Building I3X context for event from source {Source}", evt.Source);

            // Get context based on event source
            var context = await _i3xClient.GetManufacturingContextAsync(evt.Source);

            // Enrich with additional relationships if needed
            if (context.Job != null)
            {
                // Get order details from ERP
                if (context.Job.Attributes.TryGetValue("orderId", out var orderId))
                {
                    context.Order = await _i3xClient.GetObjectAsync(orderId.ToString(), true);
                }

                // Get material details from ERP
                var materialRels = await _i3xClient.GetRelationshipsAsync(context.Job.ElementId, "ConsumedMaterial");
                if (materialRels.Any())
                {
                    context.MaterialBatch = materialRels.First();
                }
            }

            if (context.Equipment != null)
            {
                // Get maintenance history
                var maintenanceRels = await _i3xClient.GetRelationshipsAsync(context.Equipment.ElementId, "MaintainedBy");
                context.AllRelationships["Maintenance"] = maintenanceRels.Cast<Relationship>().ToList();

                // Get efficiency history for trend analysis
                var history = await _i3xClient.GetHistoryAsync(
                    context.Equipment.ElementId,
                    DateTime.UtcNow.AddDays(-7),
                    DateTime.UtcNow,
                    100);

                if (history != null && history.Any())
                {
                    context.AllRelationships["EfficiencyHistory"] = history
                        .Select(h => new Relationship
                        {
                            SubjectId = context.Equipment.ElementId,
                            PredicateType = "HistoricalEfficiency",
                            ObjectId = h.Values.GetValueOrDefault("efficiency")?.ToString() ?? "0",
                            EstablishedAt = h.Timestamp
                        }).ToList();
                }
            }

            _logger.LogInformation("Built context with Job: {Job}, Line: {Line}, Equipment: {Equipment}",
                context.Job?.ElementId, context.Line?.ElementId, context.Equipment?.ElementId);

            return context;
        }

        /// <summary>
        /// Uses AI to determine the optimal response strategy based on I3X context
        /// </summary>
        private async Task<ResponseStrategy> DetermineStrategyWithAIAsync(
            ManufacturingEvent evt,
            ManufacturingContext context)
        {
            var prompt = $"""
                Analyze this manufacturing event and determine the optimal response strategy:

                Event Type: {evt.EventType}
                Severity: {evt.Severity}
                Source: {evt.Source}
                Event ID: {evt.EventId}

                Context:
                - Job: {context.Job?.Name} ({context.Job?.Attributes.GetValueOrDefault("status")})
                - Order Priority: {context.Order?.Attributes.GetValueOrDefault("priority")}
                - Equipment State: {context.Equipment?.Attributes.GetValueOrDefault("state")}
                - Equipment Efficiency: {context.Equipment?.Attributes.GetValueOrDefault("efficiency")}%
                - Line OEE: {context.Line?.Attributes.GetValueOrDefault("OEE")}%

                Determine:
                1. Immediate actions required
                2. Which agents should be involved
                3. Priority level (1-10)
                4. Expected impact on production
                5. Risk assessment

                Format response as JSON.
                """;

            var response = await _kernel.InvokePromptAsync<string>(prompt);

            // Parse AI response into strategy
            return ParseAIStrategy(response);
        }

        /// <summary>
        /// Executes the determined strategy using relevant agents
        /// </summary>
        private async Task<OrchestrationResult> ExecuteStrategyAsync(
            ResponseStrategy strategy,
            ManufacturingEvent evt,
            ManufacturingContext context)
        {
            var results = new Dictionary<string, object>();
            var actionsTaken = new List<string>();
            var errors = new List<string>();

            // Execute immediate actions
            foreach (var action in strategy.ImmediateActions)
            {
                try
                {
                    switch (action.Type)
                    {
                        case "StopEquipment":
                            if (!string.IsNullOrEmpty(action.TargetId))
                            {
                                await UpdateEquipmentStateAsync(action.TargetId, "Stopped");
                                actionsTaken.Add($"Stopped equipment {action.TargetId}");
                            }
                            break;

                        case "NotifyOperator":
                            if (context.Operator != null)
                            {
                                actionsTaken.Add($"Notified operator {context.Operator.Name}");
                            }
                            break;

                        case "ScheduleMaintenance":
                            if (!string.IsNullOrEmpty(action.TargetId))
                            {
                                await ScheduleMaintenanceAsync(action.TargetId, action.Priority);
                                actionsTaken.Add($"Scheduled maintenance for {action.TargetId}");
                            }
                            break;

                        case "AdjustProduction":
                            if (context.Line != null)
                            {
                                await AdjustProductionRateAsync(context.Line.ElementId, action.Value);
                                actionsTaken.Add($"Adjusted production rate to {action.Value}%");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing action {ActionType}", action.Type);
                    errors.Add($"Failed to execute {action.Type}: {ex.Message}");
                }
            }

            // Dispatch to relevant agents
            var relevantAgents = DetermineRelevantAgents(strategy, evt);
            var agentTasks = relevantAgents.Select(async agent =>
            {
                try
                {
                    var agentName = _agents.FirstOrDefault(x => x.Value == agent).Key;
                    _logger.LogInformation("Dispatching to I3X-aware agent: {AgentName}", agentName);

                    // Create enhanced event with I3X context
                    var enhancedEvent = new ManufacturingEvent
                    {
                        EventId = evt.EventId,
                        EventType = evt.EventType,
                        Source = evt.Source,
                        Severity = evt.Severity,
                        Timestamp = evt.Timestamp,
                        Data = new Dictionary<string, object>(evt.Data ?? new())
                        {
                            ["I3XContext"] = context,
                            ["Strategy"] = strategy
                        }
                    };

                    var result = await agent.ProcessEvent(enhancedEvent);
                    results[agentName] = result;
                    actionsTaken.Add($"{agentName} processed with I3X context");
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in agent processing");
                    errors.Add($"Agent error: {ex.Message}");
                    return null;
                }
            });

            await Task.WhenAll(agentTasks);

            return new OrchestrationResult
            {
                Success = errors.Count == 0,
                Results = results,
                ActionsTaken = actionsTaken,
                Errors = errors,
                Recommendations = strategy.Recommendations?.Select(r => r.Description).ToList() ?? new List<string>()
            };
        }

        /// <summary>
        /// Monitors the outcome and adjusts if needed
        /// </summary>
        private async Task MonitorOutcomeAsync(OrchestrationResult results, ManufacturingContext context)
        {
            if (!results.Success)
            {
                _logger.LogWarning("Orchestration had errors, initiating recovery");

                // Use AI to determine recovery actions
                var recoveryPrompt = $"""
                    The orchestration encountered errors:
                    {string.Join(", ", results.Errors)}

                    Context:
                    - Critical Order: {context.Order?.Attributes.GetValueOrDefault("priority") == "High"}
                    - Equipment State: {context.Equipment?.Attributes.GetValueOrDefault("state")}

                    Suggest recovery actions.
                    """;

                var recovery = await _kernel.InvokePromptAsync<string>(recoveryPrompt);
                results.Recommendations.Add($"Recovery action: {recovery}");
            }

            // Subscribe to updates for continuous monitoring
            if (context.Equipment != null)
            {
                _ = Task.Run(async () => await MonitorEquipmentAsync(context.Equipment.ElementId));
            }
        }

        private async Task MonitorEquipmentAsync(string equipmentId)
        {
            await foreach (var update in _i3xClient.StreamUpdatesAsync(equipmentId))
            {
                if (update.Attributes.TryGetValue("efficiency", out var efficiency))
                {
                    var effValue = Convert.ToDouble(efficiency);
                    if (effValue < 95.0)
                    {
                        _logger.LogWarning("Equipment {EquipmentId} efficiency dropped to {Efficiency}%",
                            equipmentId, effValue);
                    }
                }
            }
        }

        private List<IManufacturingAgent> DetermineRelevantAgents(ResponseStrategy strategy, ManufacturingEvent evt)
        {
            var relevantAgents = new List<IManufacturingAgent>();

            foreach (var agentType in strategy.RequiredAgents)
            {
                if (_agents.TryGetValue(agentType.ToLower(), out var agent))
                {
                    relevantAgents.Add(agent);
                }
            }

            // Fallback to event-based selection if no specific agents in strategy
            if (!relevantAgents.Any())
            {
                switch (evt.EventType)
                {
                    case "EquipmentFailure":
                    case "CalibrationDrift":
                        if (_agents.ContainsKey("maintenance"))
                            relevantAgents.Add(_agents["maintenance"]);
                        break;

                    case "QualityIssue":
                    case "RejectRate":
                        if (_agents.ContainsKey("quality"))
                            relevantAgents.Add(_agents["quality"]);
                        break;

                    case "ProductionDelay":
                    case "EfficiencyDrop":
                        if (_agents.ContainsKey("production"))
                            relevantAgents.Add(_agents["production"]);
                        break;
                }
            }

            return relevantAgents;
        }

        private async Task UpdateEquipmentStateAsync(string equipmentId, string state)
        {
            await _i3xClient.UpdateValueAsync(equipmentId, new Dictionary<string, object>
            {
                ["state"] = state,
                ["lastStateChange"] = DateTime.UtcNow
            });
        }

        private async Task ScheduleMaintenanceAsync(string equipmentId, int priority)
        {
            // In a real system, this would create a maintenance work order
            _logger.LogInformation("Scheduling maintenance for {EquipmentId} with priority {Priority}",
                equipmentId, priority);
            await Task.CompletedTask;
        }

        private async Task AdjustProductionRateAsync(string lineId, double rate)
        {
            await _i3xClient.UpdateValueAsync(lineId, new Dictionary<string, object>
            {
                ["throughput"] = rate,
                ["adjustedAt"] = DateTime.UtcNow
            });
        }

        private ResponseStrategy ParseAIStrategy(string aiResponse)
        {
            // In production, use proper JSON parsing
            return new ResponseStrategy
            {
                ImmediateActions = new List<StrategyAction>
                {
                    new() { Type = "NotifyOperator", Priority = 1 }
                },
                RequiredAgents = new List<string> { "maintenance", "quality" },
                Priority = 8,
                ExpectedImpact = "Minimal production disruption",
                RiskLevel = "Medium",
                Recommendations = new List<Recommendation>()
            };
        }

        /// <summary>
        /// Performs root cause analysis using I3X graph traversal
        /// </summary>
        public async Task<WasteAnalysis> AnalyzeWasteRootCauseAsync(string jobId)
        {
            _logger.LogInformation("Analyzing waste root cause for job {JobId}", jobId);

            // Build full context
            var context = await _i3xClient.GetManufacturingContextAsync(jobId);

            // Get all equipment in the line
            var equipment = await _i3xClient.GetChildrenAsync(context.Line?.ElementId ?? "", true);

            // Find equipment with high reject counts
            var rejectsByEquipment = new Dictionary<string, int>();
            Equipment problematicEquipment = null;
            int maxRejects = 0;

            foreach (var eq in equipment)
            {
                if (eq.Attributes.TryGetValue("rejectCount", out var rejectObj))
                {
                    var rejects = Convert.ToInt32(rejectObj);
                    rejectsByEquipment[eq.Name] = rejects;

                    if (rejects > maxRejects)
                    {
                        maxRejects = rejects;
                        problematicEquipment = new Equipment
                        {
                            EquipmentId = eq.ElementId,
                            Name = eq.Name,
                            RejectCount = rejects
                        };
                    }
                }
            }

            // Determine root cause
            string rootCause = "Unknown";
            var contributingFactors = new List<string>();

            if (problematicEquipment != null)
            {
                var eqInstance = equipment.First(e => e.ElementId == problematicEquipment.EquipmentId);

                // Check calibration
                if (eqInstance.Attributes.TryGetValue("lastCalibration", out var lastCalObj))
                {
                    var lastCal = DateTime.Parse(lastCalObj.ToString());
                    var daysSinceCalibration = (DateTime.UtcNow - lastCal).Days;

                    if (daysSinceCalibration > 7)
                    {
                        rootCause = $"{problematicEquipment.Name} calibration drift";
                        contributingFactors.Add($"{daysSinceCalibration} days since last calibration");
                    }
                }

                // Check efficiency
                if (eqInstance.Attributes.TryGetValue("efficiency", out var effObj))
                {
                    var efficiency = Convert.ToDouble(effObj);
                    if (efficiency < 97.0)
                    {
                        contributingFactors.Add($"Low efficiency: {efficiency}%");
                    }
                }
            }

            // Calculate waste percentage
            decimal wastePercentage = 0;
            if (context.Job != null)
            {
                var planned = Convert.ToDecimal(context.Job.Attributes.GetValueOrDefault("plannedQuantity", 0));
                var actual = Convert.ToDecimal(context.Job.Attributes.GetValueOrDefault("actualQuantity", 0));
                if (planned > 0)
                {
                    wastePercentage = ((planned - actual) / planned) * 100;
                }
            }

            return new WasteAnalysis
            {
                JobId = jobId,
                WastePercentage = wastePercentage,
                RejectsByEquipment = rejectsByEquipment,
                PrimaryRootCause = rootCause,
                ContributingFactors = contributingFactors,
                RecommendedActions = new List<RecommendedAction>
                {
                    new()
                    {
                        ActionType = "Calibration",
                        TargetId = problematicEquipment?.EquipmentId,
                        Description = $"Immediate calibration of {problematicEquipment?.Name}",
                        Priority = "High",
                        EstimatedImpact = wastePercentage
                    }
                }
            };
        }
    }

    // Supporting classes for I3X orchestration
    public class ResponseStrategy
    {
        public List<StrategyAction> ImmediateActions { get; set; } = new();
        public List<string> RequiredAgents { get; set; } = new();
        public int Priority { get; set; }
        public string ExpectedImpact { get; set; }
        public string RiskLevel { get; set; }
        public List<Recommendation> Recommendations { get; set; } = new();
    }

    public class StrategyAction
    {
        public string Type { get; set; }
        public string TargetId { get; set; }
        public int Priority { get; set; }
        public double Value { get; set; }
    }
}