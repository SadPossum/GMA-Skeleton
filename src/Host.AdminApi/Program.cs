using Gma.Modules.Administration.AdminApi;
using Gma.Modules.Auth.AdminApi;
using Gma.Modules.Auth.Contracts;
using ServiceDefaults;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.OpenApi;
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
builder.AddAuthAdminApiModule(AuthProfile.TenantScoped());

builder.AddServiceDefaults();
builder.AddGmaOpenApi();
builder.ValidateModuleComposition();

WebApplication app = builder.Build();

app.UseGmaOpenApi();
app.UseGmaSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAdminApiModules();

app.Run();
