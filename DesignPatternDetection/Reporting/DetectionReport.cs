using System.Text.Json;
using System.Text.Json.Serialization;
using DesignPatternDetection.Detection;

namespace DesignPatternDetection.Reporting;

/// <summary>
/// One role of a match: the display label, the source span when the node was declared in the scanned source,
/// <c>Iri</c> naming the role in the FDP ontology (see <see cref="FdpVocabulary"/>) and <c>Fragment</c> naming its
/// node in the scan graph (<c>Demo.PagerAdapter</c>). The span is null for a metadata-only or unresolved type,
/// <c>Iri</c> for a role FDP does not model, and <c>Fragment</c> for a role bound to a literal or blank node.
/// </summary>
public sealed record RoleBinding(
    string Role,
    string Label,
    string? File,
    int? StartLine,
    int? EndLine,
    string? Iri = null,
    string? Fragment = null);

/// <summary>A reviewer's ruling on a match, present only when the scan was verified.</summary>
public sealed record VerdictReport(string Outcome, string Rationale, string Model);

public sealed record MatchReport(IReadOnlyList<RoleBinding> Roles, VerdictReport? Verdict = null);

/// <summary>
/// One detector's matches. <c>Iri</c> names the pattern in the FDP ontology, and is null both for a pattern FDP does
/// not model and for a detector that matched nothing.
/// </summary>
public sealed record PatternReport(
    string Pattern,
    [property: JsonPropertyOrder(1)] IReadOnlyList<MatchReport> Matches,
    string? Iri = null);

/// <summary>
/// The machine-readable result of one scan: every detector with its matches, each role resolved to a source span
/// where possible.
/// </summary>
public sealed record DetectionReport(
    string Tool,
    string Version,
    DateTimeOffset GeneratedAt,
    int FileCount,
    IReadOnlyList<PatternReport> Patterns)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static DetectionReport From(ScanResult scan) =>
        new(
            "DesignPatternDetection",
            typeof(PatternDetectionEngine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            DateTimeOffset.UtcNow,
            scan.FileCount,
            scan.Detections
                .Select(detection => new PatternReport(
                    detection.PatternName,
                    detection.Matches.Select(match => ToMatchReport(match, scan.Locations)).ToList(),
                    detection.Matches.FirstOrDefault()?.PatternIri))
                .ToList());

    private static MatchReport ToMatchReport(PatternMatch match, IReadOnlyDictionary<string, SourceSpan> locations)
    {
        var roles = match.Bindings
            .Select(binding =>
            {
                var fragment = match.Fragments is { } fragments && fragments.TryGetValue(binding.Key, out var found)
                    ? found
                    : null;

                var span = fragment is not null && locations.TryGetValue(fragment, out var located)
                    ? located
                    : null;

                var iri = match.RoleIris is { } iris && iris.TryGetValue(binding.Key, out var role)
                    ? role
                    : null;

                return new RoleBinding(
                    binding.Key, binding.Value, span?.FilePath, span?.StartLine, span?.EndLine, iri, fragment);
            })
            .ToList();

        var verdict = match.Verdict is { } ruling
            ? new VerdictReport(ruling.Outcome.ToString().ToLowerInvariant(), ruling.Rationale, ruling.Model)
            : null;

        return new MatchReport(roles, verdict);
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static DetectionReport Load(string path) =>
        JsonSerializer.Deserialize<DetectionReport>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' does not contain a detection report.");
}
