using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>Writes an enum as lower-case words joined by hyphens, such as <c>in-place</c>.</summary>
public sealed class KebabEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false)
	where T : struct, Enum;

/// <summary>The AI program inside a sandbox.</summary>
[JsonConverter(typeof(KebabEnumConverter<Agent>))]
public enum Agent
{
	/// <summary>Claude Code, the program <c>claude</c>.</summary>
	Claude,
	/// <summary>Codex, the program <c>codex</c>.</summary>
	Codex,
}

/// <summary>A folder on this computer shared into a sandbox.</summary>
public sealed record Folder(string HostPath, bool ReadOnly);

/// <summary>A kit in the kit catalogue. <see cref="Default"/> is true when new sandboxes get it.</summary>
public sealed record Kit(string Name, string Path, bool Default);

/// <summary>A kit chosen for a new sandbox.</summary>
public sealed record KitRef(string Name, string Path);

/// <summary>The memory trimmer's settings.</summary>
public sealed record MemorySettings(bool Trim, int CacheKeepMiB, int TrimEverySeconds);

/// <summary>The models offered for each agent.</summary>
public sealed record AgentModels(IReadOnlyList<string> Claude, IReadOnlyList<string> Codex);

/// <summary>The model a new sandbox gets for each agent; empty means the agent's own default.</summary>
public sealed record DefaultModels(string Claude, string Codex);

/// <summary>
/// What <c>GET /v1/settings</c> returns. <see cref="StorageRoot"/> is the folder set in <c>config.json</c>, or null;
/// <see cref="StorageRootInUse"/> is the folder actually used.
/// </summary>
public sealed record Settings(MemorySettings Memory, AgentModels Models, string? StorageRoot, string StorageRootInUse);

/// <summary>What a new sandbox gets unless the user chooses otherwise. A null <see cref="DockerGiB"/> leaves Docker Sandboxes' own setting.</summary>
public sealed record SandboxDefaults(Agent Agent, int RamGiB, int? DockerGiB, DefaultModels Model, int StartTimeoutSeconds);

/// <summary>A sandbox to create. An empty <see cref="Model"/> means the agent's own default; <see cref="Login"/> is a label or <c>none</c>.</summary>
public sealed record NewSandbox(
	Agent Agent,
	string Name,
	string Model,
	int RamGiB,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DockerGiB,
	string Login,
	IReadOnlyList<KitRef> Kits,
	Folder? Workspace,
	IReadOnlyList<Folder> Mounts,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Template = null);

/// <summary>The state of a task.</summary>
[JsonConverter(typeof(KebabEnumConverter<TaskState>))]
public enum TaskState
{
	/// <summary>Still running.</summary>
	Running,
	/// <summary>Finished successfully.</summary>
	Done,
	/// <summary>Finished with an error.</summary>
	Failed,
	/// <summary>Stopped by a client.</summary>
	Cancelled,
}

/// <summary>A long piece of work running in the service, with its progress lines.</summary>
public sealed record TaskInfo(
	string Id,
	string Title,
	string? Sandbox,
	TaskState State,
	IReadOnlyList<string> Lines,
	string? Error,
	IReadOnlyList<string>? Recovery,
	bool Cancellable,
	DateTimeOffset StartedAt,
	DateTimeOffset? EndedAt);

/// <summary>This computer's memory use, and its CPU use where the service can measure it.</summary>
public sealed record HostStats(double? CpuPercent, long MemoryUsedMiB, long MemoryTotalMiB);

/// <summary>One faulty field of a request, with the sentence the user sees.</summary>
public sealed record FieldError(string Field, string Message);

/// <summary>One message from <c>GET /v1/events</c>: its channel and its JSON data.</summary>
public sealed record SbudEvent(string Channel, JsonElement Data);

/// <summary>The event channels the service sends.</summary>
public static class EventChannels
{
	/// <summary>Chat items and chat state.</summary>
	public const string Chat = "chat";
	/// <summary>Consoles starting, ending and closing.</summary>
	public const string Console = "console";
	/// <summary>Every change to a task, sent whole.</summary>
	public const string Task = "task";
	/// <summary>Sign-in progress.</summary>
	public const string SignIn = "sign-in";
	/// <summary>Something about the sandboxes changed; list them again.</summary>
	public const string Sandboxes = "sandboxes";
	/// <summary>CPU and memory, once a second, only when asked for.</summary>
	public const string Stats = "stats";
}
