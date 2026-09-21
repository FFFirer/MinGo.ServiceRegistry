using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting;
using MinGo.ServiceRegistry.Hosting.Services;
using MinGo.ServiceRegistry.Hosting.Store;
using Xunit;

namespace MinGo.ServiceRegistry.Server.Tests;

public sealed class RegistrationManagerTests
{
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (RegistrationManager manager, MemoryServiceRegistryStore store, FakeTimeProvider time) Create(
        ServiceRegistryServerOptions? options = null)
    {
        var store = new MemoryServiceRegistryStore();
        var time = new FakeTimeProvider(Start);
        var manager = new RegistrationManager(
            store,
            Options.Create(options ?? new ServiceRegistryServerOptions()),
            time,
            NullLogger<RegistrationManager>.Instance);
        return (manager, store, time);
    }

    private static ServiceRegistration Registration(string? instanceId = null, int? ttl = null) =>
        new()
        {
            ServiceName = "svc",
            InstanceId = instanceId,
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
            Lease = ttl is null ? null : new LeaseOptions { TtlSeconds = ttl.Value },
        };

    [Fact]
    public async Task Register_generates_instance_id_and_lease()
    {
        var (manager, store, _) = Create();

        var result = await manager.RegisterAsync("svc", Registration());

        Assert.False(string.IsNullOrWhiteSpace(result.LeaseId));
        Assert.StartsWith("svc-", result.InstanceId, StringComparison.Ordinal);
        Assert.Equal(15, result.TtlSeconds);
        Assert.Equal(Start.AddSeconds(15), result.ExpiresAt);
        Assert.NotNull(await store.GetByLeaseAsync(result.LeaseId));
    }

    [Fact]
    public async Task Register_honours_supplied_instance_id()
    {
        var (manager, _, _) = Create();

        var result = await manager.RegisterAsync("svc", Registration(instanceId: "fixed-id"));

        Assert.Equal("fixed-id", result.InstanceId);
    }

    [Theory]
    [InlineData(1, 5)]     // below min -> clamped up
    [InlineData(60, 60)]   // within range -> unchanged
    [InlineData(9999, 300)]// above max -> clamped down
    public async Task Register_clamps_requested_ttl(int requested, int expected)
    {
        var (manager, _, _) = Create();

        var result = await manager.RegisterAsync("svc", Registration(ttl: requested));

        Assert.Equal(expected, result.TtlSeconds);
        Assert.Equal(Start.AddSeconds(expected), result.ExpiresAt);
    }

    [Fact]
    public async Task DeregisterByInstance_removes_entry()
    {
        var (manager, store, _) = Create();
        var result = await manager.RegisterAsync("svc", Registration(instanceId: "i1"));

        Assert.True(await manager.DeregisterByInstanceAsync("svc", "i1"));
        Assert.Null(await store.GetByLeaseAsync(result.LeaseId));
    }
}
