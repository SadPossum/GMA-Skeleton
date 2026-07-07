namespace Gma.Modules.Auth.Domain.Entities;

using Gma.Framework.Naming;
using System.Text.RegularExpressions;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed partial class MemberUsername : TenantEntity<MemberUsernameId>
{
    public const int ValueMaxLength = 256;
    public const int NormalizedValueMaxLength = ValueMaxLength;

    private MemberUsername() { }

    private MemberUsername(
        MemberUsernameId id,
        MemberId memberId,
        string tenantId,
        string value,
        MemberUsernameType usernameType)
        : base(id, tenantId)
    {
        string normalizedValue = Normalize(value);

        this.MemberId = memberId;
        this.Value = value.Trim();
        this.NormalizedValue = normalizedValue;
        this.UsernameType = usernameType;
        this.IsActive = true;
    }

    public MemberId MemberId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string NormalizedValue { get; private set; } = string.Empty;
    public MemberUsernameType UsernameType { get; private set; }
    public bool IsActive { get; private set; }

    internal static Result<MemberUsername> Create(
        MemberUsernameId id,
        MemberId memberId,
        string tenantId,
        string value,
        MemberUsernameType usernameType)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<MemberUsername>(AuthDomainErrors.UsernameIdRequired);
        }

        if (memberId.Value == Guid.Empty)
        {
            return Result.Failure<MemberUsername>(AuthDomainErrors.MemberIdRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out _))
        {
            return Result.Failure<MemberUsername>(AuthDomainErrors.TenantInvalid);
        }

        if (!TryNormalize(value, out string? normalizedValue) ||
            normalizedValue.Length > NormalizedValueMaxLength ||
            !IsValidUsernameFormat(value.Trim(), usernameType))
        {
            return Result.Failure<MemberUsername>(AuthDomainErrors.UsernameNotValid);
        }

        return Result.Success(new MemberUsername(id, memberId, tenantId, value.Trim(), usernameType));
    }

    internal void Deactivate() => this.IsActive = false;

    public static string Normalize(string? value) =>
        TryNormalize(value, out string? normalized)
            ? normalized
            : string.Empty;

    public static bool TryNormalize(string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmedValue = value.Trim();
        if (trimmedValue.Length > ValueMaxLength)
        {
            return false;
        }

        normalized = trimmedValue.ToUpperInvariant();
        return true;
    }

    private static bool IsValidUsernameFormat(string value, MemberUsernameType usernameType) =>
        usernameType switch
        {
            MemberUsernameType.Email => EmailRegex().IsMatch(value),
            MemberUsernameType.Phone => value.Length == 10 && value.All(char.IsDigit),
            _ => false
        };

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();
}
