namespace ServiceDefaults.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceDefaults.Observability;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ServiceDefaultsRegistrationTests
{
    [Fact]
    public void Map_default_endpoints_rejects_null_builder()
    {
        Assert.Throws<ArgumentNullException>(() => Extensions.MapDefaultEndpoints(null!));
    }

    [Fact]
    public void Map_default_endpoints_owns_health_endpoint_paths()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "src", "ServiceDefaults", "Extensions.cs"));

        Assert.Contains("MapHealthChecks(\"/health\",", source, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/alive\",", source, StringComparison.Ordinal);
        Assert.Contains("registration.Tags.Contains(\"ready\")", source, StringComparison.Ordinal);
        Assert.Contains("Predicate = _ => false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_defaults_registration_is_idempotent()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();
        builder.AddServiceDefaults();

        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<ObservabilityOptions>));
        Assert.Single(builder.Services, HasService<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType.Name == "ServiceDefaultsRegistrationMarker");
    }

    [Theory]
    [InlineData("Observability:Prometheus:EndpointPath", "metrics", "EndpointPath")]
    [InlineData("Observability:Otlp:Endpoint", "not-a-uri", "Endpoint")]
    public void Service_defaults_rejects_invalid_observability_settings_at_composition(
        string setting,
        string value,
        string expectedFailure)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration[setting] = value;

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddServiceDefaults());

        Assert.Contains(exception.Failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void Service_defaults_rejects_enabled_otlp_without_signals_at_composition()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Observability:Otlp:Enabled"] = "true";
        builder.Configuration["Observability:Otlp:ExportMetrics"] = "false";
        builder.Configuration["Observability:Otlp:ExportTraces"] = "false";
        builder.Configuration["Observability:Otlp:ExportLogs"] = "false";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddServiceDefaults());

        Assert.Contains(exception.Failures, failure => failure.Contains("at least one signal", StringComparison.Ordinal));
    }

    private static Predicate<ServiceDescriptor> HasService<TService, TImplementation>() =>
        descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GMA-Skeleton.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
