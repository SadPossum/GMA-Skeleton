namespace Architecture.Tests;

using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Infrastructure.JwtBearer;
using Gma.Modules.Auth.Persistence;
using Catalog.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Modules.Notifications.Persistence;
using Ordering.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Api.Security;
using Gma.Framework.Messaging;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class ModuleRegistrationIdempotencyTests
{
    private const string FakeSqlServerConnectionString =
        "Server=localhost;Database=gma_registration_tests;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public async Task Auth_infrastructure_registration_is_idempotent_and_scheme_free()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureFakeAuthInfrastructure(builder);

        builder.Services.AddGmaApiSecurityDefaults();
        builder.Services.AddAuthInfrastructure(builder.Configuration);
        builder.Services.AddAuthInfrastructure(builder.Configuration);

        Assert.Single(builder.Services, HasService<IPasswordHashingService>());
        Assert.Single(builder.Services, HasService<IRefreshTokenHashingService>());
        Assert.Single(builder.Services, HasService<ITokenService>());

        using IHost host = builder.Build();
        IAuthenticationSchemeProvider schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Null(await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme));
    }

    [Fact]
    public async Task Auth_jwt_bearer_registration_is_idempotent()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureFakeAuthInfrastructure(builder);

        builder.AddAuthJwtBearerAuthentication();
        builder.AddAuthJwtBearerAuthentication();

        Assert.Single(builder.Services, HasService<IPasswordHashingService>());
        Assert.Single(builder.Services, HasService<IRefreshTokenHashingService>());
        Assert.Single(builder.Services, HasService<ITokenService>());

        using IHost host = builder.Build();
        IAuthenticationSchemeProvider schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme));
    }

    [Fact]
    public void Module_persistence_registration_is_idempotent()
    {
        AssertPersistenceRegistration(
            builder => builder.AddAuthPersistence(),
            typeof(AuthDbContext),
            [
                (typeof(IUnitOfWork), "AuthUnitOfWork"),
                (typeof(IOutboxWriter), "AuthOutboxWriter"),
                (typeof(IOutboxStore), "AuthOutboxStore")
            ]);
        AssertPersistenceRegistration(
            builder => builder.AddCatalogPersistence(),
            typeof(CatalogDbContext),
            [
                (typeof(IUnitOfWork), "CatalogUnitOfWork"),
                (typeof(IOutboxWriter), "CatalogOutboxWriter"),
                (typeof(IOutboxStore), "CatalogOutboxStore"),
                (typeof(IInboxStore), "CatalogInboxStore")
            ]);
        AssertPersistenceRegistration(
            builder => builder.AddOrderingPersistence(),
            typeof(OrderingDbContext),
            [
                (typeof(IUnitOfWork), "OrderingUnitOfWork"),
                (typeof(IInboxStore), "OrderingInboxStore")
            ]);
        AssertPersistenceRegistration(
            builder => builder.AddAdministrationPersistence(),
            typeof(AdminDbContext),
            [
                (typeof(IUnitOfWork), "AdminUnitOfWork"),
                (typeof(IAdminAuditSink), "AdminAuditSink")
            ]);
        AssertPersistenceRegistration(
            builder => builder.AddNotificationsPersistence(),
            typeof(NotificationsDbContext),
            [
                (typeof(IUnitOfWork), "NotificationsUnitOfWork"),
                (typeof(Gma.Framework.Notifications.IUserNotificationHistoryWriter), "NotificationHistoryWriter"),
                (typeof(IInboxStore), "NotificationsInboxStore")
            ]);
    }

    [Fact]
    public void Shared_persistence_options_registration_is_idempotent_across_modules()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureFakePersistence(builder);

        builder.AddAuthPersistence();
        builder.AddCatalogPersistence();
        builder.AddOrderingPersistence();
        builder.AddAdministrationPersistence();
        builder.AddNotificationsPersistence();

        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IValidateOptions<PersistenceOptions>) &&
            string.Equals(descriptor.ImplementationType?.Name, "PersistenceOptionsValidator", StringComparison.Ordinal));
    }

    private static void AssertPersistenceRegistration(
        Action<HostApplicationBuilder> register,
        Type dbContextType,
        IReadOnlyCollection<(Type ServiceType, string ImplementationTypeName)> expectedEnumerableServices)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureFakePersistence(builder);

        register(builder);
        register(builder);

        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == dbContextType);
        foreach ((Type serviceType, string implementationTypeName) in expectedEnumerableServices)
        {
            Assert.Single(builder.Services, descriptor =>
                descriptor.ServiceType == serviceType &&
                string.Equals(descriptor.ImplementationType?.Name, implementationTypeName, StringComparison.Ordinal));
        }
    }

    private static Predicate<ServiceDescriptor> HasService<TService>() =>
        descriptor => descriptor.ServiceType == typeof(TService);

    private static void ConfigureFakePersistence(HostApplicationBuilder builder)
    {
        builder.Configuration["Persistence:Provider"] = "SqlServer";
        builder.Configuration["ConnectionStrings:SqlServer"] = FakeSqlServerConnectionString;
    }

    private static void ConfigureFakeAuthInfrastructure(HostApplicationBuilder builder)
    {
        builder.Configuration["Auth:Jwt:SigningKey"] = "test-jwt-signing-key-000000000000000000000000";
        builder.Configuration["Auth:RefreshTokens:Pepper"] = "test-refresh-token-pepper-000000000000000000000000";
    }
}
