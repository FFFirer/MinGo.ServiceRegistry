using System.Net;
using System.Net.Http.Json;
using MinGo.ServiceRegistry.Abstractions;
using Xunit;

namespace MinGo.ServiceRegistry.IntegrationTests;

public sealed class RegistryApiIntegrationTests
{
    private static ServiceRegistration Registration(string service = "svc", string instance = "i1", int ttl = 15, string host = "localhost") =>
        new()
        {
            ServiceName = service,
            InstanceId = instance,
            Scheme = "http",
            Host = host,
            Port = 5000,
            Lease = new LeaseOptions { TtlSeconds = ttl },
        };

    private static async Task<RegistrationResult> RegisterAsync(HttpClient client, ServiceRegistration registration)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/registry/services/{registration.ServiceName}/instances",
            registration,
            RegistryJsonContext.Default.ServiceRegistration);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync(RegistryJsonContext.Default.RegistrationResult);
        Assert.NotNull(result);
        return result!;
    }

    private static async Task<ServiceInstancesResponse> DiscoverAsync(HttpClient client, string service)
    {
        var response = await client.GetAsync($"/api/registry/services/{service}/instances");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync(RegistryJsonContext.Default.ServiceInstancesResponse);
        Assert.NotNull(body);
        return body!;
    }

    [Fact]
    public async Task Register_then_discover_returns_the_instance()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await RegisterAsync(client, Registration());

        var discovered = await DiscoverAsync(client, "svc");
        var instance = Assert.Single(discovered.Instances);
        Assert.Equal(result.InstanceId, instance.InstanceId);
        Assert.Equal("http", instance.Scheme);
        Assert.Equal(5000, instance.Port);
    }

    [Fact]
    public async Task Renew_extends_the_lease()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();
        var result = await RegisterAsync(client, Registration());

        var renew = await client.PutAsync($"/api/registry/leases/{result.LeaseId}", content: null);

        Assert.Equal(HttpStatusCode.OK, renew.StatusCode);
        var renewed = await renew.Content.ReadFromJsonAsync(RegistryJsonContext.Default.RenewResult);
        Assert.Equal(result.LeaseId, renewed!.LeaseId);
    }

    [Fact]
    public async Task Expired_lease_disappears_from_discovery()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, Registration(ttl: 10));

        factory.Time.Advance(TimeSpan.FromSeconds(30));

        var discovered = await DiscoverAsync(client, "svc");
        Assert.Empty(discovered.Instances);
    }

    [Fact]
    public async Task Renew_after_expiry_returns_404()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();
        var result = await RegisterAsync(client, Registration(ttl: 10));

        factory.Time.Advance(TimeSpan.FromSeconds(30));
        var renew = await client.PutAsync($"/api/registry/leases/{result.LeaseId}", content: null);

        // An expired lease is not renewable: renewal 404s so the client re-registers.
        Assert.Equal(HttpStatusCode.NotFound, renew.StatusCode);
    }

    [Fact]
    public async Task Renew_unknown_lease_returns_404()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();

        var renew = await client.PutAsync("/api/registry/leases/does-not-exist", content: null);

        Assert.Equal(HttpStatusCode.NotFound, renew.StatusCode);
    }

    [Fact]
    public async Task Deregister_by_lease_removes_the_instance()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();
        var result = await RegisterAsync(client, Registration());

        var delete = await client.DeleteAsync($"/api/registry/leases/{result.LeaseId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var discovered = await DiscoverAsync(client, "svc");
        Assert.Empty(discovered.Instances);

        var renew = await client.PutAsync($"/api/registry/leases/{result.LeaseId}", content: null);
        Assert.Equal(HttpStatusCode.NotFound, renew.StatusCode);
    }

    [Fact]
    public async Task Deregister_unknown_lease_returns_404()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();

        var delete = await client.DeleteAsync("/api/registry/leases/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_400_problem_details()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/registry/services/svc/instances",
            Registration(host: string.Empty),
            RegistryJsonContext.Default.ServiceRegistration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_services_returns_registered_names()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, Registration(service: "svc-a", instance: "a1"));
        await RegisterAsync(client, Registration(service: "svc-b", instance: "b1"));

        var response = await client.GetAsync("/api/registry/services");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync(RegistryJsonContext.Default.ServiceListResponse);

        Assert.Equal(new[] { "svc-a", "svc-b" }, body!.Services);
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        await using var factory = new RegistryWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
