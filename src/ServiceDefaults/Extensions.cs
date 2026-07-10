namespace ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using ServiceDefaults.Observability;
using Gma.Framework.Naming;
using Gma.Framework.Runtime;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ServiceDefaultsRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<ServiceDefaultsRegistrationMarker>();
        IConfigurationSection observabilitySection = builder.Configuration.GetSection(ObservabilityOptions.SectionName);
        ObservabilityOptions observability = observabilitySection.Get<ObservabilityOptions>() ?? new();
        ApplicationIdentityOptions applicationIdentity = builder.Configuration
            .GetSection(ApplicationIdentityOptions.SectionName)
            .Get<ApplicationIdentityOptions>() ?? new ApplicationIdentityOptions();
        ValidateObservabilityOptions(observability);
        ValidateApplicationIdentityOptions(applicationIdentity);
        string applicationNamespace = applicationIdentity.EffectiveNamespace;

        builder.Services
            .AddOptions<ObservabilityOptions>()
            .Bind(observabilitySection)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());
        builder.Services.AddHealthChecks();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter($"{applicationNamespace}.*");

                if (observability.Otlp.Enabled && observability.Otlp.ExportMetrics)
                {
                    metrics.AddOtlpExporter(exporter => ConfigureOtlpExporter(exporter, observability.Otlp));
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddSource($"{applicationNamespace}.*");

                if (observability.Otlp.Enabled && observability.Otlp.ExportTraces)
                {
                    tracing.AddOtlpExporter(exporter => ConfigureOtlpExporter(exporter, observability.Otlp));
                }
            });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;

            if (observability.Otlp.Enabled && observability.Otlp.ExportLogs)
            {
                logging.AddOtlpExporter(exporter => ConfigureOtlpExporter(exporter, observability.Otlp));
            }
        });

        return builder;
    }

    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });

        ObservabilityOptions observability = endpoints.ServiceProvider
            .GetRequiredService<IOptions<ObservabilityOptions>>()
            .Value;

        if (observability.Prometheus.Enabled)
        {
            string endpointPath = observability.Prometheus.EndpointPath.StartsWith('/')
                ? observability.Prometheus.EndpointPath
                : $"/{observability.Prometheus.EndpointPath}";

            endpoints.MapMetrics(endpointPath);
        }

        return endpoints;
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions exporter, OtlpOptions options)
    {
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            exporter.Endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        }
    }

    private static void ValidateObservabilityOptions(ObservabilityOptions options)
    {
        ValidateOptionsResult result = new ObservabilityOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(ObservabilityOptions.SectionName, typeof(ObservabilityOptions), result.Failures);
        }
    }

    private static void ValidateApplicationIdentityOptions(ApplicationIdentityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DisplayName))
        {
            throw new OptionsValidationException(
                ApplicationIdentityOptions.SectionName,
                typeof(ApplicationIdentityOptions),
                [$"{ApplicationIdentityOptions.SectionName}:DisplayName is required."]);
        }

        if (!ApplicationNamespaces.IsValid(options.Namespace))
        {
            throw new OptionsValidationException(
                ApplicationIdentityOptions.SectionName,
                typeof(ApplicationIdentityOptions),
                [
                    $"{ApplicationIdentityOptions.SectionName}:Namespace must be a lowercase kebab-case value " +
                    $"with {ApplicationNamespaces.MaxLength} characters or fewer."
                ]);
        }
    }

    private sealed class ServiceDefaultsRegistrationMarker;
}
