namespace Catalog.Contracts;

using Gma.Framework.ModuleComposition;

public static class CatalogCompositionFeatures
{
    public static readonly CompositionFeatureId Items = new("catalog.items");

    public static ProvidedCompositionFeature ItemsProvided(string provider) =>
        new(Items, provider, "Catalog item management and read model are selected.");

    public static RequiredCompositionFeature ItemsRequired(string owner, string? reason = null, bool optional = false) =>
        new(Items, owner, optional, reason);
}
