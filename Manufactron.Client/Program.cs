using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Manufactron.Client.Services;
using Manufactron.Client.Plugins;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Setup dependency injection
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
services.AddHttpClient();

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

// Build Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton<ILoggerFactory>(loggerFactory);

// Configure AI service
var apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4-turbo-preview";
var aggregatorUrl = configuration["I3XServices:AggregatorEndpoint"] ?? "http://localhost:7000";

if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("sk-proj-"))
{
    kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
}
else
{
    Console.WriteLine("⚠️ OpenAI API key not configured. Running in demo mode.");
    Console.WriteLine("To enable AI features, set your API key in appsettings.json\n");
}

var kernel = kernelBuilder.Build();

// Register manufacturing plugins
kernel.RegisterManufacturingPlugins(serviceProvider, aggregatorUrl);

// Create semantic functions service
var semanticFunctions = new SemanticFunctions(kernel, loggerFactory.CreateLogger<SemanticFunctions>());

// Display header
Console.WriteLine(@"
╔════════════════════════════════════════════════════════════════════╗
║          MANUFACTRON - Conversational Manufacturing Assistant       ║
║                   Powered by I3X & Semantic Kernel                 ║
╚════════════════════════════════════════════════════════════════════╝
");

// Check I3X Aggregator availability
await CheckAggregatorService();

// Create conversational assistant
ConversationalAssistant? assistant = null;
var chatService = kernel.Services.GetService<IChatCompletionService>();

if (chatService != null)
{
    assistant = new ConversationalAssistant(
        kernel,
        chatService,
        loggerFactory.CreateLogger<ConversationalAssistant>());

    Console.WriteLine("✅ AI Assistant Ready\n");
}
else
{
    Console.WriteLine("⚠️ AI Assistant not available (API key required)\n");
}

// Main menu loop
await RunMainMenu();

// Helper Functions

async Task CheckAggregatorService()
{
    Console.WriteLine("🔍 Checking I3X Aggregator Service...");

    try
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/namespaces");

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"✅ I3X Aggregator available at {aggregatorUrl}\n");
        }
        else
        {
            Console.WriteLine($"⚠️ I3X Aggregator returned {response.StatusCode}");
            Console.WriteLine("Run Start-AllServices.ps1 to start the services\n");
        }
    }
    catch
    {
        Console.WriteLine($"❌ I3X Aggregator not available at {aggregatorUrl}");
        Console.WriteLine("Run Start-AllServices.ps1 to start the services\n");
    }
}

async Task RunMainMenu()
{
    while (true)
    {
        Console.WriteLine("\n═══ Manufacturing Intelligence Menu ═══");
        Console.WriteLine("1. 💬 Conversational Assistant (AI Chat)");
        Console.WriteLine("2. 🔍 Quick Status Check");
        Console.WriteLine("3. 📊 Analyze Production Efficiency");
        Console.WriteLine("4. 🔧 Equipment Diagnostics");
        Console.WriteLine("5. 📈 Quality Analysis");
        Console.WriteLine("6. 🎯 Root Cause Analysis");
        Console.WriteLine("7. 📋 View Suggested Questions");
        Console.WriteLine("8. 🔄 Reset Conversation");
        Console.WriteLine("9. 🚪 Exit");
        Console.Write("\nSelect option: ");

        var choice = Console.ReadLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await RunConversationalMode();
                    break;
                case "2":
                    await QuickStatusCheck();
                    break;
                case "3":
                    await AnalyzeEfficiency();
                    break;
                case "4":
                    await EquipmentDiagnostics();
                    break;
                case "5":
                    await QualityAnalysis();
                    break;
                case "6":
                    await RootCauseAnalysis();
                    break;
                case "7":
                    ShowSuggestedQuestions();
                    break;
                case "8":
                    assistant?.ResetConversation();
                    Console.WriteLine("✅ Conversation reset");
                    break;
                case "9":
                    Console.WriteLine("\n👋 Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in menu option {Choice}", choice);
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
    }
}

async Task RunConversationalMode()
{
    if (assistant == null)
    {
        Console.WriteLine("\n⚠️ Conversational mode requires OpenAI API key configuration");
        return;
    }

    Console.WriteLine("\n💬 Conversational Assistant Mode");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("Chat naturally about manufacturing operations.");
    Console.WriteLine("Type 'exit' to return to main menu.\n");

    while (true)
    {
        Console.Write("You: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
        {
            break;
        }

        Console.WriteLine("\n🤔 Thinking...");
        var response = await assistant.ProcessMessageAsync(input);
        Console.WriteLine($"\nAssistant: {response}");
    }
}

async Task QuickStatusCheck()
{
    Console.WriteLine("\n📊 Quick Production Status");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━");

    if (assistant != null)
    {
        var response = await assistant.ProcessMessageAsync(
            "Give me a quick overview of the current production status including active lines, equipment status, and any issues");
        Console.WriteLine(response);
    }
    else
    {
        Console.WriteLine("Use the conversational assistant for status checks");
    }
}

async Task AnalyzeEfficiency()
{
    Console.WriteLine("\n📊 Production Efficiency Analysis");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    if (assistant != null)
    {
        var response = await assistant.ProcessMessageAsync(
            "Analyze the production efficiency across all lines and equipment. Show me OEE metrics and identify any underperforming areas");
        Console.WriteLine(response);
    }
    else
    {
        Console.WriteLine("Efficiency analysis requires the AI assistant");
    }
}

async Task EquipmentDiagnostics()
{
    Console.WriteLine("\n🔧 Equipment Diagnostics");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━");

    Console.Write("Enter equipment ID (or press Enter for all): ");
    var equipmentId = Console.ReadLine();

    if (assistant != null)
    {
        var query = string.IsNullOrWhiteSpace(equipmentId)
            ? "Check all equipment for maintenance needs and potential failures"
            : $"Diagnose equipment {equipmentId} including status, performance, and maintenance prediction";

        var response = await assistant.ProcessMessageAsync(query);
        Console.WriteLine(response);
    }
    else
    {
        Console.WriteLine("Equipment diagnostics requires the AI assistant");
    }
}

async Task QualityAnalysis()
{
    Console.WriteLine("\n📈 Quality Analysis");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━");

    if (assistant != null)
    {
        var response = await assistant.ProcessMessageAsync(
            "Analyze quality trends across recent production jobs. Identify any quality issues and their root causes");
        Console.WriteLine(response);
    }
    else
    {
        Console.WriteLine("Quality analysis requires the AI assistant");
    }
}

async Task RootCauseAnalysis()
{
    Console.WriteLine("\n🎯 Root Cause Analysis");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━");

    Console.Write("Describe the issue to analyze: ");
    var issue = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(issue))
    {
        Console.WriteLine("Please describe an issue to analyze");
        return;
    }

    if (semanticFunctions != null)
    {
        // First get context using assistant
        if (assistant != null)
        {
            var contextResponse = await assistant.ProcessMessageAsync(
                $"Gather relevant data for analyzing this issue: {issue}");

            // Then perform root cause analysis
            var analysis = await semanticFunctions.AnalyzeRootCauseAsync(
                equipment: "Manufacturing System",
                status: "Issue Detected",
                metrics: new { Issue = issue },
                events: new List<string> { $"User reported: {issue}" },
                patterns: new { });

            Console.WriteLine("\n" + analysis);
        }
    }
    else
    {
        Console.WriteLine("Root cause analysis requires semantic functions");
    }
}

void ShowSuggestedQuestions()
{
    Console.WriteLine("\n📋 Suggested Questions");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine("\nTry asking these questions:");

    if (assistant != null)
    {
        var suggestions = assistant.GetSuggestedQuestions();
        for (int i = 0; i < suggestions.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {suggestions[i]}");
        }
    }
    else
    {
        Console.WriteLine("1. What's the current production status?");
        Console.WriteLine("2. Which equipment needs maintenance?");
        Console.WriteLine("3. Show efficiency metrics");
        Console.WriteLine("4. Analyze quality trends");
        Console.WriteLine("5. Find production bottlenecks");
    }

    Console.WriteLine("\n💡 Tip: You can ask questions naturally in the conversational mode!");
}