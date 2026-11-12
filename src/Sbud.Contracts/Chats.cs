using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>One thing a chat shows. Its <c>kind</c> is <c>user</c>, <c>assistant</c>, <c>tool</c> or <c>notice</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UserItem), "user")]
[JsonDerivedType(typeof(AssistantItem), "assistant")]
[JsonDerivedType(typeof(ToolItem), "tool")]
[JsonDerivedType(typeof(NoticeItem), "notice")]
public abstract record ChatItem(string Id);

/// <summary>A message the user sent.</summary>
public sealed record UserItem(string Id, string Text) : ChatItem(Id);

/// <summary>Text the agent wrote.</summary>
public sealed record AssistantItem(string Id, string Text) : ChatItem(Id);

/// <summary>A tool the agent called, with a one-line summary. <see cref="Result"/> is absent while the tool runs.</summary>
public sealed record ToolItem(
	string Id,
	string Name,
	string Summary,
	JsonNode? Input,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ToolResult? Result = null) : ChatItem(Id);

/// <summary>What a tool call returned, and whether it failed.</summary>
public sealed record ToolResult(string Text, bool IsError);

/// <summary>How serious a notice is.</summary>
[JsonConverter(typeof(KebabEnumConverter<NoticeLevel>))]
public enum NoticeLevel
{
	/// <summary>For information.</summary>
	Info,
	/// <summary>Something failed.</summary>
	Error,
}

/// <summary>A notice from sbud in the chat, such as an agent's error.</summary>
public sealed record NoticeItem(string Id, NoticeLevel Level, string Text) : ChatItem(Id);

/// <summary>
/// An open chat. <see cref="AgentSessionId"/> is the agent's own session ID, known once the agent reports it;
/// <see cref="Model"/> is null for the sandbox's default; <see cref="Busy"/> is true while the agent is replying.
/// </summary>
public sealed record ChatSessionInfo(
	string Id,
	string Sandbox,
	Agent Agent,
	string Title,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AgentSessionId,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model,
	bool Busy);

/// <summary>
/// A chat in a sandbox's list: an open chat, or a conversation the agent saved earlier, which can be opened again.
/// <see cref="ModifiedAt"/> and <see cref="Messages"/> describe saved chats.
/// </summary>
public sealed record ChatEntry(
	bool Open,
	ChatSessionInfo? Chat,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AgentSessionId,
	string Title,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ModifiedAt = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Messages = null);

/// <summary>A new chat: <see cref="Model"/> null for the sandbox's default; <see cref="Resume"/> a saved chat's agent session ID to continue it.</summary>
public sealed record NewChat(string? Model = null, string? Resume = null);

/// <summary>A message to send to the agent.</summary>
public sealed record ChatMessage(string Text);
