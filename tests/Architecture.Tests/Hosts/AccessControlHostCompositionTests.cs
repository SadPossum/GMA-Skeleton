namespace Architecture.Tests;

using Xunit;

[Trait("Category", "Architecture")]
public sealed class AccessControlHostCompositionTests
{
    [Fact]
    public void Public_api_composes_scoped_access_profiles_with_a_deny_by_default_allowlist()
    {
        string repositoryRoot = FindRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Hosts",
            "Host.Api",
            "Program.cs"));
        string project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Hosts",
            "Host.Api",
            "Host.Api.csproj"));

        Assert.Contains("builder.AddModule<AccessControlApiModule>();", program, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddOrganizationsAccessControlExtension();", program, StringComparison.Ordinal);
        Assert.Contains(
            "AddGmaEntityFrameworkReadinessCheck<AccessControlDbContext>",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AddAccessProfilePermissionAllowlist", program, StringComparison.Ordinal);
        Assert.Contains("Gma.Modules.AccessControl.Api.csproj", project, StringComparison.Ordinal);
        Assert.Contains(
            "Gma.Extensions.Organizations.AccessControl.csproj",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "Gma.Modules.AccessControl.Persistence.PostgreSqlMigrations.csproj",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "Gma.Modules.AccessControl.Persistence.SqlServerMigrations.csproj",
            project,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "GMA-Skeleton.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
