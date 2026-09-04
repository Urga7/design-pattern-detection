using DesignPatternDetection.Detection;
using VDS.RDF;

namespace DesignPatternDetection.Tests;

/// <summary>
/// Builds a source-code graph from inline C# snippets by writing them to a throwaway temp directory and feeding them
/// through <see cref="SourceGraphBuilder.Build"/>.
/// </summary>
internal static class TestGraph
{
    public static IGraph From(params string[] sources) => Scan(sources).Graph;

    /// <summary>
    /// Like <see cref="From"/> but keeps the provenance side table. Sources become <c>Source0.cs</c>,
    /// <c>Source1.cs</c>, ... and the temp directory is deleted before returning, so recorded paths and line numbers
    /// are meaningful but the files themselves no longer exist.
    /// </summary>
    public static SourceGraph Scan(params string[] sources)
    {
        using var directory = new TempDirectory("dpd-tests-");

        var paths = sources
            .Select((source, index) => directory.Write($"Source{index}.cs", source))
            .ToList();

        return SourceGraphBuilder.Build(paths);
    }
}
