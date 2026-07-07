namespace Gma.Modules.Auth.Contracts;

public static class UsernameTypeNames
{
    public static string ToWireName(UsernameType usernameType) =>
        usernameType switch
        {
            UsernameType.Email => "email",
            UsernameType.Phone => "phone",
            _ => throw new ArgumentOutOfRangeException(
                nameof(usernameType),
                usernameType,
                "Username type is invalid.")
        };

    public static bool TryParse(string? value, out UsernameType usernameType)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out usernameType) &&
            usernameType is not UsernameType.Unknown &&
            Enum.IsDefined(usernameType))
        {
            return true;
        }

        usernameType = normalized switch
        {
            "email" => UsernameType.Email,
            "phone" => UsernameType.Phone,
            _ => UsernameType.Unknown
        };

        return usernameType is not UsernameType.Unknown;
    }
}
