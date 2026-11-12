using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>A repair for a problem the service found. Its <c>type</c> says which repair it is.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(SbxLoginRepair), "sbx-login")]
[JsonDerivedType(typeof(ProxyCaRepair), "proxy-ca")]
[JsonDerivedType(typeof(HistoryRestoreRepair), "history-restore")]
public abstract record Repair;

/// <summary>Signs Docker Sandboxes in with <c>sbx login</c>, which opens the browser.</summary>
public sealed record SbxLoginRepair : Repair;

/// <summary>
/// Gives each sandbox's proxy back the certificate the sandbox trusts. An empty <see cref="Sandboxes"/> means every
/// sandbox that needs it. It restarts Docker Sandboxes, so <see cref="ConfirmStops"/> must name every running sandbox.
/// </summary>
public sealed record ProxyCaRepair(IReadOnlyList<string> Sandboxes, IReadOnlyList<string> ConfirmStops) : Repair;

/// <summary>Copies agent history saved by a failed rebuild back into a sandbox.</summary>
public sealed record HistoryRestoreRepair(string Sandbox, string File) : Repair;

/// <summary>What kind of problem the service found.</summary>
[JsonConverter(typeof(KebabEnumConverter<ProblemKind>))]
public enum ProblemKind
{
	/// <summary>Docker Sandboxes is signed out.</summary>
	SignedOut,
	/// <summary>A sandbox trusts a proxy certificate its proxy no longer uses, so its downloads fail.</summary>
	ProxyCa,
	/// <summary>A failed rebuild left a sandbox's agent history in a file.</summary>
	HistoryLeft,
}

/// <summary>A problem the service found, the sentence the user sees, and the repair that fixes it, or null when there is none.</summary>
public sealed record Problem(
	ProblemKind Kind,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sandbox,
	string Title,
	Repair? Repair);
