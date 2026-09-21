using System.Text.Json;
using MinGo.ServiceRegistry.Abstractions;
using Xunit;

namespace MinGo.ServiceRegistry.ProtocolTests;

/// <summary>
/// Locks the wire contract shared by the SDK client and the Registry Server: camelCase property
/// names, null omission, and stable round-tripping through the source-generated
/// <see cref="RegistryJsonContext"/>.
/// </summary>
public sealed class ProtocolContractTests
{
    [Fact]
    public void ServiceRegistration_serializes_with_camelCase_names()
    {
        var registration = new ServiceRegistration
        {
            ServiceName = "svc",
            InstanceId = "i1",
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
            Metadata = new Dictionary<string, string> { ["zone"] = "eu" },
            Lease = new LeaseOptions { TtlSeconds = 20 },
        };

        var json = JsonSerializer.Serialize(registration, RegistryJsonContext.Default.ServiceRegistration);

        Assert.Contains("\"serviceName\":\"svc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"instanceId\":\"i1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lease\":{\"ttlSeconds\":20}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceName", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceRegistration_omits_null_metadata_and_lease()
    {
        var registration = new ServiceRegistration
        {
            ServiceName = "svc",
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
        };

        var json = JsonSerializer.Serialize(registration, RegistryJsonContext.Default.ServiceRegistration);

        Assert.DoesNotContain("metadata", json, StringComparison.Ordinal);
        Assert.DoesNotContain("lease", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationResult_round_trips()
    {
        var original = new RegistrationResult
        {
            LeaseId = "lease-1",
            InstanceId = "i1",
            ExpiresAt = new DateTimeOffset(2024, 1, 1, 0, 0, 15, TimeSpan.Zero),
            TtlSeconds = 15,
        };

        var json = JsonSerializer.Serialize(original, RegistryJsonContext.Default.RegistrationResult);
        var restored = JsonSerializer.Deserialize(json, RegistryJsonContext.Default.RegistrationResult);

        Assert.NotNull(restored);
        Assert.Equal(original.LeaseId, restored!.LeaseId);
        Assert.Equal(original.InstanceId, restored.InstanceId);
        Assert.Equal(original.ExpiresAt, restored.ExpiresAt);
        Assert.Equal(original.TtlSeconds, restored.TtlSeconds);
    }

    [Fact]
    public void ServiceInstancesResponse_deserializes_from_camelCase_json()
    {
        const string json = """
        {
          "serviceName": "svc",
          "instances": [
            { "serviceName": "svc", "instanceId": "i1", "scheme": "http", "host": "localhost", "port": 5000, "health": 1 }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize(json, RegistryJsonContext.Default.ServiceInstancesResponse);

        Assert.NotNull(response);
        Assert.Equal("svc", response!.ServiceName);
        var instance = Assert.Single(response.Instances);
        Assert.Equal("i1", instance.InstanceId);
        Assert.Equal(ServiceHealthStatus.Healthy, instance.Health);
    }

    [Fact]
    public void ServiceListResponse_round_trips()
    {
        var original = new ServiceListResponse { Services = new[] { "a", "b" } };

        var json = JsonSerializer.Serialize(original, RegistryJsonContext.Default.ServiceListResponse);
        var restored = JsonSerializer.Deserialize(json, RegistryJsonContext.Default.ServiceListResponse);

        Assert.NotNull(restored);
        Assert.Equal(new[] { "a", "b" }, restored!.Services);
    }
}
