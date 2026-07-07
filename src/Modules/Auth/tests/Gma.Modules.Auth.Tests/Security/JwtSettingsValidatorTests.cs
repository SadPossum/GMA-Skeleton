namespace Gma.Modules.Auth.Tests;

using Gma.Modules.Auth.Infrastructure;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime;
using Xunit;

[Trait("Category", "Unit")]
public sealed class JwtSettingsValidatorTests
{
    private readonly JwtSettingsValidator validator = new();

    [Fact]
    public void Validate_rejects_default_settings_without_identity_values()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, new JwtSettings());

        AssertFailure(result, "Issuer");
    }

    [Fact]
    public void Validate_accepts_explicit_valid_settings()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, ValidSettings());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Apply_identity_defaults_uses_application_display_name_for_missing_issuer_and_audience()
    {
        JwtSettings settings = ValidSettings(issuer: string.Empty, audience: " ");

        AuthInfrastructureOptionsValidation.ApplyJwtIdentityDefaults(
            settings,
            new ApplicationIdentityOptions { DisplayName = "Acme Orders" });

        Assert.Equal("Acme Orders", settings.Issuer);
        Assert.Equal("Acme Orders", settings.Audience);
    }

    [Fact]
    public void Validate_rejects_missing_issuer()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            ValidSettings(issuer: string.Empty));

        AssertFailure(result, "Issuer");
    }

    [Fact]
    public void Validate_rejects_missing_audience()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            ValidSettings(audience: string.Empty));

        AssertFailure(result, "Audience");
    }

    [Fact]
    public void Validate_rejects_short_signing_key()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            ValidSettings(signingKey: "short"));

        AssertFailure(result, "SigningKey");
    }

    [Fact]
    public void Validate_rejects_non_positive_access_token_lifetime()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            ValidSettings(accessTokenLifetimeMinutes: 0));

        AssertFailure(result, "AccessTokenLifetimeMinutes");
    }

    private static JwtSettings ValidSettings(
        string issuer = "GenericModularApi",
        string audience = "GenericModularApi",
        string signingKey = "test-jwt-signing-key-000000000000000000000000",
        int accessTokenLifetimeMinutes = 15) =>
        new()
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = signingKey,
            AccessTokenLifetimeMinutes = accessTokenLifetimeMinutes
        };

    private static void AssertFailure(ValidateOptionsResult result, string expectedFailure)
    {
        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.FailureMessage, StringComparison.Ordinal);
    }
}
