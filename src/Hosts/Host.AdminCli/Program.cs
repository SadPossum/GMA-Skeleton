using Gma.Modules.Administration.AdminCli;
using Gma.Modules.AccessControl.AdminCli;
using Gma.Modules.Auth.AdminCli;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using System.CommandLine;
using System.CommandLine.Parsing;

try
{
    HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
        new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

    builder.Services.AddGmaAdministrationCli();
    builder.AddRedisCaching();
    builder.AddCachingCqrs();
    builder.AddGmaInfrastructure();
    builder.AddTenantCaching();
    builder.AddMessagingInfrastructure();
    builder.AddTenantAwareMessaging();
    builder.AddAdminModule<AdministrationAdminCliModule>();
    builder.AddAdminModule<AccessControlAdminCliModule>();
    builder.AddAuthAdminModule(AuthProfile.ScopeAware());
    builder.ValidateModuleComposition();

    using IHost host = builder.Build();

    host.Services.ValidateAdminCliStartup();
    RootCommand rootCommand = host.Services.CreateAdminRootCommand();
    ParseResult parseResult = rootCommand.Parse(args);
    InvocationConfiguration invocation = new()
    {
        EnableDefaultExceptionHandler = false
    };

    return await parseResult.InvokeAsync(invocation, CancellationToken.None).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    AdminCliOutput.WriteError("Admin command was canceled.");
    return AdminExitCodes.Failed;
}
catch (OptionsValidationException exception)
{
    AdminCliOutput.WriteError("Admin CLI configuration is invalid.");

    foreach (string failure in exception.Failures.Distinct(StringComparer.Ordinal))
    {
        AdminCliOutput.WriteError(failure);
    }

    return AdminExitCodes.Failed;
}
catch (ModuleCompositionValidationException exception)
{
    AdminCliOutput.WriteError("Admin CLI module composition is invalid.");

    foreach (string error in exception.Errors)
    {
        AdminCliOutput.WriteError(error);
    }

    return AdminExitCodes.Failed;
}
catch (Exception)
{
    AdminCliOutput.WriteError("Admin command failed unexpectedly.");
    return AdminExitCodes.Failed;
}
