namespace MinGo.ServiceRegistry.Hosting.Store;

/// <summary>
/// Persistence abstraction for the Registry. The MVP ships an in-memory implementation; the
/// interface is designed so Redis / PostgreSQL / distributed stores can be substituted later
/// without touching the Registry core.
/// </summary>
public interface IServiceRegistryStore
{
    /// <summary>
    /// Inserts or replaces an entry. Any older entry for the same (service, instance) pair but a
    /// different lease is removed, so a client restart re-registering the same instance id does not
    /// leave a duplicate.
    /// </summary>
    Task UpsertAsync(ServiceEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Renews the lease with the given id, or returns <see langword="false"/> if it does not exist.</summary>
    Task<bool> TryRenewAsync(string leaseId, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Removes an entry by lease id. Returns <see langword="false"/> if it does not exist.</summary>
    Task<bool> TryRemoveByLeaseAsync(string leaseId, CancellationToken cancellationToken = default);

    /// <summary>Removes an entry by (service, instance). Returns <see langword="false"/> if it does not exist.</summary>
    Task<bool> TryRemoveByInstanceAsync(string serviceName, string instanceId, CancellationToken cancellationToken = default);

    /// <summary>Gets the entry for a lease id, or <see langword="null"/>.</summary>
    Task<ServiceEntry?> GetByLeaseAsync(string leaseId, CancellationToken cancellationToken = default);

    /// <summary>Gets all entries (regardless of expiry) for a service.</summary>
    Task<IReadOnlyList<ServiceEntry>> GetEntriesAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>Gets the lease ids that have expired relative to <paramref name="now"/>.</summary>
    Task<IReadOnlyList<string>> GetExpiredLeaseIdsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Gets the distinct names of all services that currently have entries.</summary>
    Task<IReadOnlyList<string>> GetServiceNamesAsync(CancellationToken cancellationToken = default);
}
