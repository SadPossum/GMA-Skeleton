namespace Ordering.Persistence;

using Catalog.Contracts;
using Ordering.Domain.Aggregates;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class CatalogItemProjection : ScopedEntity<Guid>
{
    private CatalogItemProjection() { }

    private CatalogItemProjection(
        Guid id,
        string scopeId,
        Guid catalogItemId,
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status)
        : base(id, scopeId)
    {
        this.CatalogItemId = catalogItemId;
        this.Apply(sku, name, price, currency, status);
    }

    private CatalogItemProjection(
        Guid id,
        string scopeId,
        Guid catalogItemId)
        : base(id, scopeId)
    {
        this.CatalogItemId = catalogItemId;
        this.Sku = string.Empty;
        this.Name = string.Empty;
        this.Price = 0;
        this.Currency = string.Empty;
        this.Status = CatalogItemStatus.Discontinued;
    }

    public Guid CatalogItemId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public CatalogItemStatus Status { get; private set; }
    public string AvailableRegionCodes { get; private set; } = string.Empty;

    public static CatalogItemProjection Create(
        Guid id,
        string scopeId,
        Guid catalogItemId,
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status,
        IReadOnlyCollection<string>? availableRegions = null) =>
        new(id, scopeId, catalogItemId, sku, name, price, currency, status)
        {
            AvailableRegionCodes = EncodeAvailableRegions(availableRegions)
        };

    public static CatalogItemProjection CreateDiscontinuedPlaceholder(
        Guid id,
        string scopeId,
        Guid catalogItemId) =>
        new(id, scopeId, catalogItemId);

    public void Update(
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status,
        IReadOnlyCollection<string>? availableRegions)
    {
        this.Apply(sku, name, price, currency, status);
        this.AvailableRegionCodes = EncodeAvailableRegions(availableRegions);
    }

    public void MarkDiscontinued() => this.Status = CatalogItemStatus.Discontinued;

    public IReadOnlyCollection<string> GetAvailableRegions() =>
        DecodeAvailableRegions(this.AvailableRegionCodes);

    private void Apply(string sku, string name, decimal price, string currency, CatalogItemStatus status)
    {
        Result validation = Order.ValidateCatalogSnapshot(
            this.CatalogItemId,
            sku,
            name,
            price,
            currency);

        if (validation.IsFailure)
        {
            throw new ArgumentException(validation.Error.Code, nameof(sku));
        }

        this.Sku = Order.NormalizeCatalogSku(sku);
        this.Name = Order.NormalizeCatalogItemName(name);
        this.Price = price;
        this.Currency = Order.NormalizeCurrency(currency);
        this.Status = NormalizeStatus(status);
    }

    private static CatalogItemStatus NormalizeStatus(CatalogItemStatus status) =>
        Enum.IsDefined(status) ? status : CatalogItemStatus.Unknown;

    private static string EncodeAvailableRegions(IReadOnlyCollection<string>? availableRegions) =>
        string.Join(',', CatalogRegionCodes.NormalizeMany(availableRegions));

    private static IReadOnlyCollection<string> DecodeAvailableRegions(string? availableRegionCodes) =>
        string.IsNullOrWhiteSpace(availableRegionCodes)
            ? []
            : CatalogRegionCodes.NormalizeMany(availableRegionCodes.Split(',', StringSplitOptions.RemoveEmptyEntries));
}
