using System.Collections.Concurrent;

namespace MinGo.ServiceRegistry.Hosting.Store;

/// <summary>
/// In-memory <see cref="IServiceRegistryStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by lease id. Intended for the MVP / single-node deployment.
/// </summary>
public sealed class MemoryServiceRegistryStore : IServiceRegistryStore
{
    private readonly ConcurrentDictionary<string, ServiceEntry> _byLease = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task UpsertAsync(ServiceEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Drop any older lease for the same (service, instance) so a re-registration is not duplicated.
        foreach (var kvp in _byLease)
        {
            var existing = kvp.Value.Instance;
            if (!string.Equals(kvp.Key, entry.Lease.LeaseId, StringComparison.Ordinal)
                && string.Equals(existing.ServiceName, entry.Instance.ServiceName, StringComparison.Ordinal)
                && string.Equals(existing.InstanceId, entry.Instance.InstanceId, StringComparison.Ordinal))
            {
                _byLease.TryRemove(kvp.Key, out _);
            }
        }

        _byLease[entry.Lease.LeaseId] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryRenewAsync(string leaseId, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

        // Compare-and-swap loop: retry only under concurrent mutation of the same lease.
        while (_byLease.TryGetValue(leaseId, out var existing))
        {
            var updated = existing with { Lease = existing.Lease.Renew(now, expiresAt) };
            if (_byLease.TryUpdate(leaseId, updated, existing))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveByLeaseAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        return Task.FromResult(_byLease.TryRemove(leaseId, out _));
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveByInstanceAsync(string serviceName, string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var removed = false;
        foreach (var kvp in _byLease)
        {
            if (string.Equals(kvp.Value.Instance.ServiceName, serviceName, StringComparison.Ordinal)
                && string.Equals(kvp.Value.Instance.InstanceId, instanceId, StringComparison.Ordinal)
                && _byLease.TryRemove(kvp.Key, out _))
            {
                removed = true;
            }
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<ServiceEntry?> GetByLeaseAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        _byLease.TryGetValue(leaseId, out var entry);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceEntry>> GetEntriesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        IReadOnlyList<ServiceEntry> result = _byLease.Values
            .Where(e => string.Equals(e.Instance.ServiceName, serviceName, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetExpiredLeaseIdsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _byLease
            .Where(kvp => kvp.Value.Lease.IsExpired(now))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _byLease.Values
            .Select(e => e.Instance.ServiceName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(result);
    }
}
