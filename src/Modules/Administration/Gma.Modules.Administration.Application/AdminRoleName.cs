namespace Gma.Modules.Administration.Application;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

public static partial class AdminRoleName
{
    public const int MaxLength = 128;

    public static bool TryNormalize(string? value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim().ToLowerInvariant();

        if (candidate.Length > MaxLength || !RoleNameRegex().IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string Normalize(string value) =>
        TryNormalize(value, out string? normalized)
            ? normalized
            : throw new ArgumentException("Admin role name must be a lowercase slug.", nameof(value));

    [GeneratedRegex(@"^[a-z][a-z0-9-]*$")]
    private static partial Regex RoleNameRegex();
}
