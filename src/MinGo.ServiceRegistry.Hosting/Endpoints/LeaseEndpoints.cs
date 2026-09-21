using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Services;

namespace MinGo.ServiceRegistry.Hosting.Endpoints;

/// <summary>
/// Maps the lease renewal and deregistration endpoints. A 404 signals a lost lease, which the SDK
/// interprets as "re-register".
/// </summary>
public static class LeaseEndpoints
{
    /// <summary>Maps <c>PUT</c> and <c>DELETE</c> routes for leases.</summary>
    public static IEndpointRouteBuilder MapLeaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/registry/leases");

        group.MapPut("/{leaseId}", RenewAsync)
            .WithName("RenewLease")
            .WithSummary("Renews a lease, extending its expiry.")
            .Produces<RenewResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{leaseId}", DeregisterAsync)
            .WithName("DeregisterLease")
            .WithSummary("Deregisters the instance owning a lease.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> RenewAsync(
        string leaseId,
        LeaseManager manager,
        CancellationToken cancellationToken)
    {
        var result = await manager.RenewAsync(leaseId, cancellationToken).ConfigureAwait(false);
        return result is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: "Lease not found.")
            : TypedResults.Ok(result);
    }

    private static async Task<IResult> DeregisterAsync(
        string leaseId,
        LeaseManager manager,
        CancellationToken cancellationToken)
    {
        var removed = await manager.DeregisterAsync(leaseId, cancellationToken).ConfigureAwait(false);
        return removed
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: "Lease not found.");
    }
}
