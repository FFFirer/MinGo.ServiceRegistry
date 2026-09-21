using MinGo.ServiceRegistry.Abstractions;
using MinGo.ServiceRegistry.Hosting.Services;
using Xunit;

namespace MinGo.ServiceRegistry.Server.Tests;

public sealed class ServiceRegistrationValidatorTests
{
    private static ServiceRegistration Valid() =>
        new()
        {
            ServiceName = "svc",
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
        };

    [Fact]
    public void Valid_registration_produces_no_errors()
    {
        var errors = ServiceRegistrationValidator.Validate("svc", Valid());

        Assert.Empty(errors);
    }

    [Fact]
    public void Null_body_is_rejected()
    {
        var errors = ServiceRegistrationValidator.Validate("svc", null);

        Assert.True(errors.Count > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_service_name_is_rejected(string? serviceName)
    {
        var errors = ServiceRegistrationValidator.Validate(serviceName, Valid());

        Assert.True(errors.ContainsKey("serviceName"));
    }

    [Fact]
    public void Missing_host_is_rejected()
    {
        var registration = new ServiceRegistration { ServiceName = "svc", Scheme = "http", Host = "", Port = 5000 };

        var errors = ServiceRegistrationValidator.Validate("svc", registration);

        Assert.True(errors.ContainsKey(nameof(ServiceRegistration.Host)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Out_of_range_port_is_rejected(int port)
    {
        var registration = new ServiceRegistration { ServiceName = "svc", Scheme = "http", Host = "localhost", Port = port };

        var errors = ServiceRegistrationValidator.Validate("svc", registration);

        Assert.True(errors.ContainsKey(nameof(ServiceRegistration.Port)));
    }

    [Fact]
    public void Non_positive_ttl_is_rejected()
    {
        var registration = new ServiceRegistration
        {
            ServiceName = "svc",
            Scheme = "http",
            Host = "localhost",
            Port = 5000,
            Lease = new LeaseOptions { TtlSeconds = 0 },
        };

        var errors = ServiceRegistrationValidator.Validate("svc", registration);

        Assert.True(errors.ContainsKey("lease.ttlSeconds"));
    }
}
