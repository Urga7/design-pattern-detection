using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DesignPatternDetection.Detection.InputResolution;

/// <summary>
/// Expands a user-supplied path into the C# source files to scan: a single <c>.cs</c> file, a directory,
/// a <c>.csproj</c> project (the sources beside it), or a <c>.sln</c>/<c>.slnx</c> solution.
/// </summary>
public static partial class SourceFileResolver
{
    /// <summary>
    /// Matches the project line of the classic <c>.sln</c> format, capturing the relative project path.
    /// </summary>
    [GeneratedRegex("""^Project\("[^"]*"\)\s*=\s*"[^"]*",\s*"(?<path>[^"]*)"\s*,""", RegexOptions.Multiline)]
    private static partial Regex SlnProjectLineRegex();

    public static IReadOnlyList<string> Resolve(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (Directory.Exists(fullPath))
            return Finish(SourcesUnder(fullPath));

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"'{path}' was not found.", fullPath);

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".cs" => [fullPath],
            ".csproj" => Finish(SourcesUnder(Path.GetDirectoryName(fullPath)!)),
            ".sln" => Finish(SolutionSources(fullPath, SlnProjectPaths)),
            ".slnx" => Finish(SolutionSources(fullPath, SlnxProjectPaths)),
            var extension => throw new ArgumentException(
                $"'{path}' is not a supported input: expected a .cs file, a directory, " +
                $"a .csproj project or a .sln/.slnx solution, but got '{extension}'.")
        };
    }

    private static IEnumerable<string> SolutionSources(string solutionPath, Func<string, IEnumerable<string>> projectPaths)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        return projectPaths(solutionPath)
            .Select(projectPath => ResolveProject(solutionDirectory, projectPath))
            .SelectMany(projectFile => SourcesUnder(Path.GetDirectoryName(projectFile)!));
    }

    private static string ResolveProject(string solutionDirectory, string relativeProjectPath)
    {
        var normalized = relativeProjectPath.Replace('\\', Path.DirectorySeparatorChar);
        var projectFile = Path.GetFullPath(Path.Combine(solutionDirectory, normalized));

        if (!File.Exists(projectFile))
            throw new FileNotFoundException($"The solution references '{relativeProjectPath}', which was not found.", projectFile);

        return projectFile;
    }

    /// <summary>Project paths from the classic <c>.sln</c> format.</summary>
    private static IEnumerable<string> SlnProjectPaths(string solutionPath) =>
        SlnProjectLineRegex().Matches(File.ReadAllText(solutionPath))
            .Select(match => match.Groups["path"].Value)
            .Where(IsProjectPath);

    /// <summary>Project paths from the XML-based <c>.slnx</c> format.</summary>
    private static IEnumerable<string> SlnxProjectPaths(string solutionPath) =>
        XDocument.Load(solutionPath)
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .OfType<string>()
            .Where(IsProjectPath);

    private static bool IsProjectPath(string path) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SourcesUnder(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(Path.GetRelativePath(directory, file)));

    private static bool IsBuildOutput(string relativePath) =>
        relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

    /// <summary>Deduplicates and orders the files.</summary>
    private static List<string> Finish(IEnumerable<string> files) => files
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
