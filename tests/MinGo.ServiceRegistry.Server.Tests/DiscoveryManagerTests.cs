using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting;
using MinGo.ServiceRegistry.Hosting.Services;
using MinGo.ServiceRegistry.Hosting.Store;
using Xunit;

namespace MinGo.ServiceRegistry.Server.Tests;

public sealed class DiscoveryManagerTests
{
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (RegistrationManager registration, DiscoveryManager discovery, LeaseReaper reaper, MemoryServiceRegistryStore store, FakeTimeProvider time) Create()
    {
        var store = new MemoryServiceRegistryStore();
        var time = new FakeTimeProvider(Start);
        var options = Options.Create(new ServiceRegistryServerOptions());
        var registration = new RegistrationManager(store, options, time, NullLogger<RegistrationManager>.Instance);
        var discovery = new DiscoveryManager(store, time);
        var reaper = new LeaseReaper(store, options, time, NullLogger<LeaseReaper>.Instance);
        return (registration, discovery, reaper, store, time);
    }

    private static ServiceRegistration Registration(string instanceId, int ttl) =>
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
    public async Task GetInstances_excludes_expired_before_reaping()
    {
        var (registration, discovery, _, _, time) = Create();
        await registration.RegisterAsync("svc", Registration("short", ttl: 10));
        await registration.RegisterAsync("svc", Registration("long", ttl: 60));

        time.Advance(TimeSpan.FromSeconds(30));
        var response = await discovery.GetInstancesAsync("svc");

        Assert.Equal("svc", response.ServiceName);
        var instance = Assert.Single(response.Instances);
        Assert.Equal("long", instance.InstanceId);
    }

    [Fact]
    public async Task GetInstances_returns_empty_for_unknown_service()
    {
        var (_, discovery, _, _, _) = Create();

        var response = await discovery.GetInstancesAsync("missing");

        Assert.Empty(response.Instances);
    }

    [Fact]
    public async Task GetServices_lists_registered_service_names()
    {
        var (registration, discovery, _, _, _) = Create();
        await registration.RegisterAsync("svc-a", Registration("i1", ttl: 30));
        await registration.RegisterAsync("svc-b", Registration("i2", ttl: 30));

        var response = await discovery.GetServicesAsync();

        Assert.Equal(new[] { "svc-a", "svc-b" }, response.Services);
    }

    [Fact]
    public async Task Reaper_removes_expired_leases_from_store()
    {
        var (registration, discovery, reaper, store, time) = Create();
        var registered = await registration.RegisterAsync("svc", Registration("i1", ttl: 10));

        time.Advance(TimeSpan.FromSeconds(30));
        await reaper.ReapOnceAsync();

        Assert.Null(await store.GetByLeaseAsync(registered.LeaseId));
        Assert.Empty((await discovery.GetInstancesAsync("svc")).Instances);
    }
}
