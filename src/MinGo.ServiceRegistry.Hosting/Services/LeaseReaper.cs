using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Hosting.Services;

/// <summary>
/// Periodically scans the store for expired leases and removes them. Discovery already filters out
/// expired entries, so the reaper exists to reclaim memory and keep the store bounded. The scan
/// interval and clock are driven by the injected <see cref="TimeProvider"/> for testability.
/// </summary>
public sealed class LeaseReaper : BackgroundService
{
    private readonly IServiceRegistryStore _store;
    private readonly ServiceRegistryServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LeaseReaper> _logger;

    public LeaseReaper(
        IServiceRegistryStore store,
        IOptions<ServiceRegistryServerOptions> options,
        TimeProvider timeProvider,
        ILogger<LeaseReaper> logger)
    {
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.ReaperIntervalSeconds));
        using var timer = new PeriodicTimer(interval, _timeProvider);

        _logger.LogInformation("Lease reaper started (interval {Interval}s).", interval.TotalSeconds);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReapOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>Removes all currently-expired leases. Exposed for tests and manual triggering.</summary>
    public async Task ReapOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var expired = await _store.GetExpiredLeaseIdsAsync(now, cancellationToken).ConfigureAwait(false);

        foreach (var leaseId in expired)
        {
            if (await _store.TryRemoveByLeaseAsync(leaseId, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Reaped expired lease {LeaseId}.", leaseId);
            }
        }
    }
}
