using System.IO.Pipes;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace Sbud;

/// <summary>Talks to the sbud service over its named pipe (Windows) or Unix socket (macOS, Linux).</summary>
public sealed class SbudClient : IDisposable
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private readonly HttpClient _http;

	/// <summary>Connects to <paramref name="endpoint"/>, or to <see cref="SbudEndpoint.Default"/> when it is null.</summary>
	public SbudClient(string? endpoint = null)
	{
		Endpoint = endpoint ?? SbudEndpoint.Default();
		var handler = new SocketsHttpHandler { UseProxy = false, ConnectCallback = (_, cancel) => ConnectAsync(Endpoint, cancel) };
		_http = new HttpClient(handler) { BaseAddress = new Uri("http://sbud") };
	}

	/// <summary>The pipe or socket this client connects to.</summary>
	public string Endpoint { get; }

	/// <summary><c>GET /v1/service</c>: the running service's API version, version, process and endpoint.</summary>
	public Task<ServiceInfo> GetServiceAsync(CancellationToken cancel = default) => SendAsync<ServiceInfo>(HttpMethod.Get, "/v1/service", cancel);

	/// <inheritdoc/>
	public void Dispose() => _http.Dispose();

	private async Task<T> SendAsync<T>(HttpMethod method, string path, CancellationToken cancel)
	{
		HttpResponseMessage response;
		try
		{
			response = await _http.SendAsync(new HttpRequestMessage(method, path), cancel);
		}
		catch (HttpRequestException error)
		{
			throw new SbudException("unavailable", $"The sbud service is not answering at {Endpoint}.", inner: error);
		}
		using (response)
		{
			if (!response.IsSuccessStatusCode)
			{
				var problem = await ReadProblemAsync(response, cancel);
				throw new SbudException(problem?.Kind ?? "failed", problem?.Title ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode);
			}
			return (await response.Content.ReadFromJsonAsync<T>(Json, cancel))!;
		}
	}

	private static async Task<Problem?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancel)
	{
		try
		{
			return await response.Content.ReadFromJsonAsync<Problem>(Json, cancel);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static async ValueTask<Stream> ConnectAsync(string endpoint, CancellationToken cancel)
	{
		if (OperatingSystem.IsWindows() && endpoint.StartsWith(@"\\.\pipe\", StringComparison.Ordinal))
		{
			var pipe = new NamedPipeClientStream(".", endpoint[@"\\.\pipe\".Length..], PipeDirection.InOut, PipeOptions.Asynchronous);
			await pipe.ConnectAsync(cancel);
			return pipe;
		}
		var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		try
		{
			await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), cancel);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	private sealed record Problem(string? Kind, string? Title);
}
