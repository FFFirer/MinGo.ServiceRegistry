using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MinGo.ServiceRegistry.Server.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same-origin HttpClient: in Auto mode the WASM bundle is served by the Registry Server itself,
// so BaseAddress always resolves to the API host.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddScoped<RegistryApiClient>();

await builder.Build().RunAsync();
