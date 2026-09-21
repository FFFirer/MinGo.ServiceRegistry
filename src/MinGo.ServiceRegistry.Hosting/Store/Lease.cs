namespace MinGo.ServiceRegistry.Hosting.Store;

/// <summary>
/// Lease state tracked by the server for a registered instance.
/// </summary>
/// <param name="LeaseId">Server-issued lease identifier.</param>
/// <param name="TtlSeconds">Effective time-to-live, in seconds.</param>
/// <param name="ExpiresAt">UTC instant at which the lease expires if not renewed.</param>
/// <param name="LastRenewalAt">UTC instant of the last registration or renewal.</param>
public sealed record Lease(string LeaseId, int TtlSeconds, DateTimeOffset ExpiresAt, DateTimeOffset LastRenewalAt)
{
    /// <summary>Returns a copy with a new expiry and renewal instant.</summary>
    public Lease Renew(DateTimeOffset now, DateTimeOffset expiresAt) =>
        this with { ExpiresAt = expiresAt, LastRenewalAt = now };

    /// <summary>Whether the lease has expired relative to <paramref name="now"/>.</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
}
