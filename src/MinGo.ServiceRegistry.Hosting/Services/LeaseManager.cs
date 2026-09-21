using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Hosting.Services;

/// <summary>
/// Handles lease renewal and deregistration. A renewal or deregistration that references an
/// unknown lease id returns a "not found" signal so callers can emit HTTP 404, prompting the
/// client to re-register.
/// </summary>
public sealed class LeaseManager
{
    private readonly IServiceRegistryStore _store;
    private readonly ServiceRegistryServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LeaseManager> _logger;

    public LeaseManager(
        IServiceRegistryStore store,
        IOptions<ServiceRegistryServerOptions> options,
        TimeProvider timeProvider,
        ILogger<LeaseManager> logger)
    {
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Renews the lease with the given id, extending its expiry by the stored TTL.
    /// Returns <see langword="null"/> when the lease does not exist or has already expired, in which
    /// case the client must re-register rather than renew.
    /// </summary>
    public async Task<RenewResult?> RenewAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

        var existing = await _store.GetByLeaseAsync(leaseId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogDebug("Renewal rejected: lease {LeaseId} not found.", leaseId);
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (existing.Lease.IsExpired(now))
        {
            // An expired lease is treated as lost even before the reaper removes it, so renewal 404s
            // deterministically and the client re-registers.
            _logger.LogDebug("Renewal rejected: lease {LeaseId} expired.", leaseId);
            return null;
        }

        var ttl = ClampTtl(existing.Lease.TtlSeconds);
        var expiresAt = now.AddSeconds(ttl);

        if (!await _store.TryRenewAsync(leaseId, expiresAt, now, cancellationToken).ConfigureAwait(false))
        {
            // Lost the race against the reaper; treat as a missing lease.
            return null;
        }

        return new RenewResult { LeaseId = leaseId, ExpiresAt = expiresAt };
    }

    /// <summary>
    /// Deregisters the instance owning the given lease. Returns <see langword="false"/> when the
    /// lease does not exist (deregistration is otherwise idempotent).
    /// </summary>
    public async Task<bool> DeregisterAsync(string leaseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

        var removed = await _store.TryRemoveByLeaseAsync(leaseId, cancellationToken).ConfigureAwait(false);
        if (removed)
        {
            _logger.LogInformation("Deregistered lease {LeaseId}.", leaseId);
        }

        return removed;
    }

    private int ClampTtl(int requested) =>
        Math.Clamp(requested, Math.Max(1, _options.MinLeaseTtlSeconds), Math.Max(1, _options.MaxLeaseTtlSeconds));
}
