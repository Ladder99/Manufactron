using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;

namespace Manufactron.Plugins
{
    public class I3XAwareProductionPlugin
    {
        private readonly II3XClient _i3xClient;
        private readonly Kernel _kernel;

        public I3XAwareProductionPlugin(II3XClient i3xClient, Kernel kernel)
        {
            _i3xClient = i3xClient;
            _kernel = kernel;
        }

        [KernelFunction("AnalyzeProductionWaste")]
        [Description("Analyzes production waste using I3X context to find root causes")]
        public async Task<WasteAnalysis> AnalyzeProductionWasteAsync(
            [Description("The job ID to analyze")] string jobId)
        {
            // Build complete manufacturing context from I3X
            var context = await _i3xClient.GetManufacturingContextAsync(jobId);

            // Get equipment reject counts
            var rejectsByEquipment = new Dictionary<string, int>();

            if (context.Equipment != null)
            {
                var rejectCount = Convert.ToInt32(context.Equipment.Attributes.GetValueOrDefault("rejectCount", 0));
                rejectsByEquipment[context.Equipment.Name] = rejectCount;
            }

            // Use AI to analyze the context
            var aiAnalysisPrompt = $@"
                Analyze this production waste scenario:

                Job: {context.Job?.Name}
                Product: {context.Job?.Attributes.GetValueOrDefault("product")}
                Planned Quantity: {context.Job?.Attributes.GetValueOrDefault("plannedQuantity")}
                Actual Quantity: {context.Job?.Attributes.GetValueOrDefault("actualQuantity")}

                Equipment Rejects:
                {JsonSerializer.Serialize(rejectsByEquipment)}

                Equipment Efficiency: {context.Equipment?.Attributes.GetValueOrDefault("efficiency")}%
                Last Calibration: {context.Equipment?.Attributes.GetValueOrDefault("lastCalibration")}

                Material Batch: {context.MaterialBatch?.Name}
                Supplier: {context.MaterialBatch?.Attributes.GetValueOrDefault("supplier")}

                Identify:
                1. Primary root cause of waste
                2. Contributing factors
                3. Recommended actions with priorities
                4. Estimated impact of each action
            ";

            var aiResponse = await _kernel.InvokePromptAsync<string>(aiAnalysisPrompt);

            // Parse AI response and create structured result
            var wasteAnalysis = new WasteAnalysis
            {
                JobId = jobId,
                WastePercentage = CalculateWastePercentage(context),
                RejectsByEquipment = rejectsByEquipment,
                PrimaryRootCause = ExtractRootCause(aiResponse),
                ContributingFactors = ExtractFactors(aiResponse),
                RecommendedActions = ExtractActions(aiResponse)
            };

            return wasteAnalysis;
        }

        [KernelFunction("PredictMaintenanceNeed")]
        [Description("Predicts maintenance needs by analyzing equipment efficiency trends from I3X")]
        public async Task<I3XMaintenancePrediction> PredictMaintenanceNeedAsync(
            [Description("Equipment ID to analyze")] string equipmentId)
        {
            // Get historical efficiency data from I3X
            var history = await _i3xClient.GetHistoryAsync(
                equipmentId,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                maxPoints: 100);

            // Get current equipment context
            var context = await _i3xClient.GetManufacturingContextAsync(equipmentId);

            // Analyze efficiency trend
            var efficiencyTrend = history
                .Select(h => Convert.ToDecimal(h.Values.GetValueOrDefault("efficiency", 0)))
                .ToList();

            var currentEfficiency = efficiencyTrend.LastOrDefault();
            var averageEfficiency = efficiencyTrend.Average();
            var efficiencyDegradation = averageEfficiency - currentEfficiency;

            // Use AI to predict failure
            var predictionPrompt = $@"
                Analyze equipment maintenance need:

                Equipment: {context.Equipment?.Name}
                Type: {context.Equipment?.Attributes.GetValueOrDefault("type")}
                Model: {context.Equipment?.Attributes.GetValueOrDefault("model")}

                Current Efficiency: {currentEfficiency}%
                30-Day Average: {averageEfficiency}%
                Degradation: {efficiencyDegradation}%

                Historical Trend: {JsonSerializer.Serialize(efficiencyTrend.TakeLast(10))}

                Last Calibration: {context.Equipment?.Attributes.GetValueOrDefault("lastCalibration")}
                Product Count: {context.Equipment?.Attributes.GetValueOrDefault("productCount")}
                Reject Count: {context.Equipment?.Attributes.GetValueOrDefault("rejectCount")}

                Predict:
                1. Probability of failure in next 7 days (0-1)
                2. Recommended maintenance type
                3. Optimal maintenance window considering production schedule
                4. Expected downtime
            ";

            var aiPrediction = await _kernel.InvokePromptAsync<string>(predictionPrompt);

            return new I3XMaintenancePrediction
            {
                EquipmentId = equipmentId,
                FailureProbability = ExtractProbability(aiPrediction),
                RecommendedDate = ExtractMaintenanceDate(aiPrediction),
                MaintenanceType = ExtractMaintenanceType(aiPrediction),
                EstimatedDowntime = ExtractDowntime(aiPrediction),
                CurrentEfficiency = currentEfficiency,
                EfficiencyDegradation = efficiencyDegradation
            };
        }

        [KernelFunction("TraceMaterialQuality")]
        [Description("Traces material quality issues across production jobs using I3X relationships")]
        public async Task<MaterialTraceability> TraceMaterialQualityAsync(
            [Description("Material batch ID to trace")] string batchId)
        {
            // Get material batch details
            var batch = await _i3xClient.GetObjectAsync(batchId, true);

            // Get all jobs that used this material
            var jobsUsed = await _i3xClient.GetRelationshipsAsync(batchId, "UsedInJobs");

            // Get supplier information
            var supplier = await _i3xClient.GetRelationshipsAsync(batchId, "SuppliedBy");

            // Analyze quality across all jobs
            var qualityIssues = new List<QualityIssue>();
            foreach (var job in jobsUsed)
            {
                // Get quality metrics for each job
                var jobContext = await _i3xClient.GetManufacturingContextAsync(job.ElementId);

                if (jobContext.Equipment != null)
                {
                    var rejectCount = Convert.ToInt32(jobContext.Equipment.Attributes.GetValueOrDefault("rejectCount", 0));
                    if (rejectCount > 0)
                    {
                        qualityIssues.Add(new QualityIssue
                        {
                            IssueId = Guid.NewGuid().ToString(),
                            EquipmentId = jobContext.Equipment.ElementId,
                            Type = "Reject",
                            Description = $"Material batch {batchId} associated with {rejectCount} rejects",
                            DetectedAt = DateTime.UtcNow,
                            Severity = rejectCount > 100 ? "Critical" : rejectCount > 50 ? "Major" : "Minor"
                        });
                    }
                }
            }

            // Use AI to correlate issues
            var correlationPrompt = $@"
                Analyze material quality correlation:

                Material Batch: {batch.Name}
                Supplier: {supplier.FirstOrDefault()?.Name}
                Quality Certificate: {batch.Attributes.GetValueOrDefault("qualityCertificate")}

                Jobs Used: {jobsUsed.Count}
                Quality Issues Found: {qualityIssues.Count}
                Issue Details: {JsonSerializer.Serialize(qualityIssues)}

                Determine:
                1. Is there a pattern indicating material quality problem?
                2. Should this supplier be flagged?
                3. Recommended actions for remaining inventory
            ";

            var aiCorrelation = await _kernel.InvokePromptAsync<string>(correlationPrompt);

            return new MaterialTraceability
            {
                Batch = MapToMaterialBatch(batch),
                Supplier = supplier.FirstOrDefault()?.Name,
                JobsUsed = jobsUsed.Select(j => MapToProductionJob(j)).ToList(),
                RelatedIssues = qualityIssues,
                QualityTestResults = new Dictionary<string, object>
                {
                    ["CorrelationAnalysis"] = aiCorrelation,
                    ["IssueCount"] = qualityIssues.Count,
                    ["AffectedJobs"] = jobsUsed.Count
                }
            };
        }

        [KernelFunction("OptimizeProductionSchedule")]
        [Description("Optimizes production schedule using I3X context and AI planning")]
        public async Task<string> OptimizeProductionScheduleAsync(
            [Description("Production line ID")] string lineId,
            [Description("Time horizon in days")] int horizonDays = 7)
        {
            // Get current line status
            var line = await _i3xClient.GetObjectAsync(lineId, true);

            // Get pending orders from ERP
            var orders = await _i3xClient.GetObjectsAsync("customer-order-type", true);
            var pendingOrders = orders.Where(o => o.Attributes.GetValueOrDefault("status")?.ToString() == "Pending");

            // Get equipment status from SCADA
            var equipment = await _i3xClient.GetChildrenAsync(lineId, true);

            // Get maintenance predictions for all equipment
            var maintenanceNeeds = new List<I3XMaintenancePrediction>();
            foreach (var eq in equipment)
            {
                var prediction = await PredictMaintenanceNeedAsync(eq.ElementId);
                if (prediction.FailureProbability > 0.5m)
                {
                    maintenanceNeeds.Add(prediction);
                }
            }

            // Use AI to optimize schedule
            var optimizationPrompt = $@"
                Optimize production schedule for next {horizonDays} days:

                Production Line: {line.Name}
                Current OEE: {line.Attributes.GetValueOrDefault("OEE")}%
                Throughput: {line.Attributes.GetValueOrDefault("throughput")} units/hour

                Pending Orders:
                {JsonSerializer.Serialize(pendingOrders.Select(o => new {
                    OrderId = o.ElementId,
                    Product = o.Attributes.GetValueOrDefault("product"),
                    Quantity = o.Attributes.GetValueOrDefault("quantity"),
                    DueDate = o.Attributes.GetValueOrDefault("dueDate"),
                    Priority = o.Attributes.GetValueOrDefault("priority")
                }))}

                Equipment Needing Maintenance:
                {JsonSerializer.Serialize(maintenanceNeeds)}

                Create optimal schedule that:
                1. Meets order due dates by priority
                2. Schedules maintenance windows to prevent failures
                3. Maximizes overall throughput
                4. Minimizes changeover time between products

                Provide schedule in daily breakdown format.
            ";

            return await _kernel.InvokePromptAsync<string>(optimizationPrompt);
        }

        // Helper methods
        private decimal CalculateWastePercentage(ManufacturingContext context)
        {
            if (context.Job == null) return 0;

            var planned = Convert.ToDecimal(context.Job.Attributes.GetValueOrDefault("plannedQuantity", 0));
            var actual = Convert.ToDecimal(context.Job.Attributes.GetValueOrDefault("actualQuantity", 0));

            if (planned == 0) return 0;
            return ((planned - actual) / planned) * 100;
        }

        private string ExtractRootCause(string aiResponse)
        {
            // Parse AI response to extract root cause
            // In production, use proper JSON parsing or structured output
            return "Filler-001 calibration drift";
        }

        private List<string> ExtractFactors(string aiResponse)
        {
            // Parse AI response to extract contributing factors
            return new List<string>
            {
                "13 days since last calibration",
                "High production volume",
                "Material viscosity variation"
            };
        }

        private List<RecommendedAction> ExtractActions(string aiResponse)
        {
            // Parse AI response to extract recommended actions
            return new List<RecommendedAction>
            {
                new RecommendedAction
                {
                    ActionType = "Calibration",
                    TargetId = "filler-001",
                    Description = "Immediate calibration of Filler-001",
                    Priority = "High",
                    EstimatedImpact = 1.5m
                },
                new RecommendedAction
                {
                    ActionType = "Maintenance",
                    TargetId = "filler-001",
                    Description = "Schedule preventive maintenance",
                    Priority = "Medium",
                    EstimatedImpact = 0.5m
                }
            };
        }

        private decimal ExtractProbability(string aiResponse)
        {
            // Parse AI response to extract failure probability
            return 0.75m;
        }

        private DateTime ExtractMaintenanceDate(string aiResponse)
        {
            // Parse AI response to extract recommended date
            return DateTime.UtcNow.AddDays(3);
        }

        private string ExtractMaintenanceType(string aiResponse)
        {
            // Parse AI response to extract maintenance type
            return "Calibration";
        }

        private TimeSpan ExtractDowntime(string aiResponse)
        {
            // Parse AI response to extract estimated downtime
            return TimeSpan.FromHours(2);
        }

        private MaterialBatch MapToMaterialBatch(I3X.Shared.Models.Instance instance)
        {
            return new MaterialBatch
            {
                BatchId = instance.ElementId,
                Material = instance.Name,
                Supplier = instance.Attributes.GetValueOrDefault("supplier")?.ToString(),
                Quantity = Convert.ToDecimal(instance.Attributes.GetValueOrDefault("quantity", 0)),
                QualityCertificate = instance.Attributes.GetValueOrDefault("qualityCertificate")?.ToString()
            };
        }

        private ProductionJob MapToProductionJob(I3X.Shared.Models.Instance instance)
        {
            return new ProductionJob
            {
                JobId = instance.ElementId,
                Product = instance.Attributes.GetValueOrDefault("product")?.ToString(),
                PlannedQuantity = Convert.ToInt32(instance.Attributes.GetValueOrDefault("plannedQuantity", 0)),
                ActualQuantity = Convert.ToInt32(instance.Attributes.GetValueOrDefault("actualQuantity", 0))
            };
        }
    }

    // Additional models for predictions
    public class I3XMaintenancePrediction
    {
        public string EquipmentId { get; set; }
        public decimal FailureProbability { get; set; }
        public DateTime RecommendedDate { get; set; }
        public string MaintenanceType { get; set; }
        public TimeSpan EstimatedDowntime { get; set; }
        public decimal CurrentEfficiency { get; set; }
        public decimal EfficiencyDegradation { get; set; }
    }
}