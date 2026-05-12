namespace ArchitectCentral.AgentStudio.Models.Conversation;

/// <summary>
/// Represents a message in the conversation
/// </summary>
public class ConversationMessage
{
    public MessageRole Role { get; set; } = MessageRole.User;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
