namespace Architecture.Tests;

using Xunit;

[Trait("Category", "Architecture")]
public sealed partial class DeveloperExperienceGuardTests
{
    [Fact]
    public void Independent_source_workflows_enforce_solution_sync_before_restore()
    {
        string repositoryRoot = FindRepositoryRoot();
        GmaSourceLayout sourceLayout = GmaSourceLayout.FromRepositoryRoot(repositoryRoot);
        Dictionary<string, string> solutions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gma.Framework.slnx"] = Path.Combine(sourceLayout.FrameworkRepositoryRoot, "Gma.Framework.slnx"),
            ["Gma.Extensions.slnx"] = Path.Combine(sourceLayout.ExtensionsRepositoryRoot, "Gma.Extensions.slnx"),
            ["Gma.Modules.AccessControl.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("AccessControl"), "Gma.Modules.AccessControl.slnx"),
            ["Gma.Modules.Administration.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Administration"), "Gma.Modules.Administration.slnx"),
            ["Gma.Modules.Auth.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Auth"), "Gma.Modules.Auth.slnx"),
            ["Gma.Modules.Files.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Files"), "Gma.Modules.Files.slnx"),
            ["Gma.Modules.Notifications.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Notifications"), "Gma.Modules.Notifications.slnx"),
            ["Gma.Modules.Organizations.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Organizations"), "Gma.Modules.Organizations.slnx"),
            ["Gma.Modules.TaskRuntime.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("TaskRuntime"), "Gma.Modules.TaskRuntime.slnx"),
            ["Gma.Modules.Tenancy.slnx"] = Path.Combine(sourceLayout.GetModulePackageRoot("Tenancy"), "Gma.Modules.Tenancy.slnx")
        };

        string[] offenders = solutions
            .SelectMany(item =>
            {
                string packageRoot = Path.GetDirectoryName(item.Value)!;
                string workflowPath = Path.Combine(packageRoot, ".github", "workflows", "validate.yml");
                if (!File.Exists(workflowPath))
                {
                    return [$"{CanonicalRelativePath(repositoryRoot, workflowPath)} is missing"];
                }

                string workflow = File.ReadAllText(workflowPath);
                string invocation = $"sync-solution.ps1 -RepositoryRoot . -Solution {item.Key} -Check";
                int checkIndex = workflow.IndexOf(invocation, StringComparison.Ordinal);
                int restoreIndex = workflow.IndexOf("- name: Restore", StringComparison.Ordinal);
                string expectedToolPath = string.Equals(
                    item.Key,
                    "Gma.Framework.slnx",
                    StringComparison.OrdinalIgnoreCase)
                        ? "./eng/sync-solution.ps1"
                        : "../gma-framework/eng/sync-solution.ps1";

                return new[]
                {
                    checkIndex >= 0
                        ? null
                        : $"{CanonicalRelativePath(repositoryRoot, workflowPath)} missing {invocation}",
                    workflow.Contains(expectedToolPath, StringComparison.Ordinal)
                        ? null
                        : $"{CanonicalRelativePath(repositoryRoot, workflowPath)} should use {expectedToolPath}",
                    checkIndex >= 0 && restoreIndex >= 0 && checkIndex < restoreIndex
                        ? null
                        : $"{CanonicalRelativePath(repositoryRoot, workflowPath)} should check solution sync before restore"
                }
                    .Where(offender => offender is not null)
                    .Select(offender => offender!);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Skeleton_checks_all_mounted_source_solutions_before_expensive_work()
    {
        string repositoryRoot = FindRepositoryRoot();
        string guardPath = Path.Combine(repositoryRoot, "eng", "check-source-solutions.ps1");
        string guard = File.ReadAllText(guardPath);
        string verify = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "verify.ps1"));
        string focusedValidation = File.ReadAllText(Path.Combine(repositoryRoot, "eng", "gma-validate.ps1"));
        string[] requiredGuardTokens =
        [
            "gma\\framework\\eng\\sync-solution.ps1",
            "gma\\framework",
            "gma\\extensions",
            "gma\\modules",
            "Get-ChildItem -LiteralPath $modulesRoot -Directory",
            "Get-ChildItem -LiteralPath $sourceRoot -Filter '*.slnx' -File",
            "$solutions.Count -ne 1",
            "-RepositoryRoot $sourceRoot",
            "-Solution $solutions[0].Name",
            "-Check"
        ];
        string[] hardCodedModulePaths =
        [
            "gma\\modules\\access-control",
            "gma\\modules\\administration",
            "gma\\modules\\auth",
            "gma\\modules\\files",
            "gma\\modules\\notifications",
            "gma\\modules\\organizations",
            "gma\\modules\\task-runtime",
            "gma\\modules\\tenancy"
        ];

        string[] offenders = requiredGuardTokens
            .Where(token => !guard.Contains(token, StringComparison.Ordinal))
            .Select(token => $"eng/check-source-solutions.ps1 missing {token}")
            .Concat(hardCodedModulePaths
                .Where(path => guard.Contains(path, StringComparison.OrdinalIgnoreCase))
                .Select(path => $"eng/check-source-solutions.ps1 hard-codes {path}"))
            .Concat(!verify.Contains("check-source-solutions.ps1", StringComparison.Ordinal)
                ? ["eng/verify.ps1 should check mounted source solutions."]
                : [])
            .Concat(!focusedValidation.Contains("check-source-solutions.ps1", StringComparison.Ordinal)
                ? ["eng/gma-validate.ps1 should check mounted source solutions."]
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }
}
