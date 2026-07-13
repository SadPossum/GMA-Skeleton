namespace Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class SourceFirstGeneratorGuardTests
{
    [Fact]
    public void App_generator_module_catalog_matches_mounted_reusable_modules()
    {
        string repositoryRoot = FindRepositoryRoot();
        string gitmodules = File.ReadAllText(Path.Combine(repositoryRoot, ".gitmodules"));
        string generator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));

        string[] mountedModuleAliases = Regex
            .Matches(gitmodules, @"^\s*path\s*=\s*gma/modules/(?<alias>[^\s]+)\s*$", RegexOptions.Multiline)
            .Select(match => match.Groups["alias"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] generatedModuleAliases = Regex
            .Matches(generator, @"^\s*Alias\s*=\s*'(?<alias>[^']+)'\s*$", RegexOptions.Multiline)
            .Select(match => match.Groups["alias"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(mountedModuleAliases);
        Assert.Equal(mountedModuleAliases, generatedModuleAliases);
    }

    [Fact]
    public void App_generator_emits_framework_owned_tool_wrappers_and_branded_modules()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string[] requiredTokens =
        [
            "gma/framework/eng/add-migration.ps1",
            "gma/framework/eng/check-migrations.ps1",
            "gma/framework/eng/check-source-packages.ps1",
            "gma/framework/eng/check-submodule-heads.ps1",
            "gma/framework/eng/export-source-set.ps1",
            "gma/framework/eng/sync-solution.ps1",
            "-ProjectPrefix '$ApplicationName.Modules'",
            "-PublicApiHostProject",
            "AppModuleProjectsUseApplicationPrefix",
            "SolutionListsEveryApplicationProject",
        ];

        Assert.DoesNotContain(requiredTokens, token => !generator.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void App_generator_keeps_api_default_and_offers_explicit_production_surfaces()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string[] requiredTokens =
        [
            "[string[]] $Hosts = @('Api')",
            "'AdminApi'",
            "'AdminCli'",
            "'Worker'",
            "'Aspire'",
            "[switch] $ServiceDefaults",
            "[switch] $DockerValidation",
            "Aspire.AppHost.Sdk/13.4.2",
        ];

        Assert.DoesNotContain(requiredTokens, token => !generator.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void App_generator_composes_optional_auth_and_notification_adapters()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string[] requiredTokens =
        [
            "Gma.Modules.Auth.Providers.OpenIdConnect.csproj",
            "builder.AddAuthOpenIdConnectProviders();",
            "ExternalExchangeLifetimeMinutes = 5",
            "EmailVerificationRequestCooldownSeconds = 60",
            "'/api/auth/browser'",
            "OpenIdConnect = [ordered]@{",
            "Gma.Modules.Notifications.Adapters.Email.csproj",
            "builder.Services.AddNotificationEmailAdapter(builder.Configuration);",
            "GMA-Extensions.git",
            "GmaExtensionsRoot",
            "Gma.Extensions.Auth.Notifications.csproj",
            "builder.Services.AddAuthNotificationsExtension();",
            "Delivery = [ordered]@{",
            "MaxBatchesPerCategoryPerCycle = 4",
        ];

        Assert.DoesNotContain(requiredTokens, token => !generator.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void App_generator_selection_matrix_guards_optional_extension_composition()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-generated-app-selections.ps1"));
        string[] requiredTokens =
        [
            "AuthOnly",
            "NotificationsOnly",
            "AuthNotifications",
            "HasExtension = $false",
            "HasExtension = $true",
            "Gma.Extensions.Auth.Notifications",
            "AddAuthNotificationsExtension",
        ];

        Assert.DoesNotContain(requiredTokens, token => !script.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_ci_uses_immutable_actions_and_release_source_set_evidence()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string[] requiredTokens =
        [
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            "actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1",
            "actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
            "persist-credentials: false",
            ".github\\dependabot.yml",
            ".github\\workflows\\release-source-set.yml",
        ];

        Assert.DoesNotContain(requiredTokens, token => !generator.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void Composition_wrappers_delegate_to_the_mounted_framework()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] wrappers =
        [
            "add-migration.ps1",
            "check-migrations.ps1",
            "check-source-packages.ps1",
            "check-submodule-dev-heads.ps1",
            "export-source-set.ps1",
            "sync-solution.ps1",
        ];

        string[] errors = wrappers.SelectMany(wrapper =>
        {
            string path = Path.Combine(repositoryRoot, "eng", wrapper);
            string source = File.ReadAllText(path);
            return new[]
            {
                source.Contains("gma/framework/eng/", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : $"{wrapper} does not delegate to framework tooling.",
                File.ReadLines(path).Count() <= 65
                    ? null
                    : $"{wrapper} contains composition logic instead of remaining a thin wrapper.",
            }.OfType<string>();
        }).ToArray();

        Assert.Empty(errors);
    }

    [Fact]
    public void Developer_experience_guard_support_is_split_from_behavior_tests()
    {
        string repositoryRoot = FindRepositoryRoot();
        string root = Path.Combine(repositoryRoot, "tests", "Architecture.Tests", "DeveloperExperience");
        string guard = Path.Combine(root, "DeveloperExperienceGuardTests.cs");
        string support = Path.Combine(root, "DeveloperExperienceGuardTestSupport.cs");

        Assert.True(File.Exists(support));
        Assert.True(File.ReadLines(guard).Count() < 8_000);
        Assert.Contains("private static bool ImplementsOpenGeneric", File.ReadAllText(support), StringComparison.Ordinal);
    }

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
}
