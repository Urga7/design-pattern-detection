using System.Globalization;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Renders the structural-versus-reviewed comparison: one row per corpus with both stages' precision, recall and F1,
/// then the pooled effect of review with a 95% interval on each change.
/// </summary>
public static class ConsoleAnalysisWriter
{
    public static void Write(TextWriter output, EvaluationReport report)
    {
        if (report.Review is null)
            throw new InvalidOperationException(
                "This report has no review stage, so there is nothing to compare it against. " +
                "Analyse a report from a --verify run.");

        var units = ReviewImpactAnalysis.UnitsOf(report);

        if (units.Count == 0)
            throw new InvalidOperationException(
                "This report records no per-unit outcomes, so the two stages cannot be paired. " +
                "It predates --analyze; re-run the evaluation to produce one that can be analysed.");

        output.WriteLine($"Reviewer: {report.Review.Model}");
        output.WriteLine(
            $"Adjudicated: {report.Review.Reviewed} candidate match(es) - {report.Review.Confirmed} confirmed, "
            + $"{report.Review.Uncertain} uncertain, {report.Review.Rejected} rejected, "
            + $"{report.Review.Unreviewed} unreviewed.");
        output.WriteLine();

        WriteCorpusTable(output, report, units);
        output.WriteLine();
        WriteImpact(output, ReviewImpactAnalysis.Analyse(report.Corpus, units));
    }

    private static void WriteCorpusTable(TextWriter output, EvaluationReport report, IReadOnlyList<UnitOutcome> pooled)
    {
        var rows = report.Corpora is { } corpora
            ? corpora.Select(corpus => (Name: corpus.Corpus, Units: corpus.Units ?? (IReadOnlyList<UnitOutcome>)[]))
                .ToList()
            : [];

        var width = Math.Max(22, rows.Select(row => row.Name.Length).Append(0).Max() + 1);

        output.WriteLine($"{new string(' ', width + 7)}{"structural",-19}{"reviewed",-19}{"delta",-21}");
        output.WriteLine($"{"Corpus".PadRight(width)}{"Units",6} {Head()} {Head()} {"dP",7}{"dR",7}{"dF1",7}");
        output.WriteLine(new string('-', width + 7 + 19 + 19 + 21));

        foreach (var (name, units) in rows)
            WriteRow(output, name, units, width);

        WriteRow(output, rows.Count > 0 ? "pooled" : report.Corpus, pooled, width);

        return;

        static string Head() => $"{"P",6}{"R",6}{"F1",6}";
    }

    private static void WriteRow(TextWriter output, string name, IReadOnlyList<UnitOutcome> units, int width)
    {
        if (units.Count == 0)
            return;

        var (structural, semantic) = ReviewImpactAnalysis.Pooled(units);

        output.WriteLine(
            name.PadRight(width)
            + $"{units.Count,6} "
            + Scores(structural) + " "
            + Scores(semantic) + " "
            + $"{Delta(semantic.Precision - structural.Precision),7}"
            + $"{Delta(semantic.Recall - structural.Recall),7}"
            + $"{Delta(semantic.F1 - structural.F1),7}");

        return;

        static string Scores(PatternMetrics metrics) =>
            $"{Metric(metrics.Precision),6}{Metric(metrics.Recall),6}{Metric(metrics.F1),6}";
    }

    private static void WriteImpact(TextWriter output, ReviewImpact impact)
    {
        output.WriteLine($"Effect of review over {impact.Units} units, 95% jackknife interval on each change:");
        output.WriteLine();
        output.WriteLine($"  {"Metric",-10}{"structural",12}{"reviewed",12}{"delta",10}   95% CI");

        foreach (var delta in impact.Deltas)
            output.WriteLine(
                $"  {delta.Metric,-10}{Metric(delta.Structural),12}{Metric(delta.Semantic),12}"
                + $"{Delta(delta.Delta),10}   [{Delta(delta.Lower)}, {Delta(delta.Upper)}]");

        output.WriteLine();
        output.WriteLine(
            $"  Counts: {impact.Structural.TruePositives} TP, {impact.Structural.FalsePositives} FP, "
            + $"{impact.Structural.FalseNegatives} FN  ->  {impact.Semantic.TruePositives} TP, "
            + $"{impact.Semantic.FalsePositives} FP, {impact.Semantic.FalseNegatives} FN");
    }

    private static string Metric(double? value) =>
        value?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-";

    private static string Delta(double? value) =>
        value?.ToString("+0.000;-0.000;+0.000", CultureInfo.InvariantCulture) ?? "-";
}
