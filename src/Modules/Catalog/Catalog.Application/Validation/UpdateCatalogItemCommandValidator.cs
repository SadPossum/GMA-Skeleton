namespace Catalog.Application.Validation;

using Catalog.Application.Commands;
using Catalog.Contracts;
using Gma.Framework.Cqrs;

internal sealed class UpdateCatalogItemCommandValidator : ICommandValidator<UpdateCatalogItemCommand>
{
    public IEnumerable<string> Validate(UpdateCatalogItemCommand command)
    {
        if (command.ItemId == Guid.Empty)
        {
            yield return "Catalog item id is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Sku))
        {
            yield return "SKU is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return "Name is required.";
        }

        if (command.Price <= 0)
        {
            yield return "Price must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(command.Currency))
        {
            yield return "Currency is required.";
        }

        string[] availableRegions = command.AvailableRegions?
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .ToArray() ?? [];
        if (availableRegions.Length > CatalogContractLimits.AvailableRegionMaxCount)
        {
            yield return $"At most {CatalogContractLimits.AvailableRegionMaxCount} available regions can be supplied.";
        }

        foreach (string region in availableRegions)
        {
            if (!CatalogRegionCodes.TryNormalize(region, out _))
            {
                yield return "Available region code is invalid.";
                yield break;
            }
        }
    }
}
