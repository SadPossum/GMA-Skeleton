namespace Gma.Modules.Auth.Application;

public sealed class AuthApplicationOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
