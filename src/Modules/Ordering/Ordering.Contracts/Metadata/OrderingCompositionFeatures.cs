namespace Ordering.Contracts;

using Gma.Framework.ModuleComposition;

public static class OrderingCompositionFeatures
{
    public static readonly CompositionFeatureId Orders = new("ordering.orders");
    public static readonly CompositionFeatureId CatalogItemProjections = new("ordering.catalog-item-projections");

    public static ProvidedCompositionFeature OrdersProvided(string provider) =>
        new(Orders, provider, "Ordering aggregate and order read model are selected.");

    public static ProvidedCompositionFeature CatalogItemProjectionsProvided(string provider) =>
        new(CatalogItemProjections, provider, "Ordering-owned catalog item projection store is selected.");

    public static RequiredCompositionFeature OrdersRequired(string owner, string? reason = null, bool optional = false) =>
        new(Orders, owner, optional, reason);

    public static RequiredCompositionFeature CatalogItemProjectionsRequired(string owner, string? reason = null, bool optional = false) =>
        new(CatalogItemProjections, owner, optional, reason);
}
