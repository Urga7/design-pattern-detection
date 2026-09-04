namespace DesignPatternDetection.Evaluation;

/// <summary>
/// One metric's before/after values and the uncertainty on their difference. An interval containing zero means the
/// corpus is too small to establish the direction of that change, whatever its point estimate says.
/// </summary>
public sealed record DeltaEstimate(
    string Metric,
    double? Structural,
    double? Semantic,
    double? Delta,
    double? Lower,
    double? Upper);

/// <summary>What semantic review did to a set of units, with the uncertainty attached.</summary>
public sealed record ReviewImpact(
    string Scope,
    int Units,
    PatternMetrics Structural,
    PatternMetrics Semantic,
    IReadOnlyList<DeltaEstimate> Deltas);

/// <summary>
/// Measures what the semantic pass changed, with a 95% interval on each change.
/// </summary>
/// <remarks>
/// The stages are compared as a paired sample - both outcomes come from the same run over the same units - and the
/// interval covers their difference rather than the two scores separately. It comes from an exhaustive
/// leave-one-unit-out jackknife, so the bounds are exact for the corpus and reproduce without a seed.
/// </remarks>
public static class ReviewImpactAnalysis
{
    /// <summary>Standard normal quantile for a two-sided 95% interval (p = 0.05).</summary>
    private const double Z95 = 1.959964;

    public static ReviewImpact Analyse(string scope, IReadOnlyList<UnitOutcome> units)
    {
        if (units.Count == 0)
            throw new ArgumentException($"'{scope}' has no units to analyse.", nameof(units));

        var counted = units.Select(Counts).ToList();
        var structural = Total(counted, unit => unit.Structural);
        var semantic = Total(counted, unit => unit.Semantic);

        List<DeltaEstimate> deltas =
        [
            Estimate("precision", counted, structural, semantic, metrics => metrics.Precision),
            Estimate("recall", counted, structural, semantic, metrics => metrics.Recall),
            Estimate("F1", counted, structural, semantic, metrics => metrics.F1)
        ];

        return new ReviewImpact(scope, units.Count, structural, semantic, deltas);
    }

    /// <summary>Both stages' pooled counts over a set of units, without the interval.</summary>
    public static (PatternMetrics Structural, PatternMetrics Semantic) Pooled(IReadOnlyList<UnitOutcome> units)
    {
        var counted = units.Select(Counts).ToList();
        return (Total(counted, unit => unit.Structural), Total(counted, unit => unit.Semantic));
    }

    /// <summary>
    /// The units a report was scored over: its own when it evaluated a single corpus, and those of its contributing
    /// corpora when it pooled several.
    /// </summary>
    public static IReadOnlyList<UnitOutcome> UnitsOf(EvaluationReport report) =>
        report.Corpora is { } corpora
            ? corpora.SelectMany(corpus => corpus.Units ?? []).ToList()
            : report.Units ?? [];

    private sealed record UnitCounts(PatternMetrics Structural, PatternMetrics Semantic);

    private static UnitCounts Counts(UnitOutcome unit) =>
        new(Score(unit.Labeled, unit.Structural), Score(unit.Labeled, unit.Detected));

    private static PatternMetrics Score(IReadOnlyList<string> labeled, IReadOnlyList<string> predicted)
    {
        var expected = labeled.ToHashSet(StringComparer.Ordinal);
        var found = predicted.ToHashSet(StringComparer.Ordinal);

        return new PatternMetrics(
            "unit",
            found.Count(expected.Contains),
            found.Count(pattern => !expected.Contains(pattern)),
            expected.Count(pattern => !found.Contains(pattern)),
            MatchRows: 0);
    }

    private static PatternMetrics Total(
        IReadOnlyList<UnitCounts> units,
        Func<UnitCounts, PatternMetrics> stage)
    {
        int truePositives = 0, falsePositives = 0, falseNegatives = 0;

        foreach (var scored in units.Select(stage))
        {
            truePositives += scored.TruePositives;
            falsePositives += scored.FalsePositives;
            falseNegatives += scored.FalseNegatives;
        }

        return new PatternMetrics("pooled", truePositives, falsePositives, falseNegatives, MatchRows: 0);
    }

    private static DeltaEstimate Estimate(
        string metric,
        IReadOnlyList<UnitCounts> units,
        PatternMetrics structural,
        PatternMetrics semantic,
        Func<PatternMetrics, double?> value)
    {
        var observed = Difference(structural, semantic, value);

// A unit whose removal leaves the metric undefined contributes no pseudo-value.
        var leaveOneOut = units
            .Select(unit => Difference(
                Without(structural, unit.Structural),
                Without(semantic, unit.Semantic),
                value))
            .OfType<double>()
            .ToList();

        var error = StandardError(leaveOneOut);

        return new DeltaEstimate(
            metric,
            value(structural),
            value(semantic),
            observed,
            observed - Z95 * error,
            observed + Z95 * error);
    }

    private static double? StandardError(IReadOnlyList<double> leaveOneOut)
    {
        if (leaveOneOut.Count < 2)
            return null;

        var mean = leaveOneOut.Average();
        var spread = leaveOneOut.Sum(value => (value - mean) * (value - mean));

        return Math.Sqrt(spread * (leaveOneOut.Count - 1) / leaveOneOut.Count);
    }

    private static double? Difference(
        PatternMetrics structural,
        PatternMetrics semantic,
        Func<PatternMetrics, double?> value) =>
        value(structural) is { } before && value(semantic) is { } after ? after - before : null;

    private static PatternMetrics Without(PatternMetrics total, PatternMetrics unit) =>
        total with
        {
            TruePositives = total.TruePositives - unit.TruePositives,
            FalsePositives = total.FalsePositives - unit.FalsePositives,
            FalseNegatives = total.FalseNegatives - unit.FalseNegatives
        };
}
