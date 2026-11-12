# sbud for .NET

The .NET client for the sbud service, to be published on NuGet as `Sbud`. Its project, Sbud.Contracts, holds the request and response types of the service's API and the client class that sends them. `SbudClient` has a method for each route of the service's API: sandboxes and changes to them, chats, consoles (including the terminal's WebSocket), the kit catalogue, logins and sign-ins, diagnostics and repairs, settings, tasks and the event stream. A refused request throws `SbudException`, which lists every faulty field when there are several. Nothing has been published yet, and the service itself has not been released.

```csharp
using Sbud;

using var client = new SbudClient();
Console.WriteLine((await client.GetServiceAsync()).Version);
```
