using System.IO.Pipes;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Sbud;

/// <summary>Talks to the sbud service over its named pipe (Windows) or Unix socket (macOS, Linux).</summary>
public sealed class SbudClient : IDisposable
{
	private readonly HttpClient _http;

	/// <summary>Connects to <paramref name="endpoint"/>, or to <see cref="SbudEndpoint.Default"/> when it is null.</summary>
	public SbudClient(string? endpoint = null)
	{
		Endpoint = endpoint ?? SbudEndpoint.Default();
		var handler = new SocketsHttpHandler { UseProxy = false, ConnectCallback = (_, cancel) => ConnectAsync(Endpoint, cancel) };
		_http = new HttpClient(handler) { BaseAddress = new Uri("http://sbud"), Timeout = Timeout.InfiniteTimeSpan };
	}

	/// <summary>The pipe or socket this client connects to.</summary>
	public string Endpoint { get; }

	/// <summary><c>GET /v1/service</c>: the running service's API version, version, process and endpoint.</summary>
	public Task<ServiceInfo> GetServiceAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/service", null, SbudJsonContext.Default.ServiceInfo, cancel);

	/// <summary><c>POST /v1/service/stop</c>: stops the service, closing every chat and console.</summary>
	public Task StopServiceAsync(CancellationToken cancel = default) => SendAsync(HttpMethod.Post, "/v1/service/stop", null, cancel);

	/// <summary><c>GET /v1/tasks</c>: running tasks and those that finished in the last hour, newest first.</summary>
	public Task<List<TaskInfo>> GetTasksAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/tasks", null, SbudJsonContext.Default.ListTaskInfo, cancel);

	/// <summary><c>GET /v1/tasks/{id}</c>: one task, with its progress lines.</summary>
	public Task<TaskInfo> GetTaskAsync(string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, $"/v1/tasks/{Uri.EscapeDataString(id)}", null, SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary><c>DELETE /v1/tasks/{id}</c>: cancels a task of a kind that can be stopped safely.</summary>
	public Task<TaskInfo> CancelTaskAsync(string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, $"/v1/tasks/{Uri.EscapeDataString(id)}", null, SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary><c>GET /v1/host</c>: this computer's CPU and memory use.</summary>
	public Task<HostStats> GetHostAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/host", null, SbudJsonContext.Default.HostStats, cancel);

	/// <summary><c>GET /v1/settings</c>: the app-wide settings.</summary>
	public Task<Settings> GetSettingsAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/settings", null, SbudJsonContext.Default.Settings, cancel);

	/// <summary><c>PATCH /v1/settings</c> with a JSON merge patch holding only the settings to change.</summary>
	public Task<Settings> PatchSettingsAsync(JsonObject patch, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, "/v1/settings", MergePatch(patch), SbudJsonContext.Default.Settings, cancel);

	/// <summary><c>GET /v1/sandbox-defaults</c>: the stored defaults for new sandboxes.</summary>
	public Task<SandboxDefaults> GetSandboxDefaultsAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/sandbox-defaults", null, SbudJsonContext.Default.SandboxDefaults, cancel);

	/// <summary><c>PATCH /v1/sandbox-defaults</c> with a JSON merge patch holding only the defaults to change.</summary>
	public Task<SandboxDefaults> PatchSandboxDefaultsAsync(JsonObject patch, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, "/v1/sandbox-defaults", MergePatch(patch), SbudJsonContext.Default.SandboxDefaults, cancel);

	/// <summary><c>GET /v1/sandbox-defaults/for-folder</c>: what a new sandbox in <paramref name="folder"/> would get.</summary>
	public Task<NewSandbox> GetDefaultsForFolderAsync(string? folder, Agent? agent = null, CancellationToken cancel = default)
	{
		var query = new List<string>();
		if (folder is not null)
			query.Add("folder=" + Uri.EscapeDataString(folder));
		if (agent is not null)
			query.Add("agent=" + agent.Value.ToString().ToLowerInvariant());
		var path = "/v1/sandbox-defaults/for-folder" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
		return SendAsync(HttpMethod.Get, path, null, SbudJsonContext.Default.NewSandbox, cancel);
	}

	/// <summary><c>GET /v1/kits</c>: the kit catalogue, in catalogue order.</summary>
	public Task<List<Kit>> GetKitsAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/kits", null, SbudJsonContext.Default.ListKit, cancel);

	/// <summary><c>GET /v1/kits/{name}</c>: one kit.</summary>
	public Task<Kit> GetKitAsync(string name, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, KitPath(name), null, SbudJsonContext.Default.Kit, cancel);

	/// <summary><c>POST /v1/kits</c>: adds a kit to the catalogue.</summary>
	public Task<Kit> AddKitAsync(Kit kit, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, "/v1/kits", JsonContent.Create(kit, SbudJsonContext.Default.Kit), SbudJsonContext.Default.Kit, cancel);

	/// <summary><c>PATCH /v1/kits/{name}</c> with any of <c>name</c>, <c>path</c> and <c>default</c>.</summary>
	public Task<Kit> PatchKitAsync(string name, JsonObject patch, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, KitPath(name), MergePatch(patch), SbudJsonContext.Default.Kit, cancel);

	/// <summary><c>DELETE /v1/kits/{name}</c>: removes a kit from the catalogue, leaving its files and the sandboxes that have it.</summary>
	public Task RemoveKitAsync(string name, CancellationToken cancel = default) => SendAsync(HttpMethod.Delete, KitPath(name), null, cancel);

	/// <summary>
	/// <c>GET /v1/events</c>: the service's events as they happen, until the stream ends or <paramref name="cancel"/> fires.
	/// When <paramref name="channels"/> is null or empty, the service sends every channel except <c>stats</c>. The stream does not reconnect by itself.
	/// </summary>
	public async IAsyncEnumerable<SbudEvent> StreamEventsAsync(IEnumerable<string>? channels = null, [EnumeratorCancellation] CancellationToken cancel = default)
	{
		var path = "/v1/events";
		var list = channels?.ToList();
		if (list is { Count: > 0 })
			path += "?channels=" + Uri.EscapeDataString(string.Join(',', list));
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
		using var response = await SendRawAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
		await ThrowIfFailedAsync(response, cancel);
		using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel), Encoding.UTF8);
		string? channel = null;
		var data = new StringBuilder();
		while (await reader.ReadLineAsync(cancel) is { } line)
		{
			if (line.Length == 0)
			{
				if (channel is not null && data.Length > 0)
				{
					using var document = JsonDocument.Parse(data.ToString());
					yield return new SbudEvent(channel, document.RootElement.Clone());
				}
				channel = null;
				data.Clear();
			}
			else if (line.StartsWith("event:", StringComparison.Ordinal))
				channel = line[6..].Trim();
			else if (line.StartsWith("data:", StringComparison.Ordinal))
				data.Append(line[5..].TrimStart());
		}
	}

	/// <inheritdoc/>
	public void Dispose() => _http.Dispose();

	/// <summary>
	/// Lists the sandboxes, running first, then by name. <paramref name="workspace"/> keeps only the sandboxes whose
	/// workspace is that folder. <paramref name="volumes"/> and <paramref name="proxyCa"/> add details that are slower to read.
	/// </summary>
	public Task<List<SandboxSummary>> GetSandboxesAsync(string? workspace = null, bool volumes = false, bool proxyCa = false, CancellationToken cancel = default)
	{
		var query = new List<string>();
		if (workspace is not null)
			query.Add("workspace=" + Uri.EscapeDataString(workspace));
		var include = new List<string>();
		if (volumes)
			include.Add("volumes");
		if (proxyCa)
			include.Add("proxy-ca");
		if (include.Count > 0)
			query.Add("include=" + string.Join(',', include));
		var path = "/v1/sandboxes" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
		return SendAsync(HttpMethod.Get, path, null, SbudJsonContext.Default.ListSandboxSummary, cancel);
	}

	/// <summary>
	/// Starts creating a sandbox and returns the task doing it. A faulty request fails at once, listing every faulty
	/// field. Creating never opens the sandbox.
	/// </summary>
	public Task<TaskInfo> CreateSandboxAsync(NewSandbox sandbox, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, "/v1/sandboxes", JsonContent.Create(sandbox, SbudJsonContext.Default.NewSandbox), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Checks a new sandbox and returns what creating it would do, without changing anything.</summary>
	public async Task<IReadOnlyList<string>> PlanSandboxAsync(NewSandbox sandbox, CancellationToken cancel = default) =>
		(await SendAsync(HttpMethod.Post, "/v1/sandboxes?dryRun=true", JsonContent.Create(sandbox, SbudJsonContext.Default.NewSandbox), SbudJsonContext.Default.PlanLines, cancel)).Lines;

	/// <summary>Reads everything about one sandbox. It never starts a stopped sandbox.</summary>
	public Task<Sandbox> GetSandboxAsync(string name, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, SandboxPath(name), null, SbudJsonContext.Default.Sandbox, cancel);

	/// <summary>Deletes a sandbox and sbud's records of it, never the folders shared into it.</summary>
	public Task DeleteSandboxAsync(string name, CancellationToken cancel = default) => SendAsync(HttpMethod.Delete, SandboxPath(name), null, cancel);

	/// <summary>Starts a sandbox, and the loop sbud runs inside it to give memory the sandbox no longer needs back to this computer.</summary>
	public Task StartSandboxAsync(string name, CancellationToken cancel = default) => SendAsync(HttpMethod.Post, SandboxPath(name) + "/start", null, cancel);

	/// <summary>Stops a sandbox.</summary>
	public Task StopSandboxAsync(string name, CancellationToken cancel = default) => SendAsync(HttpMethod.Post, SandboxPath(name) + "/stop", null, cancel);

	/// <summary>Stops a sandbox, then starts it again.</summary>
	public Task RestartSandboxAsync(string name, CancellationToken cancel = default) => SendAsync(HttpMethod.Post, SandboxPath(name) + "/restart", null, cancel);

	/// <summary>
	/// Gets a sandbox ready before the client runs sbx in the user's terminal: for the agent, makes sure the sandbox holds
	/// its label's login; either way, starts it. Run <see cref="TerminalSession.Argv"/>, then call
	/// <see cref="CloseTerminalSessionAsync"/> when it exits.
	/// </summary>
	public Task<TerminalSession> OpenTerminalSessionAsync(string sandbox, TerminalKind kind, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, SandboxPath(sandbox) + "/terminal-sessions",
			JsonContent.Create(new TerminalRequest(kind), SbudJsonContext.Default.TerminalRequest), SbudJsonContext.Default.TerminalSession, cancel);

	/// <summary>Ends a terminal session, so the service can keep a Codex login the agent renewed during it.</summary>
	public Task CloseTerminalSessionAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, $"{SandboxPath(sandbox)}/terminal-sessions/{Uri.EscapeDataString(id)}", null, cancel);

	/// <summary>Lists the labels, each with its logins, patterns and sandboxes. No token is ever included.</summary>
	public Task<List<Label>> GetLoginsAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/logins", null, SbudJsonContext.Default.ListLabel, cancel);

	/// <summary>Reads one label.</summary>
	public Task<Label> GetLoginAsync(string label, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, LabelPath(label), null, SbudJsonContext.Default.Label, cancel);

	/// <summary>Renames a label with <c>{ "label": ... }</c>, or replaces its patterns with <c>{ "patterns": [...] }</c>.</summary>
	public Task<Label> PatchLoginAsync(string label, JsonObject patch, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, LabelPath(label), MergePatch(patch), SbudJsonContext.Default.Label, cancel);

	/// <summary>Copies a label's logins into every sandbox assigned that label.</summary>
	public Task<PushReport> PushLoginAsync(string label, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, LabelPath(label) + "/push", null, SbudJsonContext.Default.PushReport, cancel);

	/// <summary>Deletes a stored login; sandboxes keep the copy they already have.</summary>
	public Task DeleteLoginAsync(string label, Agent agent, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, $"{LabelPath(label)}/{AgentName(agent)}", null, cancel);

	/// <summary>Says where the login of the agent installed on this computer is, and whether it exists.</summary>
	public Task<HostLogin> GetHostLoginAsync(Agent agent, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, $"/v1/host-logins/{AgentName(agent)}", null, SbudJsonContext.Default.HostLogin, cancel);

	/// <summary>
	/// Stores the host's own login under a label, then copies it into the sandboxes assigned that label. Ask the user
	/// first, naming the file, because it belongs to whichever account this computer is signed in to.
	/// </summary>
	public Task<SavedLogin> CopyLoginFromHostAsync(string label, Agent agent, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, $"{LabelPath(label)}/{AgentName(agent)}/copy-from-host", null, SbudJsonContext.Default.SavedLogin, cancel);

	/// <summary>
	/// Sends one request as it is and returns the raw status and body, for debugging. A JSON array goes as
	/// <c>application/json-patch+json</c> and any other JSON as <c>application/json</c>.
	/// </summary>
	public async Task<(int Status, string Body)> SendRawJsonAsync(HttpMethod method, string path, string? json = null, CancellationToken cancel = default)
	{
		using var request = new HttpRequestMessage(method, path);
		if (json is not null)
		{
			var contentType = json.TrimStart().StartsWith('[') ? "application/json-patch+json" : "application/json";
			request.Content = new StringContent(json, Encoding.UTF8, contentType);
		}
		using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancel);
		return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancel));
	}

	/// <summary>
	/// Starts signing a new login in under a label. Follow it with <see cref="GetSignInAsync"/> or the <c>sign-in</c>
	/// events: open <see cref="SignIn.Url"/> in the browser, and when the state is <c>need-code</c>, send the code the
	/// browser shows with <see cref="SendSignInCodeAsync"/>.
	/// </summary>
	public Task<SignIn> StartSignInAsync(string label, Agent agent, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, $"{LabelPath(label)}/{AgentName(agent)}/sign-ins", null, SbudJsonContext.Default.SignIn, cancel);

	/// <summary>Reads a sign-in's progress.</summary>
	public Task<SignIn> GetSignInAsync(string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, $"/v1/sign-ins/{Uri.EscapeDataString(id)}", null, SbudJsonContext.Default.SignIn, cancel);

	/// <summary>Sends the code the browser showed to a sign-in waiting for one.</summary>
	public Task SendSignInCodeAsync(string id, string code, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, $"/v1/sign-ins/{Uri.EscapeDataString(id)}/code", JsonContent.Create(new SignInCode(code), SbudJsonContext.Default.SignInCode), cancel);

	/// <summary>Cancels a sign-in; the existing login is unchanged.</summary>
	public Task CancelSignInAsync(string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, $"/v1/sign-ins/{Uri.EscapeDataString(id)}", null, cancel);

	private static string LabelPath(string label) => $"/v1/logins/{Uri.EscapeDataString(label)}";

	private static string AgentName(Agent agent) => agent == Agent.Claude ? "claude" : "codex";

	/// <summary>
	/// Shows what a change to a sandbox would do, changing nothing. <paramref name="operations"/> is a JSON Patch over the
	/// sandbox's settings; paths such as <c>/ramGiB</c>, <c>/kits/{kit}</c> and <c>/mounts/{id}</c> are listed in the API.
	/// </summary>
	public Task<ChangePlan> PlanChangeAsync(string sandbox, IReadOnlyList<PatchOperation> operations, ChangeMode mode = ChangeMode.InPlace, bool fresh = false, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, ChangePath(sandbox, mode, fresh, dryRun: true, null), JsonPatch(operations), SbudJsonContext.Default.ChangePlan, cancel);

	/// <summary>
	/// Starts a change to a sandbox and returns the task doing it. <paramref name="confirmStops"/> names the running
	/// sandboxes the user agreed to stop; the plan lists them.
	/// </summary>
	public Task<TaskInfo> ChangeSandboxAsync(string sandbox, IReadOnlyList<PatchOperation> operations, ChangeMode mode = ChangeMode.InPlace, bool fresh = false,
		IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, ChangePath(sandbox, mode, fresh, dryRun: false, confirmStops), JsonPatch(operations), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>
	/// Reinstalls a kit a sandbox has at its current version, or adds a kit to it. <paramref name="path"/> is the kit's
	/// folder, for a kit the catalogue does not list. <paramref name="confirmStops"/> names the running sandboxes the
	/// user agreed to stop, as for <see cref="ChangeSandboxAsync"/>.
	/// </summary>
	public Task<TaskInfo> PutSandboxKitAsync(string sandbox, string kit, string? path = null, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Put, ShortcutPath($"{SandboxPath(sandbox)}/kits/{Uri.EscapeDataString(kit)}", confirmStops),
			path is null ? null : Json(new JsonObject { ["path"] = path }), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Removes a kit from a sandbox.</summary>
	public Task<TaskInfo> RemoveSandboxKitAsync(string sandbox, string kit, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, ShortcutPath($"{SandboxPath(sandbox)}/kits/{Uri.EscapeDataString(kit)}", confirmStops), null, SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Gives a sandbox a different main workspace.</summary>
	public Task<TaskInfo> SetWorkspaceAsync(string sandbox, Folder folder, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Put, ShortcutPath(SandboxPath(sandbox) + "/workspace", confirmStops),
			JsonContent.Create(folder, SbudJsonContext.Default.Folder), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Shares an extra folder into a sandbox.</summary>
	public Task<TaskInfo> AddMountAsync(string sandbox, Folder folder, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, ShortcutPath(SandboxPath(sandbox) + "/mounts", confirmStops),
			JsonContent.Create(folder, SbudJsonContext.Default.Folder), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Makes the extra folder <paramref name="hostPath"/> read-only, or writable.</summary>
	public Task<TaskInfo> SetMountReadOnlyAsync(string sandbox, string hostPath, bool readOnly, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, ShortcutPath(MountPath(sandbox, hostPath), confirmStops), MergePatch(new JsonObject { ["readOnly"] = readOnly }),
			SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>Stops sharing the extra folder <paramref name="hostPath"/>; the folder itself is untouched.</summary>
	public Task<TaskInfo> RemoveMountAsync(string sandbox, string hostPath, IEnumerable<string>? confirmStops = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, ShortcutPath(MountPath(sandbox, hostPath), confirmStops), null, SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>The service's OpenAPI description, generated from its code.</summary>
	public Task<JsonObject> GetOpenApiAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/openapi/v1.json", null, SbudJsonContext.Default.JsonObject, cancel);

	/// <summary>Finds problems that need a repair, each with the repair that fixes it.</summary>
	public Task<List<Problem>> GetDiagnosticsAsync(CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, "/v1/diagnostics", null, SbudJsonContext.Default.ListProblem, cancel);

	/// <summary>Starts a repair and returns the task doing it.</summary>
	public Task<TaskInfo> RepairAsync(Repair repair, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, "/v1/repairs", JsonContent.Create(repair, SbudJsonContext.Default.Repair), SbudJsonContext.Default.TaskInfo, cancel);

	/// <summary>
	/// Lists a sandbox's open chats; with <paramref name="saved"/>, also the conversations its agent saved, newest first,
	/// which needs the sandbox running.
	/// </summary>
	public Task<List<ChatEntry>> GetChatsAsync(string sandbox, bool saved = false, int? limit = null, CancellationToken cancel = default)
	{
		var query = new List<string>();
		if (saved)
			query.Add("include=saved");
		if (limit is { } n)
			query.Add("limit=" + n);
		return SendAsync(HttpMethod.Get, SandboxPath(sandbox) + "/chats" + (query.Count > 0 ? "?" + string.Join('&', query) : ""), null, SbudJsonContext.Default.ListChatEntry, cancel);
	}

	/// <summary>Opens a new chat, or with <paramref name="resume"/> continues a saved chat. <paramref name="model"/> null means the sandbox's default.</summary>
	public Task<ChatSessionInfo> OpenChatAsync(string sandbox, string? model = null, string? resume = null, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, SandboxPath(sandbox) + "/chats", JsonContent.Create(new NewChat(model, resume), SbudJsonContext.Default.NewChat),
			SbudJsonContext.Default.ChatSessionInfo, cancel);

	/// <summary>Reads one open chat.</summary>
	public Task<ChatSessionInfo> GetChatAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, ChatPath(sandbox, id), null, SbudJsonContext.Default.ChatSessionInfo, cancel);

	/// <summary>Reads everything an open chat has shown so far; follow the <c>chat</c> events for what comes next.</summary>
	public Task<List<ChatItem>> GetChatItemsAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, ChatPath(sandbox, id) + "/items", null, SbudJsonContext.Default.ListChatItem, cancel);

	/// <summary>Changes the model a chat uses from its next message; null means the sandbox's default.</summary>
	public Task<ChatSessionInfo> SetChatModelAsync(string sandbox, string id, string? model, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Patch, ChatPath(sandbox, id), MergePatch(new JsonObject { ["model"] = model }), SbudJsonContext.Default.ChatSessionInfo, cancel);

	/// <summary>Sends a message; the reply arrives as <c>chat</c> events.</summary>
	public Task SendChatMessageAsync(string sandbox, string id, string text, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, ChatPath(sandbox, id) + "/messages", JsonContent.Create(new ChatMessage(text), SbudJsonContext.Default.ChatMessage), cancel);

	/// <summary>Stops the agent's current reply and drops waiting messages.</summary>
	public Task InterruptChatAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, ChatPath(sandbox, id) + "/interrupt", null, cancel);

	/// <summary>Closes a chat; the agent keeps the conversation as a saved chat.</summary>
	public Task CloseChatAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, ChatPath(sandbox, id), null, cancel);

	private static string ChatPath(string sandbox, string id) => $"{SandboxPath(sandbox)}/chats/{Uri.EscapeDataString(id)}";

	/// <summary>Lists a sandbox's consoles.</summary>
	public Task<List<ConsoleInfo>> GetConsolesAsync(string sandbox, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, SandboxPath(sandbox) + "/consoles", null, SbudJsonContext.Default.ListConsoleInfo, cancel);

	/// <summary>Starts bash in a sandbox, in a terminal of <paramref name="rows"/> by <paramref name="cols"/>.</summary>
	public Task<ConsoleInfo> OpenConsoleAsync(string sandbox, int rows = 24, int cols = 80, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Post, SandboxPath(sandbox) + "/consoles", JsonContent.Create(new ConsoleSize(rows, cols), SbudJsonContext.Default.ConsoleSize),
			SbudJsonContext.Default.ConsoleInfo, cancel);

	/// <summary>Reads one console.</summary>
	public Task<ConsoleInfo> GetConsoleAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Get, $"{SandboxPath(sandbox)}/consoles/{Uri.EscapeDataString(id)}", null, SbudJsonContext.Default.ConsoleInfo, cancel);

	/// <summary>Ends bash and closes the console.</summary>
	public Task CloseConsoleAsync(string sandbox, string id, CancellationToken cancel = default) =>
		SendAsync(HttpMethod.Delete, $"{SandboxPath(sandbox)}/consoles/{Uri.EscapeDataString(id)}", null, cancel);

	/// <summary>
	/// Connects a terminal to a console. The socket first carries the console's saved output, then its live output, as
	/// binary messages. Send what the user types as binary messages, and a change of size as the text message
	/// <c>{"type":"resize","rows":R,"cols":C}</c>. When bash ends, the service sends <c>{"type":"exit","code":N}</c> and
	/// closes the socket.
	/// </summary>
	public async Task<System.Net.WebSockets.ClientWebSocket> ConnectConsoleAsync(string sandbox, string id, CancellationToken cancel = default)
	{
		var socket = new System.Net.WebSockets.ClientWebSocket();
		try
		{
			// The invoker is not disposed: the upgraded connection outlives the request that opened it.
			var invoker = new HttpMessageInvoker(new SocketsHttpHandler { UseProxy = false, ConnectCallback = (_, token) => ConnectAsync(Endpoint, token) });
			await socket.ConnectAsync(new Uri($"ws://sbud{SandboxPath(sandbox)}/consoles/{Uri.EscapeDataString(id)}/terminal"), invoker, cancel);
			return socket;
		}
		catch (System.Net.WebSockets.WebSocketException error)
		{
			socket.Dispose();
			throw new SbudException("unavailable", $"Could not connect to console '{id}': {error.Message}", inner: error);
		}
	}

	private static string ChangePath(string sandbox, ChangeMode mode, bool fresh, bool dryRun, IEnumerable<string>? confirmStops)
	{
		var query = new List<string> { "mode=" + (mode switch { ChangeMode.Clone => "clone", ChangeMode.Rebuild => "rebuild", _ => "in-place" }) };
		if (fresh)
			query.Add("fresh=true");
		if (dryRun)
			query.Add("dryRun=true");
		if (confirmStops?.ToList() is { Count: > 0 } stops)
			query.Add("confirmStops=" + Uri.EscapeDataString(string.Join(',', stops)));
		return SandboxPath(sandbox) + "?" + string.Join('&', query);
	}

	private static StringContent JsonPatch(IReadOnlyList<PatchOperation> operations) =>
		new(JsonSerializer.Serialize(operations.ToList(), SbudJsonContext.Default.ListPatchOperation), Encoding.UTF8, "application/json-patch+json");

	private static string MountPath(string sandbox, string hostPath) =>
		SandboxPath(sandbox) + "/mounts/" + string.Join('/', MountId.For(hostPath).Split('/').Select(Uri.EscapeDataString));

	private static string ShortcutPath(string path, IEnumerable<string>? confirmStops) =>
		confirmStops?.ToList() is { Count: > 0 } stops ? path + "?confirmStops=" + Uri.EscapeDataString(string.Join(',', stops)) : path;

	private static StringContent Json(JsonObject body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");

	private static string SandboxPath(string name) => $"/v1/sandboxes/{Uri.EscapeDataString(name)}";

	private static string KitPath(string name) => $"/v1/kits/{Uri.EscapeDataString(name)}";

	private static StringContent MergePatch(JsonObject patch) =>
		new(patch.ToJsonString(), Encoding.UTF8, "application/merge-patch+json");

	private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? body, JsonTypeInfo<T> type, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(method, path) { Content = body };
		using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancel);
		await ThrowIfFailedAsync(response, cancel);
		return (await response.Content.ReadFromJsonAsync(type, cancel))!;
	}

	private async Task SendAsync(HttpMethod method, string path, HttpContent? body, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(method, path) { Content = body };
		using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancel);
		await ThrowIfFailedAsync(response, cancel);
	}

	private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken cancel)
	{
		try
		{
			return await _http.SendAsync(request, completion, cancel);
		}
		catch (HttpRequestException error)
		{
			throw new SbudException("unavailable", $"The sbud service is not answering at {Endpoint}.", inner: error);
		}
	}

	private static async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancel)
	{
		if (response.IsSuccessStatusCode)
			return;
		var problem = await ReadProblemAsync(response, cancel);
		var errors = problem?.Errors?.Select(e => new FieldError(e.Field ?? "", e.Message ?? "")).ToList();
		throw new SbudException(problem?.Kind ?? "failed", problem?.Title ?? $"HTTP {(int)response.StatusCode}", (int)response.StatusCode, errors: errors);
	}

	private static async Task<ProblemBody?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancel)
	{
		try
		{
			var text = await response.Content.ReadAsStringAsync(cancel);
			return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize(text, ProblemContext.Default.ProblemBody);
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
			var pipe = new NamedPipeClientStream(".", endpoint[@"\\.\pipe\".Length..], PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
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

	internal sealed record ProblemBody(string? Kind, string? Title, List<ProblemField>? Errors);

	internal sealed record ProblemField(string? Field, string? Message);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SbudClient.ProblemBody))]
internal sealed partial class ProblemContext : JsonSerializerContext;
