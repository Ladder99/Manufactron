using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.Client.Plugins;

public class AnalyticsPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalyticsPlugin> _logger;
    private readonly string _aggregatorUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnalyticsPlugin(HttpClient httpClient, ILogger<AnalyticsPlugin> logger, string aggregatorUrl)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aggregatorUrl = aggregatorUrl;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [KernelFunction("AnalyzeProductionEfficiency")]
    [Description("Analyze production efficiency across lines, equipment, or the entire facility")]
    public async Task<string> AnalyzeProductionEfficiencyAsync(
        [Description("Optional: specific line or equipment ID to analyze")] string? targetId = null,
        [Description("Time range in hours (default 24)")] int hoursBack = 24)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return "Unable to retrieve production data";
            }

            var json = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (objects == null || !objects.Any())
            {
                return "No production data available";
            }

            List<Instance> targets;
            if (!string.IsNullOrEmpty(targetId))
            {
                var target = objects.FirstOrDefault(o => o.ElementId == targetId);
                if (target == null)
                {
                    return $"Target {targetId} not found";
                }

                if (target.TypeId?.Contains("line", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    targets = objects.Where(o => o.ParentId == targetId || o.ElementId == targetId).ToList();
                }
                else
                {
                    targets = new List<Instance> { target };
                }
            }
            else
            {
                targets = objects.Where(o =>
                    (o.TypeId?.Contains("line", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (o.TypeId?.Contains("equipment", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            var efficiencyData = new List<object>();
            double totalOEE = 0;
            int oeeCount = 0;

            foreach (var target in targets)
            {
                if (target.Attributes?.ContainsKey("OEE") ?? false)
                {
                    var oee = Convert.ToDouble(target.Attributes["OEE"]);
                    totalOEE += oee;
                    oeeCount++;

                    efficiencyData.Add(new
                    {
                        Id = target.ElementId,
                        Name = target.Name,
                        Type = target.TypeId,
                        OEE = oee,
                        Availability = target.Attributes.GetValueOrDefault("availability", "N/A"),
                        Performance = target.Attributes.GetValueOrDefault("performance", "N/A"),
                        Quality = target.Attributes.GetValueOrDefault("quality", "N/A"),
                        Status = target.Attributes.GetValueOrDefault("status", "state") ??
                                target.Attributes.GetValueOrDefault("state", "Unknown")
                    });
                }
            }

            var analysis = new
            {
                TimeRange = $"Last {hoursBack} hours",
                TargetScope = string.IsNullOrEmpty(targetId) ? "Entire Facility" : targetId,
                AverageOEE = oeeCount > 0 ? (totalOEE / oeeCount) : 0,
                TotalUnitsAnalyzed = targets.Count,
                UnitsWithOEEData = oeeCount,
                EfficiencyData = efficiencyData.OrderBy(e => ((dynamic)e).OEE).ToList(),
                Recommendations = GenerateEfficiencyRecommendations(efficiencyData)
            };

            return JsonSerializer.Serialize(analysis, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing production efficiency");
            return $"Error analyzing efficiency: {ex.Message}";
        }
    }

    [KernelFunction("DetectAnomalies")]
    [Description("Detect anomalies in production metrics, quality, or equipment behavior")]
    public async Task<string> DetectAnomaliesAsync(
        [Description("The element ID to analyze for anomalies")] string elementId,
        [Description("Metric to analyze (e.g., 'temperature', 'OEE', 'quality')")] string metric)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-7);

            var response = await _httpClient.GetAsync(
                $"{_aggregatorUrl}/api/i3x/history/{elementId}?startTime={startTime:yyyy-MM-ddTHH:mm:ss}Z&endTime={endTime:yyyy-MM-ddTHH:mm:ss}Z");

            if (!response.IsSuccessStatusCode)
            {
                return $"Unable to retrieve historical data for {elementId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var history = JsonSerializer.Deserialize<List<HistoricalValue>>(json, _jsonOptions);

            if (history == null || !history.Any())
            {
                return $"No historical data available for {elementId}";
            }

            var metricValues = history
                .Where(h => h.Values.ContainsKey(metric))
                .Select(h => new
                {
                    Timestamp = h.Timestamp,
                    Value = Convert.ToDouble(h.Values[metric])
                })
                .OrderBy(v => v.Timestamp)
                .ToList();

            if (!metricValues.Any())
            {
                return $"No data available for metric '{metric}' on {elementId}";
            }

            var values = metricValues.Select(v => v.Value).ToList();
            var mean = values.Average();
            var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));

            var anomalies = new List<object>();
            var upperThreshold = mean + (2 * stdDev);
            var lowerThreshold = mean - (2 * stdDev);

            foreach (var dataPoint in metricValues)
            {
                if (dataPoint.Value > upperThreshold || dataPoint.Value < lowerThreshold)
                {
                    var severity = Math.Abs(dataPoint.Value - mean) > (3 * stdDev) ? "High" : "Medium";
                    anomalies.Add(new
                    {
                        Timestamp = dataPoint.Timestamp,
                        Value = dataPoint.Value,
                        Deviation = dataPoint.Value - mean,
                        StandardDeviations = stdDev > 0 ? Math.Round((dataPoint.Value - mean) / stdDev, 2) : 0,
                        Severity = severity,
                        Type = dataPoint.Value > upperThreshold ? "Above Normal" : "Below Normal"
                    });
                }
            }

            var recentAnomalies = anomalies.Where(a =>
                ((dynamic)a).Timestamp > DateTime.UtcNow.AddHours(-24)).ToList();

            var analysis = new
            {
                ElementId = elementId,
                Metric = metric,
                AnalysisPeriod = "Last 7 days",
                Statistics = new
                {
                    Mean = Math.Round(mean, 2),
                    StdDev = Math.Round(stdDev, 2),
                    Min = Math.Round(values.Min(), 2),
                    Max = Math.Round(values.Max(), 2),
                    DataPoints = values.Count
                },
                Thresholds = new
                {
                    Upper = Math.Round(upperThreshold, 2),
                    Lower = Math.Round(lowerThreshold, 2)
                },
                TotalAnomalies = anomalies.Count,
                RecentAnomalies = recentAnomalies.Count,
                Anomalies = anomalies.Take(10),
                Risk = CalculateAnomalyRisk(anomalies.Count, values.Count, recentAnomalies.Count)
            };

            return JsonSerializer.Serialize(analysis, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalies for {ElementId}", elementId);
            return $"Error detecting anomalies: {ex.Message}";
        }
    }

    [KernelFunction("AnalyzeQualityTrends")]
    [Description("Analyze quality trends and identify potential quality issues")]
    public async Task<string> AnalyzeQualityTrendsAsync(
        [Description("Optional: specific line or job ID")] string? targetId = null,
        [Description("Days to analyze (default 7)")] int daysBack = 7)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return "Unable to retrieve quality data";
            }

            var json = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (objects == null || !objects.Any())
            {
                return "No quality data available";
            }

            var qualityData = new List<object>();
            var jobs = objects.Where(o => o.TypeId?.Contains("job", StringComparison.OrdinalIgnoreCase) ?? false);

            if (!string.IsNullOrEmpty(targetId))
            {
                jobs = jobs.Where(j => j.ElementId == targetId || j.Attributes?.GetValueOrDefault("lineId") == targetId);
            }

            foreach (var job in jobs)
            {
                if (job.Attributes == null) continue;

                var plannedQty = Convert.ToDouble(job.Attributes.GetValueOrDefault("plannedQuantity", "0"));
                var actualQty = Convert.ToDouble(job.Attributes.GetValueOrDefault("actualQuantity", "0"));
                var defects = Convert.ToDouble(job.Attributes.GetValueOrDefault("defects", "0"));

                var qualityRate = actualQty > 0 ? ((actualQty - defects) / actualQty) * 100 : 0;

                qualityData.Add(new
                {
                    JobId = job.ElementId,
                    Product = job.Attributes.GetValueOrDefault("product", "Unknown"),
                    PlannedQuantity = plannedQty,
                    ActualQuantity = actualQty,
                    Defects = defects,
                    QualityRate = Math.Round(qualityRate, 2),
                    Status = job.Attributes.GetValueOrDefault("status", "Unknown"),
                    Date = job.Attributes.GetValueOrDefault("startTime", DateTime.UtcNow.ToString())
                });
            }

            var avgQuality = qualityData.Any() ?
                qualityData.Average(q => (double)((dynamic)q).QualityRate) : 0;

            var qualityIssues = qualityData.Where(q => ((dynamic)q).QualityRate < 95).ToList();

            var trends = new
            {
                Period = $"Last {daysBack} days",
                TotalJobs = qualityData.Count,
                AverageQualityRate = Math.Round(avgQuality, 2),
                JobsWithQualityIssues = qualityIssues.Count,
                QualityData = qualityData.OrderByDescending(q => ((dynamic)q).Date).Take(20),
                QualityIssues = qualityIssues,
                Recommendations = GenerateQualityRecommendations(avgQuality, qualityIssues)
            };

            return JsonSerializer.Serialize(trends, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing quality trends");
            return $"Error analyzing quality: {ex.Message}";
        }
    }

    [KernelFunction("CalculateProductionKPIs")]
    [Description("Calculate key production KPIs including throughput, cycle time, and utilization")]
    public async Task<string> CalculateProductionKPIsAsync(
        [Description("Time range in hours (default 24)")] int hoursBack = 24)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/objects?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                return "Unable to retrieve production data";
            }

            var json = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<List<Instance>>(json, _jsonOptions);

            if (objects == null || !objects.Any())
            {
                return "No production data available";
            }

            var jobs = objects.Where(o => o.TypeId?.Contains("job", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            var lines = objects.Where(o => o.TypeId?.Contains("line", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            var equipment = objects.Where(o => o.TypeId?.Contains("equipment", StringComparison.OrdinalIgnoreCase) ?? false).ToList();

            var totalProduced = jobs.Sum(j => Convert.ToDouble(j.Attributes?.GetValueOrDefault("actualQuantity", "0") ?? "0"));
            var totalPlanned = jobs.Sum(j => Convert.ToDouble(j.Attributes?.GetValueOrDefault("plannedQuantity", "0") ?? "0"));

            var activeEquipment = equipment.Count(e =>
                e.Attributes?.GetValueOrDefault("state", "")?.ToString()?.ToLower() == "running");

            var avgOEE = equipment
                .Where(e => e.Attributes?.ContainsKey("OEE") ?? false)
                .Select(e => Convert.ToDouble(e.Attributes["OEE"]))
                .DefaultIfEmpty(0)
                .Average();

            var kpis = new
            {
                TimeRange = $"Last {hoursBack} hours",
                Production = new
                {
                    TotalProduced = totalProduced,
                    TotalPlanned = totalPlanned,
                    AttainmentRate = totalPlanned > 0 ? Math.Round((totalProduced / totalPlanned) * 100, 2) : 0,
                    ActiveJobs = jobs.Count(j => j.Attributes?.GetValueOrDefault("status", "")?.ToString()?.ToLower() == "running")
                },
                Equipment = new
                {
                    TotalEquipment = equipment.Count,
                    ActiveEquipment = activeEquipment,
                    UtilizationRate = equipment.Any() ? Math.Round(((double)activeEquipment / equipment.Count) * 100, 2) : 0,
                    AverageOEE = Math.Round(avgOEE, 2)
                },
                Lines = new
                {
                    TotalLines = lines.Count,
                    ActiveLines = lines.Count(l => l.Attributes?.GetValueOrDefault("status", "")?.ToString()?.ToLower() == "running"),
                    AverageLineOEE = lines
                        .Where(l => l.Attributes?.ContainsKey("OEE") ?? false)
                        .Select(l => Convert.ToDouble(l.Attributes["OEE"]))
                        .DefaultIfEmpty(0)
                        .Average()
                },
                Performance = ClassifyPerformance(avgOEE, totalProduced / Math.Max(totalPlanned, 1))
            };

            return JsonSerializer.Serialize(kpis, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating KPIs");
            return $"Error calculating KPIs: {ex.Message}";
        }
    }

    private List<string> GenerateEfficiencyRecommendations(List<object> efficiencyData)
    {
        var recommendations = new List<string>();

        var lowPerformers = efficiencyData.Where(e => ((dynamic)e).OEE < 0.6).ToList();
        if (lowPerformers.Any())
        {
            recommendations.Add($"Focus improvement efforts on {lowPerformers.Count} units with OEE below 60%");
            recommendations.Add("Consider preventive maintenance for low-performing equipment");
        }

        var avgOEE = efficiencyData.Any() ? efficiencyData.Average(e => (double)((dynamic)e).OEE) : 0;
        if (avgOEE < 0.75)
        {
            recommendations.Add("Overall OEE is below world-class standards (85%). Implement continuous improvement program");
            recommendations.Add("Review and optimize changeover procedures");
            recommendations.Add("Analyze and reduce minor stops and speed losses");
        }

        return recommendations;
    }

    private List<string> GenerateQualityRecommendations(double avgQuality, List<object> qualityIssues)
    {
        var recommendations = new List<string>();

        if (avgQuality < 98)
        {
            recommendations.Add("Quality rate is below target. Implement enhanced quality control measures");
        }

        if (qualityIssues.Count > 0)
        {
            recommendations.Add($"Investigate root causes for {qualityIssues.Count} jobs with quality issues");
            recommendations.Add("Consider implementing in-line quality inspection");
            recommendations.Add("Review operator training and standard operating procedures");
        }

        if (avgQuality < 95)
        {
            recommendations.Add("Critical: Quality levels require immediate intervention");
            recommendations.Add("Perform comprehensive equipment calibration");
            recommendations.Add("Review material quality and supplier performance");
        }

        return recommendations;
    }

    private string CalculateAnomalyRisk(int totalAnomalies, int totalDataPoints, int recentAnomalies)
    {
        var anomalyRate = (double)totalAnomalies / totalDataPoints;
        var recentWeight = recentAnomalies > 0 ? 1.5 : 1.0;

        var riskScore = anomalyRate * recentWeight;

        if (riskScore > 0.15) return "High";
        if (riskScore > 0.05) return "Medium";
        return "Low";
    }

    private string ClassifyPerformance(double oee, double attainment)
    {
        if (oee >= 0.85 && attainment >= 0.95) return "World Class";
        if (oee >= 0.75 && attainment >= 0.90) return "Good";
        if (oee >= 0.65 && attainment >= 0.85) return "Fair";
        return "Needs Improvement";
    }
}