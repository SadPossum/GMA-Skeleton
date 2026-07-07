namespace Gma.Modules.Administration.Tests;

using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Handlers;
using Gma.Modules.Administration.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdministrationApplicationRegistrationTests
{
    [Fact]
    public void Administration_application_registration_is_idempotent()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddAdministrationApplication(configuration);
        services.AddAdministrationApplication(configuration);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<AdministrationOptions>));
        Assert.Single(services, HasService<IValidateOptions<AdministrationOptions>, AdministrationOptionsValidator>());
        Assert.Single(services, HasService<ICommandHandler<BootstrapOwnerCommand, Unit>, BootstrapOwnerCommandHandler>());
        Assert.Single(services, HasService<ICommandValidator<BootstrapOwnerCommand>, BootstrapOwnerCommandValidator>());
        Assert.Single(services, HasService<ICommandValidator<CreateRoleCommand>, CreateRoleCommandValidator>());
        Assert.Single(services, HasService<ICommandValidator<GrantRolePermissionCommand>, GrantRolePermissionCommandValidator>());
        Assert.Single(services, HasService<ICommandValidator<AssignRoleCommand>, AssignRoleCommandValidator>());
        Assert.Single(services, HasService<IAdminAuthorizationService, PersistedAdminAuthorizationService>());
        Assert.Single(services, descriptor => descriptor.ServiceType.Name == "AdministrationOptionsRegistrationMarker");
    }

    [Fact]
    public void Administration_application_registration_rejects_invalid_options_before_service_mutation()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(
            ("Administration:Bootstrap:OwnerRoleName", "Owner"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddAdministrationApplication(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains("OwnerRoleName", StringComparison.Ordinal));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidateOptions<AdministrationOptions>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICommandHandler<BootstrapOwnerCommand, Unit>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name == "AdministrationOptionsRegistrationMarker");
    }

    [Fact]
    public void Administration_application_registration_rejects_null_arguments()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() =>
            DependencyInjection.AddAdministrationApplication(null!, configuration));
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAdministrationApplication(null!));
    }

    private static Predicate<ServiceDescriptor> HasService<TService, TImplementation>() =>
        descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation);

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        ConfigurationBuilder builder = new();
        builder.AddInMemoryCollection(values.Select(item =>
            new KeyValuePair<string, string?>(item.Key, item.Value)));

        return builder.Build();
    }
}
