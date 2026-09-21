using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Hosting.Services;

/// <summary>
/// Serves discovery queries. Only instances whose lease has not expired relative to the injected
/// <see cref="TimeProvider"/> are returned, so a lapsed instance disappears from discovery even
/// before the reaper physically removes it.
/// </summary>
public sealed class DiscoveryManager
{
    private readonly IServiceRegistryStore _store;
    private readonly TimeProvider _timeProvider;

    public DiscoveryManager(IServiceRegistryStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns the healthy, non-expired instances of a service.</summary>
    public async Task<ServiceInstancesResponse> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var now = _timeProvider.GetUtcNow();
        var entries = await _store.GetEntriesAsync(serviceName, cancellationToken).ConfigureAwait(false);

        var instances = entries
            .Where(e => e.Instance.Health == ServiceHealthStatus.Healthy && !e.Lease.IsExpired(now))
            .Select(e => e.Instance)
            .OrderBy(i => i.InstanceId, StringComparer.Ordinal)
            .ToList();

        return new ServiceInstancesResponse { ServiceName = serviceName, Instances = instances };
    }

    /// <summary>Returns the distinct names of all services that currently have entries.</summary>
    public async Task<ServiceListResponse> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        var names = await _store.GetServiceNamesAsync(cancellationToken).ConfigureAwait(false);
        return new ServiceListResponse { Services = names };
    }
}
