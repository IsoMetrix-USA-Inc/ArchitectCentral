using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Conversation;
using ArchitectCentral.AgentStudio.Models.RAG;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Chat service for conversational AI interactions.
/// Note: This service is stateless. Conversation history should be managed by the caller.
/// </summary>
public class ChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly ChatClient _chatClient;
    private readonly int _maxHistory;

    public ChatService(ILogger<ChatService> logger, AppSettings settings)
    {
        _logger = logger;
        _maxHistory = settings.AgentStudio.MaxConversationHistory;

        var openAIClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAI.Endpoint),
            new AzureKeyCredential(settings.AzureOpenAI.ApiKey));

        _chatClient = openAIClient.GetChatClient(settings.AzureOpenAI.ChatDeploymentName);
    }

    public async Task<string> SendMessageAsync(
        string userMessage,
        string? systemPrompt = null,
        List<RAGResult>? ragContext = null,
        List<ConversationMessage>? conversationHistory = null)
    {
        // Build messages for API
        var messages = new List<ChatMessage>();

        // Add system prompt if provided
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));
        }

        // Add RAG context if provided
        if (ragContext != null && ragContext.Count > 0)
        {
            var contextMessage = BuildRAGContextMessage(ragContext);
            messages.Add(ChatMessage.CreateSystemMessage(contextMessage));
        }

        // Add conversation history (limited) if provided
        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            var historyToInclude = conversationHistory.TakeLast(_maxHistory).ToList();
            foreach (var msg in historyToInclude)
            {
                if (msg.Role == MessageRole.User)
                    messages.Add(ChatMessage.CreateUserMessage(msg.Content));
                else if (msg.Role == MessageRole.Assistant)
                    messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
            }
        }

        // Add current user message
        messages.Add(ChatMessage.CreateUserMessage(userMessage));

        // Call Azure OpenAI
        var response = await _chatClient.CompleteChatAsync(messages);
        var assistantMessage = response.Value.Content[0].Text;

        return assistantMessage;
    }

    private static string BuildRAGContextMessage(List<RAGResult> ragResults)
    {
        var context = "# Architecture Documentation Context\n\n";
        context += "The following relevant documentation has been retrieved to help answer your question:\n\n";

        foreach (var result in ragResults)
        {
            context += $"## {result.DocumentType}";
            if (!string.IsNullOrEmpty(result.Section))
                context += $" - {result.Section}";
            context += $" (Similarity: {result.Similarity:F3})\n\n";
            context += result.Content;
            context += "\n\n---\n\n";
        }

        return context;
    }

}
