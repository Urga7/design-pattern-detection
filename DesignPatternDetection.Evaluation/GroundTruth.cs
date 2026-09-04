using System.Text.Json;
using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Explicit unit labels for corpora whose file and folder names don't encode them. The JSON shape is
/// <c>{ "units": [ { "path": "src/Loggers", "patterns": ["Decorator"] } ] }</c> where <c>path</c> is a directory or
/// <c>.cs</c> file relative to the corpus root and <c>patterns</c> holds canonical (or normalizable) pattern names;
/// an empty array marks a deliberate negative unit. A ground-truth file replaces name-based discovery entirely, and
/// any invalid path or unknown pattern name fails the load.
/// </summary>
public static class GroundTruth
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CorpusUnits Load(string groundTruthPath, string corpusRoot, PatternNameNormalizer normalizer)
    {
        var file = JsonSerializer.Deserialize<GroundTruthFile>(File.ReadAllText(groundTruthPath), JsonOptions);
        if (file?.Units is not { Count: > 0 })
            throw new InvalidDataException($"'{groundTruthPath}' contains no units.");

        var units = file.Units.Select(unit => Resolve(unit, corpusRoot, normalizer)).ToList();
        return new CorpusUnits(units, SkippedUnlabeled: 0);
    }

    private static EvaluationUnit Resolve(GroundTruthUnit unit, string corpusRoot, PatternNameNormalizer normalizer)
    {
        if (string.IsNullOrWhiteSpace(unit.Path))
            throw new InvalidDataException("A ground-truth unit is missing its 'path'.");

        if (unit.Patterns is null)
            throw new InvalidDataException(
                $"Ground-truth unit '{unit.Path}' is missing its 'patterns' array " +
                "(use an empty array for a deliberate negative unit).");

        var patterns = unit.Patterns
            .Select(name => normalizer.Normalize(name) ?? 
                            throw new InvalidDataException($"Ground-truth unit '{unit.Path}' names an unknown pattern '{name}'."))
            .ToHashSet();

        var relative = unit.Path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var files = SourceFileResolver.Resolve(Path.Combine(corpusRoot, relative));

        return new EvaluationUnit(unit.Path.Replace('\\', '/'), files, patterns);
    }

    private sealed record GroundTruthFile(List<GroundTruthUnit>? Units);

    private sealed record GroundTruthUnit(string? Path, List<string>? Patterns);
}
