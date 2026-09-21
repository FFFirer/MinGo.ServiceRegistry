using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Services;

namespace MinGo.ServiceRegistry.Hosting.Endpoints;

/// <summary>
/// Maps the registration and administrative-removal endpoints.
/// </summary>
public static class RegistrationEndpoints
{
    /// <summary>Maps <c>POST</c> and administrative <c>DELETE</c> routes for service instances.</summary>
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/registry/services");

        group.MapPost("/{serviceName}/instances", RegisterAsync)
            .WithName("RegisterServiceInstance")
            .WithSummary("Registers a service instance and issues a lease.")
            .Produces<RegistrationResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{serviceName}/instances/{instanceId}", DeregisterByInstanceAsync)
            .WithName("DeregisterServiceInstance")
            .WithSummary("Administratively removes a specific instance.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        string serviceName,
        ServiceRegistration registration,
        RegistrationManager manager,
        CancellationToken cancellationToken)
    {
        var errors = ServiceRegistrationValidator.Validate(serviceName, registration);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await manager.RegisterAsync(serviceName, registration, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/registry/leases/{result.LeaseId}", result);
    }

    private static async Task<IResult> DeregisterByInstanceAsync(
        string serviceName,
        string instanceId,
        RegistrationManager manager,
        CancellationToken cancellationToken)
    {
        var removed = await manager.DeregisterByInstanceAsync(serviceName, instanceId, cancellationToken).ConfigureAwait(false);
        return removed
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: "Instance not found.");
    }
}
