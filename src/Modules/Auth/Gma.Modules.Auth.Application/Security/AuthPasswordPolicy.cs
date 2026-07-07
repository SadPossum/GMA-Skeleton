namespace Gma.Modules.Auth.Application.Security;

internal static class AuthPasswordPolicy
{
    public const int MinimumLength = 8;
    public const string MinimumLengthMessage = "Password must contain at least 8 characters.";

    public static bool IsValidPlaintextPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;
}
