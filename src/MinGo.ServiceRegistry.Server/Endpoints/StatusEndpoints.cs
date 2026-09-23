using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MinGo.ServiceRegistry.Hosting;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Server.Endpoints;

/// <summary>
/// Server-side status endpoint used by the dashboard UI. Kept out of
/// <c>MinGo.ServiceRegistry.Hosting</c> so the hosting library stays UI-agnostic;
/// everything it needs is already in DI (<see cref="IServiceRegistryStore"/>,
/// <see cref="ServiceRegistryServerOptions"/>, <see cref="TimeProvider"/>).
/// </summary>
public static class StatusEndpoints
{
    private static readonly DateTimeOffset ProcessStartedAt = DateTimeOffset.UtcNow;

    /// <summary>Maps <c>GET /api/registry/status</c>.</summary>
    public static IEndpointRouteBuilder MapStatusEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/registry/status", static async (
            IServiceRegistryStore store,
            IOptions<ServiceRegistryServerOptions> options,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var serviceNames = await store.GetServiceNamesAsync(cancellationToken).ConfigureAwait(false);

            var instances = 0;
            foreach (var name in serviceNames)
            {
                var entries = await store.GetEntriesAsync(name, cancellationToken).ConfigureAwait(false);
                instances += entries.Count;
            }

            // Read from IOptions so the effective values reflect any PostConfigure callbacks
            // (integration tests override the reaper interval this way).
            var effective = options.Value;
            var startedAt = ProcessStartedAt;
            var now = timeProvider.GetUtcNow();
            var uptime = (long)Math.Max(0, (now - startedAt).TotalSeconds);

            return Results.Json(new StatusResponse
            {
                StartedAt = startedAt,
                UptimeSeconds = uptime,
                Services = serviceNames.Count,
                Instances = instances,
                Config = new StatusConfigResponse
                {
                    DefaultLeaseTtlSeconds = effective.DefaultLeaseTtlSeconds,
                    MinLeaseTtlSeconds = effective.MinLeaseTtlSeconds,
                    MaxLeaseTtlSeconds = effective.MaxLeaseTtlSeconds,
                    ReaperIntervalSeconds = effective.ReaperIntervalSeconds,
                },
            });
        })
        .WithName("GetRegistryStatus")
        .WithTags("registry")
        .AllowAnonymous();

        return endpoints;
    }

    /// <summary>Response payload for <c>GET /api/registry/status</c>.</summary>
    public sealed class StatusResponse
    {
        /// <summary>UTC timestamp captured when the server process first handled a status request.</summary>
        public DateTimeOffset StartedAt { get; init; }

        /// <summary>Seconds elapsed since <see cref="StartedAt"/>.</summary>
        public long UptimeSeconds { get; init; }

        /// <summary>Number of distinct services with entries in the store.</summary>
        public int Services { get; init; }

        /// <summary>Total entries across all services (including expired awaiting reap).</summary>
        public int Instances { get; init; }

        /// <summary>Effective <c>ServiceRegistry</c> configuration section.</summary>
        public StatusConfigResponse Config { get; init; } = new();
    }

    /// <summary>Effective lease/reaper configuration values.</summary>
    public sealed class StatusConfigResponse
    {
        /// <summary>Default lease TTL applied when a client does not request one.</summary>
        public int DefaultLeaseTtlSeconds { get; init; }

        /// <summary>Lower bound for a client-requested lease TTL.</summary>
        public int MinLeaseTtlSeconds { get; init; }

        /// <summary>Upper bound for a client-requested lease TTL.</summary>
        public int MaxLeaseTtlSeconds { get; init; }

        /// <summary>How often the reaper scans for expired leases.</summary>
        public int ReaperIntervalSeconds { get; init; }
    }
}
