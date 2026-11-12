using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>Whether a sandbox is running.</summary>
[JsonConverter(typeof(KebabEnumConverter<SandboxState>))]
public enum SandboxState
{
	/// <summary>Running.</summary>
	Running,
	/// <summary>Stopped.</summary>
	Stopped,
	/// <summary>Docker Sandboxes reported a state sbud does not know.</summary>
	Unknown,
}

/// <summary>Whether a sandbox trusts the certificate its network proxy uses.</summary>
[JsonConverter(typeof(KebabEnumConverter<ProxyCaState>))]
public enum ProxyCaState
{
	/// <summary>The sandbox trusts its proxy's certificate.</summary>
	Ok,
	/// <summary>The proxy uses a certificate the sandbox does not trust, so its downloads fail.</summary>
	Wrong,
	/// <summary>sbud could not tell.</summary>
	Unknown,
}

/// <summary>A volume's use, in bytes.</summary>
public sealed record Volume(long UsedBytes, long SizeBytes);

/// <summary>
/// A kit a sandbox was given, with the version it got. <see cref="Current"/> is false when the kit has changed since,
/// and absent when the catalogue no longer lists the kit.
/// </summary>
public sealed record SandboxKit(
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Version,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Current);

/// <summary>
/// One sandbox as <c>GET /v1/sandboxes</c> lists it. <see cref="Mounts"/> holds the extra folders, keyed by
/// <see cref="MountId"/>. <see cref="Login"/> is a label, <c>none</c>, or null when sbud has no record.
/// The volumes and the proxy CA state are present only when the request asks for them.
/// </summary>
public sealed record SandboxSummary(
	string Name,
	Agent? Agent,
	SandboxState State,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RamGiB,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CpuPercent,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MemoryMiB,
	Folder? Workspace,
	IReadOnlyDictionary<string, Folder> Mounts,
	string? Login,
	IReadOnlyDictionary<string, SandboxKit> Kits,
	int OpenChats,
	int OpenConsoles,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Volume? HistoryVolume = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Volume? DockerVolume = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProxyCaState? ProxyCa = null);

/// <summary>
/// Everything about one sandbox, as <c>GET /v1/sandboxes/{name}</c> returns it. <see cref="Model"/> is null while the
/// sandbox is stopped and empty when the agent uses its own default. <see cref="HistoryVolume"/> is for Claude only, and
/// each volume is null when sbud cannot read it.
/// </summary>
public sealed record Sandbox(
	string Name,
	Agent? Agent,
	SandboxState State,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RamGiB,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CpuPercent,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MemoryMiB,
	Folder? Workspace,
	IReadOnlyDictionary<string, Folder> Mounts,
	string? Login,
	IReadOnlyDictionary<string, SandboxKit> Kits,
	int OpenChats,
	int OpenConsoles,
	string? StandardName,
	string? Model,
	IReadOnlyList<string> UnmanagedKits,
	Volume? HistoryVolume,
	Volume? DockerVolume,
	ProxyCaState ProxyCa);

/// <summary>What a dry run would do, one line each, as the client shows them.</summary>
public sealed record PlanLines(IReadOnlyList<string> Lines);

/// <summary>How a change is applied to a sandbox.</summary>
[JsonConverter(typeof(KebabEnumConverter<ChangeMode>))]
public enum ChangeMode
{
	/// <summary>Edit the existing sandbox.</summary>
	InPlace,
	/// <summary>Make a new sandbox, under a new name, and keep the original as it is.</summary>
	Clone,
	/// <summary>Replace the sandbox with a new one.</summary>
	Rebuild,
}

/// <summary>
/// One change in a plan. <see cref="Kind"/> is <c>ram</c>, <c>folders</c>, <c>name</c>, <c>kit-add</c>,
/// <c>kit-remove</c>, <c>kit-update</c>, <c>model</c>, <c>login</c> or <c>volume</c>; the other properties are those
/// that kind has.
/// </summary>
public sealed record Change(
	string Kind,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] System.Text.Json.Nodes.JsonNode? From = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] System.Text.Json.Nodes.JsonNode? To = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kit = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FromVersion = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Volume = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? FromBytes = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ToBytes = null);

/// <summary>
/// What a change to a sandbox will do, before anything runs: each change, the steps, whether Docker Sandboxes'
/// daemon restarts and which running sandboxes that stops, whether a volume grows (which cannot be undone), and notes.
/// </summary>
public sealed record ChangePlan(
	string Name,
	ChangeMode Mode,
	bool Fresh,
	IReadOnlyList<Change> Changes,
	IReadOnlyList<string> Steps,
	bool RestartsDaemon,
	IReadOnlyList<string> StopsSandboxes,
	bool GrowsVolumes,
	IReadOnlyList<string> Notes);

/// <summary>One operation of a JSON Patch (RFC 6902), as a change to a sandbox takes them.</summary>
public sealed record PatchOperation(
	string Op,
	string Path,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] System.Text.Json.Nodes.JsonNode? Value = null);

/// <summary>A console: bash inside a sandbox. <see cref="ExitCode"/> is set once bash has ended.</summary>
public sealed record ConsoleInfo(
	string Id,
	string Sandbox,
	string Title,
	bool Running,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ExitCode = null);

/// <summary>The size of a new console's terminal.</summary>
public sealed record ConsoleSize(int Rows = 24, int Cols = 80);
