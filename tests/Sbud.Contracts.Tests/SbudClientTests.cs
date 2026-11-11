using System.Net.Sockets;
using System.Text;

namespace Sbud.Tests;

public sealed class SbudClientTests : IDisposable
{
	private readonly string _folder = Directory.CreateTempSubdirectory("sbud-").FullName;
	private string SocketPath => Path.Combine(_folder, "sbud.sock");

	public void Dispose() => Directory.Delete(_folder, recursive: true);

	[Fact]
	public async Task GetServiceAsync_reads_the_service_info()
	{
		var served = ServeOnce("200 OK", """{"apiVersion":1,"version":"0.1.0","pid":4242,"endpoint":"/run/user/1000/sbud.sock","startedAt":"2026-11-11T10:00:00Z"}""");
		using var client = new SbudClient(SocketPath);
		var info = await client.GetServiceAsync();
		Assert.Equal("GET /v1/service HTTP/1.1", await served);
		Assert.Equal(new ServiceInfo(1, "0.1.0", 4242, "/run/user/1000/sbud.sock", new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero)), info);
	}

	[Fact]
	public async Task Problem_details_become_an_SbudException()
	{
		var served = ServeOnce("401 Unauthorized", """{"status":401,"kind":"unauthenticated","title":"Not the service's user."}""");
		using var client = new SbudClient(SocketPath);
		var error = await Assert.ThrowsAsync<SbudException>(() => client.GetServiceAsync());
		await served;
		Assert.Equal(("unauthenticated", 401, "Not the service's user."), (error.Kind, error.Status, error.Message));
	}

	[Fact]
	public async Task No_service_is_unavailable()
	{
		using var client = new SbudClient(SocketPath);
		var error = await Assert.ThrowsAsync<SbudException>(() => client.GetServiceAsync());
		Assert.Equal("unavailable", error.Kind);
		Assert.Null(error.Status);
	}

	private async Task<string> ServeOnce(string status, string json)
	{
		using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
		listener.Listen(1);
		using var connection = await listener.AcceptAsync();
		using var stream = new NetworkStream(connection);
		var request = new StringBuilder();
		var buffer = new byte[4096];
		while (!request.ToString().Contains("\r\n\r\n"))
			request.Append(Encoding.ASCII.GetString(buffer, 0, await stream.ReadAsync(buffer)));
		var body = Encoding.UTF8.GetBytes(json);
		await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
		await stream.WriteAsync(body);
		return request.ToString()[..request.ToString().IndexOf("\r\n", StringComparison.Ordinal)];
	}
}
