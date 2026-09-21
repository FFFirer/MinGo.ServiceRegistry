using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Services;

namespace MinGo.ServiceRegistry.Hosting.Endpoints;

/// <summary>
/// Maps the discovery endpoints: instance lookup for a service and the service-name catalog.
/// </summary>
public static class DiscoveryEndpoints
{
    /// <summary>Maps <c>GET</c> discovery routes.</summary>
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/registry/services");

        group.MapGet("/{serviceName}/instances", GetInstancesAsync)
            .WithName("DiscoverServiceInstances")
            .WithSummary("Returns the healthy, non-expired instances of a service.")
            .Produces<ServiceInstancesResponse>(StatusCodes.Status200OK);

        group.MapGet("/", GetServicesAsync)
            .WithName("ListServices")
            .WithSummary("Returns the names of all registered services.")
            .Produces<ServiceListResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetInstancesAsync(
        string serviceName,
        DiscoveryManager manager,
        CancellationToken cancellationToken)
    {
        var response = await manager.GetInstancesAsync(serviceName, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetServicesAsync(
        DiscoveryManager manager,
        CancellationToken cancellationToken)
    {
        var response = await manager.GetServicesAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(response);
    }
}
