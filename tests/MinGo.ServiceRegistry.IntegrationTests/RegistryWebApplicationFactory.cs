using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MinGo.ServiceRegistry.Hosting;

namespace MinGo.ServiceRegistry.IntegrationTests;

/// <summary>
/// Hosts the Registry Server in-memory via <see cref="WebApplicationFactory{TEntryPoint}"/>, replacing
/// the ambient clock with a controllable <see cref="FakeTimeProvider"/> so lease expiry can be
/// exercised deterministically.
/// </summary>
public sealed class RegistryWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>The controllable clock driving leases and the reaper.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Swap the app-registered TimeProvider for the fake, and shorten the reaper interval.
            var timeProvider = services.SingleOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (timeProvider is not null)
            {
                services.Remove(timeProvider);
            }

            services.AddSingleton<TimeProvider>(Time);
            services.PostConfigure<ServiceRegistryServerOptions>(o => o.ReaperIntervalSeconds = 1);
        });
    }
}
