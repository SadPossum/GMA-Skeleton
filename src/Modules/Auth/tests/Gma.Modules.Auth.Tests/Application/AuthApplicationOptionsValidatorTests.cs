namespace Gma.Modules.Auth.Tests;

using Gma.Modules.Auth.Application;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthApplicationOptionsValidatorTests
{
    private readonly AuthApplicationOptionsValidator validator = new();

    [Fact]
    public void Validate_accepts_default_settings()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, new AuthApplicationOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_rejects_non_positive_refresh_token_lifetime(int refreshTokenLifetimeDays)
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AuthApplicationOptions { RefreshTokenLifetimeDays = refreshTokenLifetimeDays });

        Assert.True(result.Failed);
        Assert.Contains("RefreshTokenLifetimeDays", result.FailureMessage, StringComparison.Ordinal);
    }
}
