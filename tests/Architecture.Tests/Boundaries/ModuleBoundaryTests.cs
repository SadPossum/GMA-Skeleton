namespace Architecture.Tests;

using System.Reflection;
using System.Text.RegularExpressions;
using Gma.Framework.Caching;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Cqrs.UnitOfWork;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class ModuleBoundaryTests
{
    private static readonly string[] SharedAdapterAssembliesWithNonStandardNames =
    [
        "Gma.Framework.Api.OpenApi",
        "Gma.Framework.Api.Serilog",
        "Gma.Framework.Caching.Cqrs",
        "Gma.Framework.Caching.Redis",
        "Gma.Framework.Infrastructure",
        "Gma.Framework.Logging.Serilog",
        "Gma.Framework.Messaging.Nats",
        "Gma.Framework.Messaging.Nats.Aspire",
        "Gma.Framework.Notifications.Api",
        "Gma.Framework.Notifications.Cqrs",
        "Gma.Framework.Notifications.SignalR",
        "Gma.Framework.Persistence.EntityFrameworkCore",
        "Gma.Framework.Tenancy.Api.Serilog",
        "Gma.Framework.Tenancy.Caching",
        "Gma.Framework.Tenancy.Cqrs",
        "Gma.Framework.Tenancy.Messaging.Infrastructure",
        "Gma.Framework.Tenancy.Tasks"
    ];

    [Fact]
    public void Module_domain_projects_do_not_depend_on_contracts_application_or_infrastructure()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Domain)
            .SelectMany(project => project.Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(IsForbiddenDomainDependency)
                .Select(referenceName => $"{project.ProjectName}->{referenceName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_application_projects_do_not_depend_on_adapters_or_front_doors()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Application)
            .SelectMany(project => project.Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => IsForbiddenApplicationDependency(project.ModulePrefix, referenceName))
                .Select(referenceName => $"{project.ProjectName}->{referenceName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Modules_only_reference_other_modules_through_contracts()
    {
        foreach (ModuleProject project in ArchitectureCatalog.ModuleProjects)
        {
            string[] forbiddenReferences = project.Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => IsForbiddenModuleReference(project.ModulePrefix, referenceName))
                .Select(referenceName => $"{project.ProjectName}->{referenceName}")
                .ToArray();

            Assert.Empty(forbiddenReferences);
        }
    }

    [Fact]
    public void Modules_do_not_depend_on_observability_backends()
    {
        string[] backendPrefixes = ["OpenTelemetry", "Prometheus", "Serilog.Sinks.Grafana.Loki"];

        foreach (Assembly assembly in ArchitectureCatalog.ModuleBoundaryAssemblies)
        {
            string[] backendReferences = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => backendPrefixes.Any(referenceName.StartsWith))
                .ToArray();

            Assert.Empty(backendReferences);
        }
    }

    [Fact]
    public void Modules_do_not_depend_on_caching_backends()
    {
        string[] backendPrefixes =
        [
            "Microsoft.Extensions.Caching.Hybrid",
            "Microsoft.Extensions.Caching.StackExchangeRedis",
            "Gma.Framework.Caching.Cqrs",
            "Gma.Framework.Caching.Redis",
            "StackExchange.Redis"
        ];

        foreach (Assembly assembly in ArchitectureCatalog.ModuleBoundaryAssemblies)
        {
            string[] backendReferences = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => backendPrefixes.Any(referenceName.StartsWith))
                .ToArray();

            Assert.Empty(backendReferences);
        }
    }

    [Fact]
    public void Modules_do_not_reference_nats_client_packages_directly()
    {
        foreach (Assembly assembly in ArchitectureCatalog.ModuleBoundaryAssemblies)
        {
            string[] references = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => referenceName.StartsWith("NATS.", StringComparison.Ordinal))
                .ToArray();

            Assert.Empty(references);
        }
    }

    [Fact]
    public void Modules_do_not_depend_on_notification_front_door_adapters_or_signalr()
    {
        string[] forbiddenPrefixes =
        [
            "Microsoft.AspNetCore.SignalR",
            "Gma.Framework.Notifications.Api",
            "Gma.Framework.Notifications.Cqrs",
            "Gma.Framework.Notifications.SignalR"
        ];

        foreach (Assembly assembly in ArchitectureCatalog.ModuleBoundaryAssemblies)
        {
            string[] references = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => forbiddenPrefixes.Any(referenceName.StartsWith))
                .ToArray();

            Assert.Empty(references);
        }
    }

    [Fact]
    public void Shared_api_is_isolated_to_api_front_doors()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path =>
            {
                string projectName = Path.GetFileNameWithoutExtension(path);
                return !projectName.EndsWith(".Api", StringComparison.Ordinal) &&
                       !projectName.EndsWith(".AdminApi", StringComparison.Ordinal);
            })
            .Where(path => File.ReadAllText(path).Contains("Gma.Framework.Api", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_module_contracts_do_not_reference_admin_framework()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Contracts)
            .Where(project => project.Assembly
                .GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, "Gma.Framework.Administration", StringComparison.Ordinal)))
            .Select(project => project.ProjectName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Ordering_only_references_catalog_contracts()
    {
        foreach (Assembly assembly in ArchitectureCatalog.OrderingAssemblies)
        {
            string[] forbiddenCatalogReferences = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName =>
                    referenceName.StartsWith("Catalog.", StringComparison.Ordinal) &&
                    !string.Equals(referenceName, "Catalog.Contracts", StringComparison.Ordinal))
                .ToArray();

            Assert.Empty(forbiddenCatalogReferences);
        }
    }

    [Fact]
    public void Default_hosts_do_not_register_example_modules_or_consumers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostFiles =
        [
            Path.Combine(repositoryRoot, "src", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Host.AdminCli", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Host.AdminApi", "Program.cs")
        ];

        string[] forbiddenTokens =
        [
            "CatalogModule",
            "CatalogAdminCliModule",
            "CatalogAdminApiModule",
            "Ordering",
            "TaskRuntime",
            "TaskSamples",
            "AddTaskWorkerRuntime",
            "AddNatsJetStreamConsumers"
        ];

        string[] offenders = hostFiles
            .SelectMany(path => forbiddenTokens
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Default_hosts_gate_nats_publishing_on_configuration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostFiles =
        [
            Path.Combine(repositoryRoot, "src", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Host.AdminApi", "Program.cs")
        ];
        string[] hostProjects =
        [
            Path.Combine(repositoryRoot, "src", "Host.Api", "Host.Api.csproj"),
            Path.Combine(repositoryRoot, "src", "Host.AdminApi", "Host.AdminApi.csproj")
        ];
        string adapterSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Framework",
            "Gma.Framework.Messaging.Nats.Aspire",
            "DependencyInjection.cs"));
        string adapterProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Framework",
            "Gma.Framework.Messaging.Nats.Aspire",
            "Gma.Framework.Messaging.Nats.Aspire.csproj"));

        string[] offenders = hostFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                List<string> failures = [];

                if (!source.Contains("AddConfiguredNatsJetStreamMessaging()", StringComparison.Ordinal))
                {
                    failures.Add("missing configured NATS adapter");
                }

                if (source.Contains("AddNatsClient", StringComparison.Ordinal) ||
                    source.Contains("AddNatsJetStreamMessaging()", StringComparison.Ordinal) ||
                    source.Contains("GetValue<bool>(\"NatsJetStream:Enabled\")", StringComparison.Ordinal))
                {
                    failures.Add("contains raw NATS publishing wiring");
                }

                return failures.Select(failure => $"{Path.GetRelativePath(repositoryRoot, path)}:{failure}");
            })
            .Concat(hostProjects
                .Where(path => File.ReadAllText(path).Contains("Aspire.NATS.Net", StringComparison.Ordinal))
                .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)}:direct Aspire.NATS.Net package"))
            .ToArray();

        Assert.Contains("Aspire.NATS.Net", adapterProject, StringComparison.Ordinal);
        Assert.Contains("NatsJetStreamOptions", adapterSource, StringComparison.Ordinal);
        Assert.Contains("NatsJetStreamOptionsValidator", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings:{ConnectionName}", adapterSource, StringComparison.Ordinal);
        Assert.Contains("AddNatsClient", adapterSource, StringComparison.Ordinal);
        Assert.Contains("AddNatsJetStreamMessaging()", adapterSource, StringComparison.Ordinal);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_application_handlers_do_not_inject_unqualified_outbox_writer()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => path.Contains(".Application", StringComparison.Ordinal))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("IOutboxWriter ", StringComparison.Ordinal) ||
                       text.Contains("IOutboxWriter)", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_commands_that_write_state_are_explicitly_transactional()
    {
        string[] offenders = ArchitectureCatalog.ApplicationAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.EndsWith(".Commands", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(ImplementsCommand)
            .Where(type => !IsAllowedNonTransactionalCommand(type))
            .Where(type => !ImplementsTransactionalCommand(type))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsAllowedNonTransactionalCommand(Type type) =>
        type.FullName is
            "Gma.Modules.Files.Application.Commands.DeleteFileCommand" or
            "Gma.Modules.Files.Application.Commands.UploadFileCommand";

    [Fact]
    public void Module_query_handlers_do_not_inject_side_effect_infrastructure()
    {
        string[] offenders = ArchitectureCatalog.ApplicationAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(ImplementsQueryHandler)
            .SelectMany(type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => ContainsForbiddenQuerySideEffectDependency(parameter.ParameterType))
                .Select(parameter => $"{type.FullName} depends on {parameter.ParameterType.FullName ?? parameter.ParameterType.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Integration_tests_do_not_use_ensure_created()
    {
        string repositoryRoot = FindRepositoryRoot();
        string integrationTestsRoot = Path.Combine(repositoryRoot, "tests", "Integration.Tests");
        string[] offenders = Directory
            .EnumerateFiles(integrationTestsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => File.ReadAllText(path).Contains("EnsureCreated", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Admin_api_modules_use_executor_for_tenant_enforcement()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => path.Contains(".AdminApi", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(".RequireTenant(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Admin_api_modules_do_not_expose_bootstrap_operations()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] forbiddenTokens = ["BootstrapOwnerCommand", "admin.bootstrap", "/bootstrap"];
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => path.Contains(".AdminApi", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Admin_api_get_routes_map_missing_resources_to_not_found()
    {
        string repositoryRoot = FindRepositoryRoot();
        Dictionary<string, string[]> expectedTokensByFile = new()
        {
            [Path.Combine(repositoryRoot, "src", "Modules", "Auth", "Gma.Modules.Auth.AdminApi", "AuthAdminApiModule.cs")] =
            [
                "errorStatusCodes:",
                "AuthApplicationErrors.MemberNotFound.Code"
            ],
            [Path.Combine(repositoryRoot, "src", "Modules", "Catalog", "Catalog.AdminApi", "CatalogAdminApiModule.cs")] =
            [
                "errorStatusCodes:",
                "CatalogApplicationErrors.ItemNotFound.Code"
            ]
        };
        string[] offenders = expectedTokensByFile
            .SelectMany(item =>
            {
                string source = File.ReadAllText(item.Key);
                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, item.Key)} missing {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Cqrs_request_payloads_are_not_nullable_success_contracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        Regex nullableCqrsPayloadPattern = new(@"\b(?:IQuery|ICommand|ITransactionalCommand|Result)<[^>\r\n]+\?>");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return nullableCqrsPayloadPattern.IsMatch(source);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Feature_module_commands_and_item_queries_have_explicit_validators()
    {
        string[] modulesWithValidatorPolicy = ["Administration", "Auth", "Catalog", "Files", "Ordering"];
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Application)
            .Where(project => modulesWithValidatorPolicy.Contains(project.ModulePrefix, StringComparer.Ordinal))
            .SelectMany(project => project.Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => ImplementsCommand(type) || ImplementsQuery(type))
                .Where(type => !type.Name.StartsWith("List", StringComparison.Ordinal))
                .Select(type => new
                {
                    Project = project.ProjectName,
                    RequestType = type,
                    ExpectedValidatorType = ImplementsCommand(type)
                        ? typeof(ICommandValidator<>).MakeGenericType(type)
                        : typeof(IQueryValidator<>).MakeGenericType(type),
                }))
            .Where(item => !item.RequestType.Assembly
                .GetTypes()
                .Any(type => !type.IsAbstract && item.ExpectedValidatorType.IsAssignableFrom(type)))
            .Select(item => $"{item.Project}:{item.RequestType.FullName} missing {item.ExpectedValidatorType.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void System_command_line_is_isolated_to_cli_front_doors()
    {
        foreach (Assembly assembly in ArchitectureCatalog.CommandLineCheckedAssemblies.Except(ArchitectureCatalog.CommandLineAllowedAssemblies))
        {
            string[] references = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(referenceName => referenceName.StartsWith("System.CommandLine", StringComparison.Ordinal))
                .ToArray();

            Assert.Empty(references);
        }
    }

    private static bool IsForbiddenModuleReference(string modulePrefix, string referenceName) =>
        ArchitectureCatalog.ModulePrefixes.Any(prefix => referenceName.StartsWith(prefix + ".", StringComparison.Ordinal)) &&
               !referenceName.StartsWith(modulePrefix + ".", StringComparison.Ordinal) &&
               !referenceName.EndsWith(".Contracts", StringComparison.Ordinal);

    private static bool IsForbiddenDomainDependency(string referenceName) =>
        ArchitectureCatalog.ModulePrefixes.Any(prefix => referenceName.StartsWith(prefix + ".", StringComparison.Ordinal)) ||
        IsSharedAdapterDependency(referenceName) ||
        referenceName.StartsWith("Gma.Framework.AccessControl", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Application", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Infrastructure", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Api", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Administration", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Messaging", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Tasks", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.Extensions", StringComparison.Ordinal) ||
        referenceName.StartsWith("System.CommandLine", StringComparison.Ordinal) ||
        referenceName.StartsWith("NATS.", StringComparison.Ordinal);

    private static bool IsForbiddenApplicationDependency(string modulePrefix, string referenceName) =>
        referenceName.StartsWith(modulePrefix + ".Admin", StringComparison.Ordinal) ||
        referenceName.StartsWith(modulePrefix + ".Api", StringComparison.Ordinal) ||
        referenceName.StartsWith(modulePrefix + ".Infrastructure", StringComparison.Ordinal) ||
        referenceName.StartsWith(modulePrefix + ".Persistence", StringComparison.Ordinal) ||
        IsSharedAdapterDependency(referenceName) ||
        referenceName.StartsWith("Gma.Framework.Infrastructure", StringComparison.Ordinal) ||
        referenceName.StartsWith("Gma.Framework.Api", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
        referenceName.StartsWith("Microsoft.Extensions.Hosting", StringComparison.Ordinal) ||
        referenceName.StartsWith("System.CommandLine", StringComparison.Ordinal) ||
        referenceName.StartsWith("NATS.", StringComparison.Ordinal);

    private static bool IsSharedAdapterDependency(string referenceName) =>
        (referenceName.StartsWith("Gma.Framework.", StringComparison.Ordinal) &&
         referenceName.EndsWith(".Infrastructure", StringComparison.Ordinal)) ||
        SharedAdapterAssembliesWithNonStandardNames.Contains(referenceName, StringComparer.Ordinal);

    private static bool ImplementsCommand(Type type) =>
        type.GetInterfaces().Any(IsCommandInterface);

    private static bool ImplementsTransactionalCommand(Type type) =>
        type.GetInterfaces().Any(IsTransactionalCommandInterface);

    private static bool ImplementsQueryHandler(Type type) =>
        type.GetInterfaces().Any(IsQueryHandlerInterface);

    private static bool IsCommandInterface(Type type) =>
        type.IsGenericType &&
        string.Equals(
            type.GetGenericTypeDefinition().FullName,
            "Gma.Framework.Cqrs.ICommand`1",
            StringComparison.Ordinal);

    private static bool ImplementsQuery(Type type) =>
        type.GetInterfaces().Any(IsQueryInterface);

    private static bool IsQueryInterface(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(IQuery<>);

    private static bool IsQueryHandlerInterface(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>);

    private static bool IsTransactionalCommandInterface(Type type) =>
        type.IsGenericType &&
        string.Equals(
            type.GetGenericTypeDefinition().FullName,
            "Gma.Framework.Cqrs.ITransactionalCommand`1",
            StringComparison.Ordinal);

    private static bool ContainsForbiddenQuerySideEffectDependency(Type dependencyType)
    {
        Type type = dependencyType.IsArray
            ? dependencyType.GetElementType()!
            : dependencyType;

        if (type == typeof(IUnitOfWork) ||
            type == typeof(IOutboxWriter) ||
            type == typeof(IOutboxWriterRegistry) ||
            type == typeof(ICacheInvalidationQueue))
        {
            return true;
        }

        return type.IsGenericType &&
               type.GetGenericArguments().Any(ContainsForbiddenQuerySideEffectDependency);
    }

    private static bool HasIgnoredPathSegment(string path)
    {
        string[] segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GenericModularApi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
