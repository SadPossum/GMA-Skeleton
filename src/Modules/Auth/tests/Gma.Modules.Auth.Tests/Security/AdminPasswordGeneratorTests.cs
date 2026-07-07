namespace Gma.Modules.Auth.Tests;

using Gma.Modules.Auth.Application.Security;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminPasswordGeneratorTests
{
    [Fact]
    public void Generate_returns_printable_password_with_configured_length()
    {
        string password = AdminPasswordGenerator.Generate();

        Assert.Equal(AdminPasswordGenerator.GeneratedLength, password.Length);
        Assert.All(password, character => Assert.False(char.IsWhiteSpace(character)));
    }
}
