namespace Sbud;

/// <summary>
/// The id of an extra folder: its path inside the sandbox without the leading slash. sbx shares <c>D:\x</c> at
/// <c>/d/x</c>, with the drive letter lower-cased and backslashes turned into slashes.
/// </summary>
public static class MountId
{
	/// <summary>The id of the extra folder shared from <paramref name="hostPath"/>, such as <c>d/work/shared</c> for <c>D:\work\shared</c>.</summary>
	public static string For(string hostPath)
	{
		var path = hostPath.Replace('\\', '/');
		if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
			path = "/" + char.ToLowerInvariant(path[0]) + path[2..];
		path = path.TrimEnd('/');
		return path.TrimStart('/');
	}

	/// <summary>The JSON Patch path of the extra folder with <paramref name="id"/>, such as <c>/mounts/d~1work~1shared</c>.</summary>
	public static string PatchPath(string id) => "/mounts/" + id.Replace("~", "~0").Replace("/", "~1");
}
