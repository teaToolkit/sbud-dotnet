namespace Sbud;

/// <summary>
/// A request the service refused or could not answer. <see cref="Kind"/> is the problem kind the service sent,
/// such as <c>not-found</c>, or <c>unavailable</c> when the service did not answer at all.
/// </summary>
public sealed class SbudException(string kind, string title, int? status = null, Exception? inner = null)
	: Exception(title, inner)
{
	/// <summary>The problem kind, such as <c>refused</c> or <c>unavailable</c>.</summary>
	public string Kind { get; } = kind;

	/// <summary>The HTTP status, or null when the service did not answer.</summary>
	public int? Status { get; } = status;
}
