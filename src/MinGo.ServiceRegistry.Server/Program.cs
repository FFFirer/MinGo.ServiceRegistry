using Microsoft.AspNetCore.Components;
using MinGo.ServiceRegistry.Hosting;
using MinGo.ServiceRegistry.Server.Components;
using MinGo.ServiceRegistry.Server.Client.Services;
using MinGo.ServiceRegistry.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Store, managers, lease reaper, JSON (source-generated), ProblemDetails, and health checks.
builder.Services.AddServiceRegistryServer(builder.Configuration);

// Blazor Auto: SSR + InteractiveServer on the host, InteractiveWebAssembly once the client bundle
// downloads. The Client project ships the pages/layout/shared components.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Same-origin HttpClient so RegistryApiClient can call the API during SSR. The WASM side registers
// its own HttpClient in Server.Client/Program.cs with the browser host as BaseAddress.
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});
builder.Services.AddScoped<RegistryApiClient>();

var app = builder.Build();

// RFC7807 problem+json for unhandled exceptions and error status codes.
app.UseExceptionHandler("/error", createScopeForErrors: true);
app.UseStatusCodePages();

// .NET 9+ fingerprinted static assets: serves wwwroot plus the Blazor framework scripts
// (_framework/blazor.web.js, blazor.webassembly.js) from the static web assets manifest with
// precompression + caching. UseStaticFiles alone 404s the framework scripts, which breaks Auto mode.
app.MapStaticAssets();
app.UseAntiforgery();

app.MapServiceRegistryEndpoints();
app.MapStatusEndpoint();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RegistryApiClient).Assembly);

app.Run();

/// <summary>Exposes the entry-point type for <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program
{
}
