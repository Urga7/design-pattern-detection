using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class ReviewImpactAnalysisTests
{
    [Fact]
    public void Dropping_only_false_positives_raises_precision_and_leaves_recall()
    {
        var impact = Analyse(
            Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Observer"], detected: ["Strategy"]),
            Unit("B", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"]));

        Assert.Equal((2, 2, 0), Counts(impact.Structural));
        Assert.Equal((2, 0, 0), Counts(impact.Semantic));

        var precision = Delta(impact, "precision");
        Assert.Equal(0.5, precision.Structural);
        Assert.Equal(1.0, precision.Semantic);
        Assert.Equal(0.5, precision.Delta);

        Assert.Equal(0.0, Delta(impact, "recall").Delta);
    }

    [Fact]
    public void Dropping_a_true_positive_lowers_recall()
    {
        var impact = Analyse(
            Unit("A", labeled: ["Strategy"], structural: ["Strategy"], detected: []),
            Unit("B", labeled: ["Strategy"], structural: ["Strategy"], detected: ["Strategy"]));

        Assert.Equal(1.0, Delta(impact, "recall").Structural);
        Assert.Equal(0.5, Delta(impact, "recall").Semantic);
        Assert.Equal(-0.5, Delta(impact, "recall").Delta);
    }

    [Fact]
    public void A_delta_every_unit_agrees_on_has_an_interval_of_zero_width()
    {
        var impact = Analyse(Enumerable
            .Range(0, 8)
            .Select(index => Unit($"U{index}", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"]))
            .ToArray());

        var precision = Delta(impact, "precision");
        Assert.Equal(0.5, precision.Delta);
        Assert.Equal(0.5, precision.Lower!.Value, 12);
        Assert.Equal(0.5, precision.Upper!.Value, 12);
    }

    [Fact]
    public void An_interval_widens_when_the_units_disagree()
    {
        var mixed = Analyse(
            Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Observer"], detected: ["Strategy"]),
            Unit("B", labeled: ["Observer"], structural: ["Observer"], detected: []),
            Unit("C", labeled: [], structural: ["Proxy", "State"], detected: []),
            Unit("D", labeled: ["Visitor"], structural: ["Visitor"], detected: ["Visitor"]));

        var f1 = Delta(mixed, "F1");
        Assert.True(f1.Upper - f1.Lower > 0, "units that disagree must leave a non-degenerate interval");
        Assert.True(f1.Lower <= f1.Delta && f1.Delta <= f1.Upper, "the interval must contain the point estimate");
    }

    [Fact]
    public void The_same_units_always_give_the_same_interval()
    {
        UnitOutcome[] units =
        [
            Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Observer"], detected: ["Strategy"]),
            Unit("B", labeled: ["Observer"], structural: ["Observer", "Proxy", "State"], detected: ["Observer"]),
            Unit("C", labeled: [], structural: ["Proxy"], detected: []),
            Unit("D", labeled: ["Visitor"], structural: ["Visitor"], detected: ["Visitor"])
        ];

        var first = Delta(ReviewImpactAnalysis.Analyse("scope", units), "F1");
        var second = Delta(ReviewImpactAnalysis.Analyse("scope", units.Reverse().ToArray()), "F1");

        Assert.Equal(first.Lower!.Value, second.Lower!.Value, 12);
        Assert.Equal(first.Upper!.Value, second.Upper!.Value, 12);
    }

    [Fact]
    public void A_single_unit_leaves_the_interval_undefined()
    {
        var impact = Analyse(Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"]));

        var precision = Delta(impact, "precision");
        Assert.Equal(0.5, precision.Delta);
        Assert.Null(precision.Lower);
        Assert.Null(precision.Upper);
    }

    [Fact]
    public void A_unit_whose_removal_leaves_no_prediction_contributes_no_pseudo_value()
    {
        var impact = Analyse(
            Unit("Predicts", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"]),
            Unit("Silent", labeled: [], structural: [], detected: []));

        var precision = Delta(impact, "precision");
        Assert.Equal(0.5, precision.Delta);
        Assert.Null(precision.Lower);
    }

    [Fact]
    public void Pooling_sums_both_stages_over_the_units()
    {
        var (structural, semantic) = ReviewImpactAnalysis.Pooled([
            Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"]),
            Unit("B", labeled: ["Observer"], structural: ["State"], detected: [])
        ]);

        Assert.Equal((1, 2, 1), Counts(structural));
        Assert.Equal((1, 0, 1), Counts(semantic));
    }

    [Fact]
    public void A_pooled_report_takes_its_units_from_the_corpora_it_pooled()
    {
        var report = Pooled(
            ("MediatR", Unit("M", labeled: ["Strategy"], structural: ["Strategy"], detected: ["Strategy"])),
            ("NLog", Unit("N", labeled: [], structural: ["Proxy"], detected: [])));

        Assert.Equal(["M", "N"], ReviewImpactAnalysis.UnitsOf(report).Select(unit => unit.Unit));
    }

    [Fact]
    public void Analysing_a_scope_without_units_fails_loudly()
    {
        var failure = Assert.Throws<ArgumentException>(() => ReviewImpactAnalysis.Analyse("empty", []));
        Assert.Contains("empty", failure.Message);
    }

    [Fact]
    public void The_written_comparison_reports_precision_beside_f1()
    {
        var report = Pooled(
            ("NLog", Unit("A", labeled: ["Strategy"], structural: ["Strategy", "Proxy"], detected: ["Strategy"])),
            ("Serilog", Unit("B", labeled: ["Observer"], structural: ["Observer", "State"], detected: ["Observer"])));

        var output = new StringWriter();
        ConsoleAnalysisWriter.Write(output, report);
        var text = output.ToString();

        Assert.Contains("precision", text);
        Assert.Contains("NLog", text);
        Assert.Contains("pooled", text);
    }

    [Fact]
    public void A_report_without_a_review_stage_cannot_be_compared()
    {
        var report = Pooled(("NLog", Unit("A", labeled: [], structural: [], detected: []))) with { Review = null };

        var failure = Assert.Throws<InvalidOperationException>(() => ConsoleAnalysisWriter.Write(new StringWriter(), report));
        Assert.Contains("--verify", failure.Message);
    }

    [Fact]
    public void A_report_predating_per_unit_outcomes_cannot_be_compared()
    {
        var report = Pooled(("NLog", Unit("A", labeled: [], structural: ["Proxy"], detected: [])));
        var stripped = report with { Corpora = [report.Corpora![0] with { Units = null }] };

        var failure = Assert.Throws<InvalidOperationException>(() => ConsoleAnalysisWriter.Write(new StringWriter(), stripped));
        Assert.Contains("--analyze", failure.Message);
    }

    private static ReviewImpact Analyse(params UnitOutcome[] units) =>
        ReviewImpactAnalysis.Analyse("scope", units);

    private static DeltaEstimate Delta(ReviewImpact impact, string metric) =>
        impact.Deltas.Single(delta => delta.Metric == metric);

    private static (int, int, int) Counts(PatternMetrics metrics) =>
        (metrics.TruePositives, metrics.FalsePositives, metrics.FalseNegatives);

    private static UnitOutcome Unit(string name, string[] labeled, string[] structural, string[] detected) =>
        new(name, labeled, structural, detected);

    private static EvaluationReport Pooled(params (string Name, UnitOutcome Unit)[] corpora) =>
        Empty("all corpora") with
        {
            Corpora = corpora
                .Select(corpus => Empty(corpus.Name) with { UnitCount = 1, Units = [corpus.Unit] })
                .ToList()
        };

    private static EvaluationReport Empty(string corpus) =>
        new(
            corpus,
            Commit: null,
            DateTimeOffset.UnixEpoch,
            UnitCount: 0,
            SkippedDirectories: 0,
            DetectionSeconds: 0,
            PerPattern: [],
            Macro: new AggregateMetrics(null, null, null),
            Micro: new AggregateMetrics(null, null, null),
            Errors: [],
            Review: new ReviewMetrics("test-model", 2, 1, 0, 1, 1, 0, 0, 0, 0, 0, null));
}
