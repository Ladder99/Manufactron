using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Manufactron.Client.Plugins;
using System.Text;

namespace Manufactron.Client.Services;

public class ConversationalAssistant
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ConversationalAssistant> _logger;
    private ChatHistory _chatHistory;

    public ConversationalAssistant(
        Kernel kernel,
        IChatCompletionService chatService,
        ILogger<ConversationalAssistant> logger)
    {
        _kernel = kernel;
        _chatService = chatService;
        _logger = logger;
        _chatHistory = new ChatHistory();
        InitializeSystemPrompt();
    }

    private void InitializeSystemPrompt()
    {
        _chatHistory.AddSystemMessage(@"You are an intelligent manufacturing assistant with access to real-time I3X data.
You help operators, managers, and engineers optimize production, identify issues, and make data-driven decisions.

You have access to the following plugins:
- EquipmentPlugin: Monitor equipment status, performance, and predict failures
- ContextPlugin: Build manufacturing context, search objects, understand relationships
- AnalyticsPlugin: Analyze efficiency, detect anomalies, track quality, calculate KPIs

When users ask questions:
1. Use the appropriate plugin functions to get real-time data
2. Analyze the data and provide insights
3. Make actionable recommendations
4. Be proactive in identifying potential issues

Important context:
- The I3X Aggregator provides unified access to ERP, MES, and SCADA data
- Equipment IDs typically follow patterns like 'scada-equipment-mixer-1', 'scada-equipment-filler-1'
- Line IDs follow patterns like 'scada-line-1', 'scada-line-2'
- Job IDs follow patterns like 'mes-job-J-2025-001'
- Order IDs follow patterns like 'erp-order-12345'

Always provide specific, actionable insights based on the data retrieved.");
    }

    public async Task<string> ProcessMessageAsync(string userMessage)
    {
        try
        {
            _chatHistory.AddUserMessage(userMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.7,
                MaxTokens = 1000
            };

            var result = await _chatService.GetChatMessageContentAsync(
                _chatHistory,
                settings,
                _kernel);

            var response = result.Content ?? "I couldn't process your request.";
            _chatHistory.AddAssistantMessage(response);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message");

            // Remove the last user message if there was an error
            if (_chatHistory.Count > 0)
                _chatHistory.RemoveAt(_chatHistory.Count - 1);

            return $"I encountered an error: {ex.Message}. Please try rephrasing your question.";
        }
    }

    public void ResetConversation()
    {
        _chatHistory = new ChatHistory();
        InitializeSystemPrompt();
    }

    public async Task<string> GenerateSummaryAsync()
    {
        var summaryPrompt = @"Summarize the key points from our conversation including:
- Main topics discussed
- Key findings and insights
- Recommendations made
- Any issues identified
Keep it concise and actionable.";

        var tempHistory = new ChatHistory(_chatHistory);
        tempHistory.AddUserMessage(summaryPrompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.5,
            MaxTokens = 500
        };

        var result = await _chatService.GetChatMessageContentAsync(
            tempHistory,
            settings,
            _kernel);

        return result.Content ?? "Unable to generate summary.";
    }

    public List<string> GetSuggestedQuestions()
    {
        return new List<string>
        {
            "What's the current production status?",
            "Which equipment needs maintenance soon?",
            "Show me the efficiency metrics for line 1",
            "Are there any quality issues in recent jobs?",
            "What anomalies have been detected today?",
            "Build manufacturing context for the filler equipment",
            "Calculate production KPIs for the last 24 hours",
            "Search for all mixer equipment",
            "What's causing waste in current production?",
            "Show me the production hierarchy"
        };
    }
}