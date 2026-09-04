using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class MetricsCalculatorTests
{
    [Fact]
    public void Perfect_detection_scores_one_everywhere()
    {
        var report = Compute(["Strategy"], Result("Unit", expected: ["Strategy"], detected: ["Strategy"]));

        var metrics = Assert.Single(report.PerPattern);
        Assert.Equal((1, 0, 0), (metrics.TruePositives, metrics.FalsePositives, metrics.FalseNegatives));
        Assert.Equal(1.0, metrics.Precision);
        Assert.Equal(1.0, metrics.Recall);
        Assert.Equal(1.0, metrics.F1);
        Assert.Equal(1.0, report.Micro.F1);
    }

    [Fact]
    public void Detection_on_a_unit_not_labeled_with_the_pattern_is_a_false_positive()
    {
        var report = Compute(
            ["Strategy"],
            Result("Negative", expected: [], detected: ["Strategy"]));

        var metrics = Assert.Single(report.PerPattern);
        Assert.Equal((0, 1, 0), (metrics.TruePositives, metrics.FalsePositives, metrics.FalseNegatives));
        Assert.Equal(0.0, metrics.Precision);
    }

    [Fact]
    public void A_missed_labeled_unit_is_a_false_negative()
    {
        var report = Compute(["Strategy"], Result("Unit", expected: ["Strategy"], detected: []));

        var metrics = Assert.Single(report.PerPattern);
        Assert.Equal((0, 0, 1), (metrics.TruePositives, metrics.FalsePositives, metrics.FalseNegatives));
        Assert.Equal(0.0, metrics.Recall);
        Assert.Equal(0.0, metrics.F1);
    }

    [Fact]
    public void A_pattern_that_never_fired_has_undefined_precision_excluded_from_the_macro()
    {
        var report = Compute(
            ["Observer", "Strategy"],
            Result("A", expected: ["Observer"], detected: ["Observer"]),
            Result("B", expected: ["Strategy"], detected: []));

        var strategy = report.PerPattern.Single(metrics => metrics.Pattern == "Strategy");
        Assert.Null(strategy.Precision);
        Assert.Equal(0.0, strategy.Recall);

        // Only Observer's precision is defined, so the macro is 1.0, not 0.5.
        Assert.Equal(1.0, report.Macro.Precision);
        Assert.Equal(0.5, report.Macro.Recall);
    }

    [Fact]
    public void A_pattern_absent_from_the_corpus_has_undefined_recall_and_f1()
    {
        var report = Compute(
            ["Observer", "Strategy"],
            Result("A", expected: ["Observer"], detected: ["Observer"]));

        var strategy = report.PerPattern.Single(metrics => metrics.Pattern == "Strategy");
        Assert.Null(strategy.Recall);
        Assert.Null(strategy.F1);
        Assert.Equal(1.0, report.Macro.F1);
    }

    [Fact]
    public void A_multi_pattern_unit_scores_each_expected_pattern_independently()
    {
        var report = Compute(
            ["Builder", "Decorator"],
            Result("Unit", expected: ["Builder", "Decorator"], detected: ["Builder"]));

        Assert.Equal(1.0, report.PerPattern.Single(metrics => metrics.Pattern == "Builder").F1);
        Assert.Equal(0.0, report.PerPattern.Single(metrics => metrics.Pattern == "Decorator").F1);
    }

    [Fact]
    public void The_micro_aggregate_uses_summed_counts()
    {
        // Observer: TP=1 FP=1; Strategy: TP=1 FN=1 -> micro P=2/3, R=2/3,
        // F1 = 2*2 / (2*2 + 1 + 1) = 2/3.
        var report = Compute(
            ["Observer", "Strategy"],
            Result("A", expected: ["Observer", "Strategy"], detected: ["Observer"]),
            Result("B", expected: ["Strategy"], detected: ["Observer", "Strategy"]));

        Assert.Equal(2.0 / 3, report.Micro.Precision!.Value, precision: 10);
        Assert.Equal(2.0 / 3, report.Micro.Recall!.Value, precision: 10);
        Assert.Equal(2.0 / 3, report.Micro.F1!.Value, precision: 10);
    }

    [Fact]
    public void An_empty_corpus_yields_undefined_aggregates()
    {
        var report = Compute(["Strategy"]);

        Assert.Equal(0, report.UnitCount);
        Assert.Null(report.Macro.F1);
        Assert.Null(report.Micro.F1);
    }

    [Fact]
    public void Match_rows_are_summed_across_units_and_detector_errors_are_collected()
    {
        var noisy = new UnitResult(
            new EvaluationUnit("A", [], new HashSet<string> { "Strategy" }),
            new HashSet<string> { "Strategy" },
            new Dictionary<string, int> { ["Strategy"] = 3 },
            ["A: Strategy detector failed: boom"]);

        var report = Compute(["Strategy"], noisy, Result("B", expected: [], detected: ["Strategy"]));

        Assert.Equal(4, Assert.Single(report.PerPattern).MatchRows);
        Assert.Equal("A: Strategy detector failed: boom", Assert.Single(report.Errors));
    }

    [Fact]
    public void Combining_corpora_sums_each_patterns_counts()
    {
        // Pooling corpora is the same arithmetic as pooling units, because a
        // PatternMetrics already counts units rather than matches.
        var first = Compute(
            ["Composite", "Decorator"],
            Result("A", expected: ["Composite"], detected: ["Composite"]),
            Result("B", expected: ["Decorator"], detected: []));

        var second = Compute(
            ["Composite", "Decorator"],
            Result("C", expected: [], detected: ["Composite"]),
            Result("D", expected: ["Decorator"], detected: ["Decorator"]));

        var pooled = MetricsCalculator.Combine("all", [first, second]);

        Assert.Equal(4, pooled.UnitCount);

        var composite = pooled.PerPattern.Single(metrics => metrics.Pattern == "Composite");
        Assert.Equal((1, 1, 0), (composite.TruePositives, composite.FalsePositives, composite.FalseNegatives));

        var decorator = pooled.PerPattern.Single(metrics => metrics.Pattern == "Decorator");
        Assert.Equal((1, 0, 1), (decorator.TruePositives, decorator.FalsePositives, decorator.FalseNegatives));

        // Micro over the pooled counts: 2 TP, 1 FP, 1 FN.
        Assert.Equal(2.0 * 2 / (2 * 2 + 1 + 1), pooled.Micro.F1);
    }

    [Fact]
    public void Combining_recomputes_macro_from_pooled_counts_rather_than_averaging_corpus_macros()
    {
        // Each corpus on its own reports macro F1 1.000, because the pattern it
        // never fired on is undefined there and excluded from the mean. Pooled,
        // both patterns are defined and one of them is half right - so a correct
        // combined macro must be below either corpus's own.
        var first = Compute(
            ["Composite", "Decorator"],
            Result("A", expected: ["Composite"], detected: ["Composite"]));

        var second = Compute(
            ["Composite", "Decorator"],
            Result("B", expected: [], detected: ["Decorator"]));

        var pooled = MetricsCalculator.Combine("all", [first, second]);

        Assert.Equal(1.0, first.Macro.F1);
        Assert.Equal(0.0, second.Macro.F1);

        // Composite 1/0/0 -> 1.000, Decorator 0/1/0 -> 0.000.
        Assert.Equal(0.5, pooled.Macro.F1);
    }

    [Fact]
    public void Combining_needs_at_least_one_report()
    {
        Assert.Throws<ArgumentException>(() => MetricsCalculator.Combine("all", []));
    }

    private static EvaluationReport Compute(string[] patterns, params UnitResult[] results) =>
        MetricsCalculator.Compute("test", commit: null, patterns, results, skippedDirectories: 0);

    private static UnitResult Result(string name, string[] expected, string[] detected) =>
        new(
            new EvaluationUnit(name, [], expected.ToHashSet()),
            detected.ToHashSet(),
            detected.ToDictionary(pattern => pattern, _ => 1),
            []);
}
