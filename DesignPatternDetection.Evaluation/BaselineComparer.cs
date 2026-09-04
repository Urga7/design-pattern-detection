namespace DesignPatternDetection.Evaluation;

/// <summary>
/// One pattern's F1 movement between a baseline run and the current run. Both figures are null when the pattern was
/// neither labeled nor detected in that run.
/// </summary>
public sealed record PatternDelta(string Pattern, double? BaselineF1, double? CurrentF1)
{
    private const double Tolerance = 1e-9;

    /// <summary>Whether this pattern's F1 fell.</summary>
    public bool HasFallen =>
        BaselineF1 is { } baseline && CurrentF1 is { } current && current < baseline - Tolerance;

    public double? Delta =>
        BaselineF1 is { } baseline && CurrentF1 is { } current ? current - baseline : null;
}

/// <summary>
/// Result of comparing the current report against a baseline report. The regression gate is the micro F1 alone;
/// <see cref="FallenPatterns"/> is informational.
/// </summary>
public sealed record BaselineComparison(
    IReadOnlyList<PatternDelta> Deltas,
    double? BaselineMicroF1,
    double? CurrentMicroF1)
{
    private const double Tolerance = 1e-9;

    public bool HasRegression =>
        BaselineMicroF1 is { } baseline && CurrentMicroF1 is { } current && current < baseline - Tolerance;

    /// <summary>Patterns whose F1 fell, in report order.</summary>
    public IReadOnlyList<PatternDelta> FallenPatterns => Deltas.Where(delta => delta.HasFallen).ToList();
}

/// <summary>Compares two evaluation reports, pattern by pattern and on the micro aggregate.</summary>
public static class BaselineComparer
{
    public static BaselineComparison Compare(EvaluationReport current, EvaluationReport baseline)
    {
        var baselineByPattern = baseline.PerPattern.ToDictionary(metrics => metrics.Pattern);

        var deltas = current.PerPattern
            .Select(metrics => new PatternDelta(
                metrics.Pattern,
                baselineByPattern.GetValueOrDefault(metrics.Pattern)?.F1,
                metrics.F1))
            .ToList();

        return new BaselineComparison(deltas, baseline.Micro.F1, current.Micro.F1);
    }
}
