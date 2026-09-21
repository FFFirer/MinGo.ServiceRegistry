using MinGo.ServiceRegistry.Abstractions;

namespace MinGo.ServiceRegistry.Hosting;

/// <summary>
/// Server-side options for the Registry.
/// </summary>
public sealed class ServiceRegistryServerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ServiceRegistry";

    /// <summary>Lease TTL applied when a client does not request one.</summary>
    public int DefaultLeaseTtlSeconds { get; set; } = ServiceRegistryDefaults.DefaultLeaseTtlSeconds;

    /// <summary>Lower bound for a client-requested lease TTL, in seconds.</summary>
    public int MinLeaseTtlSeconds { get; set; } = 5;

    /// <summary>Upper bound for a client-requested lease TTL, in seconds.</summary>
    public int MaxLeaseTtlSeconds { get; set; } = 300;

    /// <summary>How often the reaper scans for and removes expired leases, in seconds.</summary>
    public int ReaperIntervalSeconds { get; set; } = ServiceRegistryDefaults.DefaultReaperIntervalSeconds;
}
