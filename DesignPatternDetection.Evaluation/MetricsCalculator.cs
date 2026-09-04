using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// Turns per-unit detection outcomes into an <see cref="EvaluationReport"/>. Pure arithmetic: units and detections
/// in, counts and scores out. The macro aggregate averages each metric over the patterns where it is defined; the
/// micro aggregate computes the same formulas from the summed counts and is the headline number.
/// </summary>
public static class MetricsCalculator
{
    public static EvaluationReport Compute(
        string corpus,
        string? commit,
        IReadOnlyList<string> patternNames,
        IReadOnlyList<UnitResult> results,
        int skippedDirectories,
        string? reviewModel = null)
    {
        var perPattern = patternNames
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .Select(pattern => Count(pattern, results))
            .ToList();

        var errors = results.SelectMany(result => result.Errors).ToList();

        return new EvaluationReport(
            corpus,
            commit,
            DateTimeOffset.Now,
            results.Count,
            skippedDirectories,
            Math.Round(results.Sum(result => result.TotalDuration.TotalSeconds), 1),
            perPattern,
            Macro(perPattern),
            Micro(perPattern),
            errors,
            Review(reviewModel, results),
            Corpora: null,
            Units: results.Select(Outcome).ToList());
    }

    private static UnitOutcome Outcome(UnitResult result) =>
        new(
            result.Unit.Name,
            Sorted(result.Unit.ExpectedPatterns),
            Sorted(result.StructuralPatterns),
            Sorted(result.DetectedPatterns));

    private static List<string> Sorted(IEnumerable<string> patterns) =>
        patterns.OrderBy(pattern => pattern, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Pools several corpora into one report by summing each pattern's counts and recomputing both aggregates from
    /// the totals - macro included, rather than averaging the corpora's own macro figures.
    /// </summary>
    public static EvaluationReport Combine(string corpus, IReadOnlyList<EvaluationReport> reports)
    {
        if (reports.Count == 0)
            throw new ArgumentException("Combining needs at least one report.", nameof(reports));

        var perPattern = reports
            .SelectMany(report => report.PerPattern)
            .GroupBy(metrics => metrics.Pattern, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new PatternMetrics(
                group.Key,
                group.Sum(metrics => metrics.TruePositives),
                group.Sum(metrics => metrics.FalsePositives),
                group.Sum(metrics => metrics.FalseNegatives),
                group.Sum(metrics => metrics.MatchRows)))
            .ToList();

        return new EvaluationReport(
            corpus,
            Commit: null,
            DateTimeOffset.Now,
            reports.Sum(report => report.UnitCount),
            reports.Sum(report => report.SkippedDirectories),
            Math.Round(reports.Sum(report => report.DetectionSeconds), 1),
            perPattern,
            Macro(perPattern),
            Micro(perPattern),
            reports.SelectMany(report => report.Errors).ToList(),
            CombineReview(reports),
            Corpora: null,
            Units: null);
    }

    private static ReviewMetrics? CombineReview(IReadOnlyList<EvaluationReport> reports)
    {
        var reviews = reports.Select(report => report.Review).OfType<ReviewMetrics>().ToList();
        if (reviews.Count == 0)
            return null;

        return new ReviewMetrics(
// Every distinct model, so a mixed manifest is not reported as one.
            string.Join(", ", reviews.Select(review => review.Model).Distinct(StringComparer.Ordinal)),
            reviews.Sum(review => review.Reviewed),
            reviews.Sum(review => review.Confirmed),
            reviews.Sum(review => review.Uncertain),
            reviews.Sum(review => review.Rejected),
            reviews.Sum(review => review.Dropped),
            reviews.Sum(review => review.Unreviewed),
            reviews.Sum(review => review.CacheHits),
            reviews.Sum(review => review.InputTokens),
            reviews.Sum(review => review.OutputTokens),
            Math.Round(reviews.Sum(review => review.DurationSeconds), 1),
            reviews.Select(review => review.FirstFailure).FirstOrDefault(failure => failure is not null));
    }

    /// <summary>Folds the per-unit review tallies into one corpus figure.</summary>
    private static ReviewMetrics? Review(string? model, IReadOnlyList<UnitResult> results)
    {
        if (model is null)
            return null;

        var total = results
            .Select(result => result.Verification)
            .OfType<VerificationSummary>()
            .Aggregate(new VerificationSummary(0, 0, 0, 0, 0, 0, 0),
                (running, unit) => running + unit);

        return new ReviewMetrics(
            model,
            total.Reviewed,
            total.Confirmed,
            total.Uncertain,
            total.Rejected,
            total.Dropped,
            total.Unreviewed,
            total.CacheHits,
            total.InputTokens,
            total.OutputTokens,
            Math.Round(total.Duration.TotalSeconds, 1),
            total.FirstFailure);
    }

    private static PatternMetrics Count(string pattern, IReadOnlyList<UnitResult> results)
    {
        var truePositives = 0;
        var falsePositives = 0;
        var falseNegatives = 0;
        var matchRows = 0;

        foreach (var result in results)
        {
            var expected = result.Unit.ExpectedPatterns.Contains(pattern);
            var detected = result.DetectedPatterns.Contains(pattern);

            if (expected && detected) truePositives++;
            else if (detected) falsePositives++;
            else if (expected) falseNegatives++;

            matchRows += result.MatchRows.GetValueOrDefault(pattern);
        }

        return new PatternMetrics(pattern, truePositives, falsePositives, falseNegatives, matchRows);
    }

    private static AggregateMetrics Macro(IReadOnlyList<PatternMetrics> perPattern) =>
        new(
            Mean(perPattern.Select(metrics => metrics.Precision)),
            Mean(perPattern.Select(metrics => metrics.Recall)),
            Mean(perPattern.Select(metrics => metrics.F1)));

    private static AggregateMetrics Micro(IReadOnlyList<PatternMetrics> perPattern)
    {
        var summed = new PatternMetrics(
            "micro",
            perPattern.Sum(metrics => metrics.TruePositives),
            perPattern.Sum(metrics => metrics.FalsePositives),
            perPattern.Sum(metrics => metrics.FalseNegatives),
            perPattern.Sum(metrics => metrics.MatchRows));

        return new AggregateMetrics(summed.Precision, summed.Recall, summed.F1);
    }

    private static double? Mean(IEnumerable<double?> values)
    {
        var defined = values.OfType<double>().ToList();
        return defined.Count == 0 ? null : defined.Average();
    }
}
