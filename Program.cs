using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.Plugins.Memory;
#pragma warning restore SKEXP0001
using Manufactron.Agents;
using Manufactron.Integration;
using Manufactron.Memory;
using Manufactron.Models;
using Manufactron.Planning;
using Manufactron.Plugins;

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

services.AddHttpClient<ISystemIntegrationService, SystemIntegrationService>();

// Add all plugin dependencies
services.AddSingleton<ProductionMonitoringPlugin>();
services.AddSingleton<QualityControlPlugin>();
services.AddSingleton<MaintenancePlugin>();

var serviceProvider = services.BuildServiceProvider();

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton<ILoggerFactory>(serviceProvider.GetRequiredService<ILoggerFactory>());

var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4-turbo-preview";

if (string.IsNullOrEmpty(apiKey) || apiKey == "your-openai-api-key-here")
{
    Console.WriteLine("Please configure your OpenAI API key in appsettings.json or set the OPENAI_API_KEY environment variable.");
    Console.WriteLine("You can get an API key from: https://platform.openai.com/api-keys");
    return;
}

kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0050
var memoryBuilder = new MemoryBuilder();
memoryBuilder.WithOpenAITextEmbeddingGeneration(
    configuration["OpenAI:EmbeddingModelId"] ?? "text-embedding-ada-002",
    apiKey);
memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
var memory = memoryBuilder.Build();
#pragma warning restore SKEXP0001, SKEXP0010, SKEXP0050

#pragma warning disable SKEXP0001
kernelBuilder.Services.AddSingleton<ISemanticTextMemory>(memory);
#pragma warning restore SKEXP0001

kernelBuilder.Services.AddSingleton<IManufacturingMemory, ManufacturingMemory>();
kernelBuilder.Services.AddSingleton<IManufacturingPlanner, ManufacturingPlanner>();
kernelBuilder.Services.AddSingleton(serviceProvider.GetRequiredService<ISystemIntegrationService>());

// Register logging and plugins with kernel's DI container
kernelBuilder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
kernelBuilder.Services.AddSingleton<ProductionMonitoringPlugin>();
kernelBuilder.Services.AddSingleton<QualityControlPlugin>();
kernelBuilder.Services.AddSingleton<MaintenancePlugin>();

var kernel = kernelBuilder.Build();

kernel.ImportPluginFromType<ProductionMonitoringPlugin>("ProductionMonitoring");
kernel.ImportPluginFromType<QualityControlPlugin>("QualityControl");
kernel.ImportPluginFromType<MaintenancePlugin>("Maintenance");

#pragma warning disable SKEXP0001, SKEXP0050
kernel.ImportPluginFromObject(new TextMemoryPlugin(memory), "Memory");
#pragma warning restore SKEXP0001, SKEXP0050

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Manufacturing Agent System Starting...");

var manufacturingMemory = new ManufacturingMemory(memory, serviceProvider.GetRequiredService<ILogger<ManufacturingMemory>>());
var planner = new ManufacturingPlanner(kernel, serviceProvider.GetRequiredService<ILogger<ManufacturingPlanner>>());
var agent = new ManufacturingAgent(kernel, planner, manufacturingMemory, serviceProvider.GetRequiredService<ILogger<ManufacturingAgent>>());

var orchestrator = new ManufacturingOrchestrator(serviceProvider.GetRequiredService<ILogger<ManufacturingOrchestrator>>());

Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     MANUFACTRON - Manufacturing Intelligence System         ║");
Console.WriteLine("║         Powered by OpenAI and Semantic Kernel              ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

await RunInteractiveDemo(agent, kernel, logger);

async Task RunInteractiveDemo(ManufacturingAgent agent, Kernel kernel, ILogger logger)
{
    while (true)
    {
        Console.WriteLine("\n═══ Manufacturing Agent Menu ═══");
        Console.WriteLine("1. Check Production Status");
        Console.WriteLine("2. Analyze Quality Metrics");
        Console.WriteLine("3. Predict Maintenance Needs");
        Console.WriteLine("4. Detect Anomalies");
        Console.WriteLine("5. Generate Optimization Recommendations");
        Console.WriteLine("6. Ask Manufacturing Question");
        Console.WriteLine("7. Simulate Manufacturing Event");
        Console.WriteLine("8. Natural Language Chat (with Function Calling)");
        Console.WriteLine("9. Exit");
        Console.Write("\nSelect option: ");

        var choice = Console.ReadLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await CheckProductionStatus(kernel);
                    break;
                case "2":
                    await AnalyzeQuality(kernel);
                    break;
                case "3":
                    await PredictMaintenance(kernel);
                    break;
                case "4":
                    await DetectAnomalies(kernel);
                    break;
                case "5":
                    await GenerateRecommendations(agent);
                    break;
                case "6":
                    await AskQuestion(agent);
                    break;
                case "7":
                    await SimulateEvent(orchestrator);
                    break;
                case "8":
                    await NaturalLanguageChat(kernel);
                    break;
                case "9":
                    Console.WriteLine("\nShutting down Manufacturing Agent System...");
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

async Task CheckProductionStatus(Kernel kernel)
{
    Console.Write("\nEnter Line ID (e.g., LINE-001): ");
    var lineId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(lineId))
        lineId = "LINE-001";

    var plugin = kernel.Plugins["ProductionMonitoring"];
    var result = await kernel.InvokeAsync(plugin["GetProductionStatus"], new() { ["lineId"] = lineId });

    Console.WriteLine($"\n📊 Production Status for {lineId}:");
    var statusValue = result.GetValue<ProductionStatus>();
    if (statusValue != null)
    {
        Console.WriteLine($"  • Line: {statusValue.LineId}");
        Console.WriteLine($"  • Status: {statusValue.Status}");
        Console.WriteLine($"  • Throughput: {statusValue.Throughput:N0} units/hour");
        Console.WriteLine($"  • Efficiency: {statusValue.Efficiency:P0}");
        Console.WriteLine($"  • Last Updated: {statusValue.LastUpdated:HH:mm:ss}");

        if (statusValue.Metrics?.Count > 0)
        {
            Console.WriteLine($"  • Metrics:");
            foreach (var metric in statusValue.Metrics)
            {
                Console.WriteLine($"    - {metric.Key}: {metric.Value}");
            }
        }
    }
    else
    {
        Console.WriteLine($"  Unable to retrieve production status");
        Console.WriteLine($"  Result: {result}");
    }

    var oeeResult = await kernel.InvokeAsync(plugin["CalculateOEE"], new() { ["lineId"] = lineId, ["periodHours"] = 8 });
    Console.WriteLine($"\n📈 OEE Metrics (8 hours):");
    var oeeValue = oeeResult.GetValue<double>();
    Console.WriteLine($"  • Overall OEE: {oeeValue:P1}");
}

async Task AnalyzeQuality(Kernel kernel)
{
    Console.Write("\nEnter Batch ID (e.g., BATCH-001): ");
    var batchId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(batchId))
        batchId = "BATCH-001";

    var plugin = kernel.Plugins["QualityControl"];
    var result = await kernel.InvokeAsync(plugin["AnalyzeQuality"], new() { ["batchId"] = batchId });

    Console.WriteLine($"\n🔍 Quality Analysis for {batchId}:");
    var analysis = result.GetValue<QualityReport>();
    if (analysis != null)
    {
        Console.WriteLine($"  • Batch: {analysis.BatchId}");
        Console.WriteLine($"  • Quality Score: {analysis.QualityScore:P0}");
        Console.WriteLine($"  • Status: {(analysis.PassedQualityCheck ? "✅ PASSED" : "❌ FAILED")}");
        Console.WriteLine($"  • Inspection Date: {analysis.InspectionDate:yyyy-MM-dd HH:mm}");

        if (analysis.Defects?.Count > 0)
        {
            Console.WriteLine($"  • Defects Found:");
            foreach (var defect in analysis.Defects)
            {
                Console.WriteLine($"    - {defect}");
            }
        }

        if (analysis.Metrics?.Count > 0)
        {
            Console.WriteLine($"  • Quality Metrics:");
            foreach (var metric in analysis.Metrics)
            {
                Console.WriteLine($"    - {metric.Name}: {metric.Value:N2} (Target: {metric.Target:N2}, {(metric.InSpec ? "✓" : "✗")})");
            }
        }
    }
    else
    {
        Console.WriteLine(result);
    }
}

async Task PredictMaintenance(Kernel kernel)
{
    Console.Write("\nEnter Equipment ID (e.g., EQUIP-001): ");
    var equipmentId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(equipmentId))
        equipmentId = "EQUIP-001";

    var plugin = kernel.Plugins["Maintenance"];
    var result = await kernel.InvokeAsync(plugin["PredictMaintenance"], new() { ["equipmentId"] = equipmentId });

    Console.WriteLine($"\n🔧 Maintenance Prediction for {equipmentId}:");
    var prediction = result.GetValue<MaintenancePrediction>();
    if (prediction != null)
    {
        Console.WriteLine($"  • Equipment: {prediction.EquipmentId}");
        Console.WriteLine($"  • Equipment Name: {prediction.EquipmentName}");
        Console.WriteLine($"  • Failure Probability: {prediction.FailureProbability:P0}");
        Console.WriteLine($"  • Predicted Failure: {prediction.PredictedFailureDate:yyyy-MM-dd}");
        Console.WriteLine($"  • Recommended Action: {prediction.RecommendedAction}");
        Console.WriteLine($"  • Estimated Downtime: {prediction.EstimatedDowntime:N1} hours");
        Console.WriteLine($"  • Estimated Cost: ${prediction.EstimatedCost:N0}");

        if (prediction.RiskFactors?.Count > 0)
        {
            Console.WriteLine($"  • Risk Factors:");
            foreach (var risk in prediction.RiskFactors)
            {
                Console.WriteLine($"    - {risk}");
            }
        }
    }
    else
    {
        Console.WriteLine(result);
    }

    var mtbfResult = await kernel.InvokeAsync(plugin["CalculateMTBF"],
        new() { ["equipmentId"] = equipmentId, ["periodDays"] = 365 });
    Console.WriteLine($"\n📊 MTBF Metrics (365 days):");
    var mtbfValue = mtbfResult.GetValue<double>();
    Console.WriteLine($"  • Mean Time Between Failures: {mtbfValue:N1} hours");
}

async Task DetectAnomalies(Kernel kernel)
{
    var sensorData = new SensorData
    {
        SensorId = "SENSOR-001",
        Type = "Temperature",
        Value = 88.5,
        Unit = "°C",
        Timestamp = DateTime.UtcNow
    };

    Console.WriteLine($"\n🎯 Analyzing sensor data: {sensorData.Type} = {sensorData.Value} {sensorData.Unit}");

    var plugin = kernel.Plugins["ProductionMonitoring"];
    var result = await kernel.InvokeAsync(plugin["DetectAnomalies"], new() { ["data"] = sensorData });

    var anomalyResult = result.GetValue<AnomalyResult>();
    if (anomalyResult != null)
    {
        Console.WriteLine("\n📊 Anomaly Detection Result:");
        Console.WriteLine($"  • Status: {(anomalyResult.IsAnomaly ? "⚠️ ANOMALY DETECTED" : "✅ Normal")}");
        Console.WriteLine($"  • Type: {anomalyResult.AnomalyType}");
        Console.WriteLine($"  • Confidence: {anomalyResult.ConfidenceScore:P0}");
        Console.WriteLine($"  • Description: {anomalyResult.Description}");

        if (anomalyResult.AffectedMetrics?.Count > 0)
        {
            Console.WriteLine($"  • Affected Metrics: {string.Join(", ", anomalyResult.AffectedMetrics)}");
        }
    }
    else
    {
        Console.WriteLine("Anomaly Detection Result:");
        Console.WriteLine(result);
    }
}

async Task GenerateRecommendations(ManufacturingAgent agent)
{
    var scenario = new ManufacturingScenario
    {
        ProductionTarget = 1000,
        CurrentOutput = 750,
        EquipmentStatus = "Running with reduced efficiency",
        QualityMetrics = new Dictionary<string, double>
        {
            ["DefectRate"] = 0.03,
            ["FirstPassYield"] = 0.92,
            ["OEE"] = 0.68
        },
        ActiveAlerts = new List<string> { "Temperature variance detected", "Maintenance due soon" },
        Timestamp = DateTime.UtcNow
    };

    Console.WriteLine("\n🤖 Analyzing manufacturing scenario...");
    var recommendation = await agent.AnalyzeScenarioAsync(scenario);

    Console.WriteLine("\n💡 Recommendations:");
    Console.WriteLine($"Type: {recommendation.Type}");
    Console.WriteLine($"Priority: {recommendation.Priority}");
    Console.WriteLine($"Description: {recommendation.Description}");

    if (recommendation.Actions.Any())
    {
        Console.WriteLine("\nSuggested Actions:");
        foreach (var action in recommendation.Actions)
        {
            Console.WriteLine($"  • {action}");
        }
    }
}

async Task AskQuestion(ManufacturingAgent agent)
{
    Console.Write("\nEnter your manufacturing question: ");
    var question = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(question))
    {
        Console.WriteLine("\n🤔 Processing your question...");
        var response = await agent.ProcessQueryAsync(question);
        Console.WriteLine($"\n💬 Response:\n{response}");
    }
}

async Task SimulateEvent(ManufacturingOrchestrator orchestrator)
{
    var evt = new ManufacturingEvent
    {
        EventType = "QualityIssue",
        Source = "LINE-003",
        Timestamp = DateTime.UtcNow,
        Severity = "High",
        Data = new Dictionary<string, object>
        {
            ["DefectRate"] = 0.08,
            ["AffectedBatches"] = 3,
            ["EstimatedImpact"] = "$15,000"
        }
    };

    Console.WriteLine($"\n⚠️ Simulating {evt.EventType} event from {evt.Source}...");

    var systemStatus = await orchestrator.GetSystemStatus();
    Console.WriteLine($"\n📊 Orchestrator Status:");
    foreach (var kvp in systemStatus)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
    }
}

async Task NaturalLanguageChat(Kernel kernel)
{
    Console.WriteLine("\n🤖 Natural Language Chat with Function Calling");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("You can now chat naturally with the AI. It will automatically");
    Console.WriteLine("invoke the appropriate functions based on your request.");
    Console.WriteLine("\nExamples:");
    Console.WriteLine("• \"Schedule preventive maintenance for equipment EQUIP-001 next week\"");
    Console.WriteLine("• \"Check the production status of LINE-001\"");
    Console.WriteLine("• \"Analyze quality for batch BATCH-123\"");
    Console.WriteLine("\nType 'exit' to return to the main menu.\n");

    // Get the chat completion service
    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

    // Create chat history to maintain conversation context
    var chatHistory = new ChatHistory();
    chatHistory.AddSystemMessage("You are a manufacturing assistant that helps schedule maintenance, check production status, and analyze quality metrics. Use the available functions to fulfill user requests.");

    // Configure OpenAI to automatically invoke kernel functions
    var settings = new OpenAIPromptExecutionSettings
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
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

        // Add user message to history
        chatHistory.AddUserMessage(userInput);

        try
        {
            Console.Write("\nAI: ");

            // Get response from chat completion service with full history
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                kernel
            );

            var response = result.Content ?? "I couldn't generate a response.";
            Console.WriteLine(response);

            // Add assistant response to history for continuity
            chatHistory.AddAssistantMessage(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Please try rephrasing your request.");

            // Remove the last user message if there was an error
            if (chatHistory.Count > 0)
                chatHistory.RemoveAt(chatHistory.Count - 1);
        }
    }
}

logger.LogInformation("Manufacturing Agent System Shutdown Complete");