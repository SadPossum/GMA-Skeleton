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
