using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Manufactron.Services.I3X;
using Manufactron.Planning;
using Manufactron.Plugins;
using Manufactron.Models;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models.Manufacturing;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var services = new ServiceCollection();

services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Configure I3X services
services.AddHttpClient<II3XClient, I3XClientService>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Initialize I3X client
var i3xClient = serviceProvider.GetRequiredService<II3XClient>();

// Build Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton<ILoggerFactory>(serviceProvider.GetRequiredService<ILoggerFactory>());

var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4-turbo-preview";

if (string.IsNullOrEmpty(apiKey) || apiKey == "your-openai-api-key-here")
{
    Console.WriteLine("Please configure your OpenAI API key in appsettings.json or set the OPENAI_API_KEY environment variable.");
    Console.WriteLine("You can get an API key from: https://platform.openai.com/api-keys");
    Console.WriteLine("\nFor demo purposes, the system will run without AI features.");
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
}

if (!string.IsNullOrEmpty(apiKey) && apiKey != "your-openai-api-key-here")
{
    kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
}

var kernel = kernelBuilder.Build();

// Create I3X-aware components
var i3xPlugin = new I3XAwareProductionPlugin(i3xClient, kernel);
var orchestratorLogger = serviceProvider.GetRequiredService<ILogger<I3XManufacturingOrchestrator>>();
var orchestrator = new I3XManufacturingOrchestrator(i3xClient, kernel, orchestratorLogger);

Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     MANUFACTRON - I3X Manufacturing Intelligence System     ║");
Console.WriteLine("║         Powered by I3X Standards & Semantic Kernel         ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// Check I3X services availability
await CheckI3XServices(i3xClient, logger);

// Run interactive menu
await RunInteractiveMenu(i3xClient, i3xPlugin, orchestrator, kernel, logger);

logger.LogInformation("Manufacturing Agent System Shutdown Complete");

async Task CheckI3XServices(II3XClient client, ILogger logger)
{
    Console.WriteLine("🔍 Checking I3X Aggregator Service...\n");

    var config = configuration.GetSection("I3XServices");
    var aggregatorUrl = config["AggregatorEndpoint"] ?? "http://localhost:7000";

    // Check aggregator which will verify all downstream services
    var services = new[]
    {
        ("I3X Aggregator", aggregatorUrl)
    };

    bool allAvailable = true;

    foreach (var (name, url) in services)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetAsync($"{url}/api/i3x/namespaces");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ✅ {name} Service: Available at {url}");
            }
            else
            {
                Console.WriteLine($"  ⚠️ {name} Service: Responded with {response.StatusCode}");
                allAvailable = false;
            }
        }
        catch
        {
            Console.WriteLine($"  ❌ {name} Service: Not available at {url}");
            allAvailable = false;
        }
    }

    if (!allAvailable)
    {
        Console.WriteLine("\n⚠️ I3X Aggregator service is not available.");
        Console.WriteLine("To start all services, run: .\\Start-AllServices.ps1");
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
    }
    else
    {
        Console.WriteLine("\n✅ I3X Aggregator is available and can aggregate data from ERP, MES, and SCADA services.");
    }

    Console.WriteLine();
}

async Task RunInteractiveMenu(
    II3XClient i3xClient,
    I3XAwareProductionPlugin plugin,
    I3XManufacturingOrchestrator orchestrator,
    Kernel kernel,
    ILogger logger)
{
    while (true)
    {
        Console.WriteLine("\n═══ I3X Manufacturing Intelligence Menu ═══");
        Console.WriteLine("1. View Production Status (I3X)");
        Console.WriteLine("2. Analyze Production Waste");
        Console.WriteLine("3. Predict Equipment Maintenance");
        Console.WriteLine("4. Trace Material Quality");
        Console.WriteLine("5. Optimize Production Schedule");
        Console.WriteLine("6. Monitor Real-Time Updates");
        Console.WriteLine("7. Run Demo Scenario");
        Console.WriteLine("8. Natural Language Chat (AI)");
        Console.WriteLine("9. Exit");
        Console.Write("\nSelect option: ");

        var choice = Console.ReadLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await ViewProductionStatus(i3xClient);
                    break;
                case "2":
                    await AnalyzeWaste(plugin);
                    break;
                case "3":
                    await PredictMaintenance(plugin);
                    break;
                case "4":
                    await TraceMaterial(plugin);
                    break;
                case "5":
                    await OptimizeSchedule(plugin);
                    break;
                case "6":
                    await MonitorRealTime(i3xClient);
                    break;
                case "7":
                    await RunDemoScenario(orchestrator, plugin);
                    break;
                case "8":
                    await NaturalLanguageChat(kernel, plugin);
                    break;
                case "9":
                    Console.WriteLine("\nShutting down Manufacturing Intelligence System...");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request");
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }
}

async Task ViewProductionStatus(II3XClient client)
{
    Console.WriteLine("\n📊 Production Status via I3X\n");

    try
    {
        // Get job status from MES
        var jobContext = await client.GetManufacturingContextAsync("job-J-2025-001");

        if (jobContext?.Job != null)
        {
            Console.WriteLine($"Job: {jobContext.Job.Name} ({jobContext.Job.ElementId})");
            if (jobContext.Job.Attributes != null)
            {
                // Use the actual attribute keys from the API response
                var product = jobContext.Job.Attributes.GetValueOrDefault("product", "N/A");
                var target = jobContext.Job.Attributes.GetValueOrDefault("plannedQuantity", "N/A");
                var produced = jobContext.Job.Attributes.GetValueOrDefault("actualQuantity", "N/A");
                var status = jobContext.Job.Attributes.GetValueOrDefault("status", "N/A");

                Console.WriteLine($"Product: {product}");
                Console.WriteLine($"Target: {target} units");
                Console.WriteLine($"Produced: {produced} units");
                Console.WriteLine($"Status: {status}");
            }
        }

        if (jobContext?.Line != null)
        {
            Console.WriteLine($"\nProduction Line: {jobContext.Line.Name}");
            if (jobContext.Line.Attributes != null)
            {
                // Use lowercase keys to match API response
                var oee = jobContext.Line.Attributes.GetValueOrDefault("oee", jobContext.Line.Attributes.GetValueOrDefault("OEE", "N/A"));
                var status = jobContext.Line.Attributes.GetValueOrDefault("status", "N/A");
                Console.WriteLine($"OEE: {oee}%");
                Console.WriteLine($"Status: {status}");
            }
        }

        if (jobContext?.Equipment != null)
        {
            Console.WriteLine("\nEquipment Status:");
            var equipment = jobContext.Equipment;
            if (equipment.Attributes != null)
            {
                // Use lowercase keys to match API response
                var status = equipment.Attributes.GetValueOrDefault("status", "Unknown");
                var efficiency = equipment.Attributes.GetValueOrDefault("efficiency", "N/A");
                Console.WriteLine($"  • {equipment.Name}: {status} (Efficiency: {efficiency}%)");
            }
            else
            {
                Console.WriteLine($"  • {equipment.Name}: Status information not available");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error retrieving production status: {ex.Message}");
    }
}

async Task AnalyzeWaste(I3XAwareProductionPlugin plugin)
{
    Console.Write("\nEnter Job ID (default: job-J-2025-001): ");
    var jobId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(jobId))
        jobId = "job-J-2025-001";

    Console.WriteLine("\n🔍 Analyzing production waste...\n");

    var analysis = await plugin.AnalyzeProductionWasteAsync(jobId);

    Console.WriteLine($"Job: {analysis.JobId}");
    Console.WriteLine($"Waste Percentage: {analysis.WastePercentage:F2}%");
    Console.WriteLine($"Primary Cause: {analysis.PrimaryRootCause}");

    if (analysis.ContributingFactors?.Any() == true)
    {
        Console.WriteLine("\nContributing Factors:");
        foreach (var cause in analysis.ContributingFactors)
        {
            Console.WriteLine($"  • {cause}");
        }
    }

    if (analysis.RecommendedActions?.Any() == true)
    {
        Console.WriteLine("\nRecommended Actions:");
        foreach (var rec in analysis.RecommendedActions)
        {
            Console.WriteLine($"  • {rec.Description} (Priority: {rec.Priority})");
        }
    }

    // Calculate total estimated impact from all actions
    var totalImpact = analysis.RecommendedActions?.Sum(a => a.EstimatedImpact) ?? 0;
    Console.WriteLine($"\nEstimated Impact: {totalImpact:F1}% waste reduction");
}

async Task PredictMaintenance(I3XAwareProductionPlugin plugin)
{
    Console.Write("\nEnter Equipment ID (default: filler-001): ");
    var equipmentId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(equipmentId))
        equipmentId = "filler-001";

    Console.WriteLine("\n🔧 Predicting maintenance needs...\n");

    var prediction = await plugin.PredictMaintenanceNeedAsync(equipmentId);

    Console.WriteLine($"Equipment: {prediction.EquipmentId}");
    Console.WriteLine($"Current Efficiency: {prediction.CurrentEfficiency:F1}%");
    Console.WriteLine($"Failure Probability: {prediction.FailureProbability * 100:F1}%");
    Console.WriteLine($"Recommended Date: {prediction.RecommendedDate:yyyy-MM-dd}");
    Console.WriteLine($"Maintenance Type: {prediction.MaintenanceType}");

    if (prediction.EfficiencyDegradation > 0)
    {
        Console.WriteLine($"\nEfficiency Degradation: {prediction.EfficiencyDegradation:F1}%");
    }

    Console.WriteLine($"\nEstimated Downtime: {prediction.EstimatedDowntime.TotalHours:F1} hours");
}

async Task TraceMaterial(I3XAwareProductionPlugin plugin)
{
    Console.Write("\nEnter Batch ID (default: batch-MB-2025-0142): ");
    var batchId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(batchId))
        batchId = "batch-MB-2025-0142";

    Console.WriteLine("\n📦 Tracing material quality...\n");

    var trace = await plugin.TraceMaterialQualityAsync(batchId);

    Console.WriteLine($"Batch: {trace.Batch?.BatchId}");
    Console.WriteLine($"Material: {trace.Batch?.Material}");
    Console.WriteLine($"Supplier: {trace.Supplier}");
    if (trace.QualityTestResults?.ContainsKey("QualityScore") == true)
    {
        Console.WriteLine($"Quality Analysis: {trace.QualityTestResults["CorrelationAnalysis"]}");
    }

    if (trace.JobsUsed?.Any() == true)
    {
        Console.WriteLine("\nUsed in Jobs:");
        foreach (var job in trace.JobsUsed)
        {
            Console.WriteLine($"  • Job {job.JobId}: {job.Product} ({job.ActualQuantity}/{job.PlannedQuantity} units)");
        }
    }

    if (trace.RelatedIssues?.Any() == true)
    {
        Console.WriteLine("\nRelated Issues:");
        foreach (var issue in trace.RelatedIssues)
        {
            Console.WriteLine($"  • {issue.Type}: {issue.Description} (Severity: {issue.Severity})");
        }
    }
}

async Task OptimizeSchedule(I3XAwareProductionPlugin plugin)
{
    Console.Write("\nEnter Line ID (default: line-1): ");
    var lineId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(lineId))
        lineId = "line-1";

    Console.Write("Days to optimize (default: 7): ");
    if (!int.TryParse(Console.ReadLine(), out int days))
        days = 7;

    Console.WriteLine("\n📅 Optimizing production schedule...\n");

    var scheduleResult = await plugin.OptimizeProductionScheduleAsync(lineId, days);

    Console.WriteLine($"Line: {lineId}");
    Console.WriteLine($"Optimization Period: {days} days\n");
    Console.WriteLine("Optimized Schedule:");
    Console.WriteLine(scheduleResult);
}

async Task MonitorRealTime(II3XClient client)
{
    Console.WriteLine("\n📡 Monitoring Real-Time I3X Updates");
    Console.WriteLine("Press any key to stop monitoring...\n");

    var cts = new CancellationTokenSource();

    // Start monitoring in background
    var monitorTask = Task.Run(async () =>
    {
        try
        {
            await foreach (var update in client.SubscribeToUpdatesAsync("job-J-2025-001", cts.Token))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Update from {update.Source}:");
                Console.WriteLine($"  Type: {update.EventType}");
                Console.WriteLine($"  Entity: {update.UpdatedEntity}");

                if (update.ChangedAttributes?.Any() == true)
                {
                    foreach (var attr in update.ChangedAttributes)
                    {
                        Console.WriteLine($"  • {attr.Key}: {attr.Value}");
                    }
                }

                Console.WriteLine();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    });

    Console.ReadKey(true);
    cts.Cancel();

    try
    {
        await monitorTask;
    }
    catch (OperationCanceledException)
    {
        // Expected
    }

    Console.WriteLine("\nMonitoring stopped.");
}

async Task RunDemoScenario(I3XManufacturingOrchestrator orchestrator, I3XAwareProductionPlugin plugin)
{
    Console.WriteLine("\n🎬 Running Beverage Production Demo Scenario");
    Console.WriteLine("═══════════════════════════════════════════════\n");

    Console.WriteLine("📋 Scenario: Premium Cola Production for Walmart");
    Console.WriteLine("  Order: #12345");
    Console.WriteLine("  Job: J-2025-001");
    Console.WriteLine("  Product: Premium Cola 500ml");
    Console.WriteLine("  Target: 10,000 units\n");

    await Task.Delay(1000);

    Console.WriteLine("🏭 Starting production analysis...\n");

    // Analyze waste
    Console.WriteLine("Step 1: Analyzing production waste");
    var wasteAnalysis = await plugin.AnalyzeProductionWasteAsync("job-J-2025-001");
    Console.WriteLine($"  ⚠️ Detected {wasteAnalysis.WastePercentage:F1}% waste!");
    Console.WriteLine($"  Root cause: {wasteAnalysis.PrimaryRootCause}\n");

    await Task.Delay(1000);

    // Check equipment
    Console.WriteLine("Step 2: Investigating equipment status");
    var maintenance = await plugin.PredictMaintenanceNeedAsync("filler-001");
    Console.WriteLine($"  🔧 Filler-001 efficiency: {maintenance.CurrentEfficiency:F1}%");
    Console.WriteLine($"  Failure probability: {maintenance.FailureProbability * 100:F1}%");
    Console.WriteLine($"  Last calibration: 13 days ago (overdue!)\n");

    await Task.Delay(1000);

    // Trace materials
    Console.WriteLine("Step 3: Checking material quality");
    var materialTrace = await plugin.TraceMaterialQualityAsync("batch-MB-2025-0142");
    Console.WriteLine($"  📦 Batch {materialTrace.Batch?.BatchId} from {materialTrace.Supplier}");
    var issueCount = materialTrace.RelatedIssues?.Count ?? 0;
    Console.WriteLine($"  Quality issues found: {issueCount}");
    Console.WriteLine($"  Material is certified and within spec\n");

    await Task.Delay(1000);

    // Generate response
    Console.WriteLine("Step 4: Orchestrating response");
    var evt = new ManufacturingEvent
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = "EfficiencyDrop",
        Source = "filler-001",
        Severity = "High",
        Timestamp = DateTime.UtcNow,
        Data = new Dictionary<string, object>
        {
            ["RejectCount"] = 150,
            ["WastePercentage"] = 1.5,
            ["DaysSinceCalibration"] = 13
        }
    };

    var response = await orchestrator.HandleManufacturingEventAsync(evt);

    Console.WriteLine("\n✅ Orchestrated Response:");
    Console.WriteLine($"  Success: {response.Success}");
    Console.WriteLine($"  Priority: {response.Priority}");
    Console.WriteLine($"  Immediate Action: {response.ImmediateAction}");

    if (response.ActionsTaken?.Any() == true)
    {
        Console.WriteLine("\n  Actions Taken:");
        foreach (var action in response.ActionsTaken)
        {
            Console.WriteLine($"    • {action}");
        }
    }

    Console.WriteLine("\n📊 Summary:");
    Console.WriteLine("  ✅ Root cause identified: Filler-001 calibration drift");
    Console.WriteLine("  ✅ Immediate action: Schedule calibration");
    Console.WriteLine("  ✅ Expected waste reduction: 1.5% → 0.3%");
    Console.WriteLine("  ✅ Estimated savings: $15,000");

    Console.WriteLine("\n🎬 Demo scenario complete!");
}

async Task NaturalLanguageChat(Kernel kernel, I3XAwareProductionPlugin plugin)
{
    if (string.IsNullOrEmpty(kernel.Services.GetService<IChatCompletionService>()?.ToString()))
    {
        Console.WriteLine("\n⚠️ AI features require OpenAI API key configuration.");
        Console.WriteLine("Please configure your API key in appsettings.json");
        return;
    }

    Console.WriteLine("\n🤖 Natural Language Chat with I3X Intelligence");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Chat naturally about manufacturing operations.");
    Console.WriteLine("The AI will use I3X data to answer your questions.\n");
    Console.WriteLine("Examples:");
    Console.WriteLine("• \"Why is there waste in job J-2025-001?\"");
    Console.WriteLine("• \"What equipment needs maintenance?\"");
    Console.WriteLine("• \"Tell me about the material quality\"");
    Console.WriteLine("\nType 'exit' to return to the main menu.\n");

    var chatService = kernel.GetRequiredService<IChatCompletionService>();
    var chatHistory = new ChatHistory();

    chatHistory.AddSystemMessage(@"You are an I3X-aware manufacturing assistant with access to real-time
manufacturing data from ERP, MES, and SCADA systems. You help identify production issues,
analyze waste, predict maintenance needs, and optimize manufacturing operations.

Current context:
- Job J-2025-001: Premium Cola production for Walmart
- Line-1 running at 82.5% OEE
- Filler-001 has calibration drift causing 1.5% waste
- 150 rejects detected, last calibration 13 days ago");

    var settings = new OpenAIPromptExecutionSettings
    {
        Temperature = 0.7,
        MaxTokens = 500
    };

    while (true)
    {
        Console.Write("\nYou: ");
        var userInput = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
        {
            Console.WriteLine("Returning to main menu...");
            break;
        }

        chatHistory.AddUserMessage(userInput);

        try
        {
            Console.Write("\nAI: ");
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                kernel
            );

            Console.WriteLine(response.Content);
            chatHistory.AddAssistantMessage(response.Content ?? "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (chatHistory.Count > 0)
                chatHistory.RemoveAt(chatHistory.Count - 1);
        }
    }
}