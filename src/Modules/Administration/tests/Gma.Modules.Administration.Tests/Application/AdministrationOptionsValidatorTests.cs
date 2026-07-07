namespace Gma.Modules.Administration.Tests;

using Gma.Modules.Administration.Application;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdministrationOptionsValidatorTests
{
    private readonly AdministrationOptionsValidator validator = new();

    [Fact]
    public void Validate_accepts_default_settings()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, new AdministrationOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Owner")]
    [InlineData("owner role")]
    [InlineData("-owner")]
    public void Validate_rejects_invalid_owner_role_name(string ownerRoleName)
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdministrationOptions
            {
                Bootstrap = new AdministrationOptions.BootstrapOptions
                {
                    OwnerRoleName = ownerRoleName
                }
            });

        Assert.True(result.Failed);
        Assert.Contains("OwnerRoleName", result.FailureMessage, StringComparison.Ordinal);
    }
}
