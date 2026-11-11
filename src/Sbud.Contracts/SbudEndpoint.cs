using System.Text.RegularExpressions;

namespace Sbud;

/// <summary>Where the sbud service listens.</summary>
public static partial class SbudEndpoint
{
	/// <summary>SBUD_SOCKET if set, else the named pipe (Windows) or Unix socket (macOS, Linux) the service uses by default.</summary>
	public static string Default()
	{
		var overridden = Environment.GetEnvironmentVariable("SBUD_SOCKET");
		if (!string.IsNullOrEmpty(overridden))
			return overridden;
		if (OperatingSystem.IsWindows())
			return $@"\\.\pipe\sbud-{UnsafeInPipeName().Replace(Environment.UserName, "_")}";
		var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
		if (!string.IsNullOrEmpty(runtime))
			return Path.Combine(runtime, "sbud.sock");
		var home = Environment.GetEnvironmentVariable("SBUD_HOME");
		if (string.IsNullOrEmpty(home))
			home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sbud");
		return Path.Combine(home, "sbud.sock");
	}

	[GeneratedRegex("[^A-Za-z0-9._-]")]
	private static partial Regex UnsafeInPipeName();
}
