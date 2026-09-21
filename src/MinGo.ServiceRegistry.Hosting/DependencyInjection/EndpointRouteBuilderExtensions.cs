using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using MinGo.ServiceRegistry.Hosting.Endpoints;

namespace MinGo.ServiceRegistry.Hosting;

/// <summary>
/// Maps all Registry Server endpoints (registration, leases, discovery, and health).
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>Maps the full Registry HTTP surface plus <c>/health</c>.</summary>
    public static IEndpointRouteBuilder MapServiceRegistryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapRegistrationEndpoints();
        endpoints.MapLeaseEndpoints();
        endpoints.MapDiscoveryEndpoints();
        endpoints.MapHealthChecks("/health");

        return endpoints;
    }
}
