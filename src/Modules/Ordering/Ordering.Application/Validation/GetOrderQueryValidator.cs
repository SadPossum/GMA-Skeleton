namespace Ordering.Application.Validation;

using Ordering.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class GetOrderQueryValidator : IQueryValidator<GetOrderQuery>
{
    public IEnumerable<string> Validate(GetOrderQuery query)
    {
        if (query.OrderId == Guid.Empty)
        {
            yield return "Order id is required.";
        }

        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }
    }
}
