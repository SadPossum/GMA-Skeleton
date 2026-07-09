using Gma.Modules.Auth.Api;
using Gma.Modules.Auth.Contracts;
using ServiceDefaults;
using Gma.Framework.Api.Modules;
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
using Gma.Framework.Notifications.Api;
using Gma.Framework.Notifications.Cqrs;
using Gma.Framework.Notifications.SignalR;
using Gma.Framework.Realtime.Notifications;
using Gma.Framework.Tenancy.Api.Serilog;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Gma.Modules.Tenancy.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseConfiguredSerilog();

builder.AddUserNotificationsCqrs();
builder.AddUserNotificationsRealtime();
builder.AddRedisCaching();
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddTenantSerilogRequestLogging();
builder.AddTenantCaching();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging();
builder.AddConfiguredNatsJetStreamMessaging();
builder.AddUserNotificationServerSentEvents();
builder.AddUserNotificationSignalR();
builder.Services.AddApiSecurityDefaults();

builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.TenantScoped());
// module-scaffold:public-api-modules

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
app.MapModules();
app.MapUserNotificationServerSentEvents();
app.MapUserNotificationSignalR();

app.Run();
