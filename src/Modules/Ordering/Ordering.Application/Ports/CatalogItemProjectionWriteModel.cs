namespace Ordering.Application.Ports;

using Gma.Framework.Naming;
using Catalog.Contracts;
using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Gma.Framework.Results;

public sealed record CatalogItemProjectionWriteModel
{
    public CatalogItemProjectionWriteModel(
        string tenantId,
        Guid catalogItemId,
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status,
        IReadOnlyCollection<string>? availableRegions = null)
    {
        Result validation = Order.ValidateCatalogSnapshot(catalogItemId, sku, name, price, currency);
        if (validation.IsFailure)
        {
            throw new ArgumentException(validation.Error.Code, nameof(sku));
        }

        this.TenantId = TenantIds.Normalize(tenantId);
        this.CatalogItemId = catalogItemId;
        this.Sku = Order.NormalizeCatalogSku(sku);
        this.Name = Order.NormalizeCatalogItemName(name);
        this.Price = price;
        this.Currency = Order.NormalizeCurrency(currency);
        this.Status = NormalizeStatus(status);
        this.AvailableRegions = CatalogRegionCodes.NormalizeMany(availableRegions);
    }

    public string TenantId { get; }
    public Guid CatalogItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public CatalogItemStatus Status { get; }
    public IReadOnlyCollection<string> AvailableRegions { get; }

    private static CatalogItemStatus NormalizeStatus(CatalogItemStatus status) =>
        Enum.IsDefined(status) ? status : CatalogItemStatus.Unknown;
}
