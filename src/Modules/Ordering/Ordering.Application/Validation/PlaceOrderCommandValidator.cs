namespace Ordering.Application.Validation;

using Ordering.Application.Commands;
using Catalog.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class PlaceOrderCommandValidator : ICommandValidator<PlaceOrderCommand>
{
    public IEnumerable<string> Validate(PlaceOrderCommand command)
    {
        if (command.CatalogItemId == Guid.Empty)
        {
            yield return "Catalog item id is required.";
        }

        if (command.Quantity <= 0)
        {
            yield return "Quantity must be greater than zero.";
        }

        if (command.Subject is null || command.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }

        if (!CatalogRegionCodes.TryNormalize(command.RegionCode, out _))
        {
            yield return "Region code is invalid.";
        }
    }
}
