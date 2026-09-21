using MinGo.ServiceRegistry.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Store, managers, lease reaper, JSON (source-generated), ProblemDetails, and health checks.
builder.Services.AddServiceRegistryServer(builder.Configuration);

var app = builder.Build();

// RFC7807 problem+json for unhandled exceptions and error status codes.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapServiceRegistryEndpoints();

app.Run();

/// <summary>Exposes the entry-point type for <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program
{
}
