using ArchitectCentral.AgentStudio.Models.Conversation;
using ArchitectCentral.AgentStudio.Models.RAG;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Interface for chat service - enables testing and mocking
/// </summary>
public interface IChatService
{
    Task<string> SendMessageAsync(
        string userMessage,
        string systemPrompt,
        List<RAGResult>? ragContext = null,
        List<ConversationMessage>? conversationHistory = null);
}
