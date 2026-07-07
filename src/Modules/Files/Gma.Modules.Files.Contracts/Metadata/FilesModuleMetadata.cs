namespace Gma.Modules.Files.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;

public static class FilesModuleMetadata
{
    public const string Name = "files";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithProfile(FilesProfiles.Default)
        .Build();
}
