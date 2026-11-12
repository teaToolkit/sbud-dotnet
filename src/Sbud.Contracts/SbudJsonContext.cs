using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sbud;

/// <summary>The JSON options of the sbud API: camelCase names, as ASP.NET Core's web defaults.</summary>
public static class SbudJson
{
	/// <summary>Options shared by the service and its clients.</summary>
	public static JsonSerializerOptions Options { get; } = Create();

	private static JsonSerializerOptions Create()
	{
		// A repair's "type" may come anywhere in its object, not only first.
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { AllowOutOfOrderMetadataProperties = true };
		options.TypeInfoResolverChain.Insert(0, SbudJsonContext.Default);
		options.MakeReadOnly(populateMissingResolver: true);
		return options;
	}
}

/// <summary>Source-generated serialisation for every type on the wire.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(SandboxDefaults))]
[JsonSerializable(typeof(NewSandbox))]
[JsonSerializable(typeof(Kit))]
[JsonSerializable(typeof(List<Kit>))]
[JsonSerializable(typeof(TaskInfo))]
[JsonSerializable(typeof(List<TaskInfo>))]
[JsonSerializable(typeof(HostStats))]
[JsonSerializable(typeof(FieldError))]
[JsonSerializable(typeof(SandboxSummary))]
[JsonSerializable(typeof(List<SandboxSummary>))]
[JsonSerializable(typeof(Sandbox))]
[JsonSerializable(typeof(PlanLines))]
[JsonSerializable(typeof(ChangePlan))]
[JsonSerializable(typeof(ChatItem))]
[JsonSerializable(typeof(List<ChatItem>))]
[JsonSerializable(typeof(ChatSessionInfo))]
[JsonSerializable(typeof(List<ChatEntry>))]
[JsonSerializable(typeof(NewChat))]
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(SignIn))]
[JsonSerializable(typeof(SignInCode))]
[JsonSerializable(typeof(ConsoleInfo))]
[JsonSerializable(typeof(List<ConsoleInfo>))]
[JsonSerializable(typeof(ConsoleSize))]
[JsonSerializable(typeof(Repair))]
[JsonSerializable(typeof(List<Problem>))]
[JsonSerializable(typeof(List<PatchOperation>))]
[JsonSerializable(typeof(Label))]
[JsonSerializable(typeof(List<Label>))]
[JsonSerializable(typeof(PushReport))]
[JsonSerializable(typeof(HostLogin))]
[JsonSerializable(typeof(SavedLogin))]
[JsonSerializable(typeof(TerminalRequest))]
[JsonSerializable(typeof(TerminalSession))]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonObject))]
public sealed partial class SbudJsonContext : JsonSerializerContext;
