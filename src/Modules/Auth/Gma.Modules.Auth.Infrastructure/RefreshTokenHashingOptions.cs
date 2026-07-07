namespace Gma.Modules.Auth.Infrastructure;

public sealed class RefreshTokenHashingOptions
{
    public const string SectionName = "Auth:RefreshTokens";
    public const int MinimumPepperBytes = 32;

    public string Pepper { get; set; } = string.Empty;
}
