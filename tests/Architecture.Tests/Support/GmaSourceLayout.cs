namespace Architecture.Tests;

using System.Xml.Linq;

internal sealed class GmaSourceLayout
{
    private static readonly Dictionary<string, string> FrameworkFeatureFolders =
        new(StringComparer.Ordinal)
        {
            ["Gma.Framework.AccessControl"] = "Security",
            ["Gma.Framework.AccessControl.AspNetCore"] = "Security",
            ["Gma.Framework.Administration"] = "Administration",
            ["Gma.Framework.Administration.AccessControl"] = "Administration",
            ["Gma.Framework.Administration.Api"] = "Administration",
            ["Gma.Framework.Administration.Cli"] = "Administration",
            ["Gma.Framework.Api"] = "Api",
            ["Gma.Framework.Api.OpenApi"] = "Api",
            ["Gma.Framework.Api.Serilog"] = "Api",
            ["Gma.Framework.Application.Composition"] = "Application",
            ["Gma.Framework.Application.Events"] = "Application",
            ["Gma.Framework.Application.Events.Infrastructure"] = "Application",
            ["Gma.Framework.Permissions"] = "Security",
            ["Gma.Framework.Caching"] = "Caching",
            ["Gma.Framework.Caching.Cqrs"] = "Caching",
            ["Gma.Framework.Caching.Infrastructure"] = "Caching",
            ["Gma.Framework.Caching.Redis"] = "Caching",
            ["Gma.Framework.Cqrs"] = "Cqrs",
            ["Gma.Framework.Cqrs.Infrastructure"] = "Cqrs",
            ["Gma.Framework.Domain"] = "Domain",
            ["Gma.Framework.FileManagement"] = "FileManagement",
            ["Gma.Framework.FileManagement.LocalStorage"] = "FileManagement",
            ["Gma.Framework.FileManagement.Minio"] = "FileManagement",
            ["Gma.Framework.Infrastructure"] = "Infrastructure",
            ["Gma.Framework.Logging.Serilog"] = "Logging",
            ["Gma.Framework.Messaging"] = "Messaging",
            ["Gma.Framework.Messaging.Infrastructure"] = "Messaging",
            ["Gma.Framework.Messaging.Nats"] = "Messaging",
            ["Gma.Framework.Messaging.Nats.Aspire"] = "Messaging",
            ["Gma.Framework.ModuleComposition"] = "Modules",
            ["Gma.Framework.Modules"] = "Modules",
            ["Gma.Framework.Naming"] = "Naming",
            ["Gma.Framework.Notifications"] = "Notifications",
            ["Gma.Framework.Notifications.Api"] = "Notifications",
            ["Gma.Framework.Notifications.Cqrs"] = "Notifications",
            ["Gma.Framework.Notifications.Infrastructure"] = "Notifications",
            ["Gma.Framework.Notifications.SignalR"] = "Notifications",
            ["Gma.Framework.Numerics"] = "Numerics",
            ["Gma.Framework.Observability"] = "Observability",
            ["Gma.Framework.Observability.Infrastructure"] = "Observability",
            ["Gma.Framework.Pagination"] = "Pagination",
            ["Gma.Framework.Scoping"] = "Scoping",
            ["Gma.Framework.Scoping.Infrastructure"] = "Scoping",
            ["Gma.Framework.Persistence.EntityFrameworkCore"] = "Persistence",
            ["Gma.Framework.ProjectionRebuild"] = "ProjectionRebuild",
            ["Gma.Framework.ProjectionRebuild.EntityFrameworkCore"] = "ProjectionRebuild",
            ["Gma.Framework.ProjectionRebuild.Tasks"] = "ProjectionRebuild",
            ["Gma.Framework.Realtime"] = "Realtime",
            ["Gma.Framework.Realtime.Infrastructure"] = "Realtime",
            ["Gma.Framework.Realtime.Notifications"] = "Realtime",
            ["Gma.Framework.Results"] = "Results",
            ["Gma.Framework.Runtime"] = "Runtime",
            ["Gma.Framework.Runtime.Infrastructure"] = "Runtime",
            ["Gma.Framework.Security"] = "Security",
            ["Gma.Framework.Tasks"] = "Tasks",
            ["Gma.Framework.Tasks.Cqrs"] = "Tasks",
            ["Gma.Framework.Tasks.Infrastructure"] = "Tasks",
            ["Gma.Framework.Tenancy"] = "Tenancy",
            ["Gma.Framework.Tenancy.Api.Serilog"] = "Tenancy",
            ["Gma.Framework.Tenancy.AccessControl.AspNetCore"] = "Tenancy",
            ["Gma.Framework.Tenancy.Caching"] = "Tenancy",
            ["Gma.Framework.Tenancy.Cqrs"] = "Tenancy",
            ["Gma.Framework.Tenancy.Infrastructure"] = "Tenancy",
            ["Gma.Framework.Tenancy.Messaging"] = "Tenancy",
            ["Gma.Framework.Tenancy.Messaging.Infrastructure"] = "Tenancy",
            ["Gma.Framework.Tenancy.Scoping"] = "Tenancy",
            ["Gma.Framework.Tenancy.Tasks"] = "Tenancy",
        };

    private static readonly IReadOnlyDictionary<string, string> ModulePropertyNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AccessControl"] = "GmaModuleAccessControlRoot",
            ["Administration"] = "GmaModuleAdministrationRoot",
            ["Auth"] = "GmaModuleAuthRoot",
            ["Catalog"] = "GmaModuleCatalogRoot",
            ["Files"] = "GmaModuleFilesRoot",
            ["Notifications"] = "GmaModuleNotificationsRoot",
            ["Ordering"] = "GmaModuleOrderingRoot",
            ["TaskRuntime"] = "GmaModuleTaskRuntimeRoot",
            ["TaskSamples"] = "GmaModuleTaskSamplesRoot",
            ["Tenancy"] = "GmaModuleTenancyRoot",
        };

    private GmaSourceLayout(
        string repositoryRoot,
        string frameworkRoot,
        string modulesRoot,
        IReadOnlyDictionary<string, string> moduleRoots)
    {
        this.RepositoryRoot = EnsureTrailingSeparator(Path.GetFullPath(repositoryRoot));
        this.FrameworkRoot = EnsureTrailingSeparator(Path.GetFullPath(frameworkRoot));
        this.ModulesRoot = EnsureTrailingSeparator(Path.GetFullPath(modulesRoot));
        this.ModuleRoots = moduleRoots
            .ToDictionary(
                pair => pair.Key,
                pair => EnsureTrailingSeparator(Path.GetFullPath(pair.Value)),
                StringComparer.Ordinal);
    }

    public string RepositoryRoot { get; }

    public string FrameworkRoot { get; }

    public string ModulesRoot { get; }

    public IReadOnlyDictionary<string, string> ModuleRoots { get; }

    public bool UsesExternalSourceRoots =>
        !IsUnder(this.FrameworkRoot, Path.Combine(this.RepositoryRoot, "src", "Framework")) ||
        this.ModuleRoots.Values.Any(root => !IsUnder(root, Path.Combine(this.RepositoryRoot, "src", "Modules")));

    public static GmaSourceLayout FromRepositoryRoot(string repositoryRoot)
    {
        string normalizedRepositoryRoot = EnsureTrailingSeparator(Path.GetFullPath(repositoryRoot));
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["MSBuildThisFileDirectory"] = normalizedRepositoryRoot,
            ["GmaRepositoryRoot"] = normalizedRepositoryRoot,
            ["GmaFrameworkRoot"] = Path.Combine(normalizedRepositoryRoot, "src", "Framework") + Path.DirectorySeparatorChar,
            ["GmaModulesRoot"] = Path.Combine(normalizedRepositoryRoot, "src", "Modules") + Path.DirectorySeparatorChar,
        };

        foreach ((string moduleName, string propertyName) in ModulePropertyNames)
        {
            properties[propertyName] = Path.Combine(properties["GmaModulesRoot"], moduleName) + Path.DirectorySeparatorChar;
        }

        string sourceRootsPath = Path.Combine(normalizedRepositoryRoot, "Gma.SourceRoots.props");
        if (File.Exists(sourceRootsPath))
        {
            foreach (XElement element in XDocument
                .Load(sourceRootsPath)
                .Descendants()
                .Where(element => !element.HasElements))
            {
                string propertyName = element.Name.LocalName;
                string? rawValue = element.Value;
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                properties[propertyName] = EnsureTrailingSeparator(ExpandMsBuildProperties(rawValue.Trim(), properties));
            }
        }

        return new GmaSourceLayout(
            normalizedRepositoryRoot,
            properties["GmaFrameworkRoot"],
            properties["GmaModulesRoot"],
            ModulePropertyNames.ToDictionary(
                pair => pair.Key,
                pair => properties[pair.Value],
                StringComparer.Ordinal));
    }

    public static string FrameworkPath(string repositoryRoot, params string[] segments) =>
        FromRepositoryRoot(repositoryRoot).GetFrameworkPath(segments);

    public static string ModulePath(string repositoryRoot, string moduleName, params string[] segments) =>
        Combine(FromRepositoryRoot(repositoryRoot).GetModuleRoot(moduleName), segments);

    public static string ModuleScaffolderPath(string repositoryRoot) =>
        Combine(FromRepositoryRoot(repositoryRoot).FrameworkRepositoryRoot, ["eng", "new-module.ps1"]);

    public string FrameworkRepositoryRoot =>
        Directory.Exists(Path.Combine(Path.GetDirectoryName(this.FrameworkRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? this.FrameworkRoot, "docs"))
            ? Path.GetDirectoryName(this.FrameworkRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? this.FrameworkRoot
            : this.FrameworkRoot;

    public IEnumerable<string> DocumentationRoots()
    {
        string rootDocs = Path.Combine(this.RepositoryRoot, "docs");
        if (Directory.Exists(rootDocs))
        {
            yield return rootDocs;
        }

        string frameworkDocs = Path.Combine(this.FrameworkRepositoryRoot, "docs");
        if (Directory.Exists(frameworkDocs))
        {
            yield return frameworkDocs;
        }

        foreach (string moduleRoot in this.ModuleRoots.Values.Order(StringComparer.OrdinalIgnoreCase))
        {
            string packageRoot = GetPackageRoot(moduleRoot);
            string moduleDocs = Path.Combine(packageRoot, "docs");
            if (Directory.Exists(moduleDocs))
            {
                yield return moduleDocs;
            }
        }
    }

    public IEnumerable<string> EnumerateSourceFiles(string root)
    {
        foreach (string searchRoot in this.ResolveSourceSearchRoots(root))
        {
            if (!Directory.Exists(searchRoot))
            {
                continue;
            }

            foreach (string sourcePath in Directory.EnumerateFiles(searchRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return sourcePath;
            }
        }
    }

    public IEnumerable<string> ResolveSourceSearchRoots(string root)
    {
        string fullRoot = Path.GetFullPath(root);
        string repositorySrcRoot = Path.Combine(this.RepositoryRoot, "src");
        string repositoryFrameworkRoot = Path.Combine(repositorySrcRoot, "Framework");
        string repositoryModulesRoot = Path.Combine(repositorySrcRoot, "Modules");

        if (PathEquals(fullRoot, repositorySrcRoot))
        {
            yield return repositorySrcRoot;

            if (!IsUnder(this.FrameworkRoot, repositorySrcRoot))
            {
                yield return this.FrameworkRoot;
            }

            foreach (string moduleRoot in this.ModuleRoots.Values.Where(moduleRoot => !IsUnder(moduleRoot, repositorySrcRoot)))
            {
                yield return moduleRoot;
            }

            yield break;
        }

        if (PathEquals(fullRoot, repositoryFrameworkRoot))
        {
            yield return this.FrameworkRoot;
            yield break;
        }

        if (IsUnder(fullRoot, repositoryFrameworkRoot))
        {
            string relative = Path.GetRelativePath(repositoryFrameworkRoot, fullRoot);
            yield return Path.Combine(this.FrameworkRoot, relative);
            yield break;
        }

        if (PathEquals(fullRoot, repositoryModulesRoot))
        {
            foreach (string moduleRoot in this.ModuleRoots.Values)
            {
                yield return moduleRoot;
            }

            yield break;
        }

        if (IsUnder(fullRoot, repositoryModulesRoot))
        {
            string relative = Path.GetRelativePath(repositoryModulesRoot, fullRoot);
            string moduleName = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            string rest = relative.Length == moduleName.Length ? string.Empty : relative[(moduleName.Length + 1)..];

            if (this.ModuleRoots.TryGetValue(moduleName, out string? moduleRoot))
            {
                yield return string.IsNullOrWhiteSpace(rest) ? moduleRoot : Path.Combine(moduleRoot, rest);
                yield break;
            }
        }

        yield return fullRoot;
    }

    public string GetModuleRoot(string moduleName)
    {
        if (this.ModuleRoots.TryGetValue(moduleName, out string? moduleRoot))
        {
            return moduleRoot;
        }

        throw new InvalidOperationException($"Unknown module source root '{moduleName}'.");
    }

    public string GetModulePackageRoot(string moduleName) =>
        GetPackageRoot(this.GetModuleRoot(moduleName));

    public string GetFrameworkPath(params string[] segments)
    {
        if (segments.Length > 0 &&
            FrameworkFeatureFolders.TryGetValue(segments[0], out string? featureFolder))
        {
            return Combine(this.FrameworkRoot, [featureFolder, .. segments]);
        }

        return Combine(this.FrameworkRoot, segments);
    }

    public string ToCanonicalRelativePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (IsUnder(fullPath, this.FrameworkRoot))
        {
            return Path.Combine("src", "Framework", Path.GetRelativePath(this.FrameworkRoot, fullPath));
        }

        string frameworkPackageRoot = this.FrameworkRepositoryRoot;
        if (IsUnder(fullPath, frameworkPackageRoot))
        {
            string relativePath = Path.GetRelativePath(frameworkPackageRoot, fullPath);
            if (StartsWithSourcePackageFolder(relativePath))
            {
                return Path.Combine("src", "Framework", relativePath);
            }
        }

        foreach ((string moduleName, string moduleRoot) in this.ModuleRoots)
        {
            if (IsUnder(fullPath, moduleRoot))
            {
                return Path.Combine("src", "Modules", moduleName, Path.GetRelativePath(moduleRoot, fullPath));
            }

            string modulePackageRoot = GetPackageRoot(moduleRoot);
            if (IsUnder(fullPath, modulePackageRoot))
            {
                string relativePath = Path.GetRelativePath(modulePackageRoot, fullPath);
                if (StartsWithSourcePackageFolder(relativePath))
                {
                    return Path.Combine("src", "Modules", moduleName, relativePath);
                }
            }
        }

        return Path.GetRelativePath(this.RepositoryRoot, fullPath);
    }

    public bool TryResolveCanonicalPath(string canonicalPath, out string resolvedPath)
    {
        string normalizedCanonicalPath = canonicalPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string repositoryFrameworkPrefix = Path.Combine("src", "Framework") + Path.DirectorySeparatorChar;
        if (normalizedCanonicalPath.StartsWith(repositoryFrameworkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = normalizedCanonicalPath[repositoryFrameworkPrefix.Length..];
            resolvedPath = this.ResolveFrameworkRelativePath(relativePath);
            return true;
        }

        string repositoryModulesPrefix = Path.Combine("src", "Modules") + Path.DirectorySeparatorChar;
        if (normalizedCanonicalPath.StartsWith(repositoryModulesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = normalizedCanonicalPath[repositoryModulesPrefix.Length..];
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !this.ModuleRoots.TryGetValue(segments[0], out string? moduleRoot))
            {
                resolvedPath = string.Empty;
                return false;
            }

            string moduleRelativePath = string.Join(Path.DirectorySeparatorChar, segments.Skip(1));
            resolvedPath = Path.Combine(GetModulePathRoot(moduleRoot, moduleRelativePath), moduleRelativePath);
            return true;
        }

        resolvedPath = string.Empty;
        return false;
    }

    private static string Combine(string root, string[] segments) =>
        segments.Length == 0
            ? root
            : Path.Combine([root, .. segments]);

    private static string ExpandMsBuildProperties(string value, IReadOnlyDictionary<string, string> properties)
    {
        string expanded = value;
        bool changed;
        do
        {
            changed = false;
            foreach ((string propertyName, string propertyValue) in properties)
            {
                string token = $"$({propertyName})";
                if (!expanded.Contains(token, StringComparison.Ordinal))
                {
                    continue;
                }

                expanded = expanded.Replace(token, propertyValue, StringComparison.Ordinal);
                changed = true;
            }
        }
        while (changed);

        return expanded;
    }

    private static string GetPackageRoot(string sourceRoot)
    {
        DirectoryInfo sourceDirectory = new(sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(sourceDirectory.Name, "src", StringComparison.OrdinalIgnoreCase) && sourceDirectory.Parent is not null
            ? sourceDirectory.Parent.FullName
            : sourceRoot;
    }

    private string GetFrameworkPathRoot(string relativePath) =>
        StartsWithSourcePackageFolder(relativePath)
            ? this.FrameworkRepositoryRoot
            : this.FrameworkRoot;

    private string ResolveFrameworkRelativePath(string relativePath)
    {
        string[] segments = relativePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0 &&
            FrameworkFeatureFolders.TryGetValue(segments[0], out string? featureFolder))
        {
            return Path.Combine(this.FrameworkRoot, featureFolder, relativePath);
        }

        return Path.Combine(this.GetFrameworkPathRoot(relativePath), relativePath);
    }

    private static string GetModulePathRoot(string moduleRoot, string relativePath) =>
        StartsWithSourcePackageFolder(relativePath)
            ? GetPackageRoot(moduleRoot)
            : moduleRoot;

    private static bool StartsWithSourcePackageFolder(string relativePath)
    {
        string firstSegment = relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .FirstOrDefault() ?? string.Empty;
        return firstSegment is "docs" or "eng" or "tests";
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string parent)
    {
        string relativePath = Path.GetRelativePath(parent, path);
        return !relativePath.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relativePath);
    }

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
}
