using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Store;
using Xunit;

namespace MinGo.ServiceRegistry.Server.Tests;

public sealed class MemoryServiceRegistryStoreTests
{
    private static ServiceEntry Entry(string leaseId, string service, string instance, DateTimeOffset expiresAt) =>
        new(
            new ServiceInstance
            {
                ServiceName = service,
                InstanceId = instance,
                Scheme = "http",
                Host = "localhost",
                Port = 5000,
                Health = ServiceHealthStatus.Healthy,
            },
            new Lease(leaseId, 15, expiresAt, expiresAt.AddSeconds(-15)));

    private static readonly DateTimeOffset Now = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_then_GetByLease_returns_entry()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("lease-1", "svc", "inst-1", Now.AddSeconds(15)));

        var fetched = await store.GetByLeaseAsync("lease-1");

        Assert.NotNull(fetched);
        Assert.Equal("inst-1", fetched!.Instance.InstanceId);
    }

    [Fact]
    public async Task Upsert_same_instance_new_lease_evicts_old_lease()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("lease-old", "svc", "inst-1", Now.AddSeconds(15)));
        await store.UpsertAsync(Entry("lease-new", "svc", "inst-1", Now.AddSeconds(30)));

        Assert.Null(await store.GetByLeaseAsync("lease-old"));
        Assert.NotNull(await store.GetByLeaseAsync("lease-new"));
        Assert.Single(await store.GetEntriesAsync("svc"));
    }

    [Fact]
    public async Task TryRenew_updates_expiry_when_present()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("lease-1", "svc", "inst-1", Now.AddSeconds(15)));

        var renewed = await store.TryRenewAsync("lease-1", Now.AddSeconds(60), Now);

        Assert.True(renewed);
        var entry = await store.GetByLeaseAsync("lease-1");
        Assert.Equal(Now.AddSeconds(60), entry!.Lease.ExpiresAt);
        Assert.Equal(Now, entry.Lease.LastRenewalAt);
    }

    [Fact]
    public async Task TryRenew_returns_false_when_lease_missing()
    {
        var store = new MemoryServiceRegistryStore();

        Assert.False(await store.TryRenewAsync("nope", Now.AddSeconds(60), Now));
    }

    [Fact]
    public async Task TryRemoveByLease_returns_false_when_missing()
    {
        var store = new MemoryServiceRegistryStore();

        Assert.False(await store.TryRemoveByLeaseAsync("nope"));
    }

    [Fact]
    public async Task TryRemoveByInstance_removes_matching_entry()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("lease-1", "svc", "inst-1", Now.AddSeconds(15)));

        Assert.True(await store.TryRemoveByInstanceAsync("svc", "inst-1"));
        Assert.Null(await store.GetByLeaseAsync("lease-1"));
    }

    [Fact]
    public async Task GetExpiredLeaseIds_returns_only_expired()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("expired", "svc", "inst-1", Now.AddSeconds(-1)));
        await store.UpsertAsync(Entry("live", "svc", "inst-2", Now.AddSeconds(30)));

        var expired = await store.GetExpiredLeaseIdsAsync(Now);

        Assert.Equal(new[] { "expired" }, expired);
    }

    [Fact]
    public async Task GetServiceNames_returns_distinct_sorted_names()
    {
        var store = new MemoryServiceRegistryStore();
        await store.UpsertAsync(Entry("l1", "zeta", "i1", Now.AddSeconds(15)));
        await store.UpsertAsync(Entry("l2", "alpha", "i2", Now.AddSeconds(15)));
        await store.UpsertAsync(Entry("l3", "zeta", "i3", Now.AddSeconds(15)));

        var names = await store.GetServiceNamesAsync();

        Assert.Equal(new[] { "alpha", "zeta" }, names);
    }
}
