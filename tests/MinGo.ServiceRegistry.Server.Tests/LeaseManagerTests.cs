using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting;
using MinGo.ServiceRegistry.Hosting.Services;
using MinGo.ServiceRegistry.Hosting.Store;
using Xunit;

namespace MinGo.ServiceRegistry.Server.Tests;

public sealed class LeaseManagerTests
{
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (RegistrationManager registration, LeaseManager lease, MemoryServiceRegistryStore store, FakeTimeProvider time) Create()
    {
        var store = new MemoryServiceRegistryStore();
        var time = new FakeTimeProvider(Start);
        var options = Options.Create(new ServiceRegistryServerOptions());
        var registration = new RegistrationManager(store, options, time, NullLogger<RegistrationManager>.Instance);
        var lease = new LeaseManager(store, options, time, NullLogger<LeaseManager>.Instance);
        return (registration, lease, store, time);
    }

    private static ServiceRegistration Registration(string instanceId = "i1", int ttl = 15) =>
        new()
        {
            ServiceName = "svc",
            InstanceId = instanceId,
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
            Lease = new LeaseOptions { TtlSeconds = ttl },
        };

    [Fact]
    public async Task Renew_extends_expiry_from_current_time()
    {
        var (registration, lease, _, time) = Create();
        var registered = await registration.RegisterAsync("svc", Registration(ttl: 15));

        time.Advance(TimeSpan.FromSeconds(10));
        var renewed = await lease.RenewAsync(registered.LeaseId);

        Assert.NotNull(renewed);
        Assert.Equal(registered.LeaseId, renewed!.LeaseId);
        // 10s elapsed + 15s ttl from the original start.
        Assert.Equal(Start.AddSeconds(25), renewed.ExpiresAt);
    }

    [Fact]
    public async Task Renew_returns_null_for_unknown_lease()
    {
        var (_, lease, _, _) = Create();

        Assert.Null(await lease.RenewAsync("does-not-exist"));
    }

    [Fact]
    public async Task Deregister_removes_existing_lease()
    {
        var (registration, lease, store, _) = Create();
        var registered = await registration.RegisterAsync("svc", Registration());

        Assert.True(await lease.DeregisterAsync(registered.LeaseId));
        Assert.Null(await store.GetByLeaseAsync(registered.LeaseId));
    }

    [Fact]
    public async Task Deregister_returns_false_for_unknown_lease()
    {
        var (_, lease, _, _) = Create();

        Assert.False(await lease.DeregisterAsync("does-not-exist"));
    }
}
