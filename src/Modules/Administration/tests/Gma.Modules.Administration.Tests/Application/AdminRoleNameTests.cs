namespace Gma.Modules.Administration.Tests;

using Gma.Modules.Administration.Application;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminRoleNameTests
{
    [Fact]
    public void Normalize_accepts_max_length_slug()
    {
        string roleName = $"a{new string('1', AdminRoleName.MaxLength - 1)}";

        Assert.Equal(roleName, AdminRoleName.Normalize(roleName));
    }

    [Fact]
    public void Normalize_rejects_overlong_slug()
    {
        string roleName = $"a{new string('1', AdminRoleName.MaxLength)}";

        Assert.Throws<ArgumentException>(() => AdminRoleName.Normalize(roleName));
    }

    [Fact]
    public void Try_normalize_rejects_overlong_slug()
    {
        string roleName = $"a{new string('1', AdminRoleName.MaxLength)}";

        Assert.False(AdminRoleName.TryNormalize(roleName, out string? normalized));
        Assert.Null(normalized);
    }
}
