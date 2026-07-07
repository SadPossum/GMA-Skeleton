namespace Gma.Modules.Files.Contracts;

using Gma.Framework.ModuleComposition;

public static class FilesCompositionFeatures
{
    public static readonly CompositionFeatureId Objects = new("files.objects");

    public static ProvidedCompositionFeature ObjectsProvided(string provider) =>
        new(Objects, provider, "Tenant-scoped file object front-door services are registered.");

    public static RequiredCompositionFeature ObjectsRequired(string owner, string? reason = null, bool optional = false) =>
        new(Objects, owner, optional, reason);
}
