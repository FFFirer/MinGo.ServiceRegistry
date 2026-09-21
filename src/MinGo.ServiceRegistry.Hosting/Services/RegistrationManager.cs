using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Hosting.Services;

/// <summary>
/// Handles instance registration and administrative deregistration.
/// </summary>
public sealed class RegistrationManager
{
    private readonly IServiceRegistryStore _store;
    private readonly ServiceRegistryServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RegistrationManager> _logger;

    public RegistrationManager(
        IServiceRegistryStore store,
        IOptions<ServiceRegistryServerOptions> options,
        TimeProvider timeProvider,
        ILogger<RegistrationManager> logger)
    {
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Registers (or re-registers) an instance and returns the issued lease.</summary>
    public async Task<RegistrationResult> RegisterAsync(string serviceName, ServiceRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(registration);

        var instanceId = string.IsNullOrWhiteSpace(registration.InstanceId)
            ? $"{serviceName}-{Guid.NewGuid().ToString("N")[..12]}"
            : registration.InstanceId!;

        var ttl = ClampTtl(registration.Lease?.TtlSeconds ?? _options.DefaultLeaseTtlSeconds);
        var leaseId = Guid.NewGuid().ToString("N");
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(ttl);

        var instance = new ServiceInstance
        {
            ServiceName = serviceName,
            InstanceId = instanceId,
            Scheme = string.IsNullOrWhiteSpace(registration.Scheme) ? ServiceRegistryDefaults.DefaultScheme : registration.Scheme,
            Host = registration.Host,
            Port = registration.Port,
            Metadata = registration.Metadata,
            Health = ServiceHealthStatus.Healthy,
        };

        await _store.UpsertAsync(new ServiceEntry(instance, new Lease(leaseId, ttl, expiresAt, now)), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Registered {ServiceName}/{InstanceId} at {Scheme}://{Host}:{Port} (lease {LeaseId}, ttl {Ttl}s).",
            serviceName, instanceId, instance.Scheme, instance.Host, instance.Port, leaseId, ttl);

        return new RegistrationResult
        {
            LeaseId = leaseId,
            InstanceId = instanceId,
            ExpiresAt = expiresAt,
            TtlSeconds = ttl,
        };
    }

    /// <summary>Administratively removes an instance by (service, instance).</summary>
    public Task<bool> DeregisterByInstanceAsync(string serviceName, string instanceId, CancellationToken cancellationToken = default) =>
        _store.TryRemoveByInstanceAsync(serviceName, instanceId, cancellationToken);

    private int ClampTtl(int requested) =>
        Math.Clamp(requested, Math.Max(1, _options.MinLeaseTtlSeconds), Math.Max(1, _options.MaxLeaseTtlSeconds));
}
