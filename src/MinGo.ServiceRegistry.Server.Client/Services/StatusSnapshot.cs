using System.Text.Json.Serialization;

namespace MinGo.ServiceRegistry.Server.Client.Services;

/// <summary>
/// Snapshot returned by <c>GET /api/registry/status</c>. Mirrors the anonymous JSON shape emitted by
/// the server-side status endpoint (camelCase on the wire).
/// </summary>
public sealed class StatusSnapshot
{
    /// <summary>UTC timestamp when the server process started.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Seconds elapsed since <see cref="StartedAt"/>.</summary>
    public long UptimeSeconds { get; init; }

    /// <summary>Number of distinct services that currently have entries in the store.</summary>
    public int Services { get; init; }

    /// <summary>Total number of instances across all services (including expired entries awaiting reaping).</summary>
    public int Instances { get; init; }

    /// <summary>Effective <c>ServiceRegistry</c> configuration section.</summary>
    public StatusConfig Config { get; init; } = new();
}

/// <summary>Effective lease/reaper configuration values.</summary>
public sealed class StatusConfig
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

/// <summary>
/// Source-generated JSON context for the client-only status DTO. Keeps the WASM build trimming/AOT
/// friendly under <c>TreatWarningsAsErrors=true</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StatusSnapshot))]
[JsonSerializable(typeof(StatusConfig))]
public sealed partial class RegistryClientJsonContext : JsonSerializerContext
{
}
