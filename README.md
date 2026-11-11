# sbud for .NET

The .NET client for the sbud service, to be published on NuGet as `Sbud`. Its project, Sbud.Contracts, holds the request and response types of the service's API and the client class that sends them. This early preview has one call, `GetServiceAsync()` on `SbudClient`, which reads the running service's version. Nothing has been published yet, and the service itself has not been released, so the preview has been tested only against a stand-in service.

```csharp
using Sbud;

using var client = new SbudClient();
Console.WriteLine((await client.GetServiceAsync()).Version);
```
