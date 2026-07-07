namespace Ordering.Application.Validation;

using Ordering.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class ListOrdersQueryValidator : IQueryValidator<ListOrdersQuery>
{
    public IEnumerable<string> Validate(ListOrdersQuery query)
    {
        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }
    }
}
