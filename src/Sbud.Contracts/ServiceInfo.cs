namespace Sbud;

/// <summary>What <c>GET /v1/service</c> returns: the running service's API version, version, process and endpoint.</summary>
public sealed record ServiceInfo(int ApiVersion, string Version, int Pid, string Endpoint, DateTimeOffset StartedAt);
