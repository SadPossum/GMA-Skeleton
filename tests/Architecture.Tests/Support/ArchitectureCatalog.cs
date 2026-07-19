namespace Architecture.Tests;

using System.Reflection;
using Catalog.Admin.Contracts;
using Catalog.AdminApi;
using Catalog.AdminCli;
using Catalog.Api;
using Catalog.Contracts;
using Catalog.Domain.Aggregates;
using Catalog.Persistence;
using Gma.Framework.Modules;
using Gma.Modules.AccessControl.Admin.Contracts;
using Gma.Modules.AccessControl.AdminApi;
using Gma.Modules.AccessControl.AdminCli;
using Gma.Modules.AccessControl.Api;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.AccessControl.Domain.Aggregates;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.AdminApi;
using Gma.Modules.Administration.AdminCli;
using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Contracts;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.Admin.Contracts;
using Gma.Modules.Auth.AdminApi;
using Gma.Modules.Auth.AdminCli;
using Gma.Modules.Auth.Api;
using Gma.Modules.Auth.Authenticators.Totp;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Infrastructure.JwtBearer;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Auth.Providers.OpenIdConnect;
using Gma.Modules.Files.Api;
using Gma.Modules.Files.Application;
using Gma.Modules.Files.Contracts;
using Gma.Modules.Notifications.Adapters.Email;
using Gma.Modules.Notifications.Admin.Contracts;
using Gma.Modules.Notifications.AdminApi;
using Gma.Modules.Notifications.Api;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Persistence;
using Gma.Modules.TaskRuntime.Admin.Contracts;
using Gma.Modules.TaskRuntime.AdminApi;
using Gma.Modules.TaskRuntime.AdminCli;
using Gma.Modules.TaskRuntime.Contracts;
using Gma.Modules.TaskRuntime.Persistence;
using Gma.Modules.Tenancy.Api;
using Gma.Modules.Tenancy.Contracts;
using Host.AdminApi;
using Host.AdminCli;
using Ordering.Api;
using Ordering.Contracts;
using Ordering.Domain.Aggregates;
using Ordering.Persistence;
using TaskSamples.Application;
using TaskSamples.Contracts;

internal static class ArchitectureCatalog
{
    public static IReadOnlyList<ModuleProject> ModuleProjects { get; } =
    [
        new("AccessControl", "Gma.Modules.AccessControl.Admin.Contracts", ModuleProjectKind.AdminContracts, typeof(AccessControlAdminPermissions).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.AdminCli", ModuleProjectKind.AdminCli, typeof(AccessControlAdminCliModule).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.AdminApi", ModuleProjectKind.AdminApi, typeof(AccessControlAdminApiModule).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.Api", ModuleProjectKind.Api, typeof(AccessControlApiModule).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.Application", ModuleProjectKind.Application, typeof(Gma.Modules.AccessControl.Application.DependencyInjection).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.Contracts", ModuleProjectKind.Contracts, typeof(AccessControlModuleMetadata).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.Domain", ModuleProjectKind.Domain, typeof(AccessProfile).Assembly),
        new("AccessControl", "Gma.Modules.AccessControl.Persistence", ModuleProjectKind.Persistence, typeof(Gma.Modules.AccessControl.Persistence.DependencyInjection).Assembly),

        new("Administration", "Gma.Modules.Administration.AdminCli", ModuleProjectKind.AdminCli, typeof(AdministrationAdminCliModule).Assembly),
        new("Administration", "Gma.Modules.Administration.AdminApi", ModuleProjectKind.AdminApi, typeof(AdministrationAdminApiModule).Assembly),
        new("Administration", "Gma.Modules.Administration.Application", ModuleProjectKind.Application, typeof(Gma.Modules.Administration.Application.DependencyInjection).Assembly),
        new("Administration", "Gma.Modules.Administration.Contracts", ModuleProjectKind.Contracts, typeof(AdministrationModuleMetadata).Assembly),
        new("Administration", "Gma.Modules.Administration.Persistence", ModuleProjectKind.Persistence, typeof(Gma.Modules.Administration.Persistence.DependencyInjection).Assembly),

        new("Auth", "Gma.Modules.Auth.Admin.Contracts", ModuleProjectKind.AdminContracts, typeof(AuthAdminPermissions).Assembly),
        new("Auth", "Gma.Modules.Auth.AdminCli", ModuleProjectKind.AdminCli, typeof(AuthAdminCliModule).Assembly),
        new("Auth", "Gma.Modules.Auth.AdminApi", ModuleProjectKind.AdminApi, typeof(AuthAdminApiModule).Assembly),
        new("Auth", "Gma.Modules.Auth.Api", ModuleProjectKind.Api, typeof(AuthModule).Assembly),
        new("Auth", "Gma.Modules.Auth.Application", ModuleProjectKind.Application, typeof(Gma.Modules.Auth.Application.DependencyInjection).Assembly),
        new("Auth", "Gma.Modules.Auth.Authenticators.Totp", ModuleProjectKind.Infrastructure, typeof(Gma.Modules.Auth.Authenticators.Totp.DependencyInjection).Assembly),
        new("Auth", "Gma.Modules.Auth.Contracts", ModuleProjectKind.Contracts, typeof(AuthModuleMetadata).Assembly),
        new("Auth", "Gma.Modules.Auth.Domain", ModuleProjectKind.Domain, typeof(Member).Assembly),
        new("Auth", "Gma.Modules.Auth.Infrastructure", ModuleProjectKind.Infrastructure, typeof(Gma.Modules.Auth.Infrastructure.DependencyInjection).Assembly),
        new("Auth", "Gma.Modules.Auth.Infrastructure.JwtBearer", ModuleProjectKind.Infrastructure, typeof(Gma.Modules.Auth.Infrastructure.JwtBearer.DependencyInjection).Assembly),
        new("Auth", "Gma.Modules.Auth.Providers.OpenIdConnect", ModuleProjectKind.Infrastructure, typeof(Gma.Modules.Auth.Providers.OpenIdConnect.DependencyInjection).Assembly),
        new("Auth", "Gma.Modules.Auth.Persistence", ModuleProjectKind.Persistence, typeof(Gma.Modules.Auth.Persistence.DependencyInjection).Assembly),

        new("Catalog", "Catalog.Admin.Contracts", ModuleProjectKind.AdminContracts, typeof(CatalogAdminPermissions).Assembly),
        new("Catalog", "Catalog.AdminCli", ModuleProjectKind.AdminCli, typeof(CatalogAdminCliModule).Assembly),
        new("Catalog", "Catalog.AdminApi", ModuleProjectKind.AdminApi, typeof(CatalogAdminApiModule).Assembly),
        new("Catalog", "Catalog.Api", ModuleProjectKind.Api, typeof(CatalogModule).Assembly),
        new("Catalog", "Catalog.Application", ModuleProjectKind.Application, typeof(Catalog.Application.DependencyInjection).Assembly),
        new("Catalog", "Catalog.Contracts", ModuleProjectKind.Contracts, typeof(CatalogModuleMetadata).Assembly),
        new("Catalog", "Catalog.Domain", ModuleProjectKind.Domain, typeof(CatalogItem).Assembly),
        new("Catalog", "Catalog.Persistence", ModuleProjectKind.Persistence, typeof(Catalog.Persistence.DependencyInjection).Assembly),

        new("Files", "Gma.Modules.Files.Api", ModuleProjectKind.Api, typeof(FilesModule).Assembly),
        new("Files", "Gma.Modules.Files.Application", ModuleProjectKind.Application, typeof(Gma.Modules.Files.Application.DependencyInjection).Assembly),
        new("Files", "Gma.Modules.Files.Contracts", ModuleProjectKind.Contracts, typeof(FilesModuleMetadata).Assembly),

        new("Notifications", "Gma.Modules.Notifications.Api", ModuleProjectKind.Api, typeof(NotificationsModule).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Admin.Contracts", ModuleProjectKind.AdminContracts, typeof(NotificationsAdminPermissions).Assembly),
        new("Notifications", "Gma.Modules.Notifications.AdminApi", ModuleProjectKind.AdminApi, typeof(NotificationsAdminApiModule).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Adapters.Email", ModuleProjectKind.Infrastructure, typeof(Gma.Modules.Notifications.Adapters.Email.DependencyInjection).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Application", ModuleProjectKind.Application, typeof(Gma.Modules.Notifications.Application.DependencyInjection).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Contracts", ModuleProjectKind.Contracts, typeof(NotificationsModuleMetadata).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Domain", ModuleProjectKind.Domain, typeof(UserNotification).Assembly),
        new("Notifications", "Gma.Modules.Notifications.Persistence", ModuleProjectKind.Persistence, typeof(Gma.Modules.Notifications.Persistence.DependencyInjection).Assembly),

        new("Ordering", "Ordering.Api", ModuleProjectKind.Api, typeof(OrderingModule).Assembly),
        new("Ordering", "Ordering.Application", ModuleProjectKind.Application, typeof(Ordering.Application.DependencyInjection).Assembly),
        new("Ordering", "Ordering.Contracts", ModuleProjectKind.Contracts, typeof(OrderingModuleMetadata).Assembly),
        new("Ordering", "Ordering.Domain", ModuleProjectKind.Domain, typeof(Order).Assembly),
        new("Ordering", "Ordering.Persistence", ModuleProjectKind.Persistence, typeof(Ordering.Persistence.DependencyInjection).Assembly),

        new("TaskRuntime", "Gma.Modules.TaskRuntime.Admin.Contracts", ModuleProjectKind.AdminContracts, typeof(TaskRuntimeAdminPermissions).Assembly),
        new("TaskRuntime", "Gma.Modules.TaskRuntime.AdminCli", ModuleProjectKind.AdminCli, typeof(TaskRuntimeAdminCliModule).Assembly),
        new("TaskRuntime", "Gma.Modules.TaskRuntime.AdminApi", ModuleProjectKind.AdminApi, typeof(TaskRuntimeAdminApiModule).Assembly),
        new("TaskRuntime", "Gma.Modules.TaskRuntime.Application", ModuleProjectKind.Application, typeof(Gma.Modules.TaskRuntime.Application.DependencyInjection).Assembly),
        new("TaskRuntime", "Gma.Modules.TaskRuntime.Contracts", ModuleProjectKind.Contracts, typeof(TaskRuntimeModuleMetadata).Assembly),
        new("TaskRuntime", "Gma.Modules.TaskRuntime.Persistence", ModuleProjectKind.Persistence, typeof(Gma.Modules.TaskRuntime.Persistence.DependencyInjection).Assembly),

        new("TaskSamples", "TaskSamples.Application", ModuleProjectKind.Application, typeof(TaskSamples.Application.DependencyInjection).Assembly),
        new("TaskSamples", "TaskSamples.Contracts", ModuleProjectKind.Contracts, typeof(TaskSamplesModuleMetadata).Assembly),

        new("Tenancy", "Gma.Modules.Tenancy.Api", ModuleProjectKind.Api, typeof(TenancyModule).Assembly),
        new("Tenancy", "Gma.Modules.Tenancy.Contracts", ModuleProjectKind.Contracts, typeof(TenancyModuleMetadata).Assembly),
    ];

    public static IReadOnlyList<ModuleDescriptor> ModuleDescriptors { get; } =
    [
        AccessControlModuleMetadata.Descriptor,
        AuthModuleMetadata.Descriptor,
        AdministrationModuleMetadata.Descriptor,
        CatalogModuleMetadata.Descriptor,
        FilesModuleMetadata.Descriptor,
        NotificationsModuleMetadata.Descriptor,
        OrderingModuleMetadata.Descriptor,
        TaskRuntimeModuleMetadata.Descriptor,
        TaskSamplesModuleMetadata.Descriptor,
        TenancyModuleMetadata.Descriptor,
    ];

    public static IReadOnlyList<string> ModulePrefixes { get; } = ModuleProjects
        .Select(project => project.ModulePrefix)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<Assembly> ModuleBoundaryAssemblies { get; } = ModuleProjects
        .Select(project => project.Assembly)
        .Distinct()
        .ToArray();

    public static IReadOnlyList<Assembly> ApplicationAssemblies { get; } = ModuleProjects
        .Where(project => project.Kind == ModuleProjectKind.Application)
        .Select(project => project.Assembly)
        .Distinct()
        .ToArray();

    public static IReadOnlyList<Assembly> OrderingAssemblies { get; } = ModuleProjects
        .Where(project => string.Equals(project.ModulePrefix, "Ordering", StringComparison.Ordinal))
        .Select(project => project.Assembly)
        .Distinct()
        .ToArray();

    public static IReadOnlyList<Assembly> CommandLineAllowedAssemblies { get; } =
    [
        typeof(AccessControlAdminCliModule).Assembly,
        typeof(AdministrationAdminCliModule).Assembly,
        typeof(AuthAdminCliModule).Assembly,
        typeof(CatalogAdminCliModule).Assembly,
        typeof(TaskRuntimeAdminCliModule).Assembly,
        typeof(Gma.Framework.Administration.Cli.AdminCliExecutor).Assembly,
        AdminCliAssemblyReference.Assembly,
    ];

    public static IReadOnlyList<Assembly> CommandLineCheckedAssemblies { get; } = ModuleBoundaryAssemblies
        .Concat(
        [
            typeof(Gma.Framework.Administration.Cli.AdminCliExecutor).Assembly,
            typeof(Gma.Framework.Administration.Api.AdminApiExecutor).Assembly,
            AdminApiAssemblyReference.Assembly,
        ])
        .Distinct()
        .ToArray();
}

internal sealed record ModuleProject(
    string ModulePrefix,
    string ProjectName,
    ModuleProjectKind Kind,
    Assembly Assembly);

internal enum ModuleProjectKind
{
    AdminCli,
    AdminContracts,
    AdminApi,
    Api,
    Application,
    Contracts,
    Domain,
    Infrastructure,
    Persistence,
}
