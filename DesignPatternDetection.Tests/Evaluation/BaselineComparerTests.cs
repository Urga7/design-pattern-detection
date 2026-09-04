using DesignPatternDetection.Evaluation;

namespace DesignPatternDetection.Tests.Evaluation;

public class BaselineComparerTests
{
    [Fact]
    public void A_micro_f1_decrease_is_the_regression()
    {
        var metrics = Metrics("Strategy", truePositives: 1, falsePositives: 0, falseNegatives: 0);

        Assert.True(BaselineComparer.Compare(Report(microF1: 0.8, metrics), Report(microF1: 0.9, metrics))
            .HasRegression);
        Assert.False(BaselineComparer.Compare(Report(microF1: 0.9, metrics), Report(microF1: 0.8, metrics))
            .HasRegression);
        Assert.False(BaselineComparer.Compare(Report(microF1: 0.9, metrics), Report(microF1: 0.9, metrics))
            .HasRegression);
    }

    /// <summary>
    /// Widening a detector trades one pattern's precision for the corpus's recall, so a pattern can fall while the
    /// corpus improves. That is not a regression.
    /// </summary>
    [Fact]
    public void A_pattern_falling_while_the_corpus_improves_is_reported_but_is_not_a_regression()
    {
        var baseline = Report(
            microF1: 0.6,
            Metrics("Decorator", truePositives: 2, falsePositives: 0, falseNegatives: 0),
            Metrics("Strategy", truePositives: 0, falsePositives: 0, falseNegatives: 2));

        var current = Report(
            microF1: 0.8,
            Metrics("Decorator", truePositives: 1, falsePositives: 1, falseNegatives: 1),
            Metrics("Strategy", truePositives: 2, falsePositives: 0, falseNegatives: 0));

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.False(comparison.HasRegression);
        var fallen = Assert.Single(comparison.FallenPatterns);
        Assert.Equal("Decorator", fallen.Pattern);
        Assert.Equal(-0.5, fallen.Delta!.Value, 9);
    }

    [Fact]
    public void A_pattern_absent_from_the_baseline_is_new_and_never_counted_as_fallen()
    {
        var baseline = Report(Metrics("Strategy", truePositives: 1, falsePositives: 0, falseNegatives: 0));
        var current = Report(
            Metrics("Strategy", truePositives: 1, falsePositives: 0, falseNegatives: 0),
            Metrics("Observer", truePositives: 0, falsePositives: 0, falseNegatives: 3));

        var comparison = BaselineComparer.Compare(current, baseline);

        var observer = comparison.Deltas.Single(delta => delta.Pattern == "Observer");
        Assert.Null(observer.BaselineF1);
        Assert.False(observer.HasFallen);
        Assert.Empty(comparison.FallenPatterns);
    }

    private static PatternMetrics Metrics(
        string pattern, int truePositives, int falsePositives, int falseNegatives) =>
        new(pattern, truePositives, falsePositives, falseNegatives, MatchRows: truePositives);

    private static EvaluationReport Report(params PatternMetrics[] perPattern) =>
        Report(microF1: null, perPattern);

    private static EvaluationReport Report(double? microF1, params PatternMetrics[] perPattern) =>
        new(
            "test",
            Commit: null,
            DateTimeOffset.Now,
            UnitCount: perPattern.Length,
            SkippedDirectories: 0,
            DetectionSeconds: 0,
            perPattern,
            new AggregateMetrics(null, null, null),
            new AggregateMetrics(null, null, microF1),
            Errors: []);
}
