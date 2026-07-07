namespace Gma.Modules.Auth.Tests;

using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Handlers;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthApplicationRegistrationTests
{
    [Fact]
    public void Auth_application_registration_is_idempotent()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddAuthApplication(configuration);
        services.AddAuthApplication(configuration);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<AuthApplicationOptions>));
        Assert.Single(services, HasService<IValidateOptions<AuthApplicationOptions>, AuthApplicationOptionsValidator>());
        Assert.Single(services, HasService<ICommandHandler<RegisterMemberCommand, AuthTokensResponse>, RegisterMemberCommandHandler>());
        Assert.Single(services, descriptor => descriptor.ServiceType.Name == "AuthApplicationOptionsRegistrationMarker");
    }

    [Fact]
    public void Auth_application_registration_rejects_invalid_options_before_service_mutation()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(
            ("Auth:RefreshTokenLifetimeDays", "0"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddAuthApplication(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains("RefreshTokenLifetimeDays", StringComparison.Ordinal));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidateOptions<AuthApplicationOptions>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<RegisterMemberCommand, AuthTokensResponse>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name == "AuthApplicationOptionsRegistrationMarker");
    }

    [Theory]
    [InlineData(null, null, "SigningKey")]
    [InlineData("Auth:Jwt:SigningKey", "short", "SigningKey")]
    [InlineData("Auth:RefreshTokens:Pepper", "short", "Pepper")]
    public void Auth_infrastructure_registration_rejects_invalid_options_before_service_mutation(
        string? setting,
        string? value,
        string expectedFailure)
    {
        ServiceCollection services = new();
        (string Key, string Value)[] values = setting is null || value is null
            ? []
            : CreateValidAuthInfrastructureValues()
                .Select(item => string.Equals(item.Key, setting, StringComparison.Ordinal)
                    ? (item.Key, value)
                    : item)
                .ToArray();
        IConfiguration configuration = CreateConfiguration(values);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddAuthInfrastructure(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidateOptions<JwtSettings>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidateOptions<RefreshTokenHashingOptions>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITokenService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name == "AuthInfrastructureRegistrationMarker");
    }

    [Fact]
    public void Auth_application_registration_rejects_null_arguments()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => Application.DependencyInjection.AddAuthApplication(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddAuthApplication(null!));
        Assert.Throws<ArgumentNullException>(() => Infrastructure.DependencyInjection.AddAuthInfrastructure(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddAuthInfrastructure(null!));
    }

    private static Predicate<ServiceDescriptor> HasService<TService, TImplementation>() =>
        descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation);

    private static (string Key, string Value)[] CreateValidAuthInfrastructureValues() =>
    [
        ("Auth:Jwt:SigningKey", "test-jwt-signing-key-000000000000000000000000"),
        ("Auth:RefreshTokens:Pepper", "test-refresh-token-pepper-000000000000000000000000")
    ];

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        ConfigurationBuilder builder = new();
        builder.AddInMemoryCollection(values.Select(item =>
            new KeyValuePair<string, string?>(item.Key, item.Value)));

        return builder.Build();
    }
}
