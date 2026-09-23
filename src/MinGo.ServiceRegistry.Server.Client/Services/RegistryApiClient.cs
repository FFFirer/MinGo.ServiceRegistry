using System.Net.Http.Json;
using MinGo.ServiceRegistry.Abstractions;

namespace MinGo.ServiceRegistry.Server.Client.Services;

/// <summary>
/// Thin HTTP wrapper over the Registry Server REST API. Consumed by UI pages in both Server (SSR)
/// and WebAssembly render modes; the underlying <see cref="HttpClient"/> is always same-origin.
/// </summary>
public sealed class RegistryApiClient
{
    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="RegistryApiClient"/> class.</summary>
    public RegistryApiClient(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>Returns the list of service names that currently have entries.</summary>
    public async Task<ServiceListResponse> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("/api/registry/services", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync(RegistryJsonContext.Default.ServiceListResponse, cancellationToken)
            .ConfigureAwait(false);
        return body ?? new ServiceListResponse();
    }

    /// <summary>Returns the healthy, non-expired instances of a service.</summary>
    public async Task<ServiceInstancesResponse> GetInstancesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        var response = await _http
            .GetAsync($"/api/registry/services/{Uri.EscapeDataString(serviceName)}/instances", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync(RegistryJsonContext.Default.ServiceInstancesResponse, cancellationToken)
            .ConfigureAwait(false);
        return body ?? new ServiceInstancesResponse { ServiceName = serviceName };
    }

    /// <summary>Probes <c>GET /health</c>; returns <see langword="true"/> on 200.</summary>
    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>Fetches the runtime snapshot emitted by <c>GET /api/registry/status</c>.</summary>
    public async Task<StatusSnapshot?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("/api/registry/status", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync(RegistryClientJsonContext.Default.StatusSnapshot, cancellationToken)
            .ConfigureAwait(false);
    }
}
