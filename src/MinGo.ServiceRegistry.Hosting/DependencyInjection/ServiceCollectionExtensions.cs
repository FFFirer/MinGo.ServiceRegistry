using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Services;
using MinGo.ServiceRegistry.Hosting.Store;

namespace MinGo.ServiceRegistry.Hosting;

/// <summary>
/// Service collection extensions that assemble the Registry Server: store, managers, the lease
/// reaper, and JSON/ProblemDetails/health-check infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the Registry Server services with an optional inline configuration callback.</summary>
    public static IServiceCollection AddServiceRegistryServer(
        this IServiceCollection services,
        Action<ServiceRegistryServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddServiceRegistryServerCore(configuration: null, configure);
    }

    /// <summary>
    /// Adds the Registry Server services, binding <see cref="ServiceRegistryServerOptions"/> from the
    /// <c>ServiceRegistry</c> configuration section and then applying an optional inline callback.
    /// </summary>
    public static IServiceCollection AddServiceRegistryServer(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ServiceRegistryServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddServiceRegistryServerCore(configuration, configure);
    }

    private static IServiceCollection AddServiceRegistryServerCore(
        this IServiceCollection services,
        IConfiguration? configuration,
        Action<ServiceRegistryServerOptions>? configure)
    {
        if (configuration is not null)
        {
            services.AddOptions<ServiceRegistryServerOptions>()
                .Bind(configuration.GetSection(ServiceRegistryServerOptions.SectionName));
        }
        else
        {
            services.AddOptions<ServiceRegistryServerOptions>();
        }

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Serialize the wire protocol with the shared source-generated context (camelCase, AOT friendly).
        services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, RegistryJsonContext.Default));

        services.AddProblemDetails();
        services.AddHealthChecks();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IServiceRegistryStore, MemoryServiceRegistryStore>();
        services.TryAddSingleton<RegistrationManager>();
        services.TryAddSingleton<LeaseManager>();
        services.TryAddSingleton<DiscoveryManager>();
        services.AddHostedService<LeaseReaper>();

        return services;
    }
}
