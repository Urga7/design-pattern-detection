using System.Globalization;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Renders an <see cref="EvaluationReport"/> as an aligned console table, one row per pattern, with macro/micro
/// summary lines. Undefined metrics render as <c>-</c>. With a baseline comparison a delta column is added.
/// </summary>
public static class ConsoleReportWriter
{
    /// <summary>A unit at or above this duration also reports its slowest detectors.</summary>
    private static readonly TimeSpan SlowUnit = TimeSpan.FromSeconds(10);

    /// <summary>The shortest detector duration worth naming.</summary>
    private static readonly TimeSpan SlowDetector = TimeSpan.FromSeconds(1);

    /// <summary>
    /// One line per unit as it finishes: its name, file count, duration and detected patterns, plus any errors, the
    /// review tally when review changed or missed something, and the slowest detectors of a slow unit.
    /// </summary>
    public static void WriteUnitProgress(TextWriter output, UnitResult result)
    {
        output.WriteLine(
            $"  {result.Unit.Name} ({result.Unit.Files.Count} file(s), {Seconds(result.TotalDuration)}s)"
            + (result.DetectedPatterns.Count > 0
                ? $": {string.Join(", ", result.DetectedPatterns.OrderBy(name => name, StringComparer.Ordinal))}"
                : ""));

        foreach (var error in result.Errors)
            output.WriteLine($"      {error}");

        if (result.Verification is { } verification && (verification.Dropped > 0 || verification.Unreviewed > 0))
            output.WriteLine($"      review: {verification}");

        if (result.TotalDuration < SlowUnit)
            return;

        var worst = result.DetectorDurations
            .Where(entry => entry.Value >= SlowDetector)
            .OrderByDescending(entry => entry.Value)
            .Take(3)
            .Select(entry => $"{entry.Key} {Seconds(entry.Value)}s");

        output.WriteLine($"      graph {Seconds(result.GraphDuration)}s; slowest: {string.Join(", ", worst)}");
    }

    private static string Seconds(TimeSpan duration) => Seconds(duration.TotalSeconds);

    private static string Seconds(double seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Tokens(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    public static void Write(TextWriter output, EvaluationReport report, BaselineComparison? comparison = null)
    {
        output.WriteLine($"Corpus: {report.Corpus}" + (report.Commit is null ? "" : $" @ {report.Commit}"));
        output.WriteLine($"Units: {report.UnitCount}"
                         + (report.SkippedDirectories > 0
                             ? $" (skipped {report.SkippedDirectories} unlabeled source location(s))"
                             : ""));
        output.WriteLine();

        var nameWidth = Math.Max("Pattern".Length, report.PerPattern.Max(metrics => metrics.Pattern.Length));
        var deltaByPattern = comparison?.Deltas.ToDictionary(delta => delta.Pattern);

        output.WriteLine(
            $"{"Pattern".PadRight(nameWidth)}  {"TP",3} {"FP",3} {"FN",3}  {"Prec",6} {"Recall",6} {"F1",6}  {"Rows",4}"
            + (comparison is null ? "" : $"  {"dF1",7}"));

        foreach (var metrics in report.PerPattern)
        {
            var line =
                $"{metrics.Pattern.PadRight(nameWidth)}  " +
                $"{metrics.TruePositives,3} {metrics.FalsePositives,3} {metrics.FalseNegatives,3}  " +
                $"{Score(metrics.Precision),6} {Score(metrics.Recall),6} {Score(metrics.F1),6}  " +
                $"{metrics.MatchRows,4}";

            if (deltaByPattern is not null)
                line += $"  {Delta(deltaByPattern.GetValueOrDefault(metrics.Pattern)),7}";

            output.WriteLine(line);
        }

        output.WriteLine();
        output.WriteLine(Summary("Macro", report.Macro, nameWidth));
        output.WriteLine(Summary("Micro", report.Micro, nameWidth)
                         + (comparison is null ? "" : $"  {Delta(MicroDelta(comparison)),7}"));

        output.WriteLine();
        output.WriteLine($"Detection: {Seconds(report.DetectionSeconds)}s over {report.UnitCount} unit(s).");

        if (report.Review is { } review)
        {
            output.WriteLine(
                $"Review: {review.Reviewed} adjudicated - {review.Confirmed} confirmed, {review.Uncertain} uncertain, "
                + $"{review.Rejected} rejected, {review.CacheHits} cached, {review.Unreviewed} unreviewed.");
            output.WriteLine(
                $"Cost: {Tokens(review.InputTokens)} input and {Tokens(review.OutputTokens)} output token(s) "
                + $"in {Seconds(review.DurationSeconds)}s.");
        }

        foreach (var error in report.Errors)
            output.WriteLine($"Error: {error}");

        if (comparison is null)
            return;

        output.WriteLine();

        if (comparison.FallenPatterns.Count > 0)
            output.WriteLine($"Fell against the baseline: {string.Join(", ", comparison.FallenPatterns.Select(Fallen))}.");

        output.WriteLine(comparison.HasRegression
            ? "Micro F1 regressed against the baseline."
            : "No regression: micro F1 held or improved.");
    }

    /// <summary>One corpus's headline macro and micro F1.</summary>
    public static void WriteCorpusScores(TextWriter output, EvaluationReport report) =>
        output.WriteLine($"    macro F1 {Score(report.Macro.F1)}, micro F1 {Score(report.Micro.F1)}\n");

    /// <summary>The per-corpus scoreboard of a combined run.</summary>
    public static void WriteCorpusSummary(TextWriter output, IReadOnlyList<EvaluationReport> reports)
    {
        var nameWidth = Math.Max("Corpus".Length, reports.Max(report => report.Corpus.Length));

        output.WriteLine($"{"Corpus".PadRight(nameWidth)}  {"Units",5}  {"Macro F1",8} {"Micro F1",8}  {"Seconds",8}");

        foreach (var report in reports)
            output.WriteLine(
                $"{report.Corpus.PadRight(nameWidth)}  {report.UnitCount,5}  " +
                $"{Score(report.Macro.F1),8} {Score(report.Micro.F1),8}  " +
                $"{Seconds(report.DetectionSeconds),8}");
    }

    private static string Fallen(PatternDelta delta) =>
        $"{delta.Pattern} {Score(delta.BaselineF1)} -> {Score(delta.CurrentF1)}";

    private static string Summary(string label, AggregateMetrics metrics, int nameWidth) =>
        $"{label.PadRight(nameWidth)}  {"",3} {"",3} {"",3}  " +
        $"{Score(metrics.Precision),6} {Score(metrics.Recall),6} {Score(metrics.F1),6}  {"",4}";

    private static string Score(double? value) =>
        value is { } defined ? defined.ToString("0.000", CultureInfo.InvariantCulture) : "-";

    private static string Delta(PatternDelta? delta) =>
        delta switch
        {
            null => "",
            { BaselineF1: null, CurrentF1: not null } => "new",
            { Delta: { } value } => value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),
            _ => ""
        };

    private static PatternDelta MicroDelta(BaselineComparison comparison) =>
        new("micro", comparison.BaselineMicroF1, comparison.CurrentMicroF1);
}
