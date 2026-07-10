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
    [Fact]
    public void Path_normalization_accepts_both_directory_separator_styles()
    {
        string expected = $"alpha{Path.DirectorySeparatorChar}beta";

        Assert.Equal(expected, NormalizeDirectorySeparators(@"alpha\beta"));
        Assert.Equal(expected, NormalizeDirectorySeparators("alpha/beta"));
        Assert.Equal("Example.Project", GetProjectReferenceName(@"..\Example.Project\Example.Project.csproj"));
        Assert.Equal("Example.Project", GetProjectReferenceName("../Example.Project/Example.Project.csproj"));
    }

    [Fact]
    public void Projects_under_src_and_tests_are_in_solution()
    {
        string repositoryRoot = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string[] projectPaths = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsUnder(path, Path.Combine(repositoryRoot, "src")) ||
                           IsUnder(path, Path.Combine(repositoryRoot, "tests")))
            .Where(path => !HasIgnoredPathSegment(path))
            .Select(path => NormalizeSolutionXmlPath(Path.GetRelativePath(repositoryRoot, path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] missingProjects = projectPaths
            .Where(path => !SolutionXmlContainsPath(solution, path))
            .ToArray();

        Assert.Empty(missingProjects);
    }

    [Fact]
    public void Project_files_live_in_matching_project_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsUnder(path, Path.Combine(repositoryRoot, "src")) ||
                           IsUnder(path, Path.Combine(repositoryRoot, "tests")))
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path =>
            {
                string projectName = Path.GetFileNameWithoutExtension(path);
                string folderName = new DirectoryInfo(Path.GetDirectoryName(path)!).Name;
                return !string.Equals(projectName, folderName, StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Project_files_do_not_override_default_namespace_or_assembly_name()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsUnder(path, Path.Combine(repositoryRoot, "src")) ||
                           IsUnder(path, Path.Combine(repositoryRoot, "tests")))
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(path =>
            {
                XDocument project = XDocument.Load(path);
                return project
                    .Descendants()
                    .Where(element => element.Name.LocalName is "RootNamespace" or "AssemblyName")
                    .Select(element => $"{Path.GetRelativePath(repositoryRoot, path)}:{element.Name.LocalName}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Operational_docs_scripts_and_requests_are_solution_items()
    {
        string repositoryRoot = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string[] expectedSolutionItems = EnumerateDocumentationMarkdownFiles(repositoryRoot)
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "eng"), "*.ps1", SearchOption.TopDirectoryOnly))
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "requests"), "*.*", SearchOption.TopDirectoryOnly))
            .Where(path => !HasIgnoredPathSegment(path))
            .Select(path => NormalizeSolutionXmlPath(Path.GetRelativePath(repositoryRoot, path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] offenders = expectedSolutionItems
            .Where(path => !SolutionXmlContainsPath(solution, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Root_docs_stay_skeleton_owned()
    {
        string repositoryRoot = FindRepositoryRoot();
        string docsRoot = Path.Combine(repositoryRoot, "docs");
        string[] forbiddenRootDirectories = ["adr", "guidelines", "modules", "templates"];
        string[] allowedArchitectureDocs =
        [
            "gma-rebrand-and-source-repo-split.md",
            "overview.md"
        ];
        string[] directoryOffenders = forbiddenRootDirectories
            .Select(directory => Path.Combine(docsRoot, directory))
            .Where(Directory.Exists)
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should live under source-owned docs")
            .ToArray();
        string[] architectureOffenders = Directory
            .EnumerateFiles(Path.Combine(docsRoot, "architecture"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null &&
                               !allowedArchitectureDocs.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            .Select(fileName => $"docs/architecture/{fileName} should live under src/Framework/docs/architecture")
            .ToArray();

        Assert.Empty(directoryOffenders.Concat(architectureOffenders).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Skeleton_docs_do_not_link_to_deep_submodule_files()
    {
        string repositoryRoot = FindRepositoryRoot();
        Regex markdownLinkPattern = new(@"\[[^\]]+\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)");
        string[] skeletonDocs = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "docs"), "*.md", SearchOption.AllDirectories)
            .Prepend(Path.Combine(repositoryRoot, "README.md"))
            .Where(path => !HasIgnoredPathSegment(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] offenders = skeletonDocs
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));
                return markdownLinkPattern
                    .Matches(source)
                    .Select(match => match.Groups["target"].Value.Trim())
                    .Where(target => !target.Contains("://", StringComparison.Ordinal) &&
                                     !target.StartsWith('#'))
                    .Select(target => target.Replace('\\', '/'))
                    .Where(target => target.StartsWith("gma/", StringComparison.OrdinalIgnoreCase) ||
                                     target.StartsWith("../gma/", StringComparison.OrdinalIgnoreCase) ||
                                     target.StartsWith("../../gma/", StringComparison.OrdinalIgnoreCase))
                    .Select(target => $"{relativePath} links to submodule file '{target}'. Use the owning source repository URL instead.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Source_repo_split_docs_prefer_flat_package_roots()
    {
        string repositoryRoot = FindRepositoryRoot();
        string splitPlan = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "architecture",
            "gma-rebrand-and-source-repo-split.md"));
        string skeletonDocsIndex = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "README.md"));
        string skeletonArchitectureOverview = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "architecture",
            "overview.md"));
        string documentationGuidelines = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "documentation-guidelines.md"));
        string documentationAdr = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "adr",
            "0001-documentation-structure.md"));
        string[] requiredSplitPlanTokens =
        [
            @"GMA-Module-Auth\src\Gma.Modules.Auth.Application",
            @"gma\modules\auth",
            @"gma\modules\auth\src\",
            @"Do not preserve `src\Framework\...` or `src\Modules\<Module>\...` inside the independent repositories.",
            "root-level `docs/`, `tests/`, `eng/`, and `src/` folders",
            "skeleton tests keep only composition guards"
        ];
        string[] requiredDocsTokens =
        [
            "links to reusable docs should point at the owning source repository on GitHub",
            "GitHub cannot reliably render deep file links through a skeleton submodule path"
        ];

        string[] offenders = requiredSplitPlanTokens
            .Where(token => !splitPlan.Contains(token, StringComparison.Ordinal))
            .Select(token => $"docs/architecture/gma-rebrand-and-source-repo-split.md missing {token}")
            .Concat(requiredDocsTokens
                .Where(token =>
                    !skeletonDocsIndex.Contains(token, StringComparison.Ordinal) &&
                    !skeletonArchitectureOverview.Contains(token, StringComparison.Ordinal) &&
                    !documentationGuidelines.Contains(token, StringComparison.Ordinal) &&
                    !documentationAdr.Contains(token, StringComparison.Ordinal))
                .Select(token => $"documentation ownership docs missing {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repository_root_policy_files_are_solution_items()
    {
        string repositoryRoot = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string[] expectedSolutionItems =
        [
            @".config\dotnet-tools.json",
            ".editorconfig",
            ".gitattributes",
            ".gitignore",
            ".github/workflows/docker-tests.yml",
            ".github/workflows/validate.yml",
            ".gitmodules",
            "Directory.Build.props",
            "Directory.Packages.props",
            "Gma.SourceRoots.props.example",
            "global.json",
            "gma/README.md",
            "gma/modules/README.md",
            "LICENSE",
            "nuget.config",
            "README.md"
        ];
        string[] offenders = expectedSolutionItems
            .Select(NormalizeSolutionXmlPath)
            .Where(path => !SolutionXmlContainsPath(solution, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Example_modules_do_not_expose_reusable_module_solution_entrypoints()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] forbiddenSolutionPaths =
        [
            "Gma.Modules.Catalog.slnx",
            "Gma.Modules.Ordering.slnx",
            "Gma.Modules.TaskSamples.slnx",
            "src/Modules/Catalog/Gma.Modules.Catalog.slnx",
            "src/Modules/Ordering/Gma.Modules.Ordering.slnx",
            "src/Modules/TaskSamples/Gma.Modules.TaskSamples.slnx"
        ];

        string allUpSolution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string validationScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "gma-validate.ps1"));
        string sourcePackageChecker = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-source-packages.ps1"));
        string[] offenders = forbiddenSolutionPaths
            .SelectMany(path => new[]
            {
                File.Exists(Path.Combine(repositoryRoot, path))
                    ? $"{path} should not exist; examples are skeleton-owned and build through GMA-Skeleton.slnx"
                    : string.Empty,
                allUpSolution.Contains(path, StringComparison.OrdinalIgnoreCase)
                    ? $"GMA-Skeleton.slnx should not list {path}"
                    : string.Empty,
                validationScript.Contains(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)
                    ? $"eng/gma-validate.ps1 should not validate example solution {Path.GetFileName(path)}"
                    : string.Empty,
                sourcePackageChecker.Contains(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)
                    ? $"eng/check-source-packages.ps1 should not treat example solution {Path.GetFileName(path)} as reusable package"
                    : string.Empty
            })
            .Where(offender => offender.Length > 0)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repository_ignore_rules_keep_local_workspace_state_out_of_source()
    {
        string repositoryRoot = FindRepositoryRoot();
        string gitignore = File.ReadAllText(Path.Combine(repositoryRoot, ".gitignore"));
        string[] requiredTokens =
        [
            ".agents/",
            ".codex/",
            "Gma.SourceRoots.props",
            ".vs/",
            "[Tt]est[Rr]esult*/",
            "[Bb]in/",
            "[Oo]bj/",
            "artifacts/"
        ];
        string[] forbiddenTokens =
        [
            ".config/",
            "dotnet-tools.json"
        ];
        string[] offenders = requiredTokens
            .Where(token => !gitignore.Contains(token, StringComparison.Ordinal))
            .Select(token => $".gitignore missing {token}")
            .Concat(forbiddenTokens
                .Where(token => gitignore.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $".gitignore should not ignore tracked tool manifest token {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Documentation_templates_do_not_ship_unresolved_placeholder_language()
    {
        string repositoryRoot = FindRepositoryRoot();
        string templatesRoot = FrameworkDocsPath(repositoryRoot, "templates");
        string[] forbiddenTokens =
        [
            "TODO",
            "FIXME",
            "TBD",
            "Unknown until implemented"
        ];
        string[] offenders = Directory
            .EnumerateFiles(templatesRoot, "*.md", SearchOption.AllDirectories)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Markdown_local_links_resolve_to_repository_files()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] markdownFiles = EnumerateDocumentationMarkdownFiles(repositoryRoot)
            .Append(Path.Combine(repositoryRoot, "README.md"))
            .Where(path => !HasIgnoredPathSegment(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] offenders = markdownFiles
            .SelectMany(path => FindBrokenMarkdownLocalLinks(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Documentation_index_links_every_docs_page()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] missing = EnumerateDocumentationRoots(repositoryRoot)
            .SelectMany(docsRoot =>
            {
                string indexPath = Path.Combine(docsRoot, "README.md");
                if (!File.Exists(indexPath))
                {
                    return [$"{Path.GetRelativePath(repositoryRoot, docsRoot)} is missing README.md"];
                }

                string indexSource = File.ReadAllText(indexPath);
                string[] expectedDocs = Directory
                    .EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
                    .Where(path => !string.Equals(path, indexPath, StringComparison.OrdinalIgnoreCase))
                    .Where(path => !HasIgnoredPathSegment(path))
                    .Select(path => Path.GetRelativePath(docsRoot, path).Replace('\\', '/'))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                string[] indexedDocs = MarkdownLinkPattern()
                    .Matches(indexSource)
                    .Select(match => match.Groups["target"].Value.Trim())
                    .Where(target => !IsExternalOrAnchorMarkdownTarget(target))
                    .Select(target => target.Split('#')[0].Trim())
                    .Where(target => target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    .Select(target => Path.GetFullPath(Path.Combine(docsRoot, target.Replace('/', Path.DirectorySeparatorChar))))
                    .Where(path => IsUnder(path, docsRoot))
                    .Select(path => Path.GetRelativePath(docsRoot, path).Replace('\\', '/'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return expectedDocs
                    .Except(indexedDocs, StringComparer.OrdinalIgnoreCase)
                    .Select(path => $"{Path.GetRelativePath(repositoryRoot, docsRoot)} missing index link to {path}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Active_docs_and_scaffolder_do_not_reference_removed_shared_owners()
    {
        string repositoryRoot = FindRepositoryRoot();
        Regex[] stalePatterns =
        [
            new(@"\bShared\.ErrorHandling\b", RegexOptions.Compiled),
            new(@"\bShared\.Application\b(?!\.(?:Composition|Events)\b)", RegexOptions.Compiled),
            new(@"\bShared\.Infrastructure\.(?:Caching|Messaging|Tasks|Persistence|Observability|Cqrs|Events|Identity|Time|Tenancy|Runtime)\b", RegexOptions.Compiled)
        ];
        string[] checkedFiles = EnumerateDocumentationMarkdownFiles(repositoryRoot)
            .Where(path => !Path.GetFileName(path).EndsWith("notes.md", StringComparison.OrdinalIgnoreCase))
            .Append(ModuleScaffolderPath(repositoryRoot))
            .Where(path => !HasIgnoredPathSegment(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] offenders = checkedFiles
            .SelectMany(path => File
                .ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    Line = line,
                    LineNumber = index + 1
                }))
            .SelectMany(item => stalePatterns
                .Select(pattern => pattern.Match(item.Line))
                .Where(match => match.Success)
                .Select(match => $"{Path.GetRelativePath(repositoryRoot, item.Path)}:{item.LineNumber}:{match.Value}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Local_request_hosts_and_docs_match_launch_settings()
    {
        string repositoryRoot = FindRepositoryRoot();
        string apiLaunchSettings = Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Properties", "launchSettings.json");
        string adminApiLaunchSettings = Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Properties", "launchSettings.json");

        string apiHttpsUrl = GetLaunchProfileUrls(apiLaunchSettings, "https")
            .Single(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        string apiHttpUrl = GetLaunchProfileUrls(apiLaunchSettings, "https")
            .Single(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        string adminApiHttpsUrl = GetLaunchProfileUrls(adminApiLaunchSettings, "Host.AdminApi")
            .Single(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        string adminApiHttpUrl = GetLaunchProfileUrls(adminApiLaunchSettings, "Host.AdminApi")
            .Single(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        string authRequests = File.ReadAllText(Path.Combine(repositoryRoot, "requests", "auth.http"));
        string adminApiRequests = File.ReadAllText(Path.Combine(repositoryRoot, "requests", "admin-api.http"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string adminApiLaunchJson = File.ReadAllText(adminApiLaunchSettings);

        Assert.Contains($"@host = {apiHttpsUrl}", authRequests, StringComparison.Ordinal);
        Assert.Contains($"@host = {adminApiHttpsUrl}", adminApiRequests, StringComparison.Ordinal);
        Assert.Contains($"API HTTPS: `{apiHttpsUrl}`", setupDocs, StringComparison.Ordinal);
        Assert.Contains($"API HTTP: `{apiHttpUrl}`", setupDocs, StringComparison.Ordinal);
        Assert.Contains($"Admin API HTTPS: `{adminApiHttpsUrl}`", setupDocs, StringComparison.Ordinal);
        Assert.Contains($"Admin API HTTP: `{adminApiHttpUrl}`", setupDocs, StringComparison.Ordinal);
        Assert.Contains("\"launchUrl\": \"swagger\"", adminApiLaunchJson, StringComparison.Ordinal);
        Assert.Contains("\"dotnetRunMessages\": true", adminApiLaunchJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Local_run_scripts_select_development_configuration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string runApi = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "run-api.ps1"));
        string runAdminApi = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "run-admin-api.ps1"));
        string runAdmin = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "run-admin.ps1"));
        string runWorker = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "run-worker.ps1"));
        string appHost = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Hosts", "AppHost", "Program.cs"));

        Assert.Contains("'--launch-profile'", runApi, StringComparison.Ordinal);
        Assert.Contains("'--launch-profile'", runAdminApi, StringComparison.Ordinal);
        Assert.Contains("'Host.AdminApi'", runAdminApi, StringComparison.Ordinal);
        Assert.Contains("DOTNET_ENVIRONMENT", runAdmin, StringComparison.Ordinal);
        Assert.Contains("'Development'", runAdmin, StringComparison.Ordinal);
        Assert.Contains("DOTNET_ENVIRONMENT", runWorker, StringComparison.Ordinal);
        Assert.Contains("'Development'", runWorker, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"ASPNETCORE_ENVIRONMENT\", \"Development\")", appHost, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"DOTNET_ENVIRONMENT\", \"Development\")", appHost, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_api_requests_match_default_generated_password_policy()
    {
        string repositoryRoot = FindRepositoryRoot();
        string adminApiAppsettings = Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "appsettings.json");
        string adminApiRequests = File.ReadAllText(Path.Combine(repositoryRoot, "requests", "admin-api.http"));

        Assert.True(HasRequiredBoolean(
            adminApiAppsettings,
            ["Administration", "Api", "AllowGeneratedPasswordResponses"],
            expected: false));
        Assert.DoesNotContain("\"generatePassword\": true", adminApiRequests, StringComparison.Ordinal);
        Assert.Contains("\"generatePassword\": false", adminApiRequests, StringComparison.Ordinal);
        Assert.Contains("\"password\": \"{{adminPassword}}\"", adminApiRequests, StringComparison.Ordinal);
        Assert.Contains("\"newPassword\": \"{{newAdminPassword}}\"", adminApiRequests, StringComparison.Ordinal);
    }

    [Fact]
    public void Request_samples_do_not_commit_concrete_tokens_or_generated_password_flows()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] requestFiles = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "requests"), "*.http")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] offenders = requestFiles
            .SelectMany(path =>
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                string source = File.ReadAllText(path);
                List<string> fileOffenders = [];

                if (source.Contains("Bearer eyJ", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{relativePath} contains a concrete JWT bearer token");
                }

                if (ConcreteRequestVariablePattern().Matches(source)
                    .Any(match => IsSensitiveRequestVariable(match.Groups["name"].Value)))
                {
                    fileOffenders.Add($"{relativePath} assigns a concrete access or refresh token variable");
                }

                if (source.Contains("\"generatePassword\": true", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{relativePath} uses generated admin password responses");
                }

                return fileOffenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Http_hosts_use_shared_openapi_adapter()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostPrograms =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Program.cs")
        ];
        string[] hostProjects =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Host.Api.csproj"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Host.AdminApi.csproj")
        ];
        string openApiProject = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api.OpenApi",
            "Gma.Framework.Api.OpenApi.csproj"));
        string openApiSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api.OpenApi",
            "DependencyInjection.cs"));

        Assert.Contains("Swashbuckle.AspNetCore", openApiProject, StringComparison.Ordinal);
        Assert.Contains("AddEndpointsApiExplorer", openApiSource, StringComparison.Ordinal);
        Assert.Contains("AddSwaggerGen", openApiSource, StringComparison.Ordinal);
        Assert.Contains("UseSwagger()", openApiSource, StringComparison.Ordinal);
        Assert.Contains("UseSwaggerUI()", openApiSource, StringComparison.Ordinal);

        foreach (string hostProgram in hostPrograms)
        {
            string source = File.ReadAllText(hostProgram);

            Assert.Contains("AddGmaOpenApi()", source, StringComparison.Ordinal);
            Assert.Contains("UseGmaOpenApi()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddEndpointsApiExplorer", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddSwaggerGen", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseSwagger", source, StringComparison.Ordinal);
        }

        foreach (string hostProject in hostProjects)
        {
            Assert.DoesNotContain("Swashbuckle.AspNetCore", File.ReadAllText(hostProject), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Http_hosts_do_not_register_unconfigured_cors()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostPrograms =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Program.cs")
        ];

        foreach (string hostProgram in hostPrograms)
        {
            string source = File.ReadAllText(hostProgram);

            Assert.DoesNotContain("AddCors(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseCors(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Http_hosts_map_health_through_service_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostPrograms =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Program.cs")
        ];
        string serviceDefaults = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "ServiceDefaults",
            "Extensions.cs"));

        Assert.Contains("MapHealthChecks(\"/health\",", serviceDefaults, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/alive\",", serviceDefaults, StringComparison.Ordinal);
        Assert.Contains("registration.Tags.Contains(\"ready\")", serviceDefaults, StringComparison.Ordinal);

        foreach (string hostProgram in hostPrograms)
        {
            string source = File.ReadAllText(hostProgram);

            Assert.Contains("MapDefaultEndpoints()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MapHealthChecks(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Http_hosts_use_shared_api_security_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostPrograms =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Program.cs")
        ];
        string sharedSecurity = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api",
            "Security",
            "ApiSecurityServiceCollectionExtensions.cs"));

        Assert.Contains("AddAuthentication()", sharedSecurity, StringComparison.Ordinal);
        Assert.Contains("AddAuthorization()", sharedSecurity, StringComparison.Ordinal);

        foreach (string hostProgram in hostPrograms)
        {
            string source = File.ReadAllText(hostProgram);

            Assert.Contains("AddApiSecurityDefaults()", source, StringComparison.Ordinal);
            Assert.Contains("UseAuthentication()", source, StringComparison.Ordinal);
            Assert.Contains("UseAuthorization()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddAuthentication(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddAuthorization(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Production_sources_use_shared_claim_name_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string claimNamesPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Security",
            "ApplicationClaimNames.cs");
        string compatibilityClaimNamesPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Security",
            "GmaClaimNames.cs");
        string[] rawClaimNameLiterals =
        [
            "\"tenant_id\"",
            "\"sid\"",
            "\"sub\""
        ];
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsTestSourcePath(path))
            .Where(path => !string.Equals(path, claimNamesPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(path, compatibilityClaimNamesPath, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return rawClaimNameLiterals
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_contracts_do_not_hardcode_default_application_namespace_subjects()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");

        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.EndsWith(".Contracts", StringComparison.Ordinal)))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                List<string> fileOffenders = [];
                if (source.Contains("\"gma.", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)}:\"gma.");
                }

                if (source.Contains("\"GMA.", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)}:\"GMA.");
                }

                return fileOffenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Auth_jwt_bearer_adapter_is_explicitly_http_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        string authApiModule = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Api",
            "AuthModule.cs"));
        string authAdminApiModule = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.AdminApi",
            "AuthAdminApiModule.cs"));
        string authAdminCliModule = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.AdminCli",
            "AuthAdminCliModule.cs"));
        string authInfrastructureProject = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "Gma.Modules.Auth.Infrastructure.csproj"));
        string authInfrastructureSource = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "DependencyInjection.cs"));
        string authJwtBearerProject = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure.JwtBearer",
            "Gma.Modules.Auth.Infrastructure.JwtBearer.csproj"));
        string authAdminCliProject = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.AdminCli",
            "Gma.Modules.Auth.AdminCli.csproj"));
        string authApiProject = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Api",
            "Gma.Modules.Auth.Api.csproj"));
        string authAdminApiProject = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.AdminApi",
            "Gma.Modules.Auth.AdminApi.csproj"));

        Assert.Contains("AddAuthInfrastructure(builder.Configuration)", authApiModule, StringComparison.Ordinal);
        Assert.Contains("AddAuthJwtBearerAuthentication()", authApiModule, StringComparison.Ordinal);
        Assert.Contains("AddAuthInfrastructure(builder.Configuration)", authAdminApiModule, StringComparison.Ordinal);
        Assert.Contains("AddAuthJwtBearerAuthentication()", authAdminApiModule, StringComparison.Ordinal);
        Assert.Contains("AddAuthInfrastructure(builder.Configuration)", authAdminCliModule, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAuthJwtBearerAuthentication()", authAdminCliModule, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.Authentication.JwtBearer", authInfrastructureProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", authInfrastructureProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Extensions.Hosting", authInfrastructureProject, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostApplicationBuilder", authInfrastructureSource, StringComparison.Ordinal);
        Assert.Contains("IServiceCollection AddAuthInfrastructure", authInfrastructureSource, StringComparison.Ordinal);
        Assert.Contains("System.IdentityModel.Tokens.Jwt", authInfrastructureProject, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.Identity.Core", authInfrastructureProject, StringComparison.Ordinal);
        Assert.Contains("Microsoft.AspNetCore.Authentication.JwtBearer", authJwtBearerProject, StringComparison.Ordinal);
        Assert.Contains("Gma.Modules.Auth.Infrastructure.csproj", authJwtBearerProject, StringComparison.Ordinal);
        Assert.Contains("Gma.Modules.Auth.Infrastructure.JwtBearer.csproj", authApiProject, StringComparison.Ordinal);
        Assert.Contains("Gma.Modules.Auth.Infrastructure.JwtBearer.csproj", authAdminApiProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Gma.Modules.Auth.Infrastructure.JwtBearer.csproj", authAdminCliProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Http_hosts_use_shared_request_logging_enrichment()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostPrograms =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Program.cs")
        ];
        string[] hostProjects =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Host.Api.csproj"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "Host.AdminApi.csproj")
        ];
        string sharedApiProject = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api",
            "Gma.Framework.Api.csproj"));
        string serilogHostAdapterProject = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Logging.Serilog",
            "Gma.Framework.Logging.Serilog.csproj"));
        string serilogAdapterProject = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api.Serilog",
            "Gma.Framework.Api.Serilog.csproj"));
        string tenantSerilogBridgeProject = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Tenancy.Api.Serilog",
            "Gma.Framework.Tenancy.Api.Serilog.csproj"));
        string sharedExtension = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api.Serilog",
            "RequestLoggingApplicationBuilderExtensions.cs"));

        Assert.DoesNotContain("Serilog", sharedApiProject, StringComparison.Ordinal);
        Assert.Contains("Serilog.AspNetCore", serilogHostAdapterProject, StringComparison.Ordinal);
        Assert.Contains("Serilog.Settings.Configuration", serilogHostAdapterProject, StringComparison.Ordinal);
        Assert.Contains("Serilog.Sinks.Console", serilogHostAdapterProject, StringComparison.Ordinal);
        Assert.Contains("Serilog.AspNetCore", serilogAdapterProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Gma.Framework.Tenancy", serilogAdapterProject, StringComparison.Ordinal);
        Assert.Contains("Gma.Framework.Tenancy", tenantSerilogBridgeProject, StringComparison.Ordinal);
        Assert.Contains("UseSerilogRequestLogging", sharedExtension, StringComparison.Ordinal);
        Assert.Contains("EnrichDiagnosticContext", sharedExtension, StringComparison.Ordinal);

        foreach (string hostProgram in hostPrograms)
        {
            string source = File.ReadAllText(hostProgram);

            Assert.Contains("UseConfiguredSerilog()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseSerilog(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadFrom.Configuration", source, StringComparison.Ordinal);
            Assert.Contains("AddTenantSerilogRequestLogging()", source, StringComparison.Ordinal);
            Assert.Contains("UseGmaSerilogRequestLogging()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseSerilogRequestLogging", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EnrichDiagnosticContext", source, StringComparison.Ordinal);
        }

        foreach (string hostProject in hostProjects)
        {
            string project = File.ReadAllText(hostProject);

            Assert.DoesNotContain("Serilog.AspNetCore", project, StringComparison.Ordinal);
            Assert.DoesNotContain("Serilog.Settings.Configuration", project, StringComparison.Ordinal);
            Assert.DoesNotContain("Serilog.Sinks.Console", project, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Production_reflection_surface_is_documented_and_confined()
    {
        string repositoryRoot = FindRepositoryRoot();
        string srcRoot = Path.Combine(repositoryRoot, "src");
        string notes = File.ReadAllText(FrameworkDocsPath(repositoryRoot, "architecture", "audit-hardening-notes.md"));
        string[] requiredDocTokens =
        [
            "RequestDispatcher",
            "DomainEventDispatcher",
            "IntegrationEventHandlerInvoker",
            "TaskHandlerInvoker",
            "ModuleMetadataAttributeReader",
            "ApplicationServiceCollectionExtensions",
            "ApplyConfigurationsFromAssembly",
            "ScopedEntityTypeBuilderExtensions",
            "host assembly marker classes",
            "observability module-name inference",
            "Do not use reflection or attributes to auto-register modules"
        ];
        HashSet<string> allowedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(Path.Combine("src", "Framework", "Application", "Gma.Framework.Application.Composition", "ApplicationServiceCollectionExtensions.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Cqrs", "Gma.Framework.Cqrs.Infrastructure", "RequestDispatcher.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Application", "Gma.Framework.Application.Events.Infrastructure", "DomainEventDispatcher.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Modules", "Gma.Framework.Modules", "ModuleMetadataAttributeReader.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Messaging", "Gma.Framework.Messaging.Infrastructure", "IntegrationEventHandlerInvoker.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Tasks", "Gma.Framework.Tasks.Infrastructure", "TaskHandlerInvoker.cs")),
            NormalizePath(Path.Combine("src", "Framework", "Persistence", "Gma.Framework.Persistence.EntityFrameworkCore", "ScopedEntityTypeBuilderExtensions.cs")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.Api", "ApiAssemblyReference.cs")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.AdminApi", "AdminApiAssemblyReference.cs")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.AdminCli", "AdminCliAssemblyReference.cs")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.Worker", "WorkerAssemblyReference.cs")),
            NormalizePath(Path.Combine("src", "Modules", "AccessControl", "Gma.Modules.AccessControl.Persistence", "AccessControlDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "Administration", "Gma.Modules.Administration.Persistence", "AdminDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "Auth", "Gma.Modules.Auth.Persistence", "AuthDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "Catalog", "Catalog.Persistence", "CatalogDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "Notifications", "Gma.Modules.Notifications.Persistence", "NotificationsDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "Ordering", "Ordering.Persistence", "OrderingDbContext.cs")),
            NormalizePath(Path.Combine("src", "Modules", "TaskRuntime", "Gma.Modules.TaskRuntime.Persistence", "TaskRuntimeDbContext.cs"))
        };
        string[] reflectionTokens =
        [
            "using System.Reflection",
            "System.Linq.Expressions",
            "MakeGenericMethod",
            "MakeGenericType",
            "Activator.CreateInstance",
            ".GetTypes(",
            "GetCustomAttribute",
            "GetCustomAttributes",
            "ApplyConfigurationsFromAssembly"
        ];

        string[] documentationOffenders = requiredDocTokens
            .Where(token => !notes.Contains(token, StringComparison.Ordinal))
            .Select(token => $"audit-hardening-notes.md missing {token}")
            .ToArray();
        string[] sourceOffenders = EnumerateSourceFiles(srcRoot)
            .Where(path => !IsTestSourcePath(path))
            .Select(path => new
            {
                Path = path,
                RelativePath = NormalizePath(CanonicalRelativePath(repositoryRoot, path)),
                Source = File.ReadAllText(path)
            })
            .Where(item => reflectionTokens.Any(token => item.Source.Contains(token, StringComparison.Ordinal)))
            .Where(item => !allowedRelativePaths.Contains(item.RelativePath))
            .Select(item => item.RelativePath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(documentationOffenders.Concat(sourceOffenders));
    }

    [Fact]
    public void Repository_build_policy_enforces_warnings_and_nuget_audit()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        string property(string name) =>
            props.Descendants(name).SingleOrDefault()?.Value.Trim() ?? string.Empty;
        string warningsAsErrors = property("WarningsAsErrors");

        Assert.Equal("true", property("TreatWarningsAsErrors"));
        Assert.Equal("true", property("NuGetAudit"));
        Assert.Equal("all", property("NuGetAuditMode"));
        Assert.Equal("low", property("NuGetAuditLevel"));

        foreach (string warningCode in new[] { "NU1901", "NU1902", "NU1903", "NU1904" })
        {
            Assert.Contains(warningCode, warningsAsErrors, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Repository_sdk_policy_targets_dotnet_10_consistently()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "global.json")));
        XDocument props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        string commonScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "common.ps1"));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");
        string sdkVersion = sdk.GetProperty("version").GetString() ?? string.Empty;

        Assert.StartsWith("10.", sdkVersion, StringComparison.Ordinal);
        Assert.Equal("latestFeature", sdk.GetProperty("rollForward").GetString());
        Assert.Equal("net10.0", props.Descendants("TargetFramework").Single().Value.Trim());
        Assert.Contains($"SDK `{sdkVersion}`", setupDocs, StringComparison.Ordinal);
        Assert.Contains("GMA-Skeleton is a .NET 10 modular monolith skeleton", readme, StringComparison.Ordinal);
        Assert.Contains("$version -match '^10\\.'", commonScript, StringComparison.Ordinal);
        Assert.Contains("Could not resolve a .NET 10 SDK", commonScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Package_versions_are_centralized_unique_and_stable()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] inlineVersionOffenders = EnumerateProjectFiles(Path.Combine(repositoryRoot, "src"))
            .Concat(EnumerateProjectFiles(Path.Combine(repositoryRoot, "tests")))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);

                return project
                    .Descendants("PackageReference")
                    .Where(reference =>
                        reference.Attribute("Version") is not null ||
                        reference.Elements("Version").Any())
                    .Select(reference =>
                        $"{CanonicalRelativePath(repositoryRoot, projectPath)}:{reference.Attribute("Include")?.Value ?? "PackageReference"}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        XDocument packages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        string[] duplicatePackageVersions = packages
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .GroupBy(packageId => packageId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] prereleasePackageVersions = packages
            .Descendants("PackageVersion")
            .Where(element => element.Attribute("Version")?.Value.Contains('-', StringComparison.Ordinal) == true)
            .Select(element => $"{element.Attribute("Include")?.Value}:{element.Attribute("Version")?.Value}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        HashSet<string> referencedPackages = EnumerateProjectFiles(Path.Combine(repositoryRoot, "src"))
            .Concat(EnumerateProjectFiles(Path.Combine(repositoryRoot, "tests")))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                return project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => packageId!);
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] unusedCentralPackageVersions = packages
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Where(packageId => !referencedPackages.Contains(packageId!))
            .Select(packageId => packageId!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(inlineVersionOffenders
            .Concat(duplicatePackageVersions.Select(packageId => $"Duplicate central package version: {packageId}"))
            .Concat(prereleasePackageVersions.Select(packageId => $"Prerelease central package version: {packageId}"))
            .Concat(unusedCentralPackageVersions.Select(packageId => $"Unused central package version: {packageId}"))
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dotnet_ef_tool_manifest_matches_entity_framework_design_package()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument tools = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            ".config",
            "dotnet-tools.json")));
        XDocument packages = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        string efDesignVersion = packages
            .Descendants("PackageVersion")
            .Single(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Microsoft.EntityFrameworkCore.Design",
                StringComparison.Ordinal))
            .Attribute("Version")!
            .Value;
        JsonElement root = tools.RootElement;
        JsonElement dotnetEf = root
            .GetProperty("tools")
            .GetProperty("dotnet-ef");
        string restoreScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "restore.ps1"));
        string addMigrationScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "add-migration.ps1"));
        string checkMigrationsScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-migrations.ps1"));
        string persistenceDocs = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "architecture",
            "persistence-and-tenancy.md"));

        Assert.True(root.GetProperty("isRoot").GetBoolean());
        Assert.Equal(efDesignVersion, dotnetEf.GetProperty("version").GetString());
        Assert.Contains(dotnetEf.GetProperty("commands").EnumerateArray(), command =>
            string.Equals(command.GetString(), "dotnet-ef", StringComparison.Ordinal));
        Assert.Contains("'tool', 'restore'", restoreScript, StringComparison.Ordinal);
        Assert.Contains("'tool', 'restore'", addMigrationScript, StringComparison.Ordinal);
        Assert.Contains("src\\Modules", addMigrationScript, StringComparison.Ordinal);
        Assert.Contains("gma\\modules", addMigrationScript, StringComparison.Ordinal);
        Assert.Contains("'tool', 'restore'", checkMigrationsScript, StringComparison.Ordinal);
        Assert.Contains("'tool',", checkMigrationsScript, StringComparison.Ordinal);
        Assert.Contains("'run',", checkMigrationsScript, StringComparison.Ordinal);
        Assert.Contains("'dotnet-ef',", checkMigrationsScript, StringComparison.Ordinal);
        Assert.Contains("gma\\modules", checkMigrationsScript, StringComparison.Ordinal);
        Assert.Contains("pinned local `dotnet-ef` tool", persistenceDocs, StringComparison.Ordinal);
    }

    [Fact]
    public void Test_projects_are_discoverable_non_packable_and_keep_runner_private()
    {
        string repositoryRoot = FindRepositoryRoot();
        string testsRoot = Path.Combine(repositoryRoot, "tests");
        string[] offenders = Directory
            .EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, projectPath));
                string? property(string propertyName) =>
                    project.Descendants(propertyName).SingleOrDefault()?.Value.Trim();
                bool hasPackage(string packageId) =>
                    project
                        .Descendants("PackageReference")
                        .Any(reference => string.Equals(reference.Attribute("Include")?.Value, packageId, StringComparison.Ordinal));
                XElement? runnerReference = project
                    .Descendants("PackageReference")
                    .SingleOrDefault(reference => string.Equals(
                        reference.Attribute("Include")?.Value,
                        "xunit.runner.visualstudio",
                        StringComparison.Ordinal));
                List<string> failures = [];

                if (!string.Equals(property("IsTestProject"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("missing IsTestProject=true");
                }

                if (!string.Equals(property("IsPackable"), "false", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("missing IsPackable=false");
                }

                if (!hasPackage("Microsoft.NET.Test.Sdk"))
                {
                    failures.Add("missing Microsoft.NET.Test.Sdk");
                }

                if (!hasPackage("xunit"))
                {
                    failures.Add("missing xunit");
                }

                if (runnerReference is null)
                {
                    failures.Add("missing xunit.runner.visualstudio");
                }
                else if (!string.Equals(
                             runnerReference.Element("PrivateAssets")?.Value.Trim(),
                             "all",
                             StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("xunit.runner.visualstudio missing PrivateAssets=all");
                }

                return failures.Select(failure => $"{relativePath}:{failure}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Production_sources_do_not_disable_collection_initializer_style()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => File.ReadAllText(path).Contains("#pragma warning disable IDE0028", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Solution_folders_are_unique_per_parent_folder()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument solution = XDocument.Load(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));

        string[] duplicateFolders = solution
            .Descendants("Folder")
            .Select(folder => folder.Attribute("Name")?.Value ?? string.Empty)
            .Where(folderName => folderName.Length > 0)
            .GroupBy(folderName => folderName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(duplicateFolders);
    }

    [Fact]
    public void Test_projects_are_listed_under_test_solution_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument solution = XDocument.Load(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));

        string[] offenders = solution
            .Descendants("Folder")
            .SelectMany(folder =>
            {
                string folderName = folder.Attribute("Name")?.Value ?? string.Empty;
                return folder
                    .Elements("Project")
                    .Select(project => new
                    {
                        FolderName = folderName,
                        ProjectPath = NormalizePath(project.Attribute("Path")?.Value ?? string.Empty)
                    });
            })
            .Where(project => project.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                              (project.ProjectPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                               project.ProjectPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase)))
            .Where(project => !project.FolderName.EndsWith("/tests/", StringComparison.Ordinal))
            .Select(project => $"{project.ProjectPath} is listed under {project.FolderName}, expected a /tests/ solution folder.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Framework_source_projects_are_nested_under_framework_feature_solution_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument solution = XDocument.Load(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));

        string[] offenders = solution
            .Descendants("Folder")
            .SelectMany(folder =>
            {
                string folderName = folder.Attribute("Name")?.Value ?? string.Empty;
                return folder
                    .Elements("Project")
                    .Select(project => new
                    {
                        FolderName = folderName,
                        ProjectPath = NormalizePath(project.Attribute("Path")?.Value ?? string.Empty)
                    });
            })
            .Where(project => project.ProjectPath.StartsWith("gma/framework/src/", StringComparison.OrdinalIgnoreCase) &&
                              !project.ProjectPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase) &&
                              project.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(project => !project.FolderName.StartsWith("/gma/framework/src/", StringComparison.Ordinal) ||
                              string.Equals(project.FolderName, "/gma/framework/src/", StringComparison.Ordinal))
            .Select(project => $"{project.ProjectPath} is listed under {project.FolderName}, expected a /gma/framework/src/<feature>/ folder.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_source_projects_are_nested_under_role_solution_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument solution = XDocument.Load(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));

        string[] offenders = solution
            .Descendants("Folder")
            .SelectMany(folder =>
            {
                string folderName = folder.Attribute("Name")?.Value ?? string.Empty;
                return folder
                    .Elements("Project")
                    .Select(project => new
                    {
                        FolderName = folderName,
                        ProjectPath = NormalizePath(project.Attribute("Path")?.Value ?? string.Empty)
                    });
            })
            .Where(project =>
                project.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                (project.ProjectPath.StartsWith("gma/modules/", StringComparison.OrdinalIgnoreCase) ||
                 project.ProjectPath.StartsWith("src/Modules/", StringComparison.OrdinalIgnoreCase)) &&
                project.ProjectPath.Contains("/src/", StringComparison.OrdinalIgnoreCase))
            .Where(project => project.FolderName.EndsWith("/src/", StringComparison.Ordinal))
            .Select(project => $"{project.ProjectPath} is listed under flat {project.FolderName}, expected a /src/<role>/ solution folder.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Legacy_solution_file_is_removed_after_slnx_migration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string legacySolutionFileName = string.Concat("GMA-Skeleton", ".sln");
        string[] offenders = Directory
            .EnumerateFiles(repositoryRoot, "*.sln", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Concat(Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
                .Where(path => !HasIgnoredPathSegment(path))
                .Where(path => !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .SelectMany(path =>
                {
                    string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));
                    string text = File.ReadAllText(path);
                    return LegacySolutionReferencePattern().IsMatch(text)
                        ? [$"{relativePath} references legacy {legacySolutionFileName}"]
                        : Array.Empty<string>();
                }))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Source_root_configuration_supports_source_first_submodule_layouts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string buildProps = File.ReadAllText(Path.Combine(repositoryRoot, "Directory.Build.props"));
        string sourceRootsExample = File.ReadAllText(Path.Combine(repositoryRoot, "Gma.SourceRoots.props.example"));
        string[] requiredProperties =
        [
            "GmaFrameworkRoot",
            "GmaModulesRoot",
            "GmaModuleAccessControlRoot",
            "GmaModuleAdministrationRoot",
            "GmaModuleAuthRoot",
            "GmaModuleCatalogRoot",
            "GmaModuleFilesRoot",
            "GmaModuleNotificationsRoot",
            "GmaModuleOrderingRoot",
            "GmaModuleTaskRuntimeRoot",
            "GmaModuleTaskSamplesRoot",
            "GmaModuleTenancyRoot"
        ];
        string[] offenders = requiredProperties
            .Where(property => !buildProps.Contains(property, StringComparison.Ordinal) ||
                               !sourceRootsExample.Contains(property, StringComparison.Ordinal))
            .Select(property => $"Missing source-root property {property}.")
            .Concat(!buildProps.Contains("Gma.SourceRoots.props", StringComparison.Ordinal)
                ? ["Directory.Build.props does not import Gma.SourceRoots.props."]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Gma_bootstrap_supports_flattened_submodule_source_roots()
    {
        string repositoryRoot = FindRepositoryRoot();
        string bootstrapScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "gma-bootstrap.ps1"));
        string statusScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "gma-status.ps1"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string[] requiredBootstrapTokens =
        [
            "SupportsShouldProcess",
            "SourceLayout",
            "GmaSubmodules",
            @"gma\framework\src\",
            @"gma\modules\",
            @"$(GmaModulesRoot)access-control\src\",
            @"$(GmaModulesRoot)auth\src\",
            @"..\..\framework\src\",
            @"gma\modules\$moduleAlias\Gma.SourceRoots.props",
            "task-runtime"
        ];
        string[] requiredStatusTokens =
        [
            "GMA source package roots:",
            "eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules",
            @"gma\modules\auth\Gma.SourceRoots.props"
        ];
        string[] requiredDocsTokens =
        [
            "gma-bootstrap.ps1 -SourceLayout GmaSubmodules",
            "gma/framework",
            "gma/modules/<alias>",
            "module-local files are required"
        ];
        string[] offenders = requiredBootstrapTokens
            .Where(token => !bootstrapScript.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/gma-bootstrap.ps1 missing {token}")
            .Concat(requiredStatusTokens
                .Where(token => !statusScript.Contains(token, StringComparison.Ordinal))
                .Select(token => $"eng/gma-status.ps1 missing {token}"))
            .Concat(requiredDocsTokens
                .Where(token => !setupDocs.Contains(token, StringComparison.Ordinal))
                .Select(token => $"docs/getting-started/setup.md missing {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Github_actions_are_wired_for_fast_and_docker_validation()
    {
        string repositoryRoot = FindRepositoryRoot();
        string validateWorkflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "validate.yml"));
        string dockerWorkflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "docker-tests.yml"));
        string commonScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "common.ps1"));
        string readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string[] requiredValidateTokens =
        [
            "name: Validate",
            "pull_request:",
            "branches:",
            "- main",
            "- dev",
            "workflow_dispatch:",
            "concurrency:",
            "cancel-in-progress: true",
            "DOTNET_CLI_TELEMETRY_OPTOUT",
            "os: [windows-latest, ubuntu-latest]",
            "runs-on: ${{ matrix.os }}",
            "timeout-minutes: 30",
            "./eng/check-submodule-dev-heads.ps1",
            "./eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules -Force",
            "./eng/verify.ps1"
        ];
        string[] requiredDockerTokens =
        [
            "name: Docker Tests",
            "workflow_dispatch:",
            "GMA_REQUIRE_DOCKER_TESTS: 'true'",
            "runs-on: ubuntu-latest",
            "timeout-minutes: 45",
            "cancel-in-progress: false",
            "docker version",
            "./eng/check-submodule-dev-heads.ps1",
            "./eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules -Force",
            "./eng/restore.ps1",
            "./eng/build.ps1 -NoRestore",
            "./eng/test-docker.ps1 -NoBuild"
        ];
        string[] requiredDocsTokens =
        [
            "actions/workflows/validate.yml",
            "actions/workflows/docker-tests.yml",
            "The `Validate` workflow",
            "The `Docker Tests` workflow",
            ".github\\workflows\\docker-tests.yml",
            "GMA_REQUIRE_DOCKER_TESTS=true"
        ];

        string[] offenders = requiredValidateTokens
            .Where(token => !validateWorkflow.Contains(token, StringComparison.Ordinal))
            .Select(token => ".github/workflows/validate.yml missing " + token)
            .Concat(requiredDockerTokens
                .Where(token => !dockerWorkflow.Contains(token, StringComparison.Ordinal))
                .Select(token => ".github/workflows/docker-tests.yml missing " + token))
            .Concat(requiredDocsTokens
                .Where(token => !readme.Contains(token, StringComparison.Ordinal) &&
                                !setupDocs.Contains(token, StringComparison.Ordinal))
                .Select(token => $"GitHub Actions docs missing {token}"))
            .Concat(!commonScript.Contains("Path]::DirectorySeparatorChar", StringComparison.Ordinal)
                ? ["eng/common.ps1 should normalize separators for Linux GitHub Actions runners."]
                : [])
            .Concat(!solution.Contains(".github/workflows/docker-tests.yml", StringComparison.Ordinal)
                ? ["GMA-Skeleton.slnx missing .github/workflows/docker-tests.yml"]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Skeleton_submodule_dev_head_guard_is_wired_into_validation()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-submodule-dev-heads.ps1"));
        string workflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "validate.yml"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string gitmodules = File.ReadAllText(Path.Combine(repositoryRoot, ".gitmodules"));
        string[] submodulePaths =
        [
            "gma/framework",
            "gma/modules/administration",
            "gma/modules/auth",
            "gma/modules/files",
            "gma/modules/notifications",
            "gma/modules/task-runtime",
            "gma/modules/tenancy"
        ];
        string[] requiredScriptTokens =
        [
            "ls-remote",
            "refs/heads/$Branch",
            "HEAD:$relativePath",
            "submodule.$name.branch",
            "diff', '--cached', '--quiet'",
            "status', '--porcelain'",
            "gma-update.ps1 -Remote",
            "All GMA submodule pointers match origin/$Branch"
        ];
        string[] requiredWorkflowTokens =
        [
            "Check GMA submodule dev heads",
            "./eng/check-submodule-dev-heads.ps1",
            "./eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules -Force",
            "./eng/verify.ps1"
        ];
        string[] requiredDocsTokens =
        [
            "check-submodule-dev-heads.ps1",
            "origin/dev",
            "gma-update.ps1 -Remote",
            "Commit the skeleton's changed submodule pointer"
        ];

        string[] offenders = submodulePaths
            .Where(path => !Regex.IsMatch(
                gitmodules,
                $@"\[submodule ""{Regex.Escape(path)}""\]\s+path = {Regex.Escape(path)}\s+url = .+\s+branch = dev",
                RegexOptions.Multiline))
            .Select(path => $".gitmodules missing dev branch metadata for {path}")
            .Concat(requiredScriptTokens
                .Where(token => !script.Contains(token, StringComparison.Ordinal))
                .Select(token => $"eng/check-submodule-dev-heads.ps1 missing {token}"))
            .Concat(requiredWorkflowTokens
                .Where(token => !workflow.Contains(token, StringComparison.Ordinal))
                .Select(token => $".github/workflows/validate.yml missing {token}"))
            .Concat(requiredDocsTokens
                .Where(token => !setupDocs.Contains(token, StringComparison.Ordinal))
                .Select(token => $"docs/getting-started/setup.md missing {token}"))
            .Concat(!solution.Contains("eng/check-submodule-dev-heads.ps1", StringComparison.Ordinal)
                ? ["GMA-Skeleton.slnx missing eng/check-submodule-dev-heads.ps1"]
                : [])
            .Concat(gitmodules.Contains("github.com-private", StringComparison.Ordinal)
                ? [".gitmodules should use portable GitHub URLs instead of a local SSH host alias."]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Completed_source_split_migration_scripts_are_not_live_workflow()
    {
        string repositoryRoot = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string sourceFirstAppsDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "source-first-apps.md"));
        string appGenerator = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string[] obsoleteLiveTokens =
        [
            "gma-github-stage8.ps1",
            "gma-stage9.ps1",
            "UseLocalStage8Candidates",
            "StageRoot",
            "Add-GmaTemplateJunction"
        ];

        string[] offenders = obsoleteLiveTokens
            .Where(token => File.Exists(Path.Combine(repositoryRoot, "eng", token)))
            .Select(token => $"eng/{token} should not exist in the live script surface.")
            .Concat(obsoleteLiveTokens
                .Where(token => solution.Contains(token, StringComparison.Ordinal))
                .Select(token => $"GMA-Skeleton.slnx should not list obsolete token {token}."))
            .Concat(obsoleteLiveTokens
                .Where(token => setupDocs.Contains(token, StringComparison.Ordinal) ||
                                sourceFirstAppsDocs.Contains(token, StringComparison.Ordinal))
                .Select(token => $"Getting-started docs should not advertise obsolete token {token}."))
            .Concat(obsoleteLiveTokens
                .Where(token => appGenerator.Contains(token, StringComparison.Ordinal))
                .Select(token => $"eng/new-gma-app.ps1 should not contain obsolete token {token}."))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Stage10_production_app_template_helper_is_guarded_and_documented()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-gma-app.ps1"));
        string rootWorkflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "validate.yml"));
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string sourceFirstAppsDocs = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "getting-started",
            "source-first-apps.md"));
        string splitPlan = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "architecture",
            "gma-rebrand-and-source-repo-split.md"));
        string slnx = File.ReadAllText(Path.Combine(repositoryRoot, "GMA-Skeleton.slnx"));
        string[] requiredTokens =
        [
            "SupportsShouldProcess",
            "Get-GmaSelectedModuleSpecs",
            "Unknown GMA module alias",
            "already exists with different contents",
            "packageVersionTemplate",
            "Get-Content -LiteralPath (Join-GmaPath 'Directory.Packages.props')",
            "selectedModuleAliases",
            "submoduleCommandLines",
            "docs\\gma-source.md",
            "GMA Source Packages",
            "Add Source Repositories",
            "https://github.com/SadPossum/GMA-Framework.git",
            "Use equivalent SSH or fork URLs",
            "GMA source-package workflow notes live in",
            "dotnet run --project",
            "add or mount GMA source packages",
            "src\\Hosts",
            "src\\Modules",
            "src\\Shared",
            "src\\Hosts\\README.md",
            "src\\Modules\\README.md",
            "src\\Shared\\README.md",
            "src/Hosts/$Name.Host.Api/$Name.Host.Api.csproj",
            "src/Shared/$Name.SharedKernel/$Name.SharedKernel.csproj",
            "..\\..\\Shared\\$Name.SharedKernel\\$Name.SharedKernel.csproj",
            "Selected reusable GMA modules for this shell",
            "foreach ($moduleSpec in $selectedModuleSpecArray)",
            "SharedKernel",
            "Gma.Framework.Api",
            "Gma.Framework.Infrastructure",
            "Gma.Framework.ModuleComposition",
            "PublicApiProject",
            "Public API modules composed by",
            "AddApiSecurityDefaults",
            "ValidateModuleComposition",
            "AddAuthModule(AuthProfile.Global())",
            "AddAuthModule(AuthProfile.ScopeAware())",
            "Gma.SourceRoots.props.example",
            "gma\\framework",
            "gma\\modules",
            "eng\\gma-bootstrap.ps1",
            "eng\\gma-update.ps1",
            "eng\\gma-validate.ps1",
            "Write-GmaGeneratedWorkflow",
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            "actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1",
            "rev-parse --show-toplevel",
            "Git status: app repository not initialized"
        ];
        string[] requiredRootWorkflowTokens =
        [
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            "submodules: recursive",
            "secrets.GMA_CI_TOKEN",
            "actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1",
            "dotnet-version: 10.0.x",
            "./eng/check-submodule-dev-heads.ps1",
            "./eng/gma-bootstrap.ps1 -SourceLayout GmaSubmodules -Force",
            "./eng/verify.ps1"
        ];
        string[] requiredDocsTokens =
        [
            "new-gma-app.ps1",
            "docs/gma-source.md",
            "https://github.com/SadPossum/GMA-Framework.git",
            "src/Hosts",
            "src/Modules",
            "src/Shared",
            "-Modules auth,notifications",
            "-Modules all",
            "gma-bootstrap.ps1 -Force",
            ".github\\workflows\\validate.yml",
            "GMA_CI_TOKEN",
            "submodules: recursive",
            "SharedKernel",
            "public `IModule` front door",
            "Public API modules",
            "Admin CLI/API and worker-only",
            "Cloud credentials",
            "gma\\framework",
            "gma\\modules",
            "detached `HEAD`",
            "Upstream Or App-Local"
        ];

        string[] offenders = requiredTokens
            .Where(token => !script.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/new-gma-app.ps1 missing {token}")
            .Concat(requiredRootWorkflowTokens
                .Where(token => !rootWorkflow.Contains(token, StringComparison.Ordinal))
                .Select(token => $".github/workflows/validate.yml missing {token}"))
            .Concat(requiredDocsTokens
                .Where(token => !setupDocs.Contains(token, StringComparison.Ordinal) &&
                                !splitPlan.Contains(token, StringComparison.Ordinal) &&
                                !sourceFirstAppsDocs.Contains(token, StringComparison.Ordinal))
                .Select(token => $"Stage 10 docs missing {token}"))
            .Concat(!slnx.Contains("eng/new-gma-app.ps1", StringComparison.Ordinal)
                ? ["GMA-Skeleton.slnx missing eng/new-gma-app.ps1"]
                : [])
            .Concat(!slnx.Contains("docs/getting-started/source-first-apps.md", StringComparison.Ordinal)
                ? ["GMA-Skeleton.slnx missing docs/getting-started/source-first-apps.md"]
                : [])
            .Concat(!slnx.Contains(".github/workflows/validate.yml", StringComparison.Ordinal)
                ? ["GMA-Skeleton.slnx missing .github/workflows/validate.yml"]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
        Assert.DoesNotContain(
            "This app is generated as a source-first GMA composition shell.",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "src/$Name.Host.Api/$Name.Host.Api.csproj",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "src/$Name.SharedKernel/$Name.SharedKernel.csproj",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void External_framework_project_references_use_source_root_property()
    {
        string repositoryRoot = FindRepositoryRoot();
        string frameworkRoot = GmaSourceLayout.FrameworkPath(repositoryRoot);
        string[] offenders = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => (IsUnder(path, Path.Combine(repositoryRoot, "src")) ||
                            IsUnder(path, Path.Combine(repositoryRoot, "tests"))) &&
                           !IsUnder(path, frameworkRoot))
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(path =>
            {
                XDocument project = XDocument.Load(path);
                string relativeProjectPath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));

                return project
                    .Descendants("ProjectReference")
                    .Select(reference => reference.Attribute("Include")?.Value)
                    .Where(referencePath => !string.IsNullOrWhiteSpace(referencePath))
                    .Where(referencePath =>
                        referencePath!.Contains("Framework", StringComparison.OrdinalIgnoreCase) ||
                        referencePath.Contains("Gma.Framework", StringComparison.Ordinal))
                    .Where(referencePath => !referencePath!.StartsWith("$(GmaFrameworkRoot)", StringComparison.Ordinal))
                    .Select(referencePath => $"{relativeProjectPath}->{referencePath}");
            })
            .Concat(FindRawFrameworkReferencesInModuleGenerator(repositoryRoot))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Naming_conventions_document_all_framework_project_names()
    {
        string repositoryRoot = FindRepositoryRoot();
        string frameworkRoot = GmaSourceLayout.FrameworkPath(repositoryRoot);
        string namingConventions = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "naming-conventions.md"));
        string[] offenders = EnumerateProjectFiles(frameworkRoot)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(projectName => projectName is not null && !projectName.EndsWith(".Tests", StringComparison.Ordinal))
            .Where(projectName => projectName is not null && !namingConventions.Contains(projectName, StringComparison.Ordinal))
            .Select(projectName => $"src/Framework/docs/guidelines/naming-conventions.md missing {projectName}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Architecture_overview_documents_current_module_roots()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        string overview = File.ReadAllText(FrameworkDocsPath(repositoryRoot, "architecture", "overview.md"));
        string[] offenders = sourceLayout
            .ModuleRoots
            .Keys
            .Where(moduleName => !overview.Contains($"{moduleName}/", StringComparison.Ordinal))
            .Select(moduleName => $"src/Framework/docs/architecture/overview.md missing module root {moduleName}/")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Architecture_catalog_lists_all_non_migration_module_projects()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        string[] moduleProjects = sourceLayout
            .ModuleRoots
            .Values
            .Where(Directory.Exists)
            .SelectMany(moduleRoot => Directory.EnumerateFiles(moduleRoot, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !HasIgnoredPathSegment(path))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(projectName => !IsProviderMigrationProject(projectName))
            .Where(projectName => !projectName.EndsWith(".Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] catalogProjects = ArchitectureCatalog.ModuleProjects
            .Select(project => project.ProjectName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(moduleProjects, catalogProjects);
    }

    [Fact]
    public void Reusable_package_solutions_are_present_and_include_colocated_tests_and_docs()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, (string SolutionPath, string[] RequiredProjects, string[] RequiredFiles)> packages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Framework.slnx"] = (
                Path.Combine(sourceLayout.FrameworkRepositoryRoot, "Gma.Framework.slnx"),
                [
                    "src/Results/Gma.Framework.Results/Gma.Framework.Results.csproj",
                    "tests/Gma.Framework.Tests/Gma.Framework.Tests.csproj"
                ],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "eng/new-module.ps1"
                ]),
            ["Gma.Modules.AccessControl.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("AccessControl"), "Gma.Modules.AccessControl.slnx"),
                [
                    "src/Gma.Modules.AccessControl.AdminApi/Gma.Modules.AccessControl.AdminApi.csproj",
                    "src/Gma.Modules.AccessControl.AdminCli/Gma.Modules.AccessControl.AdminCli.csproj",
                    "src/Gma.Modules.AccessControl.Application/Gma.Modules.AccessControl.Application.csproj",
                    "tests/Gma.Modules.AccessControl.Tests/Gma.Modules.AccessControl.Tests.csproj"
                ],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.Administration.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("Administration"), "Gma.Modules.Administration.slnx"),
                [
                    "src/Gma.Modules.Administration.Application/Gma.Modules.Administration.Application.csproj",
                    "tests/Gma.Modules.Administration.Tests/Gma.Modules.Administration.Tests.csproj"
                ],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.Auth.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("Auth"), "Gma.Modules.Auth.slnx"),
                [
                    "src/Gma.Modules.Auth.Application/Gma.Modules.Auth.Application.csproj",
                    "tests/Gma.Modules.Auth.Tests/Gma.Modules.Auth.Tests.csproj"
                ],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.Files.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("Files"), "Gma.Modules.Files.slnx"),
                ["src/Gma.Modules.Files.Api/Gma.Modules.Files.Api.csproj"],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.Notifications.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("Notifications"), "Gma.Modules.Notifications.slnx"),
                [
                    "src/Gma.Modules.Notifications.Application/Gma.Modules.Notifications.Application.csproj",
                    "tests/Gma.Modules.Notifications.Tests/Gma.Modules.Notifications.Tests.csproj"
                ],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.TaskRuntime.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("TaskRuntime"), "Gma.Modules.TaskRuntime.slnx"),
                ["src/Gma.Modules.TaskRuntime.Application/Gma.Modules.TaskRuntime.Application.csproj"],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ]),
            ["Gma.Modules.Tenancy.slnx"] = (
                Path.Combine(sourceLayout.GetModulePackageRoot("Tenancy"), "Gma.Modules.Tenancy.slnx"),
                ["src/Gma.Modules.Tenancy.Api/Gma.Modules.Tenancy.Api.csproj"],
                [
                    ".github/workflows/validate.yml",
                    "docs/README.md",
                    "eng/bootstrap-source-roots.ps1",
                    "README.md"
                ])
        };

        string[] projectOffenders = packages
            .SelectMany(item =>
            {
                if (!File.Exists(item.Value.SolutionPath))
                {
                    return [$"{CanonicalRelativePath(repositoryRoot, item.Value.SolutionPath)} is missing"];
                }

                string solution = File.ReadAllText(item.Value.SolutionPath);
                return item.Value.RequiredProjects
                    .Where(projectPath => !solution.Contains(projectPath, StringComparison.OrdinalIgnoreCase))
                    .Select(projectPath => $"{item.Key} missing {projectPath}");
            })
            .ToArray();
        string[] fileOffenders = packages
            .SelectMany(item =>
            {
                if (!File.Exists(item.Value.SolutionPath))
                {
                    return [$"{CanonicalRelativePath(repositoryRoot, item.Value.SolutionPath)} is missing"];
                }

                string solution = File.ReadAllText(item.Value.SolutionPath);
                return item.Value.RequiredFiles
                    .Where(filePath => !solution.Contains(filePath, StringComparison.OrdinalIgnoreCase))
                    .Select(filePath => $"{item.Key} missing {filePath}");
            })
            .ToArray();
        string[] offenders = projectOffenders
            .Concat(fileOffenders)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Focused_source_package_solutions_use_package_local_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, (string SolutionPath, string[] AllowedFolders)> packages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Framework.slnx"] = (Path.Combine(sourceLayout.FrameworkRepositoryRoot, "Gma.Framework.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.AccessControl.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("AccessControl"), "Gma.Modules.AccessControl.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.Administration.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("Administration"), "Gma.Modules.Administration.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.Auth.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("Auth"), "Gma.Modules.Auth.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.Files.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("Files"), "Gma.Modules.Files.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.Notifications.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("Notifications"), "Gma.Modules.Notifications.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.TaskRuntime.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("TaskRuntime"), "Gma.Modules.TaskRuntime.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"]),
            ["Gma.Modules.Tenancy.slnx"] = (Path.Combine(sourceLayout.GetModulePackageRoot("Tenancy"), "Gma.Modules.Tenancy.slnx"), ["/.github/", "/Solution Items/", "/docs/", "/eng/", "/src/", "/tests/"])
        };
        string[] allowedPackageRootFiles =
        [
            ".editorconfig",
            ".gitattributes",
            ".gitignore",
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            "Gma.SourceRoots.props.example",
            "LICENSE",
            "nuget.config",
            "README.md"
        ];

        string[] offenders = packages
            .SelectMany(item =>
            {
                XDocument solution = XDocument.Load(item.Value.SolutionPath);
                string[] folderOffenders = solution
                    .Descendants("Folder")
                    .Select(folder => folder.Attribute("Name")?.Value ?? string.Empty)
                    .Where(folderName => !item.Value.AllowedFolders.Any(allowedFolder =>
                        string.Equals(folderName, allowedFolder, StringComparison.Ordinal) ||
                        folderName.StartsWith(allowedFolder, StringComparison.Ordinal)))
                    .Select(folderName => $"{item.Key} contains non-package-local folder {folderName}")
                    .ToArray();
                string[] pathOffenders = solution
                    .Descendants()
                    .Attributes("Path")
                    .Select(attribute => attribute.Value.Replace('\\', '/'))
                    .Where(path => !allowedPackageRootFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                    .Where(path => !item.Value.AllowedFolders.Any(folder => path.StartsWith(folder.Trim('/'), StringComparison.OrdinalIgnoreCase)))
                    .Select(path => $"{item.Key} lists {path} outside package-local folders")
                    .ToArray();

                return folderOffenders.Concat(pathOffenders);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Focused_module_solutions_group_source_projects_by_role()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, string> moduleSolutions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Modules.AccessControl.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("AccessControl"), "Gma.Modules.AccessControl.slnx"),
            ["Gma.Modules.Administration.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Administration"), "Gma.Modules.Administration.slnx"),
            ["Gma.Modules.Auth.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Auth"), "Gma.Modules.Auth.slnx"),
            ["Gma.Modules.Files.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Files"), "Gma.Modules.Files.slnx"),
            ["Gma.Modules.Notifications.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Notifications"), "Gma.Modules.Notifications.slnx"),
            ["Gma.Modules.TaskRuntime.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("TaskRuntime"), "Gma.Modules.TaskRuntime.slnx"),
            ["Gma.Modules.Tenancy.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Tenancy"), "Gma.Modules.Tenancy.slnx")
        };

        string[] offenders = moduleSolutions
            .SelectMany(item =>
            {
                XDocument solution = XDocument.Load(item.Value);

                return solution
                    .Descendants("Folder")
                    .SelectMany(folder =>
                    {
                        string folderName = folder.Attribute("Name")?.Value ?? string.Empty;
                        return folder
                            .Elements("Project")
                            .Select(project => new
                            {
                                FolderName = folderName,
                                ProjectPath = NormalizePath(project.Attribute("Path")?.Value ?? string.Empty)
                            });
                    })
                    .Where(project => project.ProjectPath.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
                    .Where(project => string.Equals(project.FolderName, "/src/", StringComparison.Ordinal))
                    .Select(project => $"{item.Key}:{project.ProjectPath} is listed under flat /src/, expected /src/<role>/.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Package_local_solutions_mirror_root_focused_solutions()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, string> packages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Framework.slnx"] = Path.Combine(sourceLayout.FrameworkRepositoryRoot, "Gma.Framework.slnx"),
            ["Gma.Modules.AccessControl.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("AccessControl"), "Gma.Modules.AccessControl.slnx"),
            ["Gma.Modules.Administration.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Administration"), "Gma.Modules.Administration.slnx"),
            ["Gma.Modules.Auth.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Auth"), "Gma.Modules.Auth.slnx"),
            ["Gma.Modules.Files.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Files"), "Gma.Modules.Files.slnx"),
            ["Gma.Modules.Notifications.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Notifications"), "Gma.Modules.Notifications.slnx"),
            ["Gma.Modules.TaskRuntime.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("TaskRuntime"), "Gma.Modules.TaskRuntime.slnx"),
            ["Gma.Modules.Tenancy.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Tenancy"), "Gma.Modules.Tenancy.slnx")
        };

        string[] offenders = packages
            .SelectMany(item =>
            {
                string solutionDisplayPath = CanonicalRelativePath(repositoryRoot, item.Value);
                if (!File.Exists(item.Value))
                {
                    return [$"{solutionDisplayPath} is missing"];
                }

                string[] localEntries = GetSolutionEntryPaths(XDocument.Load(item.Value));
                bool isFrameworkPackage = string.Equals(item.Key, "Gma.Framework.slnx", StringComparison.OrdinalIgnoreCase);
                string[] staleLocalPrefixes = localEntries
                    .Where(path => path.StartsWith("src/Framework/", StringComparison.OrdinalIgnoreCase) ||
                                   (!isFrameworkPackage && path.StartsWith("src/Modules/", StringComparison.OrdinalIgnoreCase)))
                    .Select(path => $"{solutionDisplayPath} keeps monorepo-staged path {path}")
                    .ToArray();
                string[] nonLocalEntries = localEntries
                    .Where(path => !path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("eng/", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith(".github/", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("src/", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) &&
                                   !IsPackageRootSolutionItem(path))
                    .Select(path => $"{solutionDisplayPath} lists non-package-local entry {path}")
                    .ToArray();

                return staleLocalPrefixes.Concat(nonLocalEntries);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);

        static string[] GetSolutionEntryPaths(XDocument solution) =>
            solution
                .Descendants()
                .Attributes("Path")
                .Select(attribute => attribute.Value.Replace('\\', '/'))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        static bool IsPackageRootSolutionItem(string path) =>
            path is ".editorconfig" or
                ".gitattributes" or
                ".gitignore" or
                "Directory.Build.props" or
                "Directory.Packages.props" or
                "global.json" or
                "Gma.SourceRoots.props.example" or
                "LICENSE" or
                "nuget.config" or
                "README.md";
    }

    [Fact]
    public void Root_module_scaffolder_delegates_to_framework_owned_implementation()
    {
        string repositoryRoot = FindRepositoryRoot();
        string wrapper = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "new-module.ps1"));
        string implementation = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));

        Assert.Contains("gma\\framework\\eng\\new-module.ps1", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-RepositoryRoot $repositoryRoot", wrapper, StringComparison.Ordinal);
        Assert.Contains("-CompositionSolution 'GMA-Skeleton.slnx'", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("function Add-Project", wrapper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompositionSolution is required", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("GMA-Skeleton.slnx", implementation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Source_package_checker_covers_all_focused_package_solutions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string checker = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-source-packages.ps1"));
        string validationScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "gma-validate.ps1"));
        string[] focusedSolutions =
        [
            "Gma.Framework.slnx",
            "Gma.Modules.AccessControl.slnx",
            "Gma.Modules.Administration.slnx",
            "Gma.Modules.Auth.slnx",
            "Gma.Modules.Files.slnx",
            "Gma.Modules.Notifications.slnx",
            "Gma.Modules.TaskRuntime.slnx",
            "Gma.Modules.Tenancy.slnx"
        ];

        string[] offenders = focusedSolutions
            .SelectMany(solution => new[]
            {
                checker.Contains(solution, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $"eng/check-source-packages.ps1 missing {solution}",
                validationScript.Contains(solution, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $"eng/gma-validate.ps1 missing {solution}"
            })
            .Where(offender => offender.Length > 0)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Root_tests_stay_composition_owned()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] allowedRootTestProjects =
        [
            "Architecture.Tests",
            "Integration.Tests",
            "ServiceDefaults.Tests"
        ];
        string[] offenders = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(projectName => projectName is not null &&
                                  !allowedRootTestProjects.Contains(projectName, StringComparer.Ordinal))
            .Select(projectName => $"tests/{projectName} should live under the owning source root")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Reusable_module_solutions_only_list_owned_module_projects()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, string> moduleBySolution = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Modules.AccessControl.slnx"] = "AccessControl",
            ["Gma.Modules.Administration.slnx"] = "Administration",
            ["Gma.Modules.Auth.slnx"] = "Auth",
            ["Gma.Modules.Files.slnx"] = "Files",
            ["Gma.Modules.Notifications.slnx"] = "Notifications",
            ["Gma.Modules.TaskRuntime.slnx"] = "TaskRuntime",
            ["Gma.Modules.Tenancy.slnx"] = "Tenancy"
        };

        string[] offenders = moduleBySolution
            .SelectMany(item =>
            {
                string solutionPath = Path.Combine(sourceLayout.GetModulePackageRoot(item.Value), item.Key);
                string solution = File.ReadAllText(solutionPath);
                string ownedModulePrefix = $"src/Gma.Modules.{item.Value}";

                return SolutionProjectPathPattern()
                    .Matches(solution)
                    .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
                    .Where(projectPath =>
                        projectPath.StartsWith("../", StringComparison.OrdinalIgnoreCase) ||
                        projectPath.StartsWith("src/Framework/", StringComparison.OrdinalIgnoreCase) ||
                        projectPath.StartsWith("src/Modules/", StringComparison.OrdinalIgnoreCase) ||
                        (projectPath.StartsWith("src/Gma.Modules.", StringComparison.OrdinalIgnoreCase) &&
                         !projectPath.StartsWith(ownedModulePrefix, StringComparison.OrdinalIgnoreCase)))
                    .Select(projectPath => $"{item.Key} should not list {projectPath}; module solutions build framework dependencies through GmaFrameworkRoot project references.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Namespaces_start_with_owning_project_name()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] misalignedNamespaces = EnumerateSourceFiles(repositoryRoot)
            .Where(path => !string.Equals(Path.GetFileName(path), "Program.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => GetMisalignedNamespaces(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(misalignedNamespaces);
    }

    [Fact]
    public void Source_files_do_not_import_their_own_declared_namespace()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(repositoryRoot)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string? declaredNamespace = NamespacePattern()
                    .Matches(source)
                    .Select(match => match.Groups["name"].Value)
                    .FirstOrDefault();

                if (declaredNamespace is null)
                {
                    return [];
                }

                return UsingNamespacePattern()
                    .Matches(source)
                    .Select(match => match.Groups["namespace"].Value)
                    .Where(importedNamespace => string.Equals(importedNamespace, declaredNamespace, StringComparison.Ordinal))
                    .Select(importedNamespace => $"{Path.GetRelativePath(repositoryRoot, path)} imports its own namespace {importedNamespace}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Test_classes_with_test_methods_use_tests_suffix()
    {
        string repositoryRoot = FindRepositoryRoot();
        string testsRoot = Path.Combine(repositoryRoot, "tests");
        string[] offenders = EnumerateSourceFiles(testsRoot)
            .Where(ContainsTestAttribute)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);

                return PublicOrInternalClassPattern()
                    .Matches(source)
                    .Where(match => ClassContainsTestAttribute(source, match.Index))
                    .Select(match => new
                    {
                        Path = path,
                        ClassName = match.Groups["name"].Value,
                    });
            })
            .Where(item => !item.ClassName.EndsWith("Tests", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.Path)}::{item.ClassName}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Test_sources_declare_expected_category_traits()
    {
        string repositoryRoot = FindRepositoryRoot();
        string testsRoot = Path.Combine(repositoryRoot, "tests");
        string[] offenders = EnumerateSourceFiles(testsRoot)
            .Where(ContainsTestAttribute)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                string expectedCategory = GetExpectedTestCategory(path);
                List<string> failures = [];

                if (!HasCategoryTrait(source, expectedCategory))
                {
                    failures.Add($"missing Category={expectedCategory}");
                }

                if (DockerFactAttributeLinePattern().IsMatch(source) &&
                    !HasCategoryTrait(source, "Docker"))
                {
                    failures.Add("missing Category=Docker");
                }

                return failures.Select(failure => $"{relativePath}:{failure}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Test_sources_live_under_intent_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(repositoryRoot)
            .Where(IsTestSourcePath)
            .Where(path => !HasProjectIntentFolder(path, FindOwningProjectDirectory(path)!))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Unit_tests_that_redirect_console_use_console_test_collection()
    {
        string repositoryRoot = FindRepositoryRoot();
        string collectionDefinition = File.ReadAllText(Path.Combine(
            GmaSourceLayout.FromRepositoryRoot(repositoryRoot).FrameworkRepositoryRoot,
            "tests",
            "Gma.Framework.Tests",
            "Support",
            "ConsoleTestIsolation.cs"));
        string[] consoleRedirectTokens =
        [
            "Console.SetOut(",
            "Console.SetError(",
            "Console.SetIn("
        ];
        string[] offenders = EnumerateSourceFiles(repositoryRoot)
            .Where(IsTestSourcePath)
            .Where(path => !string.Equals(FindOwningProjectName(path), "Integration.Tests", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return consoleRedirectTokens.Any(token => source.Contains(token, StringComparison.Ordinal)) &&
                       !source.Contains("[Collection(ConsoleTestIsolation.Name)]", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} redirects Console without ConsoleTestIsolation.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains("[CollectionDefinition(Name)]", collectionDefinition, StringComparison.Ordinal);
        Assert.Contains("public const string Name = \"Console\"", collectionDefinition, StringComparison.Ordinal);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_handler_files_contain_one_handler_class()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(IsApplicationHandlerSource)
            .Select(path => new
            {
                Path = path,
                HandlerClassCount = PublicOrInternalClassPattern().Count(File.ReadAllText(path)),
            })
            .Where(item => item.HandlerClassCount > 1)
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.Path)} contains {item.HandlerClassCount} handler classes")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Contract_files_contain_one_public_type()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(IsContractSource)
            .Select(path => new
            {
                Path = path,
                PublicTypeCount = PublicContractTypePattern().Count(File.ReadAllText(path)),
            })
            .Where(item => item.PublicTypeCount > 1)
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.Path)} contains {item.PublicTypeCount} public contract types")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_application_dependency_injection_uses_constrained_assembly_registration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string srcRoot = Path.Combine(repositoryRoot, "src");
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string helperSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Application.Composition",
            "ApplicationServiceCollectionExtensions.cs"));
        string[] handlerRegistrationTokens =
        [
            "ICommandHandler<",
            "IQueryHandler<",
            "ICommandValidator<",
            "IQueryValidator<",
            "IDomainEventHandler<"
        ];
        string[] unsafeRegistrationPrefixes =
        [
            ".AddScoped<",
            ".AddTransient<",
            ".AddSingleton<",
            ".TryAddScoped<",
            ".TryAddTransient<",
            ".TryAddSingleton<"
        ];
        string registrationCall = "services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);";

        string[] applicationRegistrationOffenders = Directory
            .EnumerateFiles(modulesRoot, "DependencyInjection.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains(".Application", StringComparison.Ordinal))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .SelectMany(item =>
            {
                List<string> offenders = [];
                if (!item.Source.Contains(registrationCall, StringComparison.Ordinal))
                {
                    offenders.Add(
                        $"{Path.GetRelativePath(repositoryRoot, item.Path)} should call AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly).");
                }

                offenders.AddRange(handlerRegistrationTokens
                    .Where(token => item.Source.Contains(token, StringComparison.Ordinal))
                    .SelectMany(token => unsafeRegistrationPrefixes
                        .Where(prefix => item.Source.Contains(prefix + token, StringComparison.Ordinal))
                        .Select(prefix => $"{Path.GetRelativePath(repositoryRoot, item.Path)} uses {prefix}{token}; use AddApplicationServicesFromAssembly instead.")));

                return offenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] misplacedRegistrationOffenders = EnumerateSourceFiles(srcRoot)
            .Where(path => !IsTestSourcePath(path))
            .Where(path => File.ReadAllText(path).Contains("AddApplicationServicesFromAssembly(", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(
                Path.Combine("Gma.Framework.Application.Composition", "ApplicationServiceCollectionExtensions.cs"),
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(
                Path.Combine(".Application", "DependencyInjection.cs"),
                StringComparison.OrdinalIgnoreCase))
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} uses application assembly registration outside module application DI.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] helperOffenders =
        [
            helperSource.Contains("typeof(ICommandHandler<,>)", StringComparison.Ordinal)
                ? string.Empty
                : "ApplicationServiceCollectionExtensions should register command handlers.",
            helperSource.Contains("typeof(IQueryHandler<,>)", StringComparison.Ordinal)
                ? string.Empty
                : "ApplicationServiceCollectionExtensions should register query handlers.",
            helperSource.Contains("typeof(ICommandValidator<>)", StringComparison.Ordinal)
                ? string.Empty
                : "ApplicationServiceCollectionExtensions should register command validators.",
            helperSource.Contains("typeof(IQueryValidator<>)", StringComparison.Ordinal)
                ? string.Empty
                : "ApplicationServiceCollectionExtensions should register query validators.",
            helperSource.Contains("typeof(IDomainEventHandler<>)", StringComparison.Ordinal)
                ? string.Empty
                : "ApplicationServiceCollectionExtensions should register domain event handlers.",
            helperSource.Contains("IIntegrationEventHandler", StringComparison.Ordinal)
                ? "ApplicationServiceCollectionExtensions must not register integration event handlers; subscriptions need explicit subject and handler metadata."
                : string.Empty
        ];
        string[] scaffoldOffenders =
        [
            scaffolder.Contains("using Gma.Framework.Application.Composition;", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold Gma.Framework.Application.Composition usage.",
            scaffolder.Contains(registrationCall, StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold AddApplicationServicesFromAssembly."
        ];
        string[] broadScanningPackageOffenders = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(repositoryRoot, "*.props", SearchOption.AllDirectories))
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => File.ReadAllText(path).Contains("Include=\"Scrutor\"", StringComparison.Ordinal))
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} references Scrutor; ADR 0006 keeps application registration in-house and constrained.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(applicationRegistrationOffenders
            .Concat(misplacedRegistrationOffenders)
            .Concat(helperOffenders.Where(offender => !string.IsNullOrWhiteSpace(offender)))
            .Concat(scaffoldOffenders.Where(offender => !string.IsNullOrWhiteSpace(offender)))
            .Concat(broadScanningPackageOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Shared_module_and_task_metadata_do_not_depend_on_messaging_naming()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] guardedRoots =
        [
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Modules"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Caching"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Tasks")
        ];
        string[] offenders = guardedRoots
            .SelectMany(EnumerateSourceFiles)
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .Where(item => item.Source.Contains("Gma.Framework.Messaging", StringComparison.Ordinal) ||
                           item.Source.Contains("IntegrationEventNaming", StringComparison.Ordinal))
            .Select(item => CanonicalRelativePath(repositoryRoot, item.Path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_boundary_enums_define_unknown_zero()
    {
        Assembly[] boundaryAssemblies = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind is ModuleProjectKind.Contracts or ModuleProjectKind.Domain)
            .Select(project => project.Assembly)
            .Concat(
            [
                typeof(AdminOperationExecutionStatus).Assembly,
                typeof(Gma.Framework.Caching.CacheScope).Assembly,
                typeof(InboxMessageStatus).Assembly,
                typeof(Gma.Framework.Tasks.TaskRunStatus).Assembly
            ])
            .Distinct()
            .ToArray();

        string[] offenders = boundaryAssemblies
            .SelectMany(assembly => assembly
                .GetTypes()
                .Where(type => type is { IsEnum: true, IsPublic: true })
                .Where(type => !string.Equals(Enum.GetName(type, 0), "Unknown", StringComparison.Ordinal))
                .Select(type => type.FullName ?? type.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_contract_enums_own_json_wire_converters()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Contracts)
            .SelectMany(project => project.Assembly.GetTypes())
            .Where(type => type is { IsEnum: true, IsPublic: true })
            .Where(type => !type.GetCustomAttributes(inherit: false)
                .Any(attribute => string.Equals(
                    attribute.GetType().FullName,
                    "System.Text.Json.Serialization.JsonConverterAttribute",
                    StringComparison.Ordinal)))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Enum_guidelines_document_unknown_and_code_list_policy()
    {
        string repositoryRoot = FindRepositoryRoot();
        string developmentGuidelines = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "development-guidelines.md"));
        string namingConventions = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "naming-conventions.md"));
        string moduleTemplate = File.ReadAllText(FrameworkDocsPath(repositoryRoot, "templates", "module.md"));
        string[] requiredDevelopmentTokens =
        [
            "Do not map unknown enum values to a valid domain value by default.",
            "smart enum",
            "value-object/code-list",
            "one small shared pattern",
            "Provider/configuration enums should also reserve `Unknown = 0`"
        ];
        string[] requiredNamingTokens =
        [
            "Public contract enums, public domain-state enums, and provider/configuration enums",
            "Unknown = 0",
            "mapping code must not collapse unknown values into meaningful business states"
        ];
        string[] requiredTemplateTokens =
        [
            "persisted enum numeric values are stable",
            "public contract/domain-state enums use `Unknown = 0`",
            "consumed producer enum/status values are validated before they affect local decisions"
        ];

        string[] offenders = requiredDevelopmentTokens
            .Where(token => !developmentGuidelines.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/docs/guidelines/development-guidelines.md missing {token}")
            .Concat(requiredNamingTokens
                .Where(token => !namingConventions.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/docs/guidelines/naming-conventions.md missing {token}"))
            .Concat(requiredTemplateTokens
                .Where(token => !moduleTemplate.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/docs/templates/module.md missing {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_sources_do_not_directly_cast_to_public_module_enums()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        Type[] publicModuleEnums = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind is ModuleProjectKind.Contracts or ModuleProjectKind.Domain)
            .SelectMany(project => project.Assembly.GetTypes())
            .Where(type => type is { IsEnum: true, IsPublic: true })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => !IsTestSourcePath(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return publicModuleEnums
                    .SelectMany(enumType =>
                    {
                        string[] castTokens =
                        [
                            $"({enumType.Name})",
                            $"({enumType.FullName})"
                        ];

                        return castTokens
                            .Where(token => source.Contains(token, StringComparison.Ordinal))
                            .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} casts with {token}");
                    });
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Static_error_contracts_are_well_formed()
    {
        Assembly[] assemblies = ArchitectureCatalog.ModuleBoundaryAssemblies
            .Concat(
            [
                typeof(AdminErrors).Assembly,
                typeof(Gma.Framework.Tenancy.TenantErrors).Assembly,
                typeof(Error).Assembly,
            ])
            .Distinct()
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToArray();
        string[] offenders = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(Error))
                .Select(field => GetStaticErrorOffender(type, field)))
            .Where(offender => offender is not null)
            .Select(offender => offender!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Production_sources_do_not_model_nullable_result_successes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return NullableResultTypePattern()
                    .Matches(source)
                    .Select(match => $"{Path.GetRelativePath(repositoryRoot, path)} uses {match.Value.Trim()}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Page_request_does_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Pagination",
            "PageRequest.cs"));

        Assert.DoesNotMatch(PositionalPageRequestPattern(), source);
        Assert.Contains("public PageRequest(int page, int pageSize)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_paging_uses_page_request_skip_count()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => File.ReadAllText(path).Contains("(page - 1) * pageSize", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Api_error_status_code_does_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api",
            "Results",
            "ApiErrorStatusCode.cs"));

        Assert.DoesNotMatch(PositionalApiErrorStatusCodePattern(), source);
        Assert.Contains("public ApiErrorStatusCode(string errorCode, int statusCode)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_operation_execution_result_does_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Administration",
            "AdminOperationExecutionResult.cs"));

        Assert.DoesNotMatch(PositionalAdminOperationExecutionResultPattern(), source);
        Assert.Contains("public AdminOperationExecutionResult(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_endpoint_metadata_does_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Api",
            "Observability",
            "ModuleEndpointMetadata.cs"));

        Assert.DoesNotMatch(PositionalModuleEndpointMetadataPattern(), source);
        Assert.Contains("public ModuleEndpointMetadata(string moduleName)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Auth_access_token_claims_do_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Domain",
            "Services",
            "ITokenService.cs"));

        Assert.DoesNotMatch(PositionalAccessTokenClaimsPattern(), source);
        Assert.Contains("public AccessTokenClaims(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Messaging_records_do_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string messagingRoot = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Messaging");
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
        {
            ["IntegrationEventEnvelope"] = File.ReadAllText(Path.Combine(messagingRoot, "IntegrationEventEnvelope.cs")),
            ["OutboxMessageRecord"] = File.ReadAllText(Path.Combine(messagingRoot, "OutboxMessageRecord.cs")),
            ["InboxMessageRecord"] = File.ReadAllText(Path.Combine(messagingRoot, "InboxMessageRecord.cs"))
        };
        string[] offenders = sources
            .Where(item => PositionalMessagingRecordPattern(item.Key).IsMatch(item.Value))
            .Select(item => item.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_integration_event_contracts_do_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Contracts", StringComparison.Ordinal))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .Where(item => Path.GetFileName(item.Path).EndsWith("IntegrationEvent.cs", StringComparison.Ordinal))
            .SelectMany(item => PositionalPublicIntegrationEventPattern()
                .Matches(item.Source)
                .Select(match => $"{Path.GetRelativePath(repositoryRoot, item.Path)}::{match.Groups["name"].Value}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_integration_event_contracts_inherit_shared_base()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Contracts)
            .SelectMany(project => project.Assembly.ExportedTypes
                .Where(type => type.Name.EndsWith("IntegrationEvent", StringComparison.Ordinal))
                .Where(type => !typeof(IntegrationEvent).IsAssignableFrom(type))
                .Select(type => $"{project.ProjectName}:{type.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_domain_events_do_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Domain", StringComparison.Ordinal))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .Where(item => Path.GetFileName(item.Path).EndsWith("DomainEvent.cs", StringComparison.Ordinal))
            .SelectMany(item => PositionalPublicDomainEventPattern()
                .Matches(item.Source)
                .Select(match => $"{Path.GetRelativePath(repositoryRoot, item.Path)}::{match.Groups["name"].Value}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_domain_events_inherit_shared_domain_event_base()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Domain", StringComparison.Ordinal))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => Path.GetFileName(path).EndsWith("DomainEvent.cs", StringComparison.Ordinal))
            .Where(path => !ModuleDomainEventBasePattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Ordering_projection_port_models_do_not_expose_positional_constructor_bypass()
    {
        string repositoryRoot = FindRepositoryRoot();
        string portsRoot = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "Ordering",
            "Ordering.Application",
            "Ports");
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
        {
            ["CatalogItemProjectionWriteModel"] = File.ReadAllText(Path.Combine(portsRoot, "CatalogItemProjectionWriteModel.cs")),
            ["CatalogItemProjectionSnapshot"] = File.ReadAllText(Path.Combine(portsRoot, "CatalogItemProjectionSnapshot.cs"))
        };
        string[] offenders = sources
            .Where(item => PositionalOrderingProjectionPortModelPattern(item.Key).IsMatch(item.Value))
            .Select(item => item.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Domain_guid_id_value_objects_are_not_positional_records()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => HasPathSegment(path, "ValueObjects"))
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Domain", StringComparison.Ordinal))
            .Where(path => PositionalGuidIdValueObjectPattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_domain_semantic_options_are_not_string_backed()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Domain", StringComparison.Ordinal))
            .Where(path => !IsGeneratedMigrationSource(path))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .SelectMany(item => StringBackedSemanticDomainValueObjectPattern()
                .Matches(item.Source)
                .Select(match => $"{Path.GetRelativePath(repositoryRoot, item.Path)}::{match.Groups["name"].Value}")
                .Concat(SemanticDomainStringMemberPattern()
                    .Matches(item.Source)
                    .Select(match => $"{Path.GetRelativePath(repositoryRoot, item.Path)}::{match.Groups["name"].Value}")))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Semantic_contract_fields_are_typed_at_boundaries()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string relativePath = CanonicalRelativePath(repositoryRoot, path);
                if (IsAllowedSemanticStringBoundary(relativePath))
                {
                    return [];
                }

                string source = File.ReadAllText(path);
                return SemanticBoundaryStringPropertyPattern()
                    .Matches(source)
                    .Select(match => $"{relativePath} exposes string {match.Groups["name"].Value}; use an enum or explicit value object/code-list.")
                    .Concat(SemanticBoundaryRecordParameterPattern()
                        .Matches(source)
                        .Select(match => $"{relativePath} exposes string {match.Groups["name"].Value}; use an enum or explicit value object/code-list."));
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Semantic_enum_mappers_do_not_collapse_unknown_values_to_meaningful_states()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                string source = File.ReadAllText(path);
                return MeaningfulSemanticDefaultSwitchArmPattern()
                    .Matches(source)
                    .Select(match => $"{relativePath} maps a default enum arm to {match.Groups["value"].Value}; return Unknown or failure instead.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Tenant_id_normalization_lives_in_shared_naming()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] tenantIdHelpers = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Where(path => string.Equals(Path.GetFileName(path), "TenantIds.cs", StringComparison.Ordinal))
            .Select(path => CanonicalRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] domainTrimOffenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src", "Modules"))
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Domain", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("tenantId.Trim()", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal([Path.Combine("src", "Framework", "Naming", "Gma.Framework.Naming", "TenantIds.cs")], tenantIdHelpers);
        Assert.Empty(domainTrimOffenders);
    }

    [Fact]
    public void Auth_refresh_token_hashing_uses_keyed_versioned_hashes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "Services",
            "RefreshTokenHashingService.cs"));

        Assert.Contains("HMACSHA256.HashData", source, StringComparison.Ordinal);
        Assert.Contains("hmac-sha256", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Auth_security_options_are_validated_on_start()
    {
        string repositoryRoot = FindRepositoryRoot();
        string infrastructureSource = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "DependencyInjection.cs"));
        string applicationSource = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Application",
            "DependencyInjection.cs"));

        string[] infrastructureRequiredTokens =
        [
            "AuthInfrastructureOptionsValidation.Validate(configuration);",
            "IValidateOptions<JwtSettings>, JwtSettingsValidator",
            "IValidateOptions<RefreshTokenHashingOptions>, RefreshTokenHashingOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] applicationRequiredTokens =
        [
            "AuthApplicationOptionsValidation.GetValidatedOptions(configuration);",
            "IValidateOptions<AuthApplicationOptions>, AuthApplicationOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] offenders = infrastructureRequiredTokens
            .Where(token => !infrastructureSource.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Modules.Auth.Infrastructure dependency injection missing {token}")
            .Concat(applicationRequiredTokens
                .Where(token => !applicationSource.Contains(token, StringComparison.Ordinal))
                .Select(token => $"Gma.Modules.Auth.Application dependency injection missing {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Auth_security_options_do_not_ship_secret_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        string jwtSettings = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "JwtSettings.cs"));
        string refreshTokenHashingOptions = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Infrastructure",
            "RefreshTokenHashingOptions.cs"));

        Assert.Contains("public string SigningKey { get; set; } = string.Empty;", jwtSettings, StringComparison.Ordinal);
        Assert.Contains("public string Pepper { get; set; } = string.Empty;", refreshTokenHashingOptions, StringComparison.Ordinal);
        Assert.DoesNotContain("local-development-", jwtSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("local-development-", refreshTokenHashingOptions, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_host_secret_configuration_is_development_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostDirectories =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminCli")
        ];
        (string Name, string[] JsonPath)[] runtimeSecretKeys =
        [
            ("ConnectionStrings:SqlServer", ["ConnectionStrings", "SqlServer"]),
            ("ConnectionStrings:PostgreSql", ["ConnectionStrings", "PostgreSql"]),
            ("ConnectionStrings:nats", ["ConnectionStrings", "nats"]),
            ("Auth:Jwt:SigningKey", ["Auth", "Jwt", "SigningKey"]),
            ("Auth:RefreshTokens:Pepper", ["Auth", "RefreshTokens", "Pepper"])
        ];
        string[] baseConfigOffenders = hostDirectories
            .SelectMany(hostDirectory =>
            {
                string hostName = Path.GetFileName(hostDirectory);
                string appsettings = Path.Combine(hostDirectory, "appsettings.json");

                return runtimeSecretKeys
                    .Select(key =>
                    {
                        string? value = GetJsonStringValue(appsettings, key.JsonPath);
                        if (value is null)
                        {
                            return $"{hostName} appsettings.json missing {key.Name}";
                        }

                        return string.IsNullOrWhiteSpace(value)
                            ? null
                            : $"{hostName} appsettings.json must leave {key.Name} blank";
                    })
                    .Where(offender => offender is not null)
                    .Select(offender => offender!);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] developmentConfigOffenders = hostDirectories
            .SelectMany(hostDirectory =>
            {
                string hostName = Path.GetFileName(hostDirectory);
                string appsettings = Path.Combine(hostDirectory, "appsettings.Development.json");

                return runtimeSecretKeys
                    .Select(key =>
                    {
                        string? value = GetJsonStringValue(appsettings, key.JsonPath);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            return $"{hostName} appsettings.Development.json missing local {key.Name}";
                        }

                        if (key.Name.StartsWith("Auth:", StringComparison.Ordinal) &&
                            !value.StartsWith("local-development-", StringComparison.Ordinal))
                        {
                            return $"{hostName} appsettings.Development.json must mark {key.Name} as local-development";
                        }

                        return null;
                    })
                    .Where(offender => offender is not null)
                    .Select(offender => offender!);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] testConfigOffenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "tests"))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("AddInMemoryCollection", StringComparison.Ordinal) &&
                       source.Contains("\"Auth:Jwt:SigningKey\"", StringComparison.Ordinal) &&
                       !source.Contains("\"Auth:RefreshTokens:Pepper\"", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} configures Auth JWT without Auth:RefreshTokens:Pepper")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string setupDocs = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "getting-started", "setup.md"));
        string localDevelopmentDocs = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "getting-started",
            "local-development.md"));
        string deploymentDocs = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "deployment-guidelines.md"));
        string authDocs = File.ReadAllText(ModuleDocsPath(repositoryRoot, "Auth", "README.md"));
        string[] docsOffenders =
        [
            .. RequiredDocumentationTokens(
                "setup.md",
                setupDocs,
                "appsettings.Development.json",
                "ConnectionStrings:*",
                "Auth:Jwt:SigningKey",
                "Auth:RefreshTokens:Pepper"),
            .. RequiredDocumentationTokens(
                "local-development.md",
                localDevelopmentDocs,
                "DOTNET_ENVIRONMENT",
                "Development"),
            .. RequiredDocumentationTokens(
                "deployment-guidelines.md",
                deploymentDocs,
                "Auth option classes intentionally have no secret defaults",
                "development configuration"),
            .. RequiredDocumentationTokens(
                "auth.md",
                authDocs,
                "no secret default",
                "Auth__RefreshTokens__Pepper")
        ];

        Assert.Empty(baseConfigOffenders
            .Concat(developmentConfigOffenders)
            .Concat(testConfigOffenders)
            .Concat(docsOffenders));
    }

    [Fact]
    public void Runtime_host_configuration_exposes_nats_consumer_options()
    {
        string repositoryRoot = FindRepositoryRoot();
        string srcRoot = Path.Combine(repositoryRoot, "src");
        string[] requiredConsumerKeys =
        [
            "Enabled",
            "FetchBatchSize",
            "PollInterval",
            "AckWait",
            "MaxDeliver",
            "HandlerTimeout",
            "NakDelay"
        ];
        string[] documentedConsumerKeys =
        [
            .. requiredConsumerKeys,
            "DurablePrefix"
        ];
        string[] hostAppsettingsOffenders = Directory
            .EnumerateFiles(srcRoot, "appsettings.json", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty)
                .StartsWith("Host.", StringComparison.Ordinal))
            .Where(path => HasRequiredPath(path, ["NatsJetStream"]))
            .SelectMany(path => requiredConsumerKeys
                .Where(key => !HasRequiredPath(path, ["NatsConsumers", key]))
                .Select(key => $"{Path.GetRelativePath(repositoryRoot, path)} missing NatsConsumers:{key}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string deploymentDocs = File.ReadAllText(FrameworkDocsPath(
            repositoryRoot,
            "guidelines",
            "deployment-guidelines.md"));
        string[] docsOffenders = documentedConsumerKeys
            .Where(key => !deploymentDocs.Contains($"NatsConsumers:{key}", StringComparison.Ordinal))
            .Select(key => $"deployment-guidelines.md missing NatsConsumers:{key}")
            .ToArray();

        Assert.Empty(hostAppsettingsOffenders.Concat(docsOffenders));
    }

    [Fact]
    public void Http_host_development_configuration_enables_local_prometheus_metrics()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] httpHostDevelopmentSettings =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "appsettings.Development.json"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi", "appsettings.Development.json")
        ];

        string[] offenders = httpHostDevelopmentSettings
            .SelectMany(path =>
            {
                List<string> fileOffenders = [];
                if (!HasRequiredBoolean(path, ["Observability", "Prometheus", "Enabled"], expected: true))
                {
                    fileOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)} must enable Observability:Prometheus:Enabled for development.");
                }

                if (!HasRequiredStringValue(path, ["Observability", "Prometheus", "EndpointPath"], "/metrics"))
                {
                    fileOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)} must expose Observability:Prometheus:EndpointPath as /metrics.");
                }

                return fileOffenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Redis_capable_hosts_compose_cache_adapter_before_cache_cqrs_bridge()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] hostDirectories =
        [
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminApi"),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminCli")
        ];
        string[] offenders = hostDirectories
            .SelectMany(hostDirectory =>
            {
                string project = File.ReadAllText(Directory.EnumerateFiles(hostDirectory, "*.csproj").Single());
                string program = File.ReadAllText(Path.Combine(hostDirectory, "Program.cs"));
                string appsettings = File.ReadAllText(Path.Combine(hostDirectory, "appsettings.json"));
                string hostName = Path.GetFileName(hostDirectory);
                List<string> hostOffenders = [];

                if (!project.Contains("Gma.Framework.Caching.Redis", StringComparison.Ordinal))
                {
                    hostOffenders.Add($"{hostName} does not reference Gma.Framework.Caching.Redis");
                }

                if (!project.Contains("Gma.Framework.Caching.Cqrs", StringComparison.Ordinal))
                {
                    hostOffenders.Add($"{hostName} does not reference Gma.Framework.Caching.Cqrs");
                }

                if (!project.Contains("Gma.Framework.Tenancy.Caching", StringComparison.Ordinal))
                {
                    hostOffenders.Add($"{hostName} does not reference Gma.Framework.Tenancy.Caching");
                }

                int redisIndex = program.IndexOf("builder.AddRedisCaching();", StringComparison.Ordinal);
                int cachingBridgeIndex = program.IndexOf("builder.AddCachingCqrs();", StringComparison.Ordinal);
                int sharedInfrastructureIndex = program.IndexOf("builder.AddGmaInfrastructure();", StringComparison.Ordinal);
                int tenantCachingIndex = program.IndexOf("builder.AddTenantCaching();", StringComparison.Ordinal);
                if (redisIndex < 0)
                {
                    hostOffenders.Add($"{hostName} does not call AddRedisCaching");
                }
                else if (cachingBridgeIndex < 0 || redisIndex > cachingBridgeIndex)
                {
                    hostOffenders.Add($"{hostName} must call AddRedisCaching before AddCachingCqrs");
                }

                if (cachingBridgeIndex < 0)
                {
                    hostOffenders.Add($"{hostName} does not call AddCachingCqrs");
                }
                else if (sharedInfrastructureIndex < 0 || cachingBridgeIndex > sharedInfrastructureIndex)
                {
                    hostOffenders.Add($"{hostName} must call AddCachingCqrs before AddGmaInfrastructure");
                }

                if (tenantCachingIndex < 0)
                {
                    hostOffenders.Add($"{hostName} does not call AddTenantCaching");
                }
                else if (sharedInfrastructureIndex < 0 || tenantCachingIndex < sharedInfrastructureIndex)
                {
                    hostOffenders.Add($"{hostName} must call AddTenantCaching after AddGmaInfrastructure");
                }

                if (!appsettings.Contains("\"Redis\"", StringComparison.Ordinal) ||
                    !appsettings.Contains("\"ConnectionName\"", StringComparison.Ordinal))
                {
                    hostOffenders.Add($"{hostName} appsettings does not expose Caching:Redis:ConnectionName");
                }

                return hostOffenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Cache_cqrs_bridge_uses_internal_invalidation_flusher_seam()
    {
        string repositoryRoot = FindRepositoryRoot();
        string queueSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Caching.Infrastructure",
            "CacheInvalidationQueue.cs"));
        string assemblyInfo = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Caching.Infrastructure",
            "Properties",
            "AssemblyInfo.cs"));
        string bridgeSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Caching.Cqrs",
            "CacheInvalidationCommandBehavior.cs"));

        Assert.Contains("internal interface ICacheInvalidationQueueFlusher", queueSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public interface ICacheInvalidationQueueFlusher", queueSource, StringComparison.Ordinal);
        Assert.Contains("InternalsVisibleTo(\"Gma.Framework.Caching.Cqrs\")", assemblyInfo, StringComparison.Ordinal);
        Assert.Contains("ICacheInvalidationQueueFlusher", bridgeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_api_composes_shared_infrastructure_before_tenancy_module()
    {
        string repositoryRoot = FindRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"));
        int sharedInfrastructureIndex = program.IndexOf("builder.AddGmaInfrastructure();", StringComparison.Ordinal);
        int tenancyModuleIndex = program.IndexOf("builder.AddModule<TenancyModule>();", StringComparison.Ordinal);

        Assert.True(sharedInfrastructureIndex >= 0, "Host.Api must compose shared infrastructure.");
        Assert.True(tenancyModuleIndex >= 0, "Host.Api must compose TenancyModule explicitly.");
        Assert.True(
            sharedInfrastructureIndex < tenancyModuleIndex,
            "Host.Api must compose shared infrastructure before TenancyModule so tenant options and default/null context are validated before enabling tenant-scoped endpoints.");
    }

    [Fact]
    public void Optional_adapter_integration_harnesses_do_not_compose_host_level_shared_infrastructure_facade()
    {
        string repositoryRoot = FindRepositoryRoot();
        string integrationTestsRoot = Path.Combine(repositoryRoot, "tests", "Integration.Tests");
        HashSet<string> allowedFacadeUsers =
        [
            Path.Combine(integrationTestsRoot, "Support", "AdminCliTestApplication.cs")
        ];

        string[] offenders = Directory
            .EnumerateFiles(integrationTestsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !allowedFacadeUsers.Contains(path))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("using Gma.Framework.Infrastructure;", StringComparison.Ordinal) ||
                       source.Contains("AddGmaInfrastructure()", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Aspire_admin_api_is_explicitly_opt_in()
    {
        string repositoryRoot = FindRepositoryRoot();
        string appHostRoot = Path.Combine(repositoryRoot, "src", "Hosts", "AppHost");
        string program = File.ReadAllText(Path.Combine(appHostRoot, "Program.cs"));
        string project = File.ReadAllText(Path.Combine(appHostRoot, "AppHost.csproj"));
        using JsonDocument appsettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(appHostRoot, "appsettings.json")));

        Assert.False(appsettings.RootElement
            .GetProperty("AppHost")
            .GetProperty("AdminApi")
            .GetProperty("Enabled")
            .GetBoolean());
        Assert.Contains("AppHost:AdminApi:Enabled", program, StringComparison.Ordinal);
        Assert.Contains("Projects.Host_AdminApi", program, StringComparison.Ordinal);
        Assert.Contains("host-admin-api", program, StringComparison.Ordinal);
        Assert.Contains("adminApi is { } configuredAdminApi", program, StringComparison.Ordinal);
        Assert.Contains("configuredAdminApi.WithReference(redis)", program, StringComparison.Ordinal);
        Assert.Contains("..\\Host.AdminApi\\Host.AdminApi.csproj", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Auth_member_persistence_uses_domain_length_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        Dictionary<string, string[]> expectedTokensByFile = new()
        {
            [Path.Combine("Configurations", "MemberConfiguration.cs")] =
            [
                "Member.PasswordHashMaxLength",
                "Member.DisabledReasonMaxLength",
                "HasIndex(member => new { member.ScopeId, member.RegisteredAtUtc })"
            ],
            [Path.Combine("Configurations", "MemberSessionConfiguration.cs")] =
            [
                "MemberSession.RefreshTokenHashMaxLength"
            ],
            [Path.Combine("Configurations", "MemberUsernameConfiguration.cs")] =
            [
                "MemberUsername.ValueMaxLength",
                "MemberUsername.NormalizedValueMaxLength"
            ]
        };
        string authPersistenceRoot = GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Persistence");
        string[] offenders = expectedTokensByFile
            .SelectMany(item =>
            {
                string path = Path.Combine(authPersistenceRoot, item.Key);
                string source = File.ReadAllText(path);

                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} missing {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Auth_password_validators_use_shared_password_policy()
    {
        string repositoryRoot = FindRepositoryRoot();
        string authApplicationRoot = GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Auth",
            "Gma.Modules.Auth.Application");
        string policySource = File.ReadAllText(Path.Combine(authApplicationRoot, "Security", "AuthPasswordPolicy.cs"));
        string[] passwordPolicyValidatorFiles =
        [
            Path.Combine(authApplicationRoot, "Validation", "AdminCreateMemberCommandValidator.cs"),
            Path.Combine(authApplicationRoot, "Validation", "RegisterMemberCommandValidator.cs"),
            Path.Combine(authApplicationRoot, "Validation", "ResetMemberPasswordCommandValidator.cs")
        ];
        string[] validatorOffenders = passwordPolicyValidatorFiles
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains("AuthPasswordPolicy.", StringComparison.Ordinal) ||
                       source.Contains("Length < 8", StringComparison.Ordinal) ||
                       source.Contains("at least 8", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should use AuthPasswordPolicy")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains("public const int MinimumLength = 15", policySource, StringComparison.Ordinal);
        Assert.Contains("public const string MinimumLengthMessage", policySource, StringComparison.Ordinal);
        Assert.Empty(validatorOffenders);
    }

    [Fact]
    public void Access_control_options_are_validated_on_start()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "AccessControl",
            "Gma.Modules.AccessControl.Application",
            "DependencyInjection.cs"));
        string[] requiredTokens =
        [
            "AccessControlOptionsValidation.GetValidatedOptions(configuration);",
            "IValidateOptions<AccessControlOptions>, AccessControlOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] offenders = requiredTokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Modules.AccessControl.Application dependency injection missing {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Design_time_factories_use_shared_persistence_helpers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("DesignTimeDbContextFactory.cs", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("private static string GetConnectionString", StringComparison.Ordinal) ||
                       source.Contains("private static DatabaseProvider GetProvider", StringComparison.Ordinal) ||
                       source.Contains("class DesignTimeScopeContext", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Design_time_factories_live_in_provider_migration_projects()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("DesignTimeDbContextFactory.cs", StringComparison.Ordinal))
            .Where(path =>
            {
                string? projectName = FindOwningProjectName(path);
                string source = File.ReadAllText(path);

                return projectName is null ||
                       !IsProviderMigrationProject(projectName) ||
                       (!source.Contains("DesignTimeDbContextOptionsFactory.CreateSqlServerOptions", StringComparison.Ordinal) &&
                        !source.Contains("DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions", StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persisted_modules_keep_sql_server_and_postgresql_migration_project_parity()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        string[] modulePaths = sourceLayout
            .ModuleRoots
            .Values
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] offenders = modulePaths
            .SelectMany(modulePath =>
            {
                string[] projectNames = Directory
                    .EnumerateFiles(modulePath, "*.csproj", SearchOption.AllDirectories)
                    .Where(path => !HasIgnoredPathSegment(path))
                    .Where(path => !HasPathSegment(path, "tests"))
                    .Select(path => Path.GetFileNameWithoutExtension(path)!)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                HashSet<string> projectNameSet = projectNames.ToHashSet(StringComparer.Ordinal);
                string[] persistenceProjects = projectNames
                    .Where(projectName => projectName.EndsWith(".Persistence", StringComparison.Ordinal))
                    .ToArray();
                string[] migrationProjectsWithoutPersistence = projectNames
                    .Where(IsProviderMigrationProject)
                    .Select(projectName =>
                    {
                        string persistenceProjectName = projectName
                            .Replace(".Persistence.SqlServerMigrations", ".Persistence", StringComparison.Ordinal)
                            .Replace(".Persistence.PostgreSqlMigrations", ".Persistence", StringComparison.Ordinal);

                        return projectNameSet.Contains(persistenceProjectName)
                            ? null
                            : $"{Path.GetRelativePath(repositoryRoot, modulePath)}: {projectName} has no {persistenceProjectName}";
                    })
                    .Where(offender => offender is not null)
                    .Select(offender => offender!)
                    .ToArray();

                string[] persistenceProjectsWithoutMigrations = persistenceProjects
                    .SelectMany(projectName =>
                    {
                        string sqlServerMigrationProject = $"{projectName}.SqlServerMigrations";
                        string postgreSqlMigrationProject = $"{projectName}.PostgreSqlMigrations";

                        return new[]
                        {
                            projectNameSet.Contains(sqlServerMigrationProject)
                                ? null
                                : $"{Path.GetRelativePath(repositoryRoot, modulePath)}: {projectName} missing {sqlServerMigrationProject}",
                            projectNameSet.Contains(postgreSqlMigrationProject)
                                ? null
                                : $"{Path.GetRelativePath(repositoryRoot, modulePath)}: {projectName} missing {postgreSqlMigrationProject}"
                        };
                    })
                    .Where(offender => offender is not null)
                    .Select(offender => offender!)
                    .ToArray();

                return migrationProjectsWithoutPersistence
                    .Concat(persistenceProjectsWithoutMigrations);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Verify_script_checks_provider_migration_drift()
    {
        string repositoryRoot = FindRepositoryRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "verify.ps1"));
        string migrationCheckScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "check-migrations.ps1"));
        string[] requiredVerifyTokens =
        [
            "check-migrations.ps1",
            "-NoBuild",
            "SkipMigrationCheck"
        ];
        string[] requiredCheckTokens =
        [
            "src\\Modules",
            "gma\\modules",
            "dotnet-ef",
            "has-pending-model-changes",
            ".Persistence.SqlServerMigrations",
            ".Persistence.PostgreSqlMigrations",
            "--startup-project",
            "--no-build"
        ];
        string[] offenders = requiredVerifyTokens
            .Where(token => !verifyScript.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/verify.ps1 missing {token}")
            .Concat(requiredCheckTokens
                .Where(token => !migrationCheckScript.Contains(token, StringComparison.Ordinal))
                .Select(token => $"eng/check-migrations.ps1 missing {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Test_scripts_keep_fast_and_docker_categories_separate()
    {
        string repositoryRoot = FindRepositoryRoot();
        string fastTestScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "test-fast.ps1"));
        string dockerTestScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "test-docker.ps1"));
        string verifyScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "verify.ps1"));
        string[] requiredFastTokens =
        [
            ". (Join-Path $PSScriptRoot 'common.ps1')",
            "GMA-Skeleton.slnx",
            "--filter",
            "Category!=Docker",
            "console;verbosity=minimal"
        ];
        string[] requiredDockerTokens =
        [
            ". (Join-Path $PSScriptRoot 'common.ps1')",
            "tests\\Integration.Tests\\Integration.Tests.csproj",
            "--filter",
            "Category=Docker",
            "$previousRequireDockerTests = $env:GMA_REQUIRE_DOCKER_TESTS",
            "$env:GMA_REQUIRE_DOCKER_TESTS = 'true'",
            "finally",
            "$env:GMA_REQUIRE_DOCKER_TESTS = $previousRequireDockerTests",
            "console;verbosity=minimal"
        ];

        string[] offenders = requiredFastTokens
            .Where(token => !fastTestScript.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/test-fast.ps1 missing {token}")
            .Concat(requiredDockerTokens
                .Where(token => !dockerTestScript.Contains(token, StringComparison.Ordinal))
                .Select(token => $"eng/test-docker.ps1 missing {token}"))
            .Concat(verifyScript.Contains("test-fast.ps1", StringComparison.Ordinal) &&
                    verifyScript.Contains("-NoBuild", StringComparison.Ordinal)
                ? []
                : ["eng/verify.ps1 should run eng/test-fast.ps1 with -NoBuild."])
            .Concat(verifyScript.Contains("test-docker.ps1", StringComparison.Ordinal)
                ? ["eng/verify.ps1 should not run Docker-backed tests by default."]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Docker_fact_availability_probe_is_bounded_and_cleans_up_timed_out_processes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string dockerFact = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "Integration.Tests",
            "Support",
            "DockerFactAttribute.cs"));
        string[] requiredTokens =
        [
            "GMA_REQUIRE_DOCKER_TESTS",
            "docker",
            "info",
            "TimeSpan.FromSeconds(10)",
            "CreateNoWindow = true",
            "KillTimedOutProcess(process)",
            "process.Kill(entireProcessTree: true)",
            "return false;"
        ];
        string[] offenders = requiredTokens
            .Where(token => !dockerFact.Contains(token, StringComparison.Ordinal))
            .Select(token => $"tests/Integration.Tests/Support/DockerFactAttribute.cs missing {token}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Add_migration_script_preserves_single_dbcontext_discovery_as_array()
    {
        string repositoryRoot = FindRepositoryRoot();
        string addMigrationScript = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "add-migration.ps1"));

        Assert.Contains("$contextNames = @($contextNames | Sort-Object -Unique)", addMigrationScript, StringComparison.Ordinal);
        Assert.DoesNotContain("$contextNames = $contextNames | Sort-Object -Unique", addMigrationScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_package_references_are_cli_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumeratePackageReferences(repositoryRoot)
            .Where(reference => string.Equals(reference.PackageId, "System.CommandLine", StringComparison.Ordinal))
            .Where(reference => !IsTestProjectPath(reference.ProjectPath))
            .Where(reference => !IsCliProject(reference.ProjectPath))
            .Select(reference => $"{reference.ProjectPath}:{reference.PackageId}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Admin_cli_front_doors_route_output_through_shared_cli_output()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] forbiddenTokens =
        [
            "Console.WriteLine",
            "Console.Error.WriteLine",
            "Console.Error.Write("
        ];
        string[] cliFrontDoorFiles =
        [
            .. Directory
            .EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path
                .GetRelativePath(repositoryRoot, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.EndsWith(".AdminCli", StringComparison.Ordinal))),
            Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminCli", "Program.cs")
        ];
        string[] offenders = cliFrontDoorFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} uses {token}; use AdminCliOutput instead.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Production_projects_use_shared_cqrs_validation_contracts_by_default()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] forbiddenPackages =
        [
            "FluentValidation",
            "FluentValidation.AspNetCore",
            "FluentValidation.DependencyInjectionExtensions"
        ];
        string[] offenders = EnumeratePackageReferences(repositoryRoot)
            .Where(reference => IsProductionProjectPath(reference.ProjectPath))
            .Where(reference => forbiddenPackages.Contains(reference.PackageId, StringComparer.Ordinal))
            .Select(reference => $"{reference.ProjectPath}:{reference.PackageId}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_front_doors_use_named_admin_operation_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path =>
            {
                string? projectName = FindOwningProjectName(path);
                return projectName is not null &&
                       (projectName.EndsWith(".AdminCli", StringComparison.Ordinal) ||
                        projectName.EndsWith(".AdminApi", StringComparison.Ordinal));
            })
            .Where(path => AdminOperationStringLiteralPattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_operation_name_constants_are_valid()
    {
        AdminPermission dummyPermission = AdminPermission.Create("system.operation");
        string[] offenders = ArchitectureCatalog.ModuleBoundaryAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsAbstract && type.IsSealed && type.Name.EndsWith("AdminOperationNames", StringComparison.Ordinal))
            .SelectMany(type => type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(field => new
                {
                    Type = type,
                    Field = field,
                    Value = field.GetRawConstantValue() as string,
                }))
            .Where(item =>
                item.Value is null ||
                !AdminOperation.TryCreate(item.Value, dummyPermission, out AdminOperation? operation) ||
                !string.Equals(operation.Name, item.Value, StringComparison.Ordinal))
            .Select(item => $"{item.Type.FullName}.{item.Field.Name}={item.Value ?? "<null>"}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_surface_constants_match_operation_and_permission_code_prefixes()
    {
        string[] offenders = ArchitectureCatalog.ModuleProjects
            .SelectMany(project => project.Assembly
                .GetTypes()
                .Where(type => type.IsAbstract && type.IsSealed)
                .Where(type => type.Name.EndsWith("AdminOperationNames", StringComparison.Ordinal) ||
                               type.Name.EndsWith("PermissionCodes", StringComparison.Ordinal))
                .Select(type => new
                {
                    Project = project,
                    Type = type,
                    SurfaceName = ResolveAdminSurfaceName(project.ModulePrefix),
                }))
            .SelectMany(item => item.Type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(field => new
                {
                    item.Project,
                    item.Type,
                    Field = field,
                    item.SurfaceName,
                    Value = field.GetRawConstantValue() as string,
                }))
            .Where(item => item.Value is null ||
                           !item.Value.StartsWith(item.SurfaceName + ".", StringComparison.Ordinal))
            .Select(item =>
                $"{item.Project.ProjectName}:{item.Type.Name}.{item.Field.Name}={item.Value ?? "<null>"} expected prefix {item.SurfaceName}.")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_permissions_use_named_permission_code_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => AdminPermissionStringLiteralPattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Administration_persistence_uses_shared_rbac_normalizers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string administrationPersistenceRoot = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "Administration",
            "Gma.Modules.Administration.Persistence");
        string[] forbiddenTokens =
        [
            "actorId.Trim()",
            "principalId.Trim()",
            "permissionCode.Trim().ToLowerInvariant()"
        ];
        string[] offenders = EnumerateSourceFiles(administrationPersistenceRoot)
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Admin_audit_writers_use_named_result_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] checkedFiles =
        [
            GmaSourceLayout.FrameworkPath(
                repositoryRoot,
                "Gma.Framework.Administration",
                "AdminOperationRunner.cs"),
            GmaSourceLayout.ModulePath(
                repositoryRoot,
                "AccessControl",
                "Gma.Modules.AccessControl.AdminCli",
                "AccessControlAdminCliModule.cs")
        ];
        string[] forbiddenTokens =
        [
            "\"succeeded\"",
            "\"denied\"",
            "\"failed\""
        ];
        string[] offenders = checkedFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Administration_audit_persistence_uses_named_length_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyDictionary<string, string[]> expectedTokensByFile = new Dictionary<string, string[]>
        {
            [Path.Combine("Configurations", "AdminAuditEntryConfiguration.cs")] =
            [
                "AdminActor.MaxLength",
                "TenantIds.MaxLength",
                "AdminOperation.MaxLength",
                "AdminPermission.MaxLength",
                "AdminAuditResults.MaxLength",
                "AdminAuditRecord.ErrorCodeMaxLength"
            ]
        };
        string administrationPersistenceRoot = GmaSourceLayout.ModulePath(
            repositoryRoot,
            "Administration",
            "Gma.Modules.Administration.Persistence");
        string[] offenders = expectedTokensByFile
            .SelectMany(item =>
            {
                string path = Path.Combine(administrationPersistenceRoot, item.Key);
                string source = File.ReadAllText(path);

                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} missing {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Access_control_persistence_uses_named_length_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyDictionary<string, string[]> expectedTokensByFile = new Dictionary<string, string[]>
        {
            [Path.Combine("Configurations", "AccessPrincipalConfiguration.cs")] =
            [
                "AccessSubject.IdMaxLength"
            ],
            [Path.Combine("Configurations", "AccessRoleConfiguration.cs")] =
            [
                "AccessControlRoleName.MaxLength"
            ],
            [Path.Combine("Configurations", "AccessRolePermissionConfiguration.cs")] =
            [
                "PermissionCode.MaxLength"
            ],
            [Path.Combine("Configurations", "AccessSubjectRoleAssignmentConfiguration.cs")] =
            [
                "AccessSubject.IdMaxLength",
                "AccessScope.MaxLength",
                "HasIndex(assignment => new { assignment.SubjectKind, assignment.SubjectId, assignment.ScopeValue })"
            ]
        };
        string accessControlPersistenceRoot = GmaSourceLayout.ModulePath(
            repositoryRoot,
            "AccessControl",
            "Gma.Modules.AccessControl.Persistence");
        string[] offenders = expectedTokensByFile
            .SelectMany(item =>
            {
                string path = Path.Combine(accessControlPersistenceRoot, item.Key);
                string source = File.ReadAllText(path);

                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} missing {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_inbox_outbox_persistence_uses_shared_message_configuration_helpers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        Dictionary<string, string> expectedCallsByFileName = new()
        {
            ["InboxMessageConfiguration.cs"] = "ConfigureInboxMessage()",
            ["OutboxMessageConfiguration.cs"] = "ConfigureOutboxMessage()"
        };
        string[] repeatedMappingTokens =
        [
            "InboxMessage.HandlerMaxLength",
            "InboxMessage.SubjectMaxLength",
            "InboxMessage.EventTypeMaxLength",
            "InboxMessage.LockedByMaxLength",
            "InboxMessage.LastErrorMaxLength",
            "OutboxMessage.SubjectMaxLength",
            "OutboxMessage.EventTypeMaxLength",
            "OutboxMessage.LockedByMaxLength"
        ];

        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => expectedCallsByFileName.ContainsKey(Path.GetFileName(path)))
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string expectedCall = expectedCallsByFileName[Path.GetFileName(path)];

                return (source.Contains(expectedCall, StringComparison.Ordinal)
                        ? Array.Empty<string>()
                        : [$"{Path.GetRelativePath(repositoryRoot, path)} missing {expectedCall}"])
                    .Concat(repeatedMappingTokens
                        .Where(token => source.Contains(token, StringComparison.Ordinal))
                        .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} repeats {token} instead of using shared messaging configuration helpers"));
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] scaffoldOffenders = expectedCallsByFileName
            .Values
            .Where(call => !scaffolder.Contains(call, StringComparison.Ordinal))
            .Select(call => $"src/Framework/eng/new-module.ps1 missing {call}")
            .Concat(repeatedMappingTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 repeats {token} instead of using shared messaging configuration helpers"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders.Concat(scaffoldOffenders));
    }

    [Fact]
    public void Example_domain_persistence_uses_domain_length_and_precision_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        Dictionary<string, string[]> expectedTokensByPath = new()
        {
            [Path.Combine("Catalog", "Catalog.Persistence", "Configurations", "CatalogItemConfiguration.cs")] =
            [
                "CatalogItem.SkuMaxLength",
                "CatalogItem.NameMaxLength",
                "CatalogItem.CurrencyLength",
                "CatalogItem.PricePrecision",
                "CatalogItem.PriceScale"
            ],
            [Path.Combine("Ordering", "Ordering.Persistence", "Configurations", "OrderConfiguration.cs")] =
            [
                "Order.CatalogSkuMaxLength",
                "Order.CatalogItemNameMaxLength",
                "Order.CurrencyLength",
                "Order.AmountPrecision",
                "Order.AmountScale"
            ],
            [Path.Combine("Ordering", "Ordering.Persistence", "Configurations", "CatalogItemProjectionConfiguration.cs")] =
            [
                "Order.CatalogSkuMaxLength",
                "Order.CatalogItemNameMaxLength",
                "Order.CurrencyLength",
                "Order.AmountPrecision",
                "Order.AmountScale"
            ]
        };
        string[] offenders = expectedTokensByPath
            .SelectMany(item =>
            {
                string path = ModulePathFromRelative(repositoryRoot, item.Key);
                string source = File.ReadAllText(path);

                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} missing {token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Tenant_aware_module_dbcontexts_use_shared_tenant_conventions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        Dictionary<string, string[]> expectedTokensByPath = new()
        {
            [Path.Combine("Auth", "Gma.Modules.Auth.Persistence", "AuthDbContext.cs")] =
            [
                ": ScopeAwareDbContext<AuthDbContext>(options, scopeContext)",
                "this.ApplyScopeConventions(modelBuilder);"
            ],
            [Path.Combine("Catalog", "Catalog.Persistence", "CatalogDbContext.cs")] =
            [
                ": ScopeAwareDbContext<CatalogDbContext>(options, scopeContext)",
                "this.ApplyScopeConventions(modelBuilder);"
            ],
            [Path.Combine("Ordering", "Ordering.Persistence", "OrderingDbContext.cs")] =
            [
                ": ScopeAwareDbContext<OrderingDbContext>(options, scopeContext)",
                "this.ApplyScopeConventions(modelBuilder);"
            ],
            [Path.Combine("Notifications", "Gma.Modules.Notifications.Persistence", "NotificationsDbContext.cs")] =
            [
                ": ScopeAwareDbContext<NotificationsDbContext>(options, scopeContext)",
                "this.ApplyScopeConventions(modelBuilder);"
            ]
        };
        string[] offenders = expectedTokensByPath
            .SelectMany(item =>
            {
                string path = ModulePathFromRelative(repositoryRoot, item.Key);
                string source = File.ReadAllText(path);

                return item.Value
                    .Where(token => !source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} missing {token}")
                    .Concat(source.Contains(".HasQueryFilter(\"TenantFilter\"", StringComparison.Ordinal)
                        ? [$"{Path.GetRelativePath(repositoryRoot, path)} still declares tenant filters manually"]
                        : Array.Empty<string>());
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] scaffoldRequiredTokens =
        [
            "using Gma.Framework.Persistence.EntityFrameworkCore;",
            "using Gma.Framework.Scoping;",
            @"$(GmaFrameworkRoot)Scoping\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
            "IScopeContext scopeContext",
            ": ScopeAwareDbContext<${Name}DbContext>(options, scopeContext)",
            "this.ApplyScopeConventions(modelBuilder);",
            "new DesignTimeScopeContext()"
        ];
        string[] scaffoldOffenders = scaffoldRequiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders.Concat(scaffoldOffenders));
    }

    [Fact]
    public void Tenant_scoped_module_entities_have_convention_filters_and_tenant_length()
    {
        IEntityType[] entityTypes = CreateTenantConventionModelEntityTypes();
        string[] offenders = entityTypes
            .Where(type => typeof(IScopedEntity).IsAssignableFrom(type.ClrType))
            .SelectMany(type =>
            {
                List<string> failures = [];
                if (type.FindProperty(nameof(IScopedEntity.ScopeId))?.GetMaxLength() != ScopeIds.MaxLength)
                {
                    failures.Add($"{type.ClrType.FullName} ScopeId is not configured with ScopeIds.MaxLength");
                }

                if (!type.GetDeclaredQueryFilters().Any(filter =>
                        string.Equals(filter.Key, ScopeFilterNames.ScopeFilter, StringComparison.Ordinal)))
                {
                    failures.Add($"{type.ClrType.FullName} is missing named {ScopeFilterNames.ScopeFilter}");
                }

                return failures;
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persisted_tenant_convention_entities_are_classified_or_documented_infrastructure_exceptions()
    {
        HashSet<Type> allowedInfrastructureExceptions =
        [
            typeof(InboxMessage),
            typeof(OutboxMessage)
        ];
        string[] offenders = CreateTenantConventionModelEntityTypes()
            .Where(type => !typeof(IScopedEntity).IsAssignableFrom(type.ClrType))
            .Where(type => type.ClrType.GetCustomAttribute<GlobalEntityAttribute>() is null)
            .Where(type => type.ClrType.GetCustomAttribute<DisableScopeFilterAttribute>() is null)
            .Where(type => !allowedInfrastructureExceptions.Contains(type.ClrType))
            .Select(type => $"{type.ClrType.FullName} is not tenant-scoped, global, tenant-filter-disabled, or an allowed infrastructure exception")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Tenant_filter_disable_attributes_have_reasons()
    {
        string[] offenders = ArchitectureCatalog.ModuleBoundaryAssemblies
            .Append(typeof(ScopeFilterNames).Assembly)
            .Append(typeof(IScopedEntity).Assembly)
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => new
            {
                Type = type,
                Attribute = type.GetCustomAttribute<DisableScopeFilterAttribute>()
            })
            .Where(item => item.Attribute is not null && string.IsNullOrWhiteSpace(item.Attribute.Reason))
            .Select(item => item.Type.FullName ?? item.Type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Administration_front_doors_do_not_reference_access_control_persistence()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] projectFiles =
        [
            GmaSourceLayout.ModulePath(
                repositoryRoot,
                "Administration",
                "Gma.Modules.Administration.AdminApi",
                "Gma.Modules.Administration.AdminApi.csproj"),
            GmaSourceLayout.ModulePath(
                repositoryRoot,
                "Administration",
                "Gma.Modules.Administration.AdminCli",
                "Gma.Modules.Administration.AdminCli.csproj")
        ];
        string[] sourceFiles =
        [
            GmaSourceLayout.ModulePath(
                repositoryRoot,
                "Administration",
                "Gma.Modules.Administration.AdminApi",
                "AdministrationAdminApiModule.cs"),
            GmaSourceLayout.ModulePath(
                repositoryRoot,
                "Administration",
                "Gma.Modules.Administration.AdminCli",
                "AdministrationAdminCliModule.cs")
        ];
        string[] offenders = projectFiles
            .SelectMany(path =>
            {
                XDocument project = XDocument.Load(path);
                return project
                    .Descendants("ProjectReference")
                    .Select(reference => NormalizePath(reference.Attribute("Include")?.Value ?? string.Empty))
                    .Where(reference => reference.Contains(
                        "Gma.Modules.AccessControl.Persistence",
                        StringComparison.Ordinal))
                    .Select(reference => $"{Path.GetRelativePath(repositoryRoot, path)} references {reference}");
            })
            .Concat(sourceFiles
                .Where(path => File.ReadAllText(path).Contains(
                        "Gma.Modules.AccessControl.Persistence",
                        StringComparison.Ordinal))
                .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} imports Gma.Modules.AccessControl.Persistence"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Access_control_role_name_length_is_not_hidden_in_regex()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.ModulePath(
            repositoryRoot,
            "AccessControl",
            "Gma.Modules.AccessControl.Application",
            "AccessControlRoleName.cs"));

        Assert.Contains("candidate.Length > MaxLength", source, StringComparison.Ordinal);
        Assert.DoesNotContain("{0,127}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_front_doors_use_named_module_identity_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path =>
            {
                string? projectName = FindOwningProjectName(path);
                return projectName is not null &&
                       (projectName.EndsWith(".Api", StringComparison.Ordinal) ||
                        projectName.EndsWith(".Admin", StringComparison.Ordinal) ||
                        projectName.EndsWith(".AdminApi", StringComparison.Ordinal));
            })
            .Where(path => ModuleNameStringLiteralPattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Framework_core_projects_keep_dependency_free_or_abstractions_only_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] dependencyFreeProjects =
        [
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Results", "Gma.Framework.Results.csproj"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Naming", "Gma.Framework.Naming.csproj"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Numerics", "Gma.Framework.Numerics.csproj"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Pagination", "Gma.Framework.Pagination.csproj"),
            GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Security", "Gma.Framework.Security.csproj")
        ];
        string notificationsProjectPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Notifications",
            "Gma.Framework.Notifications.csproj");
        XDocument notificationsProject = XDocument.Load(notificationsProjectPath);
        HashSet<string> allowedNotificationsProjectReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"),
            NormalizePath(@"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj"),
            NormalizePath(@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj")
        };
        string modulesProjectPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Modules",
            "Gma.Framework.Modules.csproj");
        XDocument modulesProject = XDocument.Load(modulesProjectPath);
        HashSet<string> allowedModulesProjectReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj")
        };
        string domainProjectPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Domain",
            "Gma.Framework.Domain.csproj");
        XDocument domainProject = XDocument.Load(domainProjectPath);
        HashSet<string> allowedDomainProjectReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
            NormalizePath(@"..\Gma.Framework.Numerics\Gma.Framework.Numerics.csproj")
        };
        string applicationProjectPath = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Application.Composition",
            "Gma.Framework.Application.Composition.csproj");
        XDocument applicationProject = XDocument.Load(applicationProjectPath);
        HashSet<string> allowedApplicationProjectReferences = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(@"..\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj"),
            NormalizePath(@"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj")
        };
        HashSet<string> allowedApplicationPackageReferences = new(StringComparer.Ordinal)
        {
            "Microsoft.Extensions.DependencyInjection.Abstractions"
        };
        string[] dependencyFreeProjectOffenders = dependencyFreeProjects
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);

                return project
                    .Descendants()
                    .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference")
                    .Select(element => $"{relativePath}->{element.Name.LocalName}:{element.Attribute("Include")?.Value}");
            })
            .ToArray();
        string[] applicationReferenceOffenders = applicationProject
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Where(reference => !allowedApplicationProjectReferences.Contains(NormalizePath(reference!)))
            .Select(reference => $"{Path.GetRelativePath(repositoryRoot, applicationProjectPath)}->ProjectReference:{reference}")
            .ToArray();
        string[] domainReferenceOffenders = domainProject
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Where(reference => !allowedDomainProjectReferences.Contains(NormalizePath(reference!)))
            .Select(reference => $"{Path.GetRelativePath(repositoryRoot, domainProjectPath)}->ProjectReference:{reference}")
            .ToArray();
        string[] domainPackageOrFrameworkOffenders = domainProject
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => $"{Path.GetRelativePath(repositoryRoot, domainProjectPath)}->{element.Name.LocalName}:{element.Attribute("Include")?.Value}")
            .ToArray();
        string[] modulesReferenceOffenders = modulesProject
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Where(reference => !allowedModulesProjectReferences.Contains(NormalizePath(reference!)))
            .Select(reference => $"{Path.GetRelativePath(repositoryRoot, modulesProjectPath)}->ProjectReference:{reference}")
            .ToArray();
        string[] modulesPackageOrFrameworkOffenders = modulesProject
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => $"{Path.GetRelativePath(repositoryRoot, modulesProjectPath)}->{element.Name.LocalName}:{element.Attribute("Include")?.Value}")
            .ToArray();
        string[] notificationReferenceOffenders = notificationsProject
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Where(reference => !allowedNotificationsProjectReferences.Contains(NormalizePath(reference!)))
            .Select(reference => $"{Path.GetRelativePath(repositoryRoot, notificationsProjectPath)}->ProjectReference:{reference}")
            .ToArray();
        string[] notificationPackageOrFrameworkOffenders = notificationsProject
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => $"{Path.GetRelativePath(repositoryRoot, notificationsProjectPath)}->{element.Name.LocalName}:{element.Attribute("Include")?.Value}")
            .ToArray();
        string[] applicationPackageOffenders = applicationProject
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Where(packageId => !allowedApplicationPackageReferences.Contains(packageId!))
            .Select(packageId => $"{Path.GetRelativePath(repositoryRoot, applicationProjectPath)}->PackageReference:{packageId}")
            .ToArray();
        string[] applicationFrameworkOffenders = applicationProject
            .Descendants("FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => $"{Path.GetRelativePath(repositoryRoot, applicationProjectPath)}->FrameworkReference:{reference}")
            .ToArray();
        Assert.Empty(dependencyFreeProjectOffenders
            .Concat(domainReferenceOffenders)
            .Concat(domainPackageOrFrameworkOffenders)
            .Concat(modulesReferenceOffenders)
            .Concat(modulesPackageOrFrameworkOffenders)
            .Concat(notificationReferenceOffenders)
            .Concat(notificationPackageOrFrameworkOffenders)
            .Concat(applicationReferenceOffenders)
            .Concat(applicationPackageOffenders)
            .Concat(applicationFrameworkOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Framework_application_boundary_projects_stay_source_narrow()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyDictionary<string, string[]> expectedProjectSources = new Dictionary<string, string[]>(
            StringComparer.Ordinal)
        {
            ["Gma.Framework.Application.Composition"] =
            [
                "ApplicationServiceCollectionExtensions.cs"
            ],
            ["Gma.Framework.Application.Events"] =
            [
                "IDomainEventDispatcher.cs",
                "IDomainEventHandler.cs"
            ]
        };

        string[] sourceShapeOffenders = expectedProjectSources
            .SelectMany(entry =>
            {
                string projectRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, entry.Key);
                string[] actualSources = Directory
                    .EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !HasIgnoredPathSegment(path))
                    .Select(path => NormalizePath(Path.GetRelativePath(projectRoot, path)))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                HashSet<string> expectedSources = new(entry.Value.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);

                return actualSources
                    .Where(source => !expectedSources.Contains(source))
                    .Select(source => $"{entry.Key} has unexpected source file {source}")
                    .Concat(expectedSources
                        .Where(source => !actualSources.Contains(source, StringComparer.OrdinalIgnoreCase))
                        .Select(source => $"{entry.Key} is missing source file {source}"));
            })
            .ToArray();
        IReadOnlyDictionary<string, string[]> forbiddenTokensByProject = new Dictionary<string, string[]>(
            StringComparer.Ordinal)
        {
            ["Gma.Framework.Application.Composition"] =
            [
                "DbContext",
                "IHostedService",
                "BackgroundService",
                "IEventBus",
                "IOutbox",
                "IInbox",
                "HybridCache",
                "IDistributedCache",
                "StackExchange",
                "Nats",
                "JetStream",
                "Endpoint",
                "HttpContext",
                "IApplicationBuilder",
                "WebApplication",
                "ITask",
                "IUnitOfWork"
            ],
            ["Gma.Framework.Application.Events"] =
            [
                "IServiceCollection",
                "Microsoft.Extensions.DependencyInjection",
                "IIntegrationEvent",
                "IntegrationEvent",
                "IEventBus",
                "IOutbox",
                "IInbox",
                "Outbox",
                "Inbox",
                "DbContext",
                "Nats",
                "JetStream"
            ]
        };
        string[] forbiddenSourceOffenders = forbiddenTokensByProject
            .SelectMany(entry =>
            {
                string projectRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, entry.Key);
                return Directory
                    .EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !HasIgnoredPathSegment(path))
                    .SelectMany(path =>
                    {
                        string source = File.ReadAllText(path);
                        string relativePath = Path.GetRelativePath(repositoryRoot, path);
                        return entry.Value
                            .Where(token => source.Contains(token, StringComparison.Ordinal))
                            .Select(token => $"{relativePath} contains forbidden boundary token '{token}'");
                    });
            })
            .ToArray();

        Assert.Empty(sourceShapeOffenders
            .Concat(forbiddenSourceOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void File_management_core_stays_dependency_neutral()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.FileManagement");
        XDocument project = XDocument.Load(Path.Combine(projectRoot, "Gma.Framework.FileManagement.csproj"));
        string[] packageReferences = GetProjectIncludes(project, "PackageReference");
        string[] projectReferences = GetProjectIncludes(project, "ProjectReference");
        string[] forbiddenSourceOffenders = Directory
            .EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                string[] forbiddenTokens =
                [
                    "using Microsoft.Extensions",
                    "using Gma.Framework.Tenancy",
                    "Gma.Framework.Tenancy",
                    "using Gma.Framework.ModuleComposition",
                    "Gma.Framework.ModuleComposition"
                ];

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{relativePath} contains forbidden dependency token '{token}'.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(packageReferences);
        Assert.Empty(projectReferences);
        Assert.Empty(forbiddenSourceOffenders);
    }

    [Fact]
    public void Framework_project_dependency_manifest_matches_intended_adapter_boundaries()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedRoot = GmaSourceLayout.FrameworkPath(repositoryRoot);
        FrameworkProjectShape[] expectedShapes =
        [
            new(
                "Gma.Framework.Administration",
                ["Microsoft.Extensions.Logging.Abstractions"],
                [],
                [
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\..\Results\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\..\Runtime\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\..\Tenancy\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Administration.AccessControl",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.Administration\Gma.Framework.Administration.csproj",
                    @"..\..\Security\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj",
                    @"..\..\Security\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj"
                ]),
            new(
                "Gma.Framework.Administration.Api",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Administration\Gma.Framework.Administration.csproj",
                    @"..\..\Api\Gma.Framework.Api\Gma.Framework.Api.csproj",
                    @"..\..\Cqrs\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\..\Results\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\..\Security\Gma.Framework.Security\Gma.Framework.Security.csproj",
                    @"..\..\Tenancy\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Administration.Cli",
                ["Microsoft.Extensions.Hosting", "System.CommandLine"],
                [],
                [
                    @"..\Gma.Framework.Administration\Gma.Framework.Administration.csproj",
                    @"..\..\Cqrs\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\..\Results\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\..\Runtime\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ]),
            new(
                "Gma.Framework.AccessControl",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj"
                ]),
            new(
                "Gma.Framework.AccessControl.AspNetCore",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj",
                    @"..\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj",
                    @"..\Gma.Framework.Security\Gma.Framework.Security.csproj"
                ]),
            new(
                "Gma.Framework.Api",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\..\Scoping\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Api.OpenApi",
                ["Swashbuckle.AspNetCore"],
                ["Microsoft.AspNetCore.App"],
                []),
            new(
                "Gma.Framework.Api.Production",
                [],
                ["Microsoft.AspNetCore.App"],
                [@"..\..\Cqrs\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"]),
            new(
                "Gma.Framework.Api.Production.EntityFrameworkCore",
                ["Microsoft.EntityFrameworkCore"],
                ["Microsoft.AspNetCore.App"],
                [@"..\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj"]),
            new(
                "Gma.Framework.Api.Serilog",
                ["Serilog.AspNetCore"],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Api\Gma.Framework.Api.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Api.Serilog",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Api.Serilog\Gma.Framework.Api.Serilog.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Application.Events.Infrastructure",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj",
                    @"..\Gma.Framework.Domain\Gma.Framework.Domain.csproj"
                ]),
            new(
                "Gma.Framework.Application.Composition",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj",
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj"
                ]),
            new(
                "Gma.Framework.Application.Events",
                [],
                [],
                [
                    @"..\Gma.Framework.Domain\Gma.Framework.Domain.csproj"
                ]),
            new(
                "Gma.Framework.Cqrs.Infrastructure",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj"
                ]),
            new(
                "Gma.Framework.Cqrs",
                [],
                [],
                [
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj"
                ]),
            new(
                "Gma.Framework.Permissions",
                [],
                [],
                [
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Caching",
                [],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Caching.Redis",
                [
                    "Microsoft.Extensions.Caching.StackExchangeRedis",
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Options.ConfigurationExtensions"
                ],
                [],
                [
                    @"..\Gma.Framework.Caching\Gma.Framework.Caching.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"
                ]),
            new(
                "Gma.Framework.Caching.Cqrs",
                [
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Caching\Gma.Framework.Caching.csproj",
                    @"..\Gma.Framework.Caching.Infrastructure\Gma.Framework.Caching.Infrastructure.csproj",
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\Gma.Framework.Cqrs.Infrastructure\Gma.Framework.Cqrs.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj"
                ]),
            new(
                "Gma.Framework.Caching.Infrastructure",
                [
                    "Microsoft.Extensions.Caching.Hybrid",
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Caching\Gma.Framework.Caching.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj"
                ]),
            new(
                "Gma.Framework.Domain",
                [],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Numerics\Gma.Framework.Numerics.csproj"
                ]),
            new(
                "Gma.Framework.FileManagement",
                [],
                [],
                []),
            new(
                "Gma.Framework.FileManagement.LocalStorage",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Options.ConfigurationExtensions"
                ],
                [],
                [
                    @"..\Gma.Framework.FileManagement\Gma.Framework.FileManagement.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"
                ]),
            new(
                "Gma.Framework.FileManagement.Minio",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Options.ConfigurationExtensions",
                    "Minio"
                ],
                [],
                [
                    @"..\Gma.Framework.FileManagement\Gma.Framework.FileManagement.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj"
                ]),
            new("Gma.Framework.Results", [], [], []),
            new(
                "Gma.Framework.Infrastructure",
                [
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Application.Events.Infrastructure\Gma.Framework.Application.Events.Infrastructure.csproj",
                    @"..\Gma.Framework.Cqrs.Infrastructure\Gma.Framework.Cqrs.Infrastructure.csproj",
                    @"..\Gma.Framework.Scoping.Infrastructure\Gma.Framework.Scoping.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj",
                    @"..\Gma.Framework.Tenancy.Cqrs\Gma.Framework.Tenancy.Cqrs.csproj",
                    @"..\Gma.Framework.Tenancy.Infrastructure\Gma.Framework.Tenancy.Infrastructure.csproj",
                    @"..\Gma.Framework.Tenancy.Scoping\Gma.Framework.Tenancy.Scoping.csproj"
                ]),
            new(
                "Gma.Framework.Logging.Serilog",
                ["Serilog.AspNetCore", "Serilog.Settings.Configuration", "Serilog.Sinks.Console"],
                [],
                []),
            new(
                "Gma.Framework.Messaging",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Numerics\Gma.Framework.Numerics.csproj"
                ]),
            new(
                "Gma.Framework.Messaging.Nats.Aspire",
                ["Aspire.NATS.Net"],
                [],
                [@"..\Gma.Framework.Messaging.Nats\Gma.Framework.Messaging.Nats.csproj"]),
            new(
                "Gma.Framework.ModuleComposition",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Messaging.Nats",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting",
                    "NATS.Net"
                ],
                [],
                [
                    @"..\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj",
                    @"..\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ]),
            new(
                "Gma.Framework.Messaging.Infrastructure",
                [
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Relational",
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj"
                ]),
            new(
                "Gma.Framework.Modules",
                [],
                [],
                [@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"]),
            new("Gma.Framework.Naming", [], [], []),
            new("Gma.Framework.Numerics", [], [], []),
            new(
                "Gma.Framework.Notifications",
                [],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Notifications.Cqrs",
                [
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\Gma.Framework.Cqrs.Infrastructure\Gma.Framework.Cqrs.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj",
                    @"..\Gma.Framework.Notifications.Infrastructure\Gma.Framework.Notifications.Infrastructure.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj"
                ]),
            new(
                "Gma.Framework.Notifications.Api",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Api\Gma.Framework.Api.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj",
                    @"..\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
                    @"..\Gma.Framework.Security\Gma.Framework.Security.csproj",
                ]),
            new(
                "Gma.Framework.Notifications.Infrastructure",
                [
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Logging.Abstractions",
                    "Microsoft.Extensions.Options.ConfigurationExtensions"
                ],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj"
                ]),
            new(
                "Gma.Framework.Notifications.SignalR",
                ["Microsoft.AspNetCore.Authentication.JwtBearer"],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
                    @"..\Gma.Framework.Security\Gma.Framework.Security.csproj",
                ]),
            new(
                "Gma.Framework.Realtime",
                [],
                [],
                [@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"]),
            new(
                "Gma.Framework.Realtime.Infrastructure",
                [
                    "Microsoft.Extensions.DependencyInjection.Abstractions",
                    "Microsoft.Extensions.Logging.Abstractions",
                    "Microsoft.Extensions.Options"
                ],
                [],
                [@"..\Gma.Framework.Realtime\Gma.Framework.Realtime.csproj"]),
            new(
                "Gma.Framework.Realtime.Notifications",
                [
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Hosting",
                    "Microsoft.Extensions.Options",
                    "Microsoft.Extensions.Options.ConfigurationExtensions"
                ],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Notifications\Gma.Framework.Notifications.csproj",
                    @"..\Gma.Framework.Realtime\Gma.Framework.Realtime.csproj",
                    @"..\Gma.Framework.Realtime.Infrastructure\Gma.Framework.Realtime.Infrastructure.csproj"
                ]),
            new(
                "Gma.Framework.Observability",
                [],
                [],
                [@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"]),
            new("Gma.Framework.Pagination", [], [], []),
            new(
                "Gma.Framework.Scoping",
                [],
                [],
                [
                    @"..\..\Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\..\Modules\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\..\Results\Gma.Framework.Results\Gma.Framework.Results.csproj"
                ]),
            new(
                "Gma.Framework.Scoping.Infrastructure",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
                    @"..\..\Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Observability.Infrastructure",
                ["Microsoft.Extensions.Options.ConfigurationExtensions"],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ]),
            new(
                "Gma.Framework.Persistence.EntityFrameworkCore",
                [
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Options.ConfigurationExtensions",
                    "Npgsql.EntityFrameworkCore.PostgreSQL"
                ],
                [],
                [
                    @"..\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj",
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\Gma.Framework.Domain\Gma.Framework.Domain.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj"
                ]),
            new(
                "Gma.Framework.ProjectionRebuild",
                [
                    "Microsoft.Extensions.DependencyInjection.Abstractions",
                    "Microsoft.Extensions.Options"
                ],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ]),
            new(
                "Gma.Framework.ProjectionRebuild.Tasks",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.ProjectionRebuild\Gma.Framework.ProjectionRebuild.csproj",
                    @"..\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"
                ]),
            new(
                "Gma.Framework.ProjectionRebuild.EntityFrameworkCore",
                [
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Relational"
                ],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.ProjectionRebuild\Gma.Framework.ProjectionRebuild.csproj"
                ]),
            new(
                "Gma.Framework.Runtime.Infrastructure",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Options.ConfigurationExtensions",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ]),
            new(
                "Gma.Framework.Tasks",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Tasks.Cqrs",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Cqrs\Gma.Framework.Cqrs.csproj",
                    @"..\Gma.Framework.Cqrs.Infrastructure\Gma.Framework.Cqrs.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"
                ]),
            new(
                "Gma.Framework.Tasks.Infrastructure",
                [
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Relational",
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Observability.Infrastructure\Gma.Framework.Observability.Infrastructure.csproj",
                    @"..\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj",
                    @"..\Gma.Framework.Runtime.Infrastructure\Gma.Framework.Runtime.Infrastructure.csproj",
                    @"..\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj"
                ]),
            new(
                "Gma.Framework.Runtime",
                [],
                [],
                [@"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj"]),
            new("Gma.Framework.Security", [], [], []),
            new(
                "Gma.Framework.Tenancy",
                [],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Modules\Gma.Framework.Modules.csproj",
                    @"..\Gma.Framework.Results\Gma.Framework.Results.csproj",
                    @"..\..\Scoping\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.AccessControl.AspNetCore",
                [],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj",
                    @"..\..\Security\Gma.Framework.AccessControl\Gma.Framework.AccessControl.csproj",
                    @"..\..\Security\Gma.Framework.AccessControl.AspNetCore\Gma.Framework.AccessControl.AspNetCore.csproj",
                    @"..\..\Security\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Infrastructure",
                [
                    "Microsoft.Extensions.Configuration.Binder",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Caching",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Caching\Gma.Framework.Caching.csproj",
                    @"..\Gma.Framework.Caching.Infrastructure\Gma.Framework.Caching.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Cqrs",
                [
                    "Microsoft.Extensions.DependencyInjection.Abstractions",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Cqrs.Infrastructure\Gma.Framework.Cqrs.Infrastructure.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Observability\Gma.Framework.Observability.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Messaging",
                [],
                [],
                [
                    @"..\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Messaging.Infrastructure",
                [
                    "Microsoft.Extensions.DependencyInjection.Abstractions",
                    "Microsoft.Extensions.Hosting"
                ],
                [],
                [
                    @"..\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj",
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj",
                    @"..\Gma.Framework.Tenancy.Messaging\Gma.Framework.Tenancy.Messaging.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Scoping",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj",
                    @"..\..\Scoping\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj",
                    @"..\..\Scoping\Gma.Framework.Scoping.Infrastructure\Gma.Framework.Scoping.Infrastructure.csproj",
                    @"..\..\Modules\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\..\Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj"
                ]),
            new(
                "Gma.Framework.Tenancy.Tasks",
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Gma.Framework.Tasks\Gma.Framework.Tasks.csproj",
                    @"..\Gma.Framework.Tasks.Infrastructure\Gma.Framework.Tasks.Infrastructure.csproj",
                    @"..\Gma.Framework.Tenancy\Gma.Framework.Tenancy.csproj"
                ])
        ];
        HashSet<string> expectedProjectNames = expectedShapes
            .Select(shape => shape.ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] undocumentedFrameworkProjects = EnumerateProjectFiles(sharedRoot)
            .Where(path => !IsTestProjectPath(path))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(projectName => projectName is not null && !expectedProjectNames.Contains(projectName))
            .Select(projectName => $"Framework project '{projectName}' missing dependency manifest entry.")
            .ToArray();
        string[] manifestOffenders = expectedShapes
            .SelectMany(shape =>
            {
                string projectPath = GmaSourceLayout.FrameworkPath(repositoryRoot, shape.ProjectName, $"{shape.ProjectName}.csproj");
                XDocument project = XDocument.Load(projectPath);
                string relativePath = CanonicalRelativePath(repositoryRoot, projectPath);

                return CompareDependencySet(relativePath, "PackageReference", shape.PackageReferences, GetProjectIncludes(project, "PackageReference"))
                    .Concat(CompareDependencySet(relativePath, "FrameworkReference", shape.FrameworkReferences, GetProjectIncludes(project, "FrameworkReference")))
                    .Concat(CompareDependencySet(relativePath, "ProjectReference", shape.ProjectReferences, GetProjectIncludes(project, "ProjectReference")));
            })
            .ToArray();

        Assert.Empty(undocumentedFrameworkProjects
            .Concat(manifestOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Projects_under_src_and_tests_reference_framework_packages_they_import_directly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string srcRoot = Path.Combine(repositoryRoot, "src");
        string testsRoot = Path.Combine(repositoryRoot, "tests");
        string frameworkRoot = GmaSourceLayout.FrameworkPath(repositoryRoot);
        string[] frameworkProjectNames = EnumerateProjectFiles(frameworkRoot)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(projectName => !string.IsNullOrWhiteSpace(projectName))
            .Select(projectName => projectName!)
            .OrderByDescending(projectName => projectName.Length)
            .ToArray();
        string[] scannedRoots = [srcRoot, testsRoot];
        string[] offenders = scannedRoots
            .SelectMany(root => EnumerateProjectFiles(root))
            .SelectMany(projectPath =>
            {
                string projectName = Path.GetFileNameWithoutExtension(projectPath);
                string projectDirectory = Path.GetDirectoryName(projectPath)!;
                string relativeProjectPath = Path.GetRelativePath(repositoryRoot, projectPath);
                HashSet<string> directReferences = XDocument
                    .Load(projectPath)
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => GetProjectReferenceName(reference!))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return EnumerateSourceFiles(projectDirectory)
                    .SelectMany(sourcePath =>
                    {
                        string source = File.ReadAllText(sourcePath);
                        string relativeSourcePath = Path.GetRelativePath(repositoryRoot, sourcePath);

                        return FrameworkUsingNamespacePattern()
                            .Matches(source)
                            .Select(match => match.Groups["namespace"].Value)
                            .Distinct(StringComparer.Ordinal)
                            .Select(importedNamespace => new
                            {
                                SourcePath = relativeSourcePath,
                                ImportedNamespace = importedNamespace,
                                FrameworkProjectName = FindBestProjectNamespaceMatch(importedNamespace, frameworkProjectNames)
                            });
                    })
                    .Where(import =>
                        import.FrameworkProjectName is not null &&
                        !string.Equals(import.FrameworkProjectName, projectName, StringComparison.OrdinalIgnoreCase) &&
                        !directReferences.Contains(import.FrameworkProjectName))
                    .Select(import =>
                        $"{relativeProjectPath} imports {import.FrameworkProjectName} via {import.SourcePath}:{import.ImportedNamespace} without a direct ProjectReference")
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Projects_under_src_and_tests_reference_module_projects_they_import_directly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string srcRoot = Path.Combine(repositoryRoot, "src");
        string testsRoot = Path.Combine(repositoryRoot, "tests");
        string modulesRoot = Path.Combine(srcRoot, "Modules");
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        string[] moduleNamespaceRoots = sourceLayout
            .ModuleRoots
            .Keys
            .OrderByDescending(moduleName => moduleName.Length)
            .ToArray();
        string[] moduleProjectNames = EnumerateProjectFiles(modulesRoot)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(projectName => !string.IsNullOrWhiteSpace(projectName))
            .Select(projectName => projectName!)
            .OrderByDescending(projectName => projectName.Length)
            .ToArray();
        string[] scannedRoots = [srcRoot, testsRoot];
        string[] offenders = scannedRoots
            .SelectMany(root => EnumerateProjectFiles(root))
            .SelectMany(projectPath =>
            {
                string projectName = Path.GetFileNameWithoutExtension(projectPath);
                string projectDirectory = Path.GetDirectoryName(projectPath)!;
                string relativeProjectPath = Path.GetRelativePath(repositoryRoot, projectPath);
                HashSet<string> directReferences = XDocument
                    .Load(projectPath)
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => GetProjectReferenceName(reference!))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return EnumerateSourceFiles(projectDirectory)
                    .SelectMany(sourcePath =>
                    {
                        string source = File.ReadAllText(sourcePath);
                        string relativeSourcePath = Path.GetRelativePath(repositoryRoot, sourcePath);

                        return FindUsingNamespaces(source, moduleNamespaceRoots)
                            .Distinct(StringComparer.Ordinal)
                            .Select(importedNamespace => new
                            {
                                SourcePath = relativeSourcePath,
                                ImportedNamespace = importedNamespace,
                                ModuleProjectName = FindBestProjectNamespaceMatch(importedNamespace, moduleProjectNames)
                            });
                    })
                    .Where(import =>
                        import.ModuleProjectName is not null &&
                        !string.Equals(import.ModuleProjectName, projectName, StringComparison.OrdinalIgnoreCase) &&
                        !directReferences.Contains(import.ModuleProjectName))
                    .Select(import =>
                        $"{relativeProjectPath} imports {import.ModuleProjectName} via {import.SourcePath}:{import.ImportedNamespace} without a direct ProjectReference")
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Framework_infrastructure_facade_stays_tiny_and_host_level_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        string facadeRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Infrastructure");
        string[] expectedSourceFiles =
        [
            NormalizePath("DependencyInjection.cs"),
            NormalizePath(Path.Combine("Properties", "AssemblyInfo.cs"))
        ];
        string[] sourceFiles = Directory
            .EnumerateFiles(facadeRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Select(path => NormalizePath(Path.GetRelativePath(facadeRoot, path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string dependencyInjection = File.ReadAllText(Path.Combine(facadeRoot, "DependencyInjection.cs"));
        string[] requiredTokens =
        [
            "public static IHostApplicationBuilder AddGmaInfrastructure(this IHostApplicationBuilder builder)",
            "builder.AddTenancyInfrastructure();",
            "builder.AddRuntimeInfrastructure();",
            "builder.AddApplicationEventsInfrastructure();",
            "builder.AddCqrsInfrastructure();",
            "GmaInfrastructureRegistrationMarker"
        ];
        string[] forbiddenTokens =
        [
            "AddCachingCqrs",
            "AddCachingInfrastructure",
            "AddMessagingInfrastructure",
            "AddNats",
            "AddRedis",
            "AddTask",
            "AddPersistence",
            "HybridCache",
            "IEventBus",
            "IHostedService",
            "DbContext",
            "EntityFrameworkCore"
        ];

        Assert.Equal(expectedSourceFiles.Order(StringComparer.OrdinalIgnoreCase), sourceFiles);
        Assert.Empty(requiredTokens
            .Where(token => !dependencyInjection.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Framework.Infrastructure facade missing {token}")
            .ToArray());
        Assert.Empty(forbiddenTokens
            .Where(token => dependencyInjection.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Framework.Infrastructure facade must not compose optional adapter/runtime concern {token}")
            .ToArray());
    }

    [Fact]
    public void Framework_infrastructure_facade_project_references_stay_host_or_facade_tests_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        HashSet<string> allowedProjectPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(Path.Combine("src", "Hosts", "Host.Api", "Host.Api.csproj")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.AdminApi", "Host.AdminApi.csproj")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.AdminCli", "Host.AdminCli.csproj")),
            NormalizePath(Path.Combine("src", "Hosts", "Host.Worker", "Host.Worker.csproj")),
            NormalizePath(Path.Combine("tests", "Integration.Tests", "Integration.Tests.csproj")),
            NormalizePath(Path.Combine("src", "Framework", "tests", "Gma.Framework.Tests", "Gma.Framework.Tests.csproj"))
        };

        string[] offenders = Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsUnder(path, Path.Combine(repositoryRoot, "src")) ||
                           IsUnder(path, Path.Combine(repositoryRoot, "tests")))
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                string relativeProjectPath = NormalizePath(Path.GetRelativePath(repositoryRoot, projectPath));
                XDocument project = XDocument.Load(projectPath);

                return project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(referencePath => !string.IsNullOrWhiteSpace(referencePath))
                    .Where(referencePath => referencePath!.Contains("Gma.Framework.Infrastructure", StringComparison.OrdinalIgnoreCase))
                    .Where(_ => !allowedProjectPaths.Contains(relativeProjectPath))
                    .Select(referencePath => $"{relativeProjectPath}->{referencePath}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Runtime_host_project_dependency_manifest_matches_explicit_composition_boundaries()
    {
        string repositoryRoot = FindRepositoryRoot();
        string srcRoot = Path.Combine(repositoryRoot, "src");
        HostProjectShape[] expectedShapes =
        [
            new(
                Path.Combine("Hosts", "AppHost", "AppHost.csproj"),
                [
                    "Aspire.Hosting.AppHost",
                    "Aspire.Hosting.Nats",
                    "Aspire.Hosting.PostgreSQL",
                    "Aspire.Hosting.Redis",
                    "Aspire.Hosting.SqlServer",
                    "MessagePack"
                ],
                [],
                [
                    @"..\Host.AdminApi\Host.AdminApi.csproj",
                    @"..\Host.Api\Host.Api.csproj",
                    @"..\Host.Worker\Host.Worker.csproj"
                ]),
            new(
                Path.Combine("Hosts", "Host.AdminApi", "Host.AdminApi.csproj"),
                [],
                [],
                [
                    @"..\Modules\Administration\Gma.Modules.Administration.AdminApi\Gma.Modules.Administration.AdminApi.csproj",
                    @"..\Modules\Administration\Gma.Modules.Administration.Persistence.PostgreSqlMigrations\Gma.Modules.Administration.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Administration\Gma.Modules.Administration.Persistence.SqlServerMigrations\Gma.Modules.Administration.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.AdminApi\Gma.Modules.AccessControl.AdminApi.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations\Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.Persistence.SqlServerMigrations\Gma.Modules.AccessControl.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.AdminApi\Gma.Modules.Auth.AdminApi.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Contracts\Gma.Modules.Auth.Contracts.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.PostgreSqlMigrations\Gma.Modules.Auth.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.SqlServerMigrations\Gma.Modules.Auth.Persistence.SqlServerMigrations.csproj",
                    @"..\..\ServiceDefaults\ServiceDefaults.csproj",
                    @"..\Framework\Gma.Framework.Administration.Api\Gma.Framework.Administration.Api.csproj",
                    @"..\Framework\Gma.Framework.Api\Gma.Framework.Api.csproj",
                    @"..\Framework\Gma.Framework.Api.OpenApi\Gma.Framework.Api.OpenApi.csproj",
                    @"..\Framework\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj",
                    @"..\Framework\Gma.Framework.Api.Production.EntityFrameworkCore\Gma.Framework.Api.Production.EntityFrameworkCore.csproj",
                    @"..\Framework\Gma.Framework.Api.Serilog\Gma.Framework.Api.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Caching.Cqrs\Gma.Framework.Caching.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Caching.Redis\Gma.Framework.Caching.Redis.csproj",
                    @"..\Framework\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Logging.Serilog\Gma.Framework.Logging.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Nats.Aspire\Gma.Framework.Messaging.Nats.Aspire.csproj",
                    @"..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Api.Serilog\Gma.Framework.Tenancy.Api.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Caching\Gma.Framework.Tenancy.Caching.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Messaging.Infrastructure\Gma.Framework.Tenancy.Messaging.Infrastructure.csproj"
                ]),
            new(
                Path.Combine("Hosts", "Host.AdminCli", "Host.AdminCli.csproj"),
                ["Microsoft.Extensions.Hosting", "System.CommandLine"],
                [],
                [
                    @"..\Modules\Administration\Gma.Modules.Administration.AdminCli\Gma.Modules.Administration.AdminCli.csproj",
                    @"..\Modules\Administration\Gma.Modules.Administration.Persistence.PostgreSqlMigrations\Gma.Modules.Administration.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Administration\Gma.Modules.Administration.Persistence.SqlServerMigrations\Gma.Modules.Administration.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.AdminCli\Gma.Modules.AccessControl.AdminCli.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations\Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\AccessControl\Gma.Modules.AccessControl.Persistence.SqlServerMigrations\Gma.Modules.AccessControl.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.AdminCli\Gma.Modules.Auth.AdminCli.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Contracts\Gma.Modules.Auth.Contracts.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.PostgreSqlMigrations\Gma.Modules.Auth.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.SqlServerMigrations\Gma.Modules.Auth.Persistence.SqlServerMigrations.csproj",
                    @"..\Framework\Gma.Framework.Administration.Cli\Gma.Framework.Administration.Cli.csproj",
                    @"..\Framework\Gma.Framework.Caching.Cqrs\Gma.Framework.Caching.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Caching.Redis\Gma.Framework.Caching.Redis.csproj",
                    @"..\Framework\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Caching\Gma.Framework.Tenancy.Caching.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Messaging.Infrastructure\Gma.Framework.Tenancy.Messaging.Infrastructure.csproj"
                ]),
            new(
                Path.Combine("Hosts", "Host.Api", "Host.Api.csproj"),
                [],
                [],
                [
                    @"..\Modules\Auth\Gma.Modules.Auth.Api\Gma.Modules.Auth.Api.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Contracts\Gma.Modules.Auth.Contracts.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.PostgreSqlMigrations\Gma.Modules.Auth.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.SqlServerMigrations\Gma.Modules.Auth.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\Tenancy\Gma.Modules.Tenancy.Api\Gma.Modules.Tenancy.Api.csproj",
                    @"..\..\ServiceDefaults\ServiceDefaults.csproj",
                    @"..\Framework\Gma.Framework.Api\Gma.Framework.Api.csproj",
                    @"..\Framework\Gma.Framework.Api.OpenApi\Gma.Framework.Api.OpenApi.csproj",
                    @"..\Framework\Gma.Framework.Api.Production\Gma.Framework.Api.Production.csproj",
                    @"..\Framework\Gma.Framework.Api.Production.EntityFrameworkCore\Gma.Framework.Api.Production.EntityFrameworkCore.csproj",
                    @"..\Framework\Gma.Framework.Api.Serilog\Gma.Framework.Api.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Caching.Cqrs\Gma.Framework.Caching.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Caching.Redis\Gma.Framework.Caching.Redis.csproj",
                    @"..\Framework\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Logging.Serilog\Gma.Framework.Logging.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Nats.Aspire\Gma.Framework.Messaging.Nats.Aspire.csproj",
                    @"..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Framework\Gma.Framework.Notifications.Api\Gma.Framework.Notifications.Api.csproj",
                    @"..\Framework\Gma.Framework.Notifications.Cqrs\Gma.Framework.Notifications.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Notifications.SignalR\Gma.Framework.Notifications.SignalR.csproj",
                    @"..\Framework\Gma.Framework.Realtime.Notifications\Gma.Framework.Realtime.Notifications.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Api.Serilog\Gma.Framework.Tenancy.Api.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Caching\Gma.Framework.Tenancy.Caching.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Messaging.Infrastructure\Gma.Framework.Tenancy.Messaging.Infrastructure.csproj"
                ]),
            new(
                Path.Combine("Hosts", "Host.Worker", "Host.Worker.csproj"),
                ["Microsoft.Extensions.Hosting"],
                [],
                [
                    @"..\Modules\Auth\Gma.Modules.Auth.Contracts\Gma.Modules.Auth.Contracts.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence\Gma.Modules.Auth.Persistence.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.PostgreSqlMigrations\Gma.Modules.Auth.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Auth\Gma.Modules.Auth.Persistence.SqlServerMigrations\Gma.Modules.Auth.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\Catalog\Catalog.Application\Catalog.Application.csproj",
                    @"..\Modules\Catalog\Catalog.Contracts\Catalog.Contracts.csproj",
                    @"..\Modules\Catalog\Catalog.Persistence\Catalog.Persistence.csproj",
                    @"..\Modules\Catalog\Catalog.Persistence.PostgreSqlMigrations\Catalog.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Catalog\Catalog.Persistence.SqlServerMigrations\Catalog.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\Ordering\Ordering.Application\Ordering.Application.csproj",
                    @"..\Modules\Ordering\Ordering.Contracts\Ordering.Contracts.csproj",
                    @"..\Modules\Ordering\Ordering.Persistence\Ordering.Persistence.csproj",
                    @"..\Modules\Ordering\Ordering.Persistence.PostgreSqlMigrations\Ordering.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\Ordering\Ordering.Persistence.SqlServerMigrations\Ordering.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\TaskRuntime\Gma.Modules.TaskRuntime.Application\Gma.Modules.TaskRuntime.Application.csproj",
                    @"..\Modules\TaskRuntime\Gma.Modules.TaskRuntime.Contracts\Gma.Modules.TaskRuntime.Contracts.csproj",
                    @"..\Modules\TaskRuntime\Gma.Modules.TaskRuntime.Persistence\Gma.Modules.TaskRuntime.Persistence.csproj",
                    @"..\Modules\TaskRuntime\Gma.Modules.TaskRuntime.Persistence.PostgreSqlMigrations\Gma.Modules.TaskRuntime.Persistence.PostgreSqlMigrations.csproj",
                    @"..\Modules\TaskRuntime\Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations\Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations.csproj",
                    @"..\Modules\TaskSamples\TaskSamples.Application\TaskSamples.Application.csproj",
                    @"..\..\ServiceDefaults\ServiceDefaults.csproj",
                    @"..\Framework\Gma.Framework.Caching.Cqrs\Gma.Framework.Caching.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Caching.Redis\Gma.Framework.Caching.Redis.csproj",
                    @"..\Framework\Gma.Framework.Infrastructure\Gma.Framework.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Logging.Serilog\Gma.Framework.Logging.Serilog.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Messaging.Nats.Aspire\Gma.Framework.Messaging.Nats.Aspire.csproj",
                    @"..\Framework\Gma.Framework.ModuleComposition\Gma.Framework.ModuleComposition.csproj",
                    @"..\Framework\Gma.Framework.Tasks.Cqrs\Gma.Framework.Tasks.Cqrs.csproj",
                    @"..\Framework\Gma.Framework.Tasks.Infrastructure\Gma.Framework.Tasks.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Caching\Gma.Framework.Tenancy.Caching.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Messaging.Infrastructure\Gma.Framework.Tenancy.Messaging.Infrastructure.csproj",
                    @"..\Framework\Gma.Framework.Tenancy.Tasks\Gma.Framework.Tenancy.Tasks.csproj"
                ]),
            new(
                Path.Combine("ServiceDefaults", "ServiceDefaults.csproj"),
                [
                    "Microsoft.Extensions.Http.Resilience",
                    "Microsoft.Extensions.ServiceDiscovery",
                    "OpenTelemetry.Exporter.OpenTelemetryProtocol",
                    "OpenTelemetry.Extensions.Hosting",
                    "OpenTelemetry.Instrumentation.AspNetCore",
                    "OpenTelemetry.Instrumentation.Http",
                    "prometheus-net.AspNetCore"
                ],
                ["Microsoft.AspNetCore.App"],
                [
                    @"..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
                    @"..\Framework\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj"
                ])
        ];
        HashSet<string> expectedProjectPaths = expectedShapes
            .Select(shape => NormalizePath(shape.ProjectPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] undocumentedRuntimeProjects = Directory
            .EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(IsRuntimeCompositionProject)
            .Select(path => NormalizePath(Path.GetRelativePath(srcRoot, path)))
            .Where(projectPath => !expectedProjectPaths.Contains(projectPath))
            .Select(projectPath => $"Runtime project '{projectPath}' missing dependency manifest entry.")
            .ToArray();
        string[] manifestOffenders = expectedShapes
            .SelectMany(shape =>
            {
                string projectPath = Path.Combine(srcRoot, shape.ProjectPath);
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);

                return CompareDependencySet(relativePath, "PackageReference", shape.PackageReferences, GetProjectIncludes(project, "PackageReference"))
                    .Concat(CompareDependencySet(relativePath, "FrameworkReference", shape.FrameworkReferences, GetProjectIncludes(project, "FrameworkReference")))
                    .Concat(CompareDependencySet(relativePath, "ProjectReference", shape.ProjectReferences, GetProjectIncludes(project, "ProjectReference")));
            })
            .ToArray();

        Assert.Empty(undocumentedRuntimeProjects
            .Concat(manifestOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));

        static bool IsRuntimeCompositionProject(string projectPath)
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            return string.Equals(projectName, "AppHost", StringComparison.Ordinal) ||
                   string.Equals(projectName, "Host.Api", StringComparison.Ordinal) ||
                   string.Equals(projectName, "Host.AdminApi", StringComparison.Ordinal) ||
                   string.Equals(projectName, "Host.AdminCli", StringComparison.Ordinal) ||
                   string.Equals(projectName, "Host.Worker", StringComparison.Ordinal) ||
                   string.Equals(projectName, "ServiceDefaults", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Framework_administration_core_remains_backend_agnostic()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedAdministrationRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Administration");
        string projectPath = Path.Combine(sharedAdministrationRoot, "Gma.Framework.Administration.csproj");
        XDocument project = XDocument.Load(projectPath);
        string[] forbiddenPackages =
        [
            "Microsoft.Extensions.Hosting",
            "System.CommandLine"
        ];
        string[] forbiddenFrameworkReferences =
        [
            "Microsoft.AspNetCore.App"
        ];
        string[] forbiddenProjectReferenceTokens =
        [
            "Gma.Framework.AccessControl"
        ];
        string[] forbiddenSourceTokens =
        [
            "AccessDecision",
            "AccessRequirement",
            "AccessScope",
            "Gma.Framework.AccessControl",
            "IAccessAuthorizationService",
            "IEndpointRouteBuilder",
            "IHostApplicationBuilder",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Hosting",
            "RootCommand",
            "System.CommandLine"
        ];

        string[] packageOffenders = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => packageId is not null && forbiddenPackages.Contains(packageId, StringComparer.Ordinal))
            .Select(packageId => $"Gma.Framework.Administration.csproj:{packageId}")
            .ToArray();
        string[] frameworkOffenders = project
            .Descendants("FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => reference is not null && forbiddenFrameworkReferences.Contains(reference, StringComparer.Ordinal))
            .Select(reference => $"Gma.Framework.Administration.csproj:{reference}")
            .ToArray();
        string[] projectReferenceOffenders = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => reference is not null && forbiddenProjectReferenceTokens.Any(token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(reference => $"Gma.Framework.Administration.csproj:{reference}")
            .ToArray();
        string[] sourceOffenders = EnumerateSourceFiles(sharedAdministrationRoot)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);

                return forbiddenSourceTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .ToArray();

        Assert.Empty(packageOffenders
            .Concat(frameworkOffenders)
            .Concat(projectReferenceOffenders)
            .Concat(sourceOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Framework_access_control_core_remains_backend_agnostic()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedAccessControlRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.AccessControl");
        string projectPath = Path.Combine(sharedAccessControlRoot, "Gma.Framework.AccessControl.csproj");
        XDocument project = XDocument.Load(projectPath);
        string[] forbiddenPackages =
        [
            "Microsoft.AspNetCore.Authentication.JwtBearer",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.Hosting",
            "NATS.Net",
            "System.CommandLine"
        ];
        string[] forbiddenFrameworkReferences =
        [
            "Microsoft.AspNetCore.App"
        ];
        string[] forbiddenSourceTokens =
        [
            "ClaimsPrincipal",
            "DbContext",
            "IAdminAuthorizationService",
            "IEndpointRouteBuilder",
            "IHostApplicationBuilder",
            "ITenantContext",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Nats",
            "OpenFGA",
            "SpiceDB",
            "System.CommandLine"
        ];

        string[] packageOffenders = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => packageId is not null && forbiddenPackages.Contains(packageId, StringComparer.Ordinal))
            .Select(packageId => $"Gma.Framework.AccessControl.csproj:{packageId}")
            .ToArray();
        string[] frameworkOffenders = project
            .Descendants("FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(reference => reference is not null && forbiddenFrameworkReferences.Contains(reference, StringComparer.Ordinal))
            .Select(reference => $"Gma.Framework.AccessControl.csproj:{reference}")
            .ToArray();
        string[] sourceOffenders = EnumerateSourceFiles(sharedAccessControlRoot)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);

                return forbiddenSourceTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .ToArray();

        Assert.Empty(packageOffenders
            .Concat(frameworkOffenders)
            .Concat(sourceOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ef_design_package_references_are_migration_project_only()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumeratePackageReferences(repositoryRoot)
            .Where(reference => string.Equals(reference.PackageId, "Microsoft.EntityFrameworkCore.Design", StringComparison.Ordinal))
            .Where(reference => !IsProviderMigrationProject(Path.GetFileNameWithoutExtension(reference.ProjectPath)))
            .Select(reference => $"{reference.ProjectPath}:{reference.PackageId}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Provider_migration_projects_reference_only_owning_persistence_project_and_shared_ef_design_helper()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateProjectReferences(repositoryRoot)
            .Where(reference => IsProviderMigrationProject(Path.GetFileNameWithoutExtension(reference.ProjectPath)))
            .Where(reference =>
            {
                string projectName = Path.GetFileNameWithoutExtension(reference.ProjectPath);
                string owningPersistenceProject = projectName
                    .Replace(".SqlServerMigrations", string.Empty, StringComparison.Ordinal)
                    .Replace(".PostgreSqlMigrations", string.Empty, StringComparison.Ordinal);
                string expectedReference = $"..\\{owningPersistenceProject}\\{owningPersistenceProject}.csproj";
                const string SharedEfDesignHelperReference =
                    @"..\..\..\Framework\Gma.Framework.Persistence.EntityFrameworkCore\Gma.Framework.Persistence.EntityFrameworkCore.csproj";

                return !string.Equals(reference.ReferencePath, NormalizePath(expectedReference), StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(reference.ReferencePath, NormalizePath(SharedEfDesignHelperReference), StringComparison.OrdinalIgnoreCase);
            })
            .Select(reference => $"{reference.ProjectPath}->{reference.ReferencePath}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_project_package_references_do_not_bypass_shared_adapters()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumeratePackageReferences(repositoryRoot)
            .Where(reference => IsModuleProject(reference.ProjectPath))
            .Where(reference => IsBackendAdapterPackage(reference.PackageId))
            .Select(reference => $"{reference.ProjectPath}:{reference.PackageId}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_domain_projects_keep_minimal_project_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        HashSet<string> allowedProjectReferences = new(
            [
                NormalizePath(@"..\..\..\Framework\Gma.Framework.Domain\Gma.Framework.Domain.csproj"),
                NormalizePath(@"..\..\..\Framework\Gma.Framework.Naming\Gma.Framework.Naming.csproj"),
                NormalizePath(@"..\..\..\Framework\Gma.Framework.Numerics\Gma.Framework.Numerics.csproj"),
                NormalizePath(@"..\..\..\Framework\Gma.Framework.Results\Gma.Framework.Results.csproj")
            ],
            StringComparer.OrdinalIgnoreCase);
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Domain.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !allowedProjectReferences.Contains(NormalizePath(reference!)))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] packageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(packageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_application_projects_keep_adapter_free_project_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        HashSet<string> allowedPackageReferences = new(StringComparer.Ordinal)
        {
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Options.ConfigurationExtensions"
        };
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Application.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string moduleName = Path.GetFileNameWithoutExtension(projectPath)
                    .Replace(".Application", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedApplicationProjectReference(moduleName, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] unexpectedPackageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Where(packageId => !allowedPackageReferences.Contains(packageId!))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(unexpectedPackageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_module_contract_projects_keep_backend_free_project_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Contracts.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".Admin.Contracts", StringComparison.Ordinal))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string moduleName = Path.GetFileNameWithoutExtension(projectPath)
                    .Replace(".Contracts", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedPublicContractsProjectReference(moduleName, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] packageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(packageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_contract_files_use_intentional_folders()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Contracts.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                string projectDirectory = Path.GetDirectoryName(projectPath)!;
                bool isAdminContracts = Path
                    .GetFileNameWithoutExtension(projectPath)
                    .EndsWith(".Admin.Contracts", StringComparison.Ordinal);

                return Directory
                    .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !HasIgnoredPathSegment(path))
                    .Select(path => ValidateContractFileFolder(
                        repositoryRoot,
                        projectDirectory,
                        path,
                        isAdminContracts))
                    .Where(offender => offender is not null)
                    .Select(offender => offender!);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_contract_projects_keep_thin_wrapper_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Admin.Contracts.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string moduleName = Path.GetFileNameWithoutExtension(projectPath)
                    .Replace(".Admin.Contracts", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedAdminContractsProjectReference(moduleName, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] packageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(packageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_persistence_projects_keep_provider_adapter_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        HashSet<string> allowedPackageReferences = new(StringComparer.Ordinal)
        {
            "Microsoft.EntityFrameworkCore.SqlServer",
            "Microsoft.Extensions.Hosting",
            "Npgsql.EntityFrameworkCore.PostgreSQL"
        };
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.Persistence.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string moduleName = Path.GetFileNameWithoutExtension(projectPath)
                    .Replace(".Persistence", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedPersistenceProjectReference(moduleName, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] unexpectedPackageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Where(packageId => !allowedPackageReferences.Contains(packageId!))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(unexpectedPackageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_http_front_door_projects_keep_http_composition_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path =>
            {
                string projectName = Path.GetFileNameWithoutExtension(path);
                return projectName.EndsWith(".Api", StringComparison.Ordinal) ||
                       projectName.EndsWith(".AdminApi", StringComparison.Ordinal);
            })
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string projectName = Path.GetFileNameWithoutExtension(projectPath);
                bool isAdminApi = projectName.EndsWith(".AdminApi", StringComparison.Ordinal);
                string moduleName = projectName
                    .Replace(".AdminApi", string.Empty, StringComparison.Ordinal)
                    .Replace(".Api", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedHttpFrontDoorProjectReference(moduleName, isAdminApi, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] packageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] unexpectedFrameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !string.Equals(reference, "Microsoft.AspNetCore.App", StringComparison.Ordinal))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(packageReferences)
                    .Concat(unexpectedFrameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_admin_cli_front_door_projects_keep_cli_composition_shape()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        HashSet<string> allowedPackageReferences = new(StringComparer.Ordinal)
        {
            "System.CommandLine"
        };
        string[] offenders = Directory
            .EnumerateFiles(modulesRoot, "*.AdminCli.csproj", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(projectPath =>
            {
                XDocument project = XDocument.Load(projectPath);
                string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
                string moduleName = Path.GetFileNameWithoutExtension(projectPath)
                    .Replace(".AdminCli", string.Empty, StringComparison.Ordinal);
                string[] unexpectedProjectReferences = project
                    .Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Where(reference => !IsAllowedAdminCliProjectReference(moduleName, reference!))
                    .Select(reference => $"{relativePath}->ProjectReference:{reference}")
                    .ToArray();
                string[] unexpectedPackageReferences = project
                    .Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                    .Where(packageId => !allowedPackageReferences.Contains(packageId!))
                    .Select(packageId => $"{relativePath}->PackageReference:{packageId}")
                    .ToArray();
                string[] frameworkReferences = project
                    .Descendants("FrameworkReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => $"{relativePath}->FrameworkReference:{reference}")
                    .ToArray();

                return unexpectedProjectReferences
                    .Concat(unexpectedPackageReferences)
                    .Concat(frameworkReferences);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_module_contract_projects_do_not_reference_admin_framework()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateProjectReferences(repositoryRoot)
            .Where(reference => IsPublicModuleContractsProject(reference.ProjectPath))
            .Where(reference => reference.ReferencePath.Contains("Gma.Framework.Administration", StringComparison.OrdinalIgnoreCase))
            .Select(reference => $"{reference.ProjectPath}->{reference.ReferencePath}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_front_door_projects_do_not_reference_shared_infrastructure_directly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] offenders = EnumerateProjectReferences(repositoryRoot)
            .Where(reference => IsModuleFrontDoorProject(reference.ProjectPath))
            .Where(reference => reference.ReferencePath.Contains("Gma.Framework.Infrastructure", StringComparison.OrdinalIgnoreCase))
            .Select(reference => $"{reference.ProjectPath}->{reference.ReferencePath}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_metadata_and_active_guidance_use_descriptor_builder_authoring()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] metadataForbiddenTokens =
        [
            "new ModuleDescriptor",
            "ModuleDescriptor.Empty(",
            ".WithFeature("
        ];
        string[] metadataOffenders = Directory
            .EnumerateFiles(modulesRoot, "*ModuleMetadata.cs", SearchOption.AllDirectories)
            .Where(path => !HasIgnoredPathSegment(path))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = Path.GetRelativePath(repositoryRoot, path);
                List<string> fileOffenders = [];
                if (!source.Contains("public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{relativePath} does not expose a static ModuleDescriptor property.");
                }

                if (!source.Contains(".Create(Name)", StringComparison.Ordinal) ||
                    !source.Contains(".Build();", StringComparison.Ordinal))
                {
                    fileOffenders.Add($"{relativePath} must use ModuleDescriptor.Create(Name)...Build().");
                }

                fileOffenders.AddRange(metadataForbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{relativePath} contains forbidden descriptor authoring token {token}"));

                return fileOffenders;
            })
            .ToArray();
        string[] guidanceFiles = EnumerateDocumentationMarkdownFiles(repositoryRoot)
            .Where(path => !HasIgnoredPathSegment(path))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith("notes", StringComparison.OrdinalIgnoreCase))
            .Append(ModuleScaffolderPath(repositoryRoot))
            .ToArray();
        string[] guidanceForbiddenTokens =
        [
            "new ModuleDescriptor",
            "ModuleDescriptor.Empty("
        ];
        string[] guidanceOffenders = guidanceFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = Path.GetRelativePath(repositoryRoot, path);

                return guidanceForbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{relativePath} contains stale descriptor authoring guidance {token}");
            })
            .ToArray();

        Assert.Empty(metadataOffenders
            .Concat(guidanceOffenders)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Module_scaffolder_uses_current_admin_cli_contracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] forbiddenTokens =
        [
            "IAdminModule",
            "IAdminCommandRegistry",
            "TenantScoped:",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.Admin.Contracts\\${Name}AdminPermissionCodes.cs\")",
            "$Admin -or $AdminApi",
            "if ($Admin)",
            "$adminProject"
        ];
        string[] requiredTokens =
        [
            "function ConvertTo-GmaKebabCase",
            "$moduleName = ConvertTo-GmaKebabCase -Value $Name",
            "public const string Name",
            "ModuleDescriptor",
            ".Create(Name)",
            ".WithSchema(Schema)",
            ".Build()",
            "$metadataDescriptorLines = @(",
            "$metadataDescriptor = $metadataDescriptorLines -join \"`r`n\"",
            "using Gma.Framework.Modules;",
            "$(GmaFrameworkRoot)Modules\\Gma.Framework.Modules\\Gma.Framework.Modules.csproj",
            "using Gma.Framework.Permissions;",
            "$(GmaFrameworkRoot)Security\\Gma.Framework.Permissions\\Gma.Framework.Permissions.csproj",
            "$(GmaFrameworkRoot)Messaging\\Gma.Framework.Messaging\\Gma.Framework.Messaging.csproj",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.Contracts\\Metadata\\${Name}ModuleMetadata.cs\")",
            "scopeRequirement: PermissionScopeRequirement.Scoped",
            "ModuleCacheTag",
            "ModuleCacheEntry",
            "using Gma.Framework.Caching;",
            "$(GmaFrameworkRoot)Caching\\Gma.Framework.Caching\\Gma.Framework.Caching.csproj",
            ".WithPermission($metadataPermissionDescriptor)",
            ".WithCacheEntry(new ModuleCacheDescriptor(ModuleCacheEntry, CacheScope.Scope, [ModuleCacheTag]))",
            "public static CacheKey ModuleKey(params string[] segments) => CacheKey.Scoped(",
            "$AdminCli -or $AdminApi",
            "if ($AdminCli)",
            "${Name}ModuleMetadata.Name",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.Contracts\\Metadata\\${Name}AdminPermissionCodes.cs\")",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.Admin.Contracts\\Permissions\\${Name}AdminPermissions.cs\")",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.Admin.Contracts\\Operations\\${Name}AdminOperationNames.cs\")",
            "$adminCliProject = Join-Path $moduleRoot \"$Name.AdminCli\\$Name.AdminCli.csproj\"",
            "Write-GmaFile (Join-Path $moduleRoot \"$Name.AdminCli\\${Name}AdminCliModule.cs\")",
            "public sealed class ${Name}AdminCliModule : IAdminCliModule",
            "IAdminCliModule",
            "IAdminCliCommandRegistry",
            "using Microsoft.AspNetCore.Http;",
            "using Gma.Framework.Administration.Cli;",
            "AdminPermissionCodes",
            "AdminOperationNames",
            "ModulePermissionDescriptor(${Name}AdminPermissionCodes.Manage",
            "public const string Manage = ${Name}ModuleMetadata.Name + \".manage\";",
            "AdminPermission.Create(${Name}AdminPermissionCodes.Manage)"
        ];
        string[] generatedMetadataForbiddenTokens =
        [
            "ModuleCacheName",
            ".WithPermissions([",
            ".WithCacheEntries(["
        ];
        string[] generatedMetadataFormattingForbiddenTokens =
        [
            "$metadataPermissionsBlock$metadataCacheDescriptorBlock",
            ".WithPermission($metadataPermissionDescriptor)`r`n",
            ".WithCacheEntry(new ModuleCacheDescriptor(ModuleCacheEntry, CacheScope.Scope, [ModuleCacheTag]))`r`n"
        ];
        string[] offenders = forbiddenTokens
            .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}")
            .Concat(generatedMetadataForbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 emits stale generated metadata token {token}"))
            .Concat(generatedMetadataFormattingForbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 risks emitting malformed descriptor chain token {token}"))
            .Concat(requiredTokens
                .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_docs_use_current_admin_cli_switch()
    {
        string repositoryRoot = FindRepositoryRoot();
        Regex staleAdminSwitchPattern = new(@"(?<![A-Za-z0-9])-Admin(?![A-Za-z0-9])");
        string[] checkedFiles = EnumerateDocumentationMarkdownFiles(repositoryRoot)
            .Append(ModuleScaffolderPath(repositoryRoot))
            .Where(path => !HasIgnoredPathSegment(path))
            .ToArray();

        string[] offenders = checkedFiles
            .SelectMany(path => File
                .ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    Line = line,
                    LineNumber = index + 1
                }))
            .Where(item => staleAdminSwitchPattern.IsMatch(item.Line))
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.Path)}:{item.LineNumber}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_uses_module_metadata_for_generated_front_door_names()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            "RouteGroupBuilder group = endpoints.MapGroup(\"/api/\" + ${Name}ModuleMetadata.Name)",
            "Command module = new(${Name}ModuleMetadata.Name, \"$Name administration operations.\");",
            "=> endpoints.MapGroup(\"/api/admin/\" + ${Name}ModuleMetadata.Name)"
        ];
        string[] forbiddenTokens =
        [
            "RouteGroupBuilder group = endpoints.MapGroup(\"/api/$moduleName\")",
            "Command module = new(\"$moduleName\", \"$Name administration operations.\");",
            "endpoints.MapGroup(\"/api/admin/$moduleName\")"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Scripts_and_docs_do_not_reference_machine_specific_dotnet_paths()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] checkedFiles = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "eng"), "*.ps1", SearchOption.TopDirectoryOnly)
            .Concat(EnumerateDocumentationMarkdownFiles(repositoryRoot))
            .Append(Path.Combine(repositoryRoot, "README.md"))
            .ToArray();
        string[] forbiddenTokens =
        [
            @"C:\Users\",
            ".dotnet-10"
        ];
        string[] offenders = checkedFiles
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains machine-specific token '{token}'.");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Dotnet_script_wrapper_resolves_sdk_from_repository_root_and_supports_project_working_directories()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "common.ps1"));
        string[] requiredTokens =
        [
            "Push-Location -LiteralPath $script:RepositoryRoot",
            "[string] $WorkingDirectory = $script:RepositoryRoot",
            "Push-Location -LiteralPath $WorkingDirectory",
            "Pop-Location",
            "function Resolve-GmaDotNet",
            "function Invoke-GmaDotNet"
        ];
        string[] offenders = requiredTokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/common.ps1 missing {token}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Long_running_host_scripts_run_from_project_directories()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] runScripts =
        [
            Path.Combine(repositoryRoot, "eng", "run-api.ps1"),
            Path.Combine(repositoryRoot, "eng", "run-admin-api.ps1"),
            Path.Combine(repositoryRoot, "eng", "run-worker.ps1"),
            Path.Combine(repositoryRoot, "eng", "run-aspire.ps1")
        ];

        string[] offenders = runScripts
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                List<string> scriptOffenders = [];
                if (!source.Contains("$projectDirectory = Split-Path -Parent $projectPath", StringComparison.Ordinal))
                {
                    scriptOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)} does not derive a project working directory.");
                }

                if (!source.Contains("-WorkingDirectory $projectDirectory", StringComparison.Ordinal))
                {
                    scriptOffenders.Add($"{Path.GetRelativePath(repositoryRoot, path)} does not run from the project directory.");
                }

                return scriptOffenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Engineering_scripts_use_shared_common_policy()
    {
        string repositoryRoot = FindRepositoryRoot();
        string engRoot = Path.Combine(repositoryRoot, "eng");
        string[] offenders = Directory
            .EnumerateFiles(engRoot, "*.ps1", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "common.ps1", StringComparison.OrdinalIgnoreCase))
            .Where(path => !File.ReadAllText(path).Contains(". (Join-Path $PSScriptRoot 'common.ps1')", StringComparison.Ordinal))
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} does not source eng/common.ps1.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_dependency_injection_extensions_guard_null_arguments()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] sourceOffenders = Directory
            .EnumerateFiles(modulesRoot, "DependencyInjection.cs", SearchOption.AllDirectories)
            .Where(path => !File.ReadAllText(path).Contains("ArgumentNullException.ThrowIfNull(", StringComparison.Ordinal))
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} does not guard null extension arguments.")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] scaffoldOffenders =
        [
            scaffolder.Contains("ArgumentNullException.ThrowIfNull(services);", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold application DI null guards.",
            scaffolder.Contains("ArgumentNullException.ThrowIfNull(builder);", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold persistence DI null guards."
        ];

        Assert.Empty(sourceOffenders
            .Concat(scaffoldOffenders.Where(offender => !string.IsNullOrWhiteSpace(offender)))
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Module_scaffolder_uses_current_inbox_store_contract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            "using Gma.Framework.Naming;",
            "using Gma.Framework.Runtime.Identity;",
            "IIdGenerator idGenerator",
            ": EfInboxStore<${Name}DbContext>(dbContext, clock, idGenerator,"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_uses_current_outbox_store_contract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            "using Microsoft.Extensions.Options;",
            "using Gma.Framework.Naming;",
            ": EfOutboxStore<${Name}DbContext>(dbContext, options, ${Name}Migrations.Schema);"
        ];
        string[] forbiddenTokens =
        [
            "ClaimPendingAsync(",
            "MarkProcessedAsync(",
            "MarkFailedAsync(",
            "BeginTransactionAsync(IsolationLevel.Serializable",
            "OutboxStore(${Name}DbContext dbContext, IOptions<OutboxOptions> options) : IOutboxStore"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_generates_admin_cli_without_raw_console_output()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            "Gma.Framework.Administration.Cli",
            "using Gma.Framework.Administration.Cli;",
            "IAdminCliModule"
        ];
        string[] forbiddenTokens =
        [
            "Console.WriteLine",
            "Console.Error.WriteLine",
            "Console.Error.Write("
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains raw admin CLI output token {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_does_not_keep_stale_project_reference_helper()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));

        Assert.DoesNotContain("function Add-ProjectReference", scaffolder, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_scaffolder_uses_current_outbox_writer_contract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            "using Microsoft.Extensions.Options;",
            "using Gma.Framework.Runtime;",
            "using Gma.Framework.Runtime.Time;",
            "IOptions<ApplicationIdentityOptions> applicationIdentity",
            ": EfOutboxWriter<${Name}DbContext>(dbContext, clock, applicationIdentity, ${Name}Migrations.Schema);"
        ];
        string[] forbiddenTokens =
        [
            "EnqueueAsync<TEvent>",
            "IntegrationEventEnvelopeFactory.Create(",
            "dbContext.OutboxMessages.Add(new OutboxMessage("
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_uses_shared_naming_for_generated_tenant_id_columns()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        List<string> requiredTokens =
        [
            @"$(GmaFrameworkRoot)Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj",
            "using Gma.Framework.Naming;"
        ];
        if (scaffolder.Contains("TenantId", StringComparison.Ordinal))
        {
            requiredTokens.Add("TenantIds.MaxLength");
        }

        string[] forbiddenTokens =
        [
            "using Gma.Framework.Domain;"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_uses_module_metadata_for_persistence_identity()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] requiredTokens =
        [
            @"<ProjectReference Include=""..\$Name.Contracts\$Name.Contracts.csproj"" />",
            "using $Name.Contracts;",
            "public const string Schema = ${Name}ModuleMetadata.Schema;",
            ": EfDomainEventUnitOfWork<${Name}DbContext>(${Name}Migrations.Schema, dbContext, domainEventDispatcher)",
            ": EfOutboxWriter<${Name}DbContext>(dbContext, clock, applicationIdentity, ${Name}Migrations.Schema);",
            ": EfOutboxStore<${Name}DbContext>(dbContext, options, ${Name}Migrations.Schema);",
            ": EfInboxStore<${Name}DbContext>(dbContext, clock, idGenerator, ${Name}Migrations.Schema)"
        ];
        string[] forbiddenTokens =
        [
            "public const string Schema = \"$moduleName\";",
            "ModuleName => \"$moduleName\"",
            "(\"$moduleName\", dbContext",
            "clock, \"$moduleName\")",
            "options, \"$moduleName\")",
            "idGenerator, \"$moduleName\")"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Concat(forbiddenTokens
                .Where(token => scaffolder.Contains(token, StringComparison.Ordinal))
                .Select(token => $"src/Framework/eng/new-module.ps1 contains stale {token}"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_scaffolder_guards_host_registration_and_prints_follow_up_checklist()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string apiHost = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Hosts", "Host.Api", "Program.cs"));
        string[] requiredTokens =
        [
            "$hostRegistrationAnchor = '// module-scaffold:public-api-modules'",
            "composition marker",
            "Could not register '$Name' in Host.Api",
            "Could not verify '$moduleRegistration'",
            @"tests\Architecture.Tests\Support\ArchitectureCatalog.cs",
            "ModuleProjects entries",
            "module descriptor",
            "Unknown = 0",
            "Compose the module explicitly"
        ];
        string[] offenders = requiredTokens
            .Where(token => !scaffolder.Contains(token, StringComparison.Ordinal))
            .Select(token => $"src/Framework/eng/new-module.ps1 missing {token}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
        Assert.Contains("// module-scaffold:public-api-modules", apiHost, StringComparison.Ordinal);
        Assert.DoesNotContain("$hostRegistrationAnchor = 'builder.AddModule<AuthModule>();'", scaffolder, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_cli_host_uses_bounded_exception_handling()
    {
        string repositoryRoot = FindRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminCli", "Program.cs"));

        Assert.Contains("EnableDefaultExceptionHandler = false", program, StringComparison.Ordinal);
        Assert.Contains("Admin command failed unexpectedly.", program, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableDefaultExceptionHandler = true", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_cli_host_validates_startup_options_without_starting_hosted_services()
    {
        string repositoryRoot = FindRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Hosts", "Host.AdminCli", "Program.cs"));
        int tryIndex = program.IndexOf("try", StringComparison.Ordinal);
        int compositionIndex = program.IndexOf("HostApplicationBuilder builder", StringComparison.Ordinal);
        int buildIndex = program.IndexOf("using IHost host = builder.Build();", StringComparison.Ordinal);
        int optionsCatchIndex = program.IndexOf("catch (OptionsValidationException", StringComparison.Ordinal);

        Assert.Contains("host.Services.ValidateAdminCliStartup();", program, StringComparison.Ordinal);
        Assert.Contains("ContentRootPath = AppContext.BaseDirectory", program, StringComparison.Ordinal);
        Assert.DoesNotContain("host.StartAsync", program, StringComparison.Ordinal);
        Assert.DoesNotContain("host.RunAsync", program, StringComparison.Ordinal);
        Assert.True(tryIndex >= 0, "Host.AdminCli should use a bounded try/catch around composition and execution.");
        Assert.InRange(compositionIndex, tryIndex + 1, optionsCatchIndex - 1);
        Assert.InRange(buildIndex, tryIndex + 1, optionsCatchIndex - 1);
    }

    [Fact]
    public void Application_projects_do_not_depend_on_host_builder()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] packageOffenders = EnumeratePackageReferences(repositoryRoot)
            .Where(reference => IsApplicationProject(reference.ProjectPath))
            .Where(reference => string.Equals(reference.PackageId, "Microsoft.Extensions.Hosting", StringComparison.Ordinal))
            .Select(reference => $"{reference.ProjectPath}:{reference.PackageId}")
            .ToArray();
        string[] sourceOffenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src", "Modules"))
            .Where(path => string.Equals(FindOwningProjectName(path)?.Split('.').LastOrDefault(), "Application", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string[] forbiddenTokens =
                [
                    "IHostApplicationBuilder",
                    "Microsoft.Extensions.Hosting"
                ];

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}:{token}");
            })
            .ToArray();

        Assert.Empty(packageOffenders.Concat(sourceOffenders).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Feature_module_sources_use_shared_time_and_id_abstractions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] forbiddenTokens =
        [
            "Guid.NewGuid(",
            "DateTimeOffset.UtcNow",
            "DateTime.UtcNow"
        ];

        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => !IsGeneratedMigrationSource(path))
            .Where(path => !IsTestSourcePath(path))
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
    public void Framework_infrastructure_centralizes_direct_time_and_id_creation()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedInfrastructureRoot = GmaSourceLayout.FrameworkPath(repositoryRoot, "Gma.Framework.Runtime.Infrastructure");
        string[] forbiddenTokens =
        [
            "Guid.NewGuid(",
            "DateTimeOffset.UtcNow",
            "DateTime.UtcNow"
        ];

        string[] offenders = EnumerateSourceFiles(sharedInfrastructureRoot)
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Where(token => !IsAllowedDirectTimeOrIdSource(relativePath, token))
                    .Select(token => $"{relativePath}:{token}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Shared_infrastructure_runtime_options_are_validated_on_start()
    {
        string repositoryRoot = FindRepositoryRoot();
        string coreSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Infrastructure",
            "DependencyInjection.cs"));
        string cachingSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Caching.Infrastructure",
            "DependencyInjection.cs"));
        string messagingSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Messaging.Infrastructure",
            "DependencyInjection.cs"));
        string natsSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Messaging.Nats",
            "DependencyInjection.cs"));
        string tenancySource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Tenancy.Infrastructure",
            "DependencyInjection.cs"));
        (string SourceName, string Source, string[] Tokens)[] requiredSources =
        [
            new(
                "Gma.Framework.Infrastructure",
                coreSource,
                [
                    "builder.AddTenancyInfrastructure();"
                ]),
            new(
                "Gma.Framework.Tenancy.Infrastructure",
                tenancySource,
                [
                    "new TenantOptionsValidator()",
                    "IValidateOptions<TenantOptions>, TenantOptionsValidator",
                    ".ValidateOnStart()"
                ]),
            new(
                "Gma.Framework.Caching.Infrastructure",
                cachingSource,
                [
                    "new CachingOptionsValidator()",
                    "IValidateOptions<CachingOptions>, CachingOptionsValidator",
                    "IValidateOptions<CachingOptions>, CachingCompositionOptionsValidator",
                    ".ValidateOnStart()"
                ]),
            new(
                "Gma.Framework.Messaging.Infrastructure",
                messagingSource,
                [
                    "new OutboxOptionsValidator()",
                    "IValidateOptions<OutboxOptions>, OutboxOptionsValidator",
                    ".ValidateOnStart()"
                ]),
            new(
                "Gma.Framework.Messaging.Nats",
                natsSource,
                [
                    "new NatsJetStreamOptionsValidator()",
                    "new NatsConsumerOptionsValidator()",
                    "IValidateOptions<NatsJetStreamOptions>, NatsJetStreamOptionsValidator",
                    "IValidateOptions<NatsConsumerOptions>, NatsConsumerOptionsValidator",
                    ".ValidateOnStart()"
                ])
        ];
        string[] offenders = requiredSources
            .SelectMany(required => required.Tokens
                .Where(token => !required.Source.Contains(token, StringComparison.Ordinal))
                .Select(token => $"{required.SourceName} dependency injection missing {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Shared_messaging_runtime_registration_composes_runtime_infrastructure()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Messaging.Infrastructure",
            "DependencyInjection.cs"));

        AssertMethodContains(source, "AddMessagingInfrastructure", "builder.AddRuntimeInfrastructure();");
        AssertMethodContains(source, "AddOutboxPublishing", "builder.AddMessagingInfrastructure();");

        string natsSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Messaging.Nats",
            "DependencyInjection.cs"));
        AssertMethodContains(natsSource, "AddNatsJetStreamMessaging", "builder.AddMessagingInfrastructure();");
        AssertMethodContains(natsSource, "AddNatsJetStreamConsumers", "builder.AddMessagingInfrastructure();");
    }

    [Fact]
    public void Shared_metric_instrument_names_are_declared_only_in_observability_contracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string allowedRelativePath = NormalizePath(Path.Combine(
            "src",
            "Framework",
            "Gma.Framework.Observability",
            "ObservabilityInstrumentNames.cs"));
        string[] sharedInstrumentNames =
        [
            ObservabilityInstrumentNames.CommandsExecuted,
            ObservabilityInstrumentNames.CommandsDuration,
            ObservabilityInstrumentNames.QueriesExecuted,
            ObservabilityInstrumentNames.QueriesDuration,
            ObservabilityInstrumentNames.OutboxClaimed,
            ObservabilityInstrumentNames.OutboxPublished,
            ObservabilityInstrumentNames.OutboxFailed,
            ObservabilityInstrumentNames.OutboxPublishDuration,
            ObservabilityInstrumentNames.InboxMessages,
            ObservabilityInstrumentNames.InboxProcessDuration,
            ObservabilityInstrumentNames.CacheRequests,
            ObservabilityInstrumentNames.CacheDuration,
            ObservabilityInstrumentNames.CacheBackendFailures,
            ObservabilityInstrumentNames.CacheInvalidationFailures
        ];
        string[] offenders = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"))
            .Concat(EnumerateSourceFiles(Path.Combine(repositoryRoot, "tests")))
            .Where(path => !IsGeneratedMigrationSource(path))
            .SelectMany(path =>
            {
                string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, path));

                if (string.Equals(relativePath, allowedRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return [];
                }

                string source = File.ReadAllText(path);

                return sharedInstrumentNames
                    .Where(metricName => source.Contains($"\"{metricName}\"", StringComparison.Ordinal))
                    .Select(metricName => $"{relativePath}:{metricName}");
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Cqrs_validation_behaviors_use_shared_validation_error_contract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string cqrsRoot = GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Cqrs.Infrastructure");
        string[] behaviorFiles =
        [
            Path.Combine(cqrsRoot, "ValidationCommandBehavior.cs"),
            Path.Combine(cqrsRoot, "ValidationQueryBehavior.cs")
        ];
        string[] offenders = behaviorFiles
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains("RequestValidationErrors.Failed(failures)", StringComparison.Ordinal) ||
                       source.Contains("\"Validation.Failed\"", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should use RequestValidationErrors")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persistence_options_are_validated_only_by_persistence_modules()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sharedInfrastructureSource = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Infrastructure",
            "DependencyInjection.cs"));
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] persistenceRegistrationOffenders = EnumerateProjectFiles(modulesRoot)
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(".Persistence", StringComparison.Ordinal))
            .Select(path => Path.Combine(Path.GetDirectoryName(path)!, "DependencyInjection.cs"))
            .Where(File.Exists)
            .Where(path => !File.ReadAllText(path).Contains("AddPersistenceOptions(builder.Configuration)", StringComparison.Ordinal))
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} does not call AddPersistenceOptions")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.DoesNotContain("AddOptions<PersistenceOptions>", sharedInfrastructureSource, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddPersistenceOptions(builder.Configuration);", scaffolder, StringComparison.Ordinal);
        Assert.Empty(persistenceRegistrationOffenders);
    }

    [Fact]
    public void Module_persistence_dependency_injection_uses_repeat_safe_registration()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string scaffolder = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] enumerablePersistenceTokens =
        [
            "IUnitOfWork",
            "IOutboxWriter",
            "IOutboxStore",
            "IInboxStore"
        ];
        string[] unsafeRegistrationPrefixes =
        [
            ".AddScoped<",
            ".AddTransient<",
            ".AddSingleton<",
            ".TryAddScoped<",
            ".TryAddTransient<",
            ".TryAddSingleton<"
        ];

        string[] sourceOffenders = Directory
            .EnumerateFiles(modulesRoot, "DependencyInjection.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains(".Persistence", StringComparison.Ordinal))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .SelectMany(item =>
            {
                List<string> offenders = [];
                if (item.Source.Contains(".AddDbContext<", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(repositoryRoot, item.Path)} uses AddDbContext; use TryAddModuleDbContext.");
                }

                offenders.AddRange(enumerablePersistenceTokens
                    .SelectMany(token => unsafeRegistrationPrefixes
                        .Where(prefix => item.Source.Contains(prefix + token, StringComparison.Ordinal))
                        .Select(prefix => $"{Path.GetRelativePath(repositoryRoot, item.Path)} uses {prefix}{token}; use services.TryAddEnumerable with ServiceDescriptor instead.")));

                return offenders;
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] scaffoldOffenders =
        [
            scaffolder.Contains("TryAddModuleDbContext<${Name}DbContext>", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold TryAddModuleDbContext.",
            scaffolder.Contains("TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, ${Name}UnitOfWork>())", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold repeat-safe IUnitOfWork registration.",
            scaffolder.Contains("TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, ${Name}OutboxWriter>())", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold repeat-safe IOutboxWriter registration.",
            scaffolder.Contains("TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, ${Name}OutboxStore>())", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold repeat-safe IOutboxStore registration.",
            scaffolder.Contains("TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, ${Name}InboxStore>())", StringComparison.Ordinal)
                ? string.Empty
                : "src/Framework/eng/new-module.ps1 should scaffold repeat-safe IInboxStore registration."
        ];

        Assert.Empty(sourceOffenders
            .Concat(scaffoldOffenders.Where(offender => !string.IsNullOrWhiteSpace(offender)))
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Transactional_commands_have_matching_persistence_unit_of_work_module_names()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, ModuleProject> persistenceProjects = ArchitectureCatalog.ModuleProjects
            .Where(project => project.Kind == ModuleProjectKind.Persistence)
            .ToDictionary(
                project => ResolveModuleName(project.ModulePrefix),
                project => project,
                StringComparer.Ordinal);

        string[] offenders = ArchitectureCatalog.ApplicationAssemblies
            .SelectMany(assembly => assembly
                .GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => ImplementsOpenGeneric(type, typeof(ITransactionalCommand<>)))
                .Select(type => new
                {
                    CommandType = type,
                    ModuleName = ModuleNameFromAssembly(type.Assembly),
                }))
            .Select(item =>
            {
                if (!persistenceProjects.TryGetValue(item.ModuleName, out ModuleProject? persistenceProject))
                {
                    return $"{item.CommandType.FullName} resolves to module '{item.ModuleName}' but no persistence project is registered";
                }

                string moduleDirectory = sourceLayout.GetModuleRoot(persistenceProject.ModulePrefix);
                string persistenceProjectDirectory = Path.Combine(moduleDirectory, persistenceProject.ProjectName);
                string requiredToken = $"ModuleName => \"{item.ModuleName}\"";
                bool hasMatchingUnitOfWork = PersistenceProjectDeclaresMatchingUnitOfWorkModuleName(
                    moduleDirectory,
                    persistenceProjectDirectory,
                    item.ModuleName);

                return hasMatchingUnitOfWork
                    ? null
                    : $"{item.CommandType.FullName} resolves to module '{item.ModuleName}' but {persistenceProject.ProjectName} does not declare {requiredToken} or an equivalent schema constant";
            })
            .Where(offender => offender is not null)
            .Select(offender => offender!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_domain_event_unit_of_work_implementations_use_shared_ef_base()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string scaffolderSource = File.ReadAllText(ModuleScaffolderPath(repositoryRoot));
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("UnitOfWork.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("IDomainEventDispatcher", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains(": EfDomainEventUnitOfWork<", StringComparison.Ordinal) ||
                       source.Contains(".ChangeTracker", StringComparison.Ordinal) ||
                       source.Contains("ClearDomainEvents()", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should inherit EfDomainEventUnitOfWork")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
        Assert.Contains("EfDomainEventUnitOfWork<", scaffolderSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".ChangeTracker", scaffolderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearDomainEvents()", scaffolderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_inbox_stores_use_module_identity_constants()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("InboxStore.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(": EfInboxStore<", StringComparison.Ordinal))
            .Where(path => RawStringArgumentPattern().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_inbox_stores_use_shared_ef_base()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("InboxStore.cs", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains(": EfInboxStore<", StringComparison.Ordinal) ||
                       source.Contains("ProcessAsync(", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should inherit EfInboxStore without hand-written process logic")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_outbox_stores_use_shared_ef_base()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("OutboxStore.cs", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains(": EfOutboxStore<", StringComparison.Ordinal) ||
                       source.Contains("ClaimPendingAsync(", StringComparison.Ordinal) ||
                       source.Contains("MarkProcessedAsync(", StringComparison.Ordinal) ||
                       source.Contains("MarkFailedAsync(", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should inherit EfOutboxStore without hand-written claim/mark methods")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_outbox_writers_use_shared_ef_base()
    {
        string repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        string[] offenders = EnumerateSourceFiles(modulesRoot)
            .Where(path => Path.GetFileName(path).EndsWith("OutboxWriter.cs", StringComparison.Ordinal))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains(": EfOutboxWriter<", StringComparison.Ordinal) ||
                       source.Contains("EnqueueAsync<TEvent>", StringComparison.Ordinal) ||
                       source.Contains("IntegrationEventEnvelopeFactory.Create(", StringComparison.Ordinal) ||
                       source.Contains("OutboxMessages.Add(new OutboxMessage(", StringComparison.Ordinal);
            })
            .Select(path => $"{Path.GetRelativePath(repositoryRoot, path)} should inherit EfOutboxWriter without hand-written enqueue logic")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Redis_caching_options_are_validated_when_adapter_is_enabled()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Caching.Redis",
            "DependencyInjection.cs"));
        string[] requiredTokens =
        [
            "IValidateOptions<RedisCachingOptions>, RedisCachingOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] offenders = requiredTokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Framework.Caching.Redis dependency injection missing {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Service_defaults_observability_options_are_validated_on_start()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "ServiceDefaults",
            "Extensions.cs"));
        string[] requiredTokens =
        [
            "IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] offenders = requiredTokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"ServiceDefaults dependency injection missing {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Shared_administration_api_options_are_validated_on_start()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(GmaSourceLayout.FrameworkPath(
            repositoryRoot,
            "Gma.Framework.Administration.Api",
            "DependencyInjection.cs"));
        string[] requiredTokens =
        [
            "IValidateOptions<AdminApiOptions>, AdminApiOptionsValidator",
            ".ValidateOnStart()"
        ];
        string[] offenders = requiredTokens
            .Where(token => !source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Gma.Framework.Administration.Api dependency injection missing {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

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
