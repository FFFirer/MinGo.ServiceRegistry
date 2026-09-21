using MinGo.ServiceRegistry.Abstractions;

namespace MinGo.ServiceRegistry.Hosting.Services;

/// <summary>
/// Validates a registration request, returning a map of field errors suitable for an RFC7807
/// <c>application/problem+json</c> validation response.
/// </summary>
public static class ServiceRegistrationValidator
{
    /// <summary>Validates the route service name and the registration payload.</summary>
    /// <returns>An empty dictionary when valid; otherwise field-name to error-messages.</returns>
    public static IDictionary<string, string[]> Validate(string? serviceName, ServiceRegistration? registration)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            errors[nameof(serviceName)] = ["A service name is required."];
        }

        if (registration is null)
        {
            errors[string.Empty] = ["A registration body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(registration.Host))
        {
            errors[nameof(ServiceRegistration.Host)] = ["Host is required."];
        }

        if (registration.Port is < 0 or > 65535)
        {
            errors[nameof(ServiceRegistration.Port)] = ["Port must be between 0 and 65535."];
        }

        if (string.IsNullOrWhiteSpace(registration.Scheme))
        {
            errors[nameof(ServiceRegistration.Scheme)] = ["Scheme is required."];
        }

        if (registration.Lease?.TtlSeconds is { } ttl && ttl <= 0)
        {
            errors["lease.ttlSeconds"] = ["Lease TTL, when supplied, must be positive."];
        }

        return errors;
    }
}
