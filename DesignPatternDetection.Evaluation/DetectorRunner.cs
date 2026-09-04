using System.Diagnostics;
using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Verification;

namespace DesignPatternDetection.Evaluation;

/// <summary>
/// What every detector found in one unit: the set of pattern names with at least one match, the raw match-row count
/// per pattern, any detector failures, and how long the graph and each detector took. A detector failure counts as
/// "nothing detected" and never aborts the run.
/// </summary>
public sealed record UnitResult(
    EvaluationUnit Unit,
    IReadOnlySet<string> DetectedPatterns,
    IReadOnlyDictionary<string, int> MatchRows,
    IReadOnlyList<string> Errors,
    TimeSpan GraphDuration = default,
    IReadOnlyDictionary<string, TimeSpan>? DetectorDurations = null,
    VerificationSummary? Verification = null,
    IReadOnlySet<string>? StructuralPatterns = null)
{
    /// <summary>What the detectors found before LLM review.</summary>
    public IReadOnlySet<string> StructuralPatterns { get; init; } = StructuralPatterns ?? DetectedPatterns;

    /// <summary>Per-detector wall time, empty for a result built without timings.</summary>
    public IReadOnlyDictionary<string, TimeSpan> DetectorDurations { get; init; } =
        DetectorDurations ?? new Dictionary<string, TimeSpan>();

    public TimeSpan TotalDuration =>
        DetectorDurations.Values.Aggregate(GraphDuration, (total, duration) => total + duration);
}

/// <summary>
/// Runs every detector against one unit at a time, each unit in its own graph. With a <paramref name="verifier"/>,
/// the unit's matches go through the same semantic pass the detector CLI applies and a pattern counts as detected
/// only if a match survives review.
/// </summary>
public sealed class DetectorRunner(IReadOnlyList<IPatternDetector> detectors, MatchVerifier? verifier = null)
{
    public async Task<UnitResult> RunAsync(EvaluationUnit unit, CancellationToken cancellationToken = default)
    {
        var graphStart = Stopwatch.GetTimestamp();
        var source = SourceGraphBuilder.Build(unit.Files);
        var graphDuration = Stopwatch.GetElapsedTime(graphStart);

        var detections = new List<PatternDetection>();
        var errors = new List<string>();
        var durations = new Dictionary<string, TimeSpan>();

        foreach (var detector in detectors)
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                detections.Add(new PatternDetection(detector.PatternName, detector.Detect(source.Graph)));
            }
            catch (Exception exception)
            {
                errors.Add($"{unit.Name}: {detector.PatternName} detector failed: {exception.Message}");
            }
            finally
            {
                durations[detector.PatternName] = Stopwatch.GetElapsedTime(start);
            }
        }

        var scan = new ScanResult(unit.Files.Count, detections, source);
        var structural = PatternsWithMatches(scan);
        VerificationSummary? verification = null;

        if (verifier is not null)
        {
            var reviewed = await verifier.VerifyAsync(scan, cancellationToken);
            scan = reviewed.Scan;
            verification = reviewed.Summary;
        }

        var detected = PatternsWithMatches(scan);
        var matchRows = scan.Detections
            .Where(detection => detection.Matches.Count > 0)
            .ToDictionary(detection => detection.PatternName, detection => detection.Matches.Count);

        return new UnitResult(
            unit, detected, matchRows, errors, graphDuration, durations, verification, structural);
    }

    private static HashSet<string> PatternsWithMatches(ScanResult scan) =>
        scan.Detections
            .Where(detection => detection.Matches.Count > 0)
            .Select(detection => detection.PatternName)
            .ToHashSet(StringComparer.Ordinal);
}
