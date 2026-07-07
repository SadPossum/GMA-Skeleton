namespace Gma.Modules.Auth.Application;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Framework.Results;

internal static class UsernameTypeMapper
{
    public static Result<MemberUsernameType> Map(UsernameType usernameType) =>
        usernameType switch
        {
            UsernameType.Email => Result.Success(MemberUsernameType.Email),
            UsernameType.Phone => Result.Success(MemberUsernameType.Phone),
            _ => Result.Failure<MemberUsernameType>(AuthApplicationErrors.UsernameTypeInvalid)
        };
}
