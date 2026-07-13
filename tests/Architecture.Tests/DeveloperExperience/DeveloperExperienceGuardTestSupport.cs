namespace Architecture.Tests;

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Catalog.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;
using Gma.Framework.Observability;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Notifications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ordering.Persistence;
using Xunit;

[Trait("Category", "Architecture")]
public sealed partial class DeveloperExperienceGuardTests
{
    private static bool ImplementsOpenGeneric(Type type, Type openGenericInterface) =>
        type.GetInterfaces()
            .Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == openGenericInterface);

    private static string ModuleNameFromAssembly(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        ModuleProject? project = ArchitectureCatalog.ModuleProjects
            .FirstOrDefault(item => ReferenceEquals(item.Assembly, assembly) || item.Assembly == assembly);
        if (project is not null)
        {
            return ResolveModuleName(project.ModulePrefix);
        }

        string[] segments = assemblyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string modulePrefix = segments is ["Gma", "Modules", { } reusableModule, ..]
            ? reusableModule
            : segments.FirstOrDefault() ?? assemblyName;

        return ResolveModuleName(modulePrefix);
    }

    private static string ResolveAdminSurfaceName(string modulePrefix)
    {
        Type[] moduleTypes = ArchitectureCatalog.ModuleProjects
            .Where(project => string.Equals(project.ModulePrefix, modulePrefix, StringComparison.Ordinal))
            .SelectMany(project => project.Assembly.GetTypes())
            .ToArray();

        string? adminSurfaceName = moduleTypes
            .Select(type => type.GetField("AdminSurfaceName", BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => field!.GetRawConstantValue() as string)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(adminSurfaceName))
        {
            return adminSurfaceName;
        }

        string? moduleName = moduleTypes
            .Where(type => type.Name is { } name &&
                           (name.EndsWith("ModuleMetadata", StringComparison.Ordinal) ||
                            name.EndsWith("ModuleIdentity", StringComparison.Ordinal)))
            .Select(type => type.GetField("Name", BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => field!.GetRawConstantValue() as string)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return moduleName ?? ToModuleName(modulePrefix);
    }

    private static string ResolveModuleName(string modulePrefix)
    {
        Type[] moduleTypes = ArchitectureCatalog.ModuleProjects
            .Where(project => string.Equals(project.ModulePrefix, modulePrefix, StringComparison.Ordinal))
            .SelectMany(project => project.Assembly.GetTypes())
            .ToArray();

        string? moduleName = moduleTypes
            .Where(type => type.Name is { } name &&
                           (name.EndsWith("ModuleMetadata", StringComparison.Ordinal) ||
                            name.EndsWith("ModuleIdentity", StringComparison.Ordinal)))
            .Select(type => type.GetField("Name", BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => field!.GetRawConstantValue() as string)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return moduleName ?? ToModuleName(modulePrefix);
    }

    private static string ToModuleName(string projectPrefix)
    {
        string withAcronymBoundaries = AcronymBoundaryPattern().Replace(projectPrefix, "$1-$2");
        return WordBoundaryPattern().Replace(withAcronymBoundaries, "$1-$2").ToLowerInvariant();
    }

    private static bool PersistenceProjectDeclaresMatchingUnitOfWorkModuleName(
        string moduleDirectory,
        string persistenceProjectDirectory,
        string moduleName)
    {
        string literalToken = $"ModuleName => \"{moduleName}\"";
        Dictionary<string, string> moduleIdentityConstants = EnumerateSourceFiles(moduleDirectory)
            .SelectMany(path => ModuleIdentityConstantPattern()
                .Matches(File.ReadAllText(path))
                .Select(match => new
                {
                    TypeName = match.Groups["type"].Value,
                    match.Groups["value"].Value,
                }))
            .GroupBy(item => item.TypeName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.Ordinal);
        HashSet<string> persistenceSchemaConstantTypes = EnumerateSourceFiles(persistenceProjectDirectory)
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path => SchemaConstantTypePattern()
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups["type"].Value))
            .ToHashSet(StringComparer.Ordinal);

        return EnumerateSourceFiles(persistenceProjectDirectory)
            .Where(path => !IsGeneratedMigrationSource(path))
            .Any(path =>
            {
                string source = File.ReadAllText(path);

                bool UsesEfDomainEventUnitOfWorkWith(string moduleNameExpression) =>
                    source.Contains("EfDomainEventUnitOfWork<", StringComparison.Ordinal) &&
                    source.Contains($"({moduleNameExpression},", StringComparison.Ordinal);

                bool UsesModuleIdentityConstant(KeyValuePair<string, string> item) =>
                    string.Equals(item.Value, moduleName, StringComparison.Ordinal) &&
                    (source.Contains($"ModuleName => {item.Key}.Name", StringComparison.Ordinal) ||
                     source.Contains($"ModuleName => {item.Key}.Schema", StringComparison.Ordinal) ||
                     UsesEfDomainEventUnitOfWorkWith($"{item.Key}.Name") ||
                     UsesEfDomainEventUnitOfWorkWith($"{item.Key}.Schema"));

                bool UsesPersistenceSchemaConstant(string typeName) =>
                    source.Contains($"ModuleName => {typeName}.Schema", StringComparison.Ordinal) ||
                    UsesEfDomainEventUnitOfWorkWith($"{typeName}.Schema");

                return source.Contains(literalToken, StringComparison.Ordinal) ||
                       moduleIdentityConstants.Any(UsesModuleIdentityConstant) ||
                       persistenceSchemaConstantTypes.Any(UsesPersistenceSchemaConstant);
            });
    }

    private static IEnumerable<string> GetMisalignedNamespaces(string repositoryRoot, string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);
        string? projectName = FindOwningProjectName(sourcePath);

        if (projectName is null)
        {
            yield break;
        }

        foreach (Match match in NamespacePattern().Matches(source))
        {
            string namespaceName = match.Groups["name"].Value;

            if (!namespaceName.StartsWith(projectName, StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, sourcePath)}::{namespaceName} expected {projectName}*";
            }
        }
    }

    private static IEnumerable<ProjectPackageReference> EnumeratePackageReferences(string repositoryRoot)
    {
        string sourceRoot = Path.Combine(repositoryRoot, "src");

        return EnumerateProjectFiles(sourceRoot)
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = CanonicalRelativePath(repositoryRoot, projectPath);

                return project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => new ProjectPackageReference(relativePath, packageId!));
            });
    }

    private static IEnumerable<ProjectReference> EnumerateProjectReferences(string repositoryRoot)
    {
        string sourceRoot = Path.Combine(repositoryRoot, "src");

        return EnumerateProjectFiles(sourceRoot)
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = CanonicalRelativePath(repositoryRoot, projectPath);

                return project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(referencePath => !string.IsNullOrWhiteSpace(referencePath))
                    .Select(referencePath => new ProjectReference(relativePath, NormalizePath(referencePath!)));
            });
    }

    private static bool IsCliProject(string projectPath)
    {
        string normalizedPath = NormalizePath(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(normalizedPath);

        return string.Equals(projectName, "Host.AdminCli", StringComparison.Ordinal) ||
               string.Equals(projectName, "Gma.Framework.Administration.Cli", StringComparison.Ordinal) ||
               (IsModuleProject(normalizedPath) &&
                projectName.EndsWith(".AdminCli", StringComparison.Ordinal));
    }

    private static bool IsModuleProject(string projectPath) =>
        NormalizePath(projectPath).StartsWith(
            $"src{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) &&
        !IsTestProjectPath(projectPath);

    private static bool IsProductionProjectPath(string projectPath) =>
        NormalizePath(projectPath).StartsWith(
            $"src{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) &&
        !IsTestProjectPath(projectPath);

    private static bool IsTestProjectPath(string projectPath)
    {
        string normalizedPath = NormalizePath(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(normalizedPath);

        return normalizedPath.StartsWith(
                   $"tests{Path.DirectorySeparatorChar}",
                   StringComparison.OrdinalIgnoreCase) ||
               projectName.EndsWith(".Tests", StringComparison.Ordinal);
    }

    private static bool IsTestSourcePath(string sourcePath) =>
        FindOwningProjectName(sourcePath)?.EndsWith(".Tests", StringComparison.Ordinal) == true;

    private static bool IsPublicModuleContractsProject(string projectPath)
    {
        string normalizedPath = NormalizePath(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(normalizedPath);

        return IsModuleProject(normalizedPath) &&
               projectName.EndsWith(".Contracts", StringComparison.Ordinal) &&
               !projectName.EndsWith(".Admin.Contracts", StringComparison.Ordinal);
    }

    private static bool IsAllowedApplicationProjectReference(string moduleName, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);
        HashSet<string> allowedSharedReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\..\..\Framework\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Application.Composition\Gma.Framework.Application.Composition.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Caching\Gma.Framework.Caching.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Domain\Gma.Framework.Domain.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.FileManagement\Gma.Framework.FileManagement.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Results\Gma.Framework.Results.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Pagination\Gma.Framework.Pagination.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ProjectionRebuild\Gma.Framework.ProjectionRebuild.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ProjectionRebuild.Tasks\Gma.Framework.ProjectionRebuild.Tasks.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks.Cqrs\Gma.Framework.Tasks.Cqrs.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj")
        };

        return allowedSharedReferences.Contains(normalizedReference) ||
               (IsLogicalModuleName(moduleName, "Administration") &&
                string.Equals(
                    normalizedReference,
                    NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration\Gma.Framework.Administration.csproj"),
                    StringComparison.OrdinalIgnoreCase)) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Contracts\{moduleName}.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Domain\{moduleName}.Domain.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               IsOtherModuleContractsReference(moduleName, normalizedReference);
    }

    private static bool IsAllowedHttpFrontDoorProjectReference(string moduleName, bool isAdminApi, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);
        HashSet<string> allowedSharedReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\..\..\Framework\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Api\Gma.Framework.Api.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Pagination\Gma.Framework.Pagination.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Results\Gma.Framework.Results.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Security\Gma.Framework.Security.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj")
        };

        if (isAdminApi)
        {
            allowedSharedReferences.Add(NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration.Api\Gma.Framework.Administration.Api.csproj"));
            allowedSharedReferences.Add(NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration\Gma.Framework.Administration.csproj"));
        }

        return allowedSharedReferences.Contains(normalizedReference) ||
               (isAdminApi &&
                string.Equals(
                    normalizedReference,
                    NormalizePath($@"..\{moduleName}.Admin.Contracts\{moduleName}.Admin.Contracts.csproj"),
                    StringComparison.OrdinalIgnoreCase)) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Contracts\{moduleName}.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Application\{moduleName}.Application.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Persistence\{moduleName}.Persistence.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Infrastructure\{moduleName}.Infrastructure.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Infrastructure.JwtBearer\{moduleName}.Infrastructure.JwtBearer.csproj"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IEntityType[] CreateTenantConventionModelEntityTypes()
    {
        using AuthDbContext auth = new(
            CreateTenantConventionOptions<AuthDbContext>(),
            new DesignTimeScopeContext());
        using CatalogDbContext catalog = new(
            CreateTenantConventionOptions<CatalogDbContext>(),
            new DesignTimeScopeContext());
        using OrderingDbContext ordering = new(
            CreateTenantConventionOptions<OrderingDbContext>(),
            new DesignTimeScopeContext());
        using NotificationsDbContext notifications = new(
            CreateTenantConventionOptions<NotificationsDbContext>(),
            new DesignTimeScopeContext());

        return auth.Model.GetEntityTypes()
            .Concat(catalog.Model.GetEntityTypes())
            .Concat(ordering.Model.GetEntityTypes())
            .Concat(notifications.Model.GetEntityTypes())
            .Where(entityType => !entityType.IsOwned())
            .ToArray();
    }

    private static DbContextOptions<TContext> CreateTenantConventionOptions<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TenantConventionArchitectureTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

    private static bool IsAllowedAdminCliProjectReference(string moduleName, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);
        HashSet<string> allowedSharedReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration.Cli\Gma.Framework.Administration.Cli.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration\Gma.Framework.Administration.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Pagination\Gma.Framework.Pagination.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Results\Gma.Framework.Results.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj")
        };

        return allowedSharedReferences.Contains(normalizedReference) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Admin.Contracts\{moduleName}.Admin.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Contracts\{moduleName}.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Application\{moduleName}.Application.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Persistence\{moduleName}.Persistence.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Infrastructure\{moduleName}.Infrastructure.csproj"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedPublicContractsProjectReference(string moduleName, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);

        return string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Caching\Gma.Framework.Caching.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.FileManagement\Gma.Framework.FileManagement.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Modules\Gma.Framework.Modules.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.ProjectionRebuild\Gma.Framework.ProjectionRebuild.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy.Messaging\Gma.Framework.Tenancy.Messaging.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               IsOtherModuleContractsReference(moduleName, normalizedReference);
    }

    private static bool IsAllowedAdminContractsProjectReference(string moduleName, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);

        return string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Contracts\{moduleName}.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration\Gma.Framework.Administration.csproj"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedPersistenceProjectReference(string moduleName, string referencePath)
    {
        string normalizedReference = NormalizePath(referencePath);
        HashSet<string> allowedSharedReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\..\..\Framework\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Domain\Gma.Framework.Domain.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Pagination\Gma.Framework.Pagination.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Persistence.EntityFrameworkCore\Gma.Framework.Persistence.EntityFrameworkCore.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ProjectionRebuild.EntityFrameworkCore\Gma.Framework.ProjectionRebuild.EntityFrameworkCore.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.ProjectionRebuild\Gma.Framework.ProjectionRebuild.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Results\Gma.Framework.Results.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tasks.Infrastructure\Gma.Framework.Tasks.Infrastructure.csproj"),
            NormalizePath(@"..\..\..\Framework\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj")
        };

        return allowedSharedReferences.Contains(normalizedReference) ||
               (IsLogicalModuleName(moduleName, "Administration") &&
                string.Equals(
                    normalizedReference,
                    NormalizePath(@"..\..\..\Framework\Gma.Framework.Administration\Gma.Framework.Administration.csproj"),
                    StringComparison.OrdinalIgnoreCase)) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Contracts\{moduleName}.Contracts.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Application\{moduleName}.Application.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedReference,
                   NormalizePath($@"..\{moduleName}.Domain\{moduleName}.Domain.csproj"),
                   StringComparison.OrdinalIgnoreCase) ||
               IsOtherModuleContractsReference(moduleName, normalizedReference);
    }

    private static bool IsOtherModuleContractsReference(string moduleName, string normalizedReference)
    {
        if (IsSourceRootOtherModuleContractsReference(moduleName, normalizedReference))
        {
            return true;
        }

        const string Prefix = "..";
        string[] segments = normalizedReference.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string targetProjectName = segments.Length == 5 ? segments[3] : string.Empty;
        string expectedExampleContractsProject = segments.Length == 5 ? $"{segments[2]}.Contracts" : string.Empty;
        string expectedReusableContractsProject = segments.Length == 5 ? $"Gma.Modules.{segments[2]}.Contracts" : string.Empty;

        return segments.Length == 5 &&
               string.Equals(segments[0], Prefix, StringComparison.Ordinal) &&
               string.Equals(segments[1], Prefix, StringComparison.Ordinal) &&
               !string.Equals(segments[2], LogicalModuleNameFromProjectName(moduleName), StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(targetProjectName, expectedExampleContractsProject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetProjectName, expectedReusableContractsProject, StringComparison.OrdinalIgnoreCase)) &&
               string.Equals(segments[4], $"{targetProjectName}.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceRootOtherModuleContractsReference(string moduleName, string normalizedReference)
    {
        const string PropertyPrefix = "$(GmaModule";
        const string PropertySuffix = "Root)";
        const string NormalizedPrefix = "$GmaModule";
        const string NormalizedSuffix = "Root";

        string targetModuleName;
        string remainder;

        if (normalizedReference.StartsWith(PropertyPrefix, StringComparison.Ordinal) &&
            normalizedReference.Contains(PropertySuffix, StringComparison.Ordinal))
        {
            int moduleNameStart = PropertyPrefix.Length;
            int moduleNameEnd = normalizedReference.IndexOf(PropertySuffix, StringComparison.Ordinal);
            targetModuleName = normalizedReference[moduleNameStart..moduleNameEnd];
            remainder = normalizedReference[(moduleNameEnd + PropertySuffix.Length)..]
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else if (normalizedReference.StartsWith(NormalizedPrefix, StringComparison.Ordinal))
        {
            int moduleNameStart = NormalizedPrefix.Length;
            int moduleNameEnd = normalizedReference.IndexOf(NormalizedSuffix, moduleNameStart, StringComparison.Ordinal);
            if (moduleNameEnd <= moduleNameStart)
            {
                return false;
            }

            targetModuleName = normalizedReference[moduleNameStart..moduleNameEnd];
            remainder = normalizedReference[(moduleNameEnd + NormalizedSuffix.Length)..]
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else
        {
            return false;
        }

        string[] segments = remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        string targetProjectName = segments.Length == 2 ? segments[0] : string.Empty;
        string expectedExampleContractsProject = $"{targetModuleName}.Contracts";
        string expectedReusableContractsProject = $"Gma.Modules.{targetModuleName}.Contracts";

        return segments.Length == 2 &&
               !string.Equals(targetModuleName, LogicalModuleNameFromProjectName(moduleName), StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(targetProjectName, expectedExampleContractsProject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetProjectName, expectedReusableContractsProject, StringComparison.OrdinalIgnoreCase)) &&
               string.Equals(segments[1], $"{targetProjectName}.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLogicalModuleName(string projectModuleName, string expectedModuleName) =>
        string.Equals(LogicalModuleNameFromProjectName(projectModuleName), expectedModuleName, StringComparison.Ordinal);

    private static string LogicalModuleNameFromProjectName(string projectModuleName)
    {
        string[] segments = projectModuleName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments is ["Gma", "Modules", { } reusableModule, ..]
            ? reusableModule
            : segments.FirstOrDefault() ?? projectModuleName;
    }

    private static bool IsModuleFrontDoorProject(string projectPath)
    {
        string normalizedPath = NormalizePath(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(normalizedPath);

        return IsModuleProject(normalizedPath) &&
               (projectName.EndsWith(".Api", StringComparison.Ordinal) ||
                projectName.EndsWith(".AdminApi", StringComparison.Ordinal) ||
                projectName.EndsWith(".AdminCli", StringComparison.Ordinal));
    }

    private static bool IsApplicationProject(string projectPath)
    {
        string normalizedPath = NormalizePath(projectPath);
        string projectName = Path.GetFileNameWithoutExtension(normalizedPath);

        return IsModuleProject(normalizedPath) &&
               projectName.EndsWith(".Application", StringComparison.Ordinal);
    }

    private static bool IsProviderMigrationProject(string projectName) =>
        projectName.EndsWith(".Persistence.SqlServerMigrations", StringComparison.Ordinal) ||
        projectName.EndsWith(".Persistence.PostgreSqlMigrations", StringComparison.Ordinal);

    private static bool IsGeneratedMigrationSource(string sourcePath) =>
        HasPathSegment(sourcePath, "Migrations") ||
        string.Equals(Path.GetFileName(sourcePath), "ModelSnapshot.cs", StringComparison.Ordinal);

    private static bool IsAllowedDirectTimeOrIdSource(string relativePath, string token) =>
        (string.Equals(token, "Guid.NewGuid(", StringComparison.Ordinal) &&
             relativePath.EndsWith(
             $"{Path.DirectorySeparatorChar}Gma.Framework.Runtime.Infrastructure{Path.DirectorySeparatorChar}Identity{Path.DirectorySeparatorChar}GuidIdGenerator.cs",
             StringComparison.OrdinalIgnoreCase)) ||
        (token.EndsWith("UtcNow", StringComparison.Ordinal) &&
         relativePath.EndsWith(
             $"{Path.DirectorySeparatorChar}Gma.Framework.Runtime.Infrastructure{Path.DirectorySeparatorChar}Time{Path.DirectorySeparatorChar}SystemClock.cs",
             StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> FindRawFrameworkReferencesInModuleGenerator(string repositoryRoot)
    {
        string scaffolderPath = ModuleScaffolderPath(repositoryRoot);
        string scaffolder = File.ReadAllText(scaffolderPath);
        string[] forbiddenTokens =
        [
            @"..\..\..\Framework\",
            @"..\Framework\",
            @"..\..\src\Framework\"
        ];

        return forbiddenTokens
            .Where(token => scaffolder.Contains(token, StringComparison.OrdinalIgnoreCase))
            .Select(token => $"src/Framework/eng/new-module.ps1 contains raw framework reference token {token}.");
    }

    private static bool IsBackendAdapterPackage(string packageId)
    {
        string[] exactPackages =
        [
            "Aspire.NATS.Net",
            "Hangfire",
            "Hangfire.AspNetCore",
            "Hangfire.Core",
            "Hangfire.SqlServer",
            "Microsoft.Extensions.Caching.Hybrid",
            "Microsoft.Extensions.Caching.StackExchangeRedis",
            "NATS.Net",
            "prometheus-net.AspNetCore",
            "Quartz",
            "Quartz.Extensions.Hosting",
            "Quartz.Serialization.Json",
            "StackExchange.Redis"
        ];

        string[] packagePrefixes =
        [
            "OpenTelemetry.",
            "Serilog."
        ];

        return exactPackages.Contains(packageId, StringComparer.Ordinal) ||
               packagePrefixes.Any(packageId.StartsWith);
    }

    private static string NormalizePath(string path)
    {
        string normalized = NormalizeDirectorySeparators(path);

        if (normalized.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            string? frameworkProjectReference = NormalizeFrameworkProjectReference(normalized);
            if (frameworkProjectReference is not null)
            {
                return frameworkProjectReference;
            }
        }

        string[] legacyFrameworkPrefixes =
        [
            @"..\..\..\..\Framework\",
            @"..\..\..\Framework\",
            @"..\..\src\Framework\",
            @"..\Framework\"
        ];

        string? frameworkPath = NormalizeSourceRoot("GmaFrameworkRoot", legacyFrameworkPrefixes);
        if (frameworkPath is not null)
        {
            return frameworkPath;
        }

        (string PropertyName, string FolderName)[] moduleRoots =
        [
            ("GmaModuleAccessControlRoot", "AccessControl"),
            ("GmaModuleAdministrationRoot", "Administration"),
            ("GmaModuleAuthRoot", "Auth"),
            ("GmaModuleCatalogRoot", "Catalog"),
            ("GmaModuleFilesRoot", "Files"),
            ("GmaModuleNotificationsRoot", "Notifications"),
            ("GmaModuleOrderingRoot", "Ordering"),
            ("GmaModuleTaskRuntimeRoot", "TaskRuntime"),
            ("GmaModuleTaskSamplesRoot", "TaskSamples"),
            ("GmaModuleTenancyRoot", "Tenancy")
        ];

        foreach ((string propertyName, string folderName) in moduleRoots)
        {
            string[] legacyModulePrefixes =
            [
                $@"..\Modules\{folderName}\",
                $@"..\..\src\Modules\{folderName}\",
                $@"..\..\{folderName}\",
                $@"..\..\..\{folderName}\",
                $@"..\..\..\..\Modules\{folderName}\"
            ];
            string? modulePath = NormalizeSourceRoot(propertyName, legacyModulePrefixes);
            if (modulePath is not null)
            {
                return modulePath;
            }
        }

        return normalized;

        static string? NormalizeFrameworkProjectReference(string value)
        {
            string[] segments = value.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < segments.Length - 1; index++)
            {
                string segment = segments[index];
                if (!segment.StartsWith("Gma.Framework.", StringComparison.Ordinal))
                {
                    continue;
                }

                string projectFileName = segments[index + 1];
                if (string.Equals(projectFileName, $"{segment}.csproj", StringComparison.OrdinalIgnoreCase))
                {
                    return $"$GmaFrameworkRoot{Path.DirectorySeparatorChar}{segment}{Path.DirectorySeparatorChar}{projectFileName}";
                }
            }

            return null;
        }

        string? NormalizeSourceRoot(string propertyName, string[] legacyPrefixes)
        {
            string propertyExpression = "$(" + propertyName + ")";
            string marker = $"${propertyName}{Path.DirectorySeparatorChar}";
            if (normalized.StartsWith(propertyExpression, StringComparison.OrdinalIgnoreCase))
            {
                return marker + normalized[propertyExpression.Length..].TrimStart(Path.DirectorySeparatorChar);
            }

            foreach (string legacyPrefix in legacyPrefixes)
            {
                string normalizedPrefix = NormalizeDirectorySeparators(legacyPrefix);
                if (normalized.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return marker + normalized[normalizedPrefix.Length..];
                }
            }

            return null;
        }
    }

    private static string NormalizeSolutionXmlPath(string path) =>
        NormalizePath(path).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string NormalizeDirectorySeparators(string path) =>
        path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    private static string GetProjectReferenceName(string referencePath) =>
        Path.GetFileNameWithoutExtension(NormalizeDirectorySeparators(referencePath));

    private static bool SolutionXmlContainsPath(string solutionXml, string relativePath) =>
        solutionXml.Contains($"Path=\"{NormalizeSolutionXmlPath(relativePath)}\"", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateDocumentationMarkdownFiles(string repositoryRoot) =>
        EnumerateDocumentationRoots(repositoryRoot)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            .Where(path => !HasIgnoredPathSegment(path));

    private static string FrameworkDocsPath(string repositoryRoot, params string[] segments) =>
        Path.Combine(
        [
            GmaSourceLayout.FromRepositoryRoot(repositoryRoot).FrameworkRepositoryRoot,
            "docs",
            ..segments
        ]);

    private static string ModuleDocsPath(string repositoryRoot, string moduleName, params string[] segments) =>
        Path.Combine(
        [
            GmaSourceLayout.FromRepositoryRoot(repositoryRoot).GetModulePackageRoot(moduleName),
            "docs",
            ..segments
        ]);

    private static string ModuleScaffolderPath(string repositoryRoot) =>
        GmaSourceLayout.ModuleScaffolderPath(repositoryRoot);

    private static IEnumerable<string> EnumerateDocumentationRoots(string repositoryRoot)
    {
        foreach (string documentationRoot in GmaSourceLayout.FromRepositoryRoot(repositoryRoot).DocumentationRoots())
        {
            yield return documentationRoot;
        }
    }

    private static string? FindBestProjectNamespaceMatch(string importedNamespace, string[] projectNames) =>
        projectNames.FirstOrDefault(projectName =>
            string.Equals(importedNamespace, projectName, StringComparison.Ordinal) ||
            importedNamespace.StartsWith($"{projectName}.", StringComparison.Ordinal));

    private static string[] FindUsingNamespaces(string source, string[] namespaceRoots)
    {
        if (namespaceRoots.Length == 0)
        {
            return [];
        }

        string rootPattern = string.Join("|", namespaceRoots.Select(Regex.Escape));
        Regex usingNamespacePattern = new(
            @$"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:(?:[A-Za-z_][A-Za-z0-9_.]*)\s*=\s*)?(?<namespace>(?:{rootPattern})\.[A-Za-z_][A-Za-z0-9_.]*)\s*;",
            RegexOptions.Multiline);

        return usingNamespacePattern
            .Matches(source)
            .Select(match => match.Groups["namespace"].Value)
            .ToArray();
    }

    private static string? FindOwningProjectName(string sourcePath)
    {
        string? projectDirectory = FindOwningProjectDirectory(sourcePath);

        return projectDirectory is null
            ? null
            : Path.GetFileNameWithoutExtension(Directory
                .EnumerateFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
                .Single());
    }

    private static string? FindOwningProjectDirectory(string sourcePath)
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!);

        while (directory is not null)
        {
            string? projectPath = Directory
                .EnumerateFiles(directory.FullName, "*.csproj", SearchOption.TopDirectoryOnly)
                .SingleOrDefault();

            if (projectPath is not null)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool ContainsTestAttribute(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);
        return TestAttributeLinePattern().IsMatch(source);
    }

    private static string GetExpectedTestCategory(string sourcePath)
    {
        string projectName = FindOwningProjectName(sourcePath) ?? string.Empty;

        return projectName switch
        {
            "Architecture.Tests" => "Architecture",
            "Integration.Tests" => "Integration",
            _ => "Unit"
        };
    }

    private static bool HasProjectIntentFolder(string sourcePath, string testProjectRoot)
    {
        string relativePath = Path.GetRelativePath(testProjectRoot, sourcePath);
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 2;
    }

    private static bool HasCategoryTrait(string source, string category) =>
        source.Contains($"\"Category\", \"{category}\"", StringComparison.Ordinal);

    private static bool ClassContainsTestAttribute(string source, int classDeclarationIndex)
    {
        int openBraceIndex = source.IndexOf('{', classDeclarationIndex);

        if (openBraceIndex < 0)
        {
            return false;
        }

        int depth = 0;
        for (int index = openBraceIndex; index < source.Length; index++)
        {
            char current = source[index];

            if (current == '{')
            {
                depth++;
                continue;
            }

            if (current != '}')
            {
                continue;
            }

            depth--;

            if (depth == 0)
            {
                string classBody = source[(openBraceIndex + 1)..index];
                return TestAttributeLinePattern().IsMatch(classBody);
            }
        }

        return false;
    }

    private static bool IsApplicationHandlerSource(string sourcePath) =>
        string.Equals(FindOwningProjectName(sourcePath)?.Split('.').LastOrDefault(), "Application", StringComparison.Ordinal) &&
        HasPathSegment(sourcePath, "Handlers");

    private static bool IsContractSource(string sourcePath) =>
        string.Equals(FindOwningProjectName(sourcePath)?.Split('.').LastOrDefault(), "Contracts", StringComparison.Ordinal);

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        GmaSourceLayout.FromRepositoryRoot(FindRepositoryRoot())
            .EnumerateSourceFiles(root)
            .Where(path => !HasIgnoredPathSegment(path));

    private static IEnumerable<string> EnumerateProjectFiles(string root, SearchOption searchOption = SearchOption.AllDirectories) =>
        GmaSourceLayout.FromRepositoryRoot(FindRepositoryRoot())
            .ResolveSourceSearchRoots(root)
            .Where(Directory.Exists)
            .SelectMany(searchRoot => Directory.EnumerateFiles(searchRoot, "*.csproj", searchOption))
            .Where(path => !HasIgnoredPathSegment(path));

    private static bool IsUnder(string path, string parent)
    {
        string relativePath = Path.GetRelativePath(parent, path);
        return !relativePath.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relativePath);
    }

    private static string CanonicalRelativePath(string repositoryRoot, string path) =>
        GmaSourceLayout.FromRepositoryRoot(repositoryRoot).ToCanonicalRelativePath(path);

    private static string ModulePathFromRelative(string repositoryRoot, string relativePath)
    {
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return GmaSourceLayout.ModulePath(repositoryRoot, segments[0], segments[1..]);
    }

    private static bool HasRequiredPath(string jsonPath, IReadOnlyList<string> path)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(jsonPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        JsonElement current = document.RootElement;

        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out JsonElement next))
            {
                return false;
            }

            current = next;
        }

        return current.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;
    }

    private static bool HasRequiredBoolean(string jsonPath, IReadOnlyList<string> path, bool expected)
    {
        return TryGetJsonElement(jsonPath, path, out JsonElement element) &&
               element.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               element.GetBoolean() == expected;
    }

    private static bool HasRequiredStringValue(string jsonPath, IReadOnlyList<string> path, string expected)
    {
        return TryGetJsonElement(jsonPath, path, out JsonElement element) &&
               element.ValueKind == JsonValueKind.String &&
               string.Equals(element.GetString(), expected, StringComparison.Ordinal);
    }

    private static string? GetJsonStringValue(string jsonPath, IReadOnlyList<string> path) =>
        TryGetJsonElement(jsonPath, path, out JsonElement element) &&
        element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static IEnumerable<string> RequiredDocumentationTokens(
        string documentName,
        string source,
        params string[] tokens) =>
        tokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{documentName} missing '{token}'");

    private static bool IsSensitiveRequestVariable(string variableName) =>
        string.Equals(variableName, "accessToken", StringComparison.Ordinal) ||
        string.Equals(variableName, "refreshToken", StringComparison.Ordinal);

    private static string? ValidateContractFileFolder(
        string repositoryRoot,
        string projectDirectory,
        string sourcePath,
        bool isAdminContracts)
    {
        string relativeToProject = Path.GetRelativePath(projectDirectory, sourcePath);
        string[] segments = relativeToProject.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string repositoryRelativePath = Path.GetRelativePath(repositoryRoot, sourcePath);

        if (segments.Length < 2)
        {
            return $"{repositoryRelativePath} must live under a contract category folder";
        }

        string expectedFolder = isAdminContracts
            ? GetExpectedAdminContractFolder(sourcePath)
            : GetExpectedPublicContractFolder(sourcePath);

        return string.Equals(segments[0], expectedFolder, StringComparison.Ordinal)
            ? null
            : $"{repositoryRelativePath} belongs in {expectedFolder}, not {segments[0]}";
    }

    private static string GetExpectedPublicContractFolder(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);
        string source = File.ReadAllText(sourcePath);

        if (fileName.EndsWith("IntegrationEvent.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("NotificationNames.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("IntegrationSubjects.cs", StringComparison.Ordinal))
        {
            return "Events";
        }

        if (fileName.EndsWith("ProjectionExport.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("ExportSource.cs", StringComparison.Ordinal))
        {
            return "Exports";
        }

        if (source.Contains(": ITaskPayload", StringComparison.Ordinal))
        {
            return "Tasks";
        }

        if (source.Contains(": IUserNotificationPayload", StringComparison.Ordinal))
        {
            return "Notifications";
        }

        if (source.Contains(": JsonConverter<", StringComparison.Ordinal))
        {
            return "Serialization";
        }

        if (fileName.EndsWith("ModuleMetadata.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("Profile.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("Profiles.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("CompositionFeatures.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("PermissionCodes.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("ContractLimits.cs", StringComparison.Ordinal))
        {
            return "Metadata";
        }

        if (source.Contains("public enum ", StringComparison.Ordinal) ||
            fileName.EndsWith("Codes.cs", StringComparison.Ordinal) ||
            fileName.EndsWith("UserIds.cs", StringComparison.Ordinal) ||
            (fileName.EndsWith("Names.cs", StringComparison.Ordinal) &&
             source.Contains("ToWireName(", StringComparison.Ordinal)))
        {
            return "Types";
        }

        if (fileName.StartsWith("Admin", StringComparison.Ordinal))
        {
            return "Admin";
        }

        return "Api";
    }

    private static string GetExpectedAdminContractFolder(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);

        if (fileName.EndsWith("Permissions.cs", StringComparison.Ordinal))
        {
            return "Permissions";
        }

        if (fileName.EndsWith("OperationNames.cs", StringComparison.Ordinal))
        {
            return "Operations";
        }

        throw new InvalidOperationException(
            $"Admin contract file '{sourcePath}' does not match a known admin contract category.");
    }

    private static string[] GetProjectIncludes(XDocument project, string elementName) =>
        project
            .Descendants(elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizePath(value!))
            .ToArray();

    private static IEnumerable<string> CompareDependencySet(
        string relativePath,
        string referenceKind,
        string[] expected,
        string[] actual)
    {
        HashSet<string> expectedSet = expected.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actualSet = actual.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return expectedSet
            .Except(actualSet, StringComparer.OrdinalIgnoreCase)
            .Select(reference => $"{relativePath}->missing {referenceKind}:{reference}")
            .Concat(actualSet
                .Except(expectedSet, StringComparer.OrdinalIgnoreCase)
                .Select(reference => $"{relativePath}->unexpected {referenceKind}:{reference}"));
    }

    private static bool TryGetJsonElement(string jsonPath, IReadOnlyList<string> path, out JsonElement element)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(jsonPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        JsonElement current = document.RootElement;

        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out JsonElement next))
            {
                element = default;
                return false;
            }

            current = next;
        }

        element = current.Clone();
        return true;
    }

    private static string[] GetLaunchProfileUrls(string launchSettingsPath, string profileName)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(launchSettingsPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

        string? applicationUrl = document.RootElement
            .GetProperty("profiles")
            .GetProperty(profileName)
            .GetProperty("applicationUrl")
            .GetString();

        return applicationUrl?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ??
               [];
    }

    private static bool HasIgnoredPathSegment(string path)
    {
        string effectivePath = path;
        if (Path.IsPathRooted(path))
        {
            try
            {
                string repositoryRoot = FindRepositoryRoot();
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                if (!relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativePath))
                {
                    effectivePath = relativePath;
                }
            }
            catch (InvalidOperationException)
            {
                effectivePath = path;
            }
        }

        string[] segments = effectivePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains("obj", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains(".tmp", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains(".git", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains(".vs", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains(".agents", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains(".codex", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedSemanticStringBoundary(string relativePath)
    {
        string normalized = NormalizePath(relativePath);
        string[] allowed =
        [
            Path.Combine("src", "Modules", "Administration", "Gma.Modules.Administration.Persistence", "Entities", "AdminAuditEntry.cs"),
            Path.Combine("src", "Modules", "Auth", "Gma.Modules.Auth.Infrastructure", "JwtSettings.cs")
        ];

        return allowed.Any(path => string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPathSegment(string path, string segment)
    {
        string[] segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains(segment, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertMethodContains(string source, string methodName, string requiredText)
    {
        int methodIndex = source.IndexOf(
            "public static IHostApplicationBuilder " + methodName + "(",
            StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, $"Could not find method '{methodName}'.");

        int nextPublicMethodIndex = source.IndexOf(
            "    public static ",
            methodIndex + methodName.Length,
            StringComparison.Ordinal);
        string methodSource = nextPublicMethodIndex >= 0
            ? source[methodIndex..nextPublicMethodIndex]
            : source[methodIndex..];

        Assert.Contains(requiredText, methodSource, StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindBrokenMarkdownLocalLinks(string repositoryRoot, string markdownFile)
    {
        string source = File.ReadAllText(markdownFile);
        string markdownDirectory = Path.GetDirectoryName(markdownFile)!;

        foreach (Match match in MarkdownLinkPattern().Matches(source))
        {
            string target = match.Groups["target"].Value.Trim();
            if (IsExternalOrAnchorMarkdownTarget(target))
            {
                continue;
            }

            string localTarget = target.Split('#')[0].Trim();
            if (localTarget.StartsWith('<') &&
                localTarget.EndsWith('>'))
            {
                localTarget = localTarget[1..^1];
            }

            if (string.IsNullOrWhiteSpace(localTarget))
            {
                continue;
            }

            string normalizedTarget = Uri.UnescapeDataString(localTarget)
                .Replace('/', Path.DirectorySeparatorChar);
            string resolvedPath = Path.GetFullPath(Path.Combine(markdownDirectory, normalizedTarget));
            if (!IsUnder(resolvedPath, repositoryRoot) &&
                !string.Equals(resolvedPath, repositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, markdownFile)} links outside the repository: {target}";
                continue;
            }

            if (!File.Exists(resolvedPath) &&
                !Directory.Exists(resolvedPath) &&
                !TryResolveSourceLayoutDocumentationTarget(repositoryRoot, resolvedPath, out resolvedPath))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, markdownFile)} has broken local link: {target}";
            }
        }
    }

    private static bool TryResolveSourceLayoutDocumentationTarget(
        string repositoryRoot,
        string resolvedPath,
        out string sourceLayoutPath)
    {
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        string canonicalPath = sourceLayout.ToCanonicalRelativePath(resolvedPath);
        if (!sourceLayout.TryResolveCanonicalPath(canonicalPath, out sourceLayoutPath))
        {
            return false;
        }

        return File.Exists(sourceLayoutPath) || Directory.Exists(sourceLayoutPath);
    }

    private static bool IsExternalOrAnchorMarkdownTarget(string target) =>
        string.IsNullOrWhiteSpace(target) ||
        target.StartsWith('#') ||
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GMA-Skeleton.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string? GetStaticErrorOffender(Type type, FieldInfo field)
    {
        try
        {
            if (field.GetValue(null) is not Error error)
            {
                return $"{type.FullName}.{field.Name} is null";
            }

            return error == Error.None || Error.TryNormalizeCode(error.Code, out _)
                ? null
                : $"{type.FullName}.{field.Name} has invalid error code '{error.Code}'";
        }
        catch (Exception exception)
        {
            return $"{type.FullName}.{field.Name} failed to initialize: {exception.GetType().Name}: {exception.Message}";
        }
    }

    private sealed record ProjectPackageReference(string ProjectPath, string PackageId);

    private sealed record ProjectReference(string ProjectPath, string ReferencePath);

    private sealed record FrameworkProjectShape(
        string ProjectName,
        string[] PackageReferences,
        string[] FrameworkReferences,
        string[] ProjectReferences);

    private sealed record HostProjectShape(
        string ProjectPath,
        string[] PackageReferences,
        string[] FrameworkReferences,
        string[] ProjectReferences);

    [GeneratedRegex(@"^\s*namespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex(@"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:(?:[A-Za-z_][A-Za-z0-9_.]*)\s*=\s*)?(?<namespace>Gma\.Framework\.[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Multiline)]
    private static partial Regex FrameworkUsingNamespacePattern();

    [GeneratedRegex(@"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:(?:[A-Za-z_][A-Za-z0-9_.]*)\s*=\s*)?(?<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingNamespacePattern();

    [GeneratedRegex(@"!?\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.Multiline)]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"<Project\s+Path=""(?<path>[^""]+\.csproj)""\s*/>", RegexOptions.Multiline)]
    private static partial Regex SolutionProjectPathPattern();

    [GeneratedRegex(@"GMA-Skeleton\.sln(?!x)", RegexOptions.IgnoreCase)]
    private static partial Regex LegacySolutionReferencePattern();

    [GeneratedRegex(@"\b(?:public|internal)\s+(?:sealed\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex PublicOrInternalClassPattern();

    [GeneratedRegex(@"^\s*\[(?:Fact|Theory|DockerFact)(?:\(|\])", RegexOptions.Multiline)]
    private static partial Regex TestAttributeLinePattern();

    [GeneratedRegex(@"^\s*\[DockerFact(?:\(|\])", RegexOptions.Multiline)]
    private static partial Regex DockerFactAttributeLinePattern();

    [GeneratedRegex(@"^\s*public\s+(?:(?:static|sealed|abstract|partial|readonly)\s+)*(?:record\s+(?:class\s+|struct\s+)?|class\s+|interface\s+|enum\s+)(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Multiline)]
    private static partial Regex PublicContractTypePattern();

    [GeneratedRegex(@"^@(?<name>accessToken|refreshToken)[ \t]*=[ \t]*\S+", RegexOptions.Multiline)]
    private static partial Regex ConcreteRequestVariablePattern();

    [GeneratedRegex(@"public\s+static\s+class\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)[\s\S]*?public\s+const\s+string\s+(?:Name|Schema)\s*=\s*""(?<value>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex ModuleIdentityConstantPattern();

    [GeneratedRegex(@"public\s+static\s+class\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)[\s\S]*?public\s+const\s+string\s+Schema\s*=", RegexOptions.Multiline)]
    private static partial Regex SchemaConstantTypePattern();

    [GeneratedRegex(@",\s*""[^""]+""\s*\)", RegexOptions.Multiline)]
    private static partial Regex RawStringArgumentPattern();

    [GeneratedRegex(@"AdminOperation\s*\.\s*Create\s*\(\s*""", RegexOptions.Multiline)]
    private static partial Regex AdminOperationStringLiteralPattern();

    [GeneratedRegex(@"AdminPermission\s*\.\s*Create\s*\(\s*""", RegexOptions.Multiline)]
    private static partial Regex AdminPermissionStringLiteralPattern();

    [GeneratedRegex(@"public\s+string\s+Name\s*=>\s*""", RegexOptions.Multiline)]
    private static partial Regex ModuleNameStringLiteralPattern();

    [GeneratedRegex(@"\bResult\s*<[^>\r\n]*\?>", RegexOptions.Multiline)]
    private static partial Regex NullableResultTypePattern();

    [GeneratedRegex(@"\brecord\s+struct\s+PageRequest\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalPageRequestPattern();

    [GeneratedRegex(@"\brecord\s+struct\s+ApiErrorStatusCode\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalApiErrorStatusCodePattern();

    [GeneratedRegex(@"\brecord\s+AdminOperationExecutionResult\s*<[^>]+>\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalAdminOperationExecutionResultPattern();

    [GeneratedRegex(@"\brecord\s+ModuleEndpointMetadata\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalModuleEndpointMetadataPattern();

    [GeneratedRegex(@"\brecord\s+AccessTokenClaims\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalAccessTokenClaimsPattern();

    private static Regex PositionalMessagingRecordPattern(string typeName) =>
        new(@$"\brecord\s+{Regex.Escape(typeName)}\s*\(", RegexOptions.Multiline);

    private static Regex PositionalOrderingProjectionPortModelPattern(string typeName) =>
        new(@$"\brecord\s+{Regex.Escape(typeName)}\s*\(", RegexOptions.Multiline);

    [GeneratedRegex(@"public\s+sealed\s+record\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalPublicIntegrationEventPattern();

    [GeneratedRegex(@"public\s+sealed\s+record\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex PositionalPublicDomainEventPattern();

    [GeneratedRegex(@"public\s+sealed\s+record\s+[A-Za-z_][A-Za-z0-9_]*DomainEvent\s*:\s*(?:DomainEvent|ScopedDomainEvent)\b", RegexOptions.Multiline)]
    private static partial Regex ModuleDomainEventBasePattern();

    [GeneratedRegex(@"\brecord\s+struct\s+[A-Za-z_][A-Za-z0-9_]*Id\s*\(\s*Guid\s+Value\s*\)", RegexOptions.Multiline)]
    private static partial Regex PositionalGuidIdValueObjectPattern();

    [GeneratedRegex(@"public\s+readonly\s+record\s+struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Status|State|Severity|Audience|Kind|Type|Result|Mode|Provider))\b[\s\S]*?public\s+string\s+Value\s*\{", RegexOptions.Multiline)]
    private static partial Regex StringBackedSemanticDomainValueObjectPattern();

    [GeneratedRegex(@"^\s*(?:public|private|protected|internal)\s+string\s+(?<name>Status|State|Severity|Audience|Kind|Type|Result|Mode|Provider|status|state|severity|audience|kind|type|result|mode|provider)\b", RegexOptions.Multiline)]
    private static partial Regex SemanticDomainStringMemberPattern();

    [GeneratedRegex(@"\bpublic\s+(?:required\s+)?string\??\s+(?<name>Status|State|Severity|Audience|RecipientKind|Kind|Type|Result|Mode|Provider|Scope)\s*\{\s*get\b", RegexOptions.Multiline)]
    private static partial Regex SemanticBoundaryStringPropertyPattern();

    [GeneratedRegex(@"\brecord\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^)]*\bstring\??\s+(?<name>Status|State|Severity|Audience|RecipientKind|Kind|Type|Result|Mode|Provider|Scope)\b", RegexOptions.Singleline)]
    private static partial Regex SemanticBoundaryRecordParameterPattern();

    [GeneratedRegex(@"_\s*=>\s*(?<value>(?:[A-Za-z_][A-Za-z0-9_.]*\.)?(?:Info|Success|Warning|Error|Active|Disabled|Submitted|Discontinued|TenantUsers|TenantAdmins|PlatformUsers|PlatformAdmins|User|Admin|Pending|Queued|Processed|Succeeded))\b", RegexOptions.Multiline)]
    private static partial Regex MeaningfulSemanticDefaultSwitchArmPattern();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundaryPattern();

    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundaryPattern();
}
