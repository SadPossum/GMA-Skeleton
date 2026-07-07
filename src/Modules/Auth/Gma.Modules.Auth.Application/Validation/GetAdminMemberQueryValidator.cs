namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetAdminMemberQueryValidator : IQueryValidator<GetAdminMemberQuery>
{
    public IEnumerable<string> Validate(GetAdminMemberQuery query)
    {
        if (query.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }
    }
}
