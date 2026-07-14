using Gma.Modules.Administration.AdminApi;
using Gma.Modules.AccessControl.AdminApi;
using Gma.Modules.Auth.AdminApi;
using Gma.Modules.Organizations.AdminApi;
using Gma.Modules.Auth.Contracts;
using ServiceDefaults;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.OpenApi;
using Gma.Framework.Api.Production;
using Gma.Framework.Api.Production.EntityFrameworkCore;
using Gma.Framework.Api.Security;
using Gma.Framework.Api.Serilog;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Infrastructure;
using Gma.Framework.Logging.Serilog;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging.Nats.Aspire;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy.Api.Serilog;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Organizations.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseConfiguredSerilog();

builder.Services.AddGmaAdministrationApi(builder.Configuration);
builder.AddRedisCaching();
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddTenantSerilogRequestLogging();
builder.AddTenantCaching();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging();
builder.AddConfiguredNatsJetStreamMessaging();
builder.Services.AddApiSecurityDefaults();

builder.AddAdminApiModule<AdministrationAdminApiModule>();
builder.AddAdminApiModule<AccessControlAdminApiModule>();
builder.AddAuthAdminApiModule(AuthProfile.Global());
builder.AddAdminApiModule<OrganizationsAdminApiModule>();

builder.AddServiceDefaults();
builder.AddGmaProductionHttp();
builder.Services.AddGmaEntityFrameworkReadinessCheck<AdminDbContext>("administration-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AccessControlDbContext>("access-control-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AuthDbContext>("auth-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<OrganizationsDbContext>("organizations-database");
builder.AddGmaOpenApi();
builder.ValidateModuleComposition();

WebApplication app = builder.Build();

app.UseGmaOpenApi();
app.UseGmaProductionHttp();
app.UseGmaSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAdminApiModules();

app.Run();
