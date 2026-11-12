using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>
/// One stored login. <see cref="Account"/> is Codex's account ID; Claude logins have none. <see cref="IssuedAt"/> is
/// when sbud stored it. No token is ever included.
/// </summary>
public sealed record LoginInfo(
	Agent Agent,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Account,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresAt,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? IssuedAt);

/// <summary>
/// A label: the name logins are filed under, the folder patterns that choose it for a new sandbox, and the sandboxes
/// assigned it. A label exists while it holds at least one login.
/// </summary>
public sealed record Label([property: JsonPropertyName("label")] string Name, IReadOnlyList<string> Patterns, IReadOnlyList<string> Sandboxes, IReadOnlyList<LoginInfo> Logins);

/// <summary>A sandbox that got its label's login.</summary>
public sealed record PushedLogin(string Sandbox, Agent Agent);

/// <summary>A sandbox whose copy failed, and why.</summary>
public sealed record FailedPush(
	string Sandbox,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Agent? Agent,
	string Reason);

/// <summary>A sandbox that was left alone, and why.</summary>
public sealed record SkippedPush(string Sandbox, string Reason);

/// <summary>
/// What pushing a label's logins did: which sandboxes got them, which failed, which were skipped because the label
/// holds no login for their agent, and which no longer exist and were dropped from sbud's records.
/// </summary>
public sealed record PushReport(IReadOnlyList<PushedLogin> Pushed, IReadOnlyList<FailedPush> Failed, IReadOnlyList<SkippedPush> Skipped, IReadOnlyList<string> Dropped);

/// <summary>The file holding the login of the agent installed on this computer, and whether it exists.</summary>
public sealed record HostLogin(Agent Agent, string File, bool Exists);

/// <summary>The label after the host's own login was stored under it, and the push that followed.</summary>
public sealed record SavedLogin(Label Label, PushReport Push);

/// <summary>What a client opens a terminal session for: the agent itself, or a bash shell.</summary>
[JsonConverter(typeof(KebabEnumConverter<TerminalKind>))]
public enum TerminalKind
{
	/// <summary><c>sbud attach</c>: the agent.</summary>
	Agent,
	/// <summary><c>sbud shell</c>: bash.</summary>
	Shell,
}

/// <summary>A request to prepare a sandbox before a client runs sbx in the user's terminal.</summary>
public sealed record TerminalRequest(TerminalKind Kind);

/// <summary>
/// A prepared terminal session: the command the client runs in the user's terminal, and the ID it ends the session
/// with when that command exits.
/// </summary>
public sealed record TerminalSession(string Id, IReadOnlyList<string> Argv);

/// <summary>Where a sign-in waits for the user.</summary>
[JsonConverter(typeof(KebabEnumConverter<SignInMode>))]
public enum SignInMode
{
	/// <summary>Claude: <c>claude setup-token</c> runs in a terminal the service makes, and asks for a code the browser shows.</summary>
	Terminal,
	/// <summary>Codex: <c>codex login</c> waits for the browser by itself.</summary>
	Browser,
}

/// <summary>How far a sign-in has got.</summary>
[JsonConverter(typeof(KebabEnumConverter<SignInState>))]
public enum SignInState
{
	/// <summary>Started; the address to open may not be known yet.</summary>
	Running,
	/// <summary>Waiting for the code the browser shows.</summary>
	NeedCode,
	/// <summary>Storing the login.</summary>
	Finishing,
	/// <summary>The login is stored and pushed.</summary>
	Done,
	/// <summary>It failed; <see cref="SignIn.Message"/> says why.</summary>
	Failed,
	/// <summary>Cancelled; the existing login is unchanged.</summary>
	Cancelled,
}

/// <summary>
/// A sign-in under way. <see cref="Url"/> is the address to open once it is known; <see cref="Prompt"/> asks for the
/// code; <see cref="Retry"/> says why the last code was refused; <see cref="Message"/> says why it failed.
/// </summary>
public sealed record SignIn(
	string Id,
	string Label,
	Agent Agent,
	SignInMode Mode,
	SignInState State,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Prompt = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Retry = null,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

/// <summary>The code the browser showed, for a sign-in waiting for one.</summary>
public sealed record SignInCode(string Code);
