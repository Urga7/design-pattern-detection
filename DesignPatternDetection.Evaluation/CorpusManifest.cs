using System.Text.Json;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// One corpus in a manifest. <see cref="Source"/> takes the same values as the positional CLI argument - a directory,
/// a GitHub URL, or the built-in names <c>examples</c> and <c>refactoring-guru</c> - and <see cref="GroundTruth"/> is
/// resolved relative to the manifest's own directory.
/// </summary>
public sealed record CorpusEntry(
    string Name,
    string Source,
    string? GroundTruth = null,
    double? QueryTimeout = null);

/// <summary>The list of corpora a combined evaluation runs.</summary>
public sealed record CorpusManifest(IReadOnlyList<CorpusEntry> Corpora)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static CorpusManifest Load(string path)
    {
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(File.ReadAllText(path), JsonOptions)
                       ?? throw new InvalidDataException($"'{path}' does not contain a corpus manifest.");

        if (manifest.Corpora.Count == 0)
            throw new InvalidDataException($"'{path}' lists no corpora.");

        // Relative label paths resolve against the manifest; absolute ones are left alone.
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";

        return manifest with
        {
            Corpora = manifest.Corpora
                .Select(entry => entry.GroundTruth is null
                    ? entry
                    : entry with { GroundTruth = Path.GetFullPath(Path.Combine(directory, entry.GroundTruth)) })
                .ToList()
        };
    }
}
