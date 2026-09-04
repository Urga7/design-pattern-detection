using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Detection quality of one pattern over a corpus, counted at unit level: a true positive is a unit labeled with the
/// pattern where the detector fired, a false positive a unit not labeled with it where it fired anyway, a false
/// negative a labeled unit the detector missed. <see cref="MatchRows"/> is the raw match-row count across all units.
/// </summary>
public sealed record PatternMetrics(
    string Pattern,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    int MatchRows)
{
    /// <summary>Null when the detector never fired - precision is then undefined, not perfect.</summary>
    public double? Precision =>
        TruePositives + FalsePositives == 0
            ? null
            : (double)TruePositives / (TruePositives + FalsePositives);

    /// <summary>Null when no unit is labeled with the pattern - recall is then undefined.</summary>
    public double? Recall =>
        TruePositives + FalseNegatives == 0
            ? null
            : (double)TruePositives / (TruePositives + FalseNegatives);

    /// <summary>
    /// <c>2TP / (2TP + FP + FN)</c>, which stays defined whenever the pattern was labeled or detected.
    /// </summary>
    public double? F1 =>
        2 * TruePositives + FalsePositives + FalseNegatives == 0
            ? null
            : 2.0 * TruePositives / (2 * TruePositives + FalsePositives + FalseNegatives);
}

/// <summary>Corpus-wide averages; null components mean no pattern contributed a defined value.</summary>
public sealed record AggregateMetrics(double? Precision, double? Recall, double? F1);

/// <summary>
/// What the semantic review did to a corpus, and what it cost. Present only on a reviewed run. <c>Unreviewed</c>
/// bounds how much of the corpus the scores actually reflect.
/// </summary>
public sealed record ReviewMetrics(
    string Model,
    int Reviewed,
    int Confirmed,
    int Uncertain,
    int Rejected,
    int Dropped,
    int Unreviewed,
    int CacheHits,
    long InputTokens,
    long OutputTokens,
    double DurationSeconds,
    string? FirstFailure);

/// <summary>
/// One unit's outcome, as the three sets a comparison needs: what it is labeled with, what the detectors found, and
/// what survived review. On an unreviewed run the last two are equal.
/// </summary>
public sealed record UnitOutcome(
    string Unit,
    IReadOnlyList<string> Labeled,
    IReadOnlyList<string> Structural,
    IReadOnlyList<string> Detected);

/// <summary>
/// The complete result of evaluating every detector against a labeled corpus.
/// </summary>
public sealed record EvaluationReport(
    string Corpus,
    string? Commit,
    DateTimeOffset GeneratedAt,
    int UnitCount,
    int SkippedDirectories,
    double DetectionSeconds,
    IReadOnlyList<PatternMetrics> PerPattern,
    AggregateMetrics Macro,
    AggregateMetrics Micro,
    IReadOnlyList<string> Errors,
    ReviewMetrics? Review = null,
    IReadOnlyList<EvaluationReport>? Corpora = null,
    IReadOnlyList<UnitOutcome>? Units = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static EvaluationReport Load(string path) =>
        JsonSerializer.Deserialize<EvaluationReport>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"'{path}' does not contain an evaluation report.");
}
