using System.Diagnostics;
using System.Globalization;

namespace DesignPatternDetection.Detection;

/// <summary>
/// Builds the source-code graph once and runs every available <see cref="IPatternDetector"/> against it. Detectors
/// are discovered by reflection.
/// </summary>
public sealed class PatternDetectionEngine(IEnumerable<IPatternDetector> detectors)
{
    private readonly List<IPatternDetector> _detectors = detectors.ToList();

    public PatternDetectionEngine() : this(DiscoverDetectors()) { }

    public IReadOnlyList<IPatternDetector> Detectors => _detectors;

    /// <summary>
    /// Builds the graph once and runs every detector, returning the matches together with the provenance table that
    /// resolves them to source spans. A detector that throws contributes no matches.
    /// </summary>
    public ScanResult Scan(IEnumerable<string> filePaths)
    {
        var paths = filePaths.ToList();

        var graphStart = Stopwatch.GetTimestamp();
        var source = SourceGraphBuilder.Build(paths);
        var graphDuration = Stopwatch.GetElapsedTime(graphStart);

        var detections = new List<PatternDetection>();
        var durations = new Dictionary<string, TimeSpan>();
        foreach (var detector in _detectors)
        {
            Console.WriteLine($"Running {detector.PatternName} detector...");
            var start = Stopwatch.GetTimestamp();
            try
            {
                detections.Add(new PatternDetection(detector.PatternName, detector.Detect(source.Graph)));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Warning: {detector.PatternName} detector failed: {exception.Message}");
                detections.Add(new PatternDetection(detector.PatternName, []));
            }
            finally
            {
                var duration = Stopwatch.GetElapsedTime(start);
                durations[detector.PatternName] = duration;
                Console.WriteLine($"Finished {detector.PatternName} detector in {Seconds(duration)}s.");
            }
        }

        return new ScanResult(paths.Count, detections, source, graphDuration, durations);
    }

    /// <summary>Writes a scan to <paramref name="output"/>, the console by default.</summary>
    public void Report(ScanResult result, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;

        writer.WriteLine($"Scanned {result.FileCount} file(s) with {_detectors.Count} detector(s).\n");
        foreach (var detection in result.Detections)
            Report(writer, detection);

        ReportTimings(writer, result);
    }

    /// <summary>Writes the graph and detector timings, naming the three slowest detectors.</summary>
    private static void ReportTimings(TextWriter writer, ScanResult result)
    {
        if (result.DetectorDurations.Count == 0)
            return;

        var detectorTotal = result.DetectorDurations.Values.Aggregate(TimeSpan.Zero, (sum, each) => sum + each);
        var slowest = result.DetectorDurations
            .OrderByDescending(entry => entry.Value)
            .Take(3)
            .Select(entry => $"{entry.Key} {Seconds(entry.Value)}s");

        writer.WriteLine(
            $"Timings: graph {Seconds(result.GraphDuration)}s, detectors {Seconds(detectorTotal)}s " +
            $"(total {Seconds(result.GraphDuration + detectorTotal)}s). Slowest: {string.Join(", ", slowest)}.");
    }

    private static string Seconds(TimeSpan duration) =>
        duration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Report(TextWriter writer, PatternDetection detection)
    {
        writer.WriteLine(detection.PatternName);

        if (detection.Matches.Count == 0)
        {
            writer.WriteLine($"    No instances of the {detection.PatternName} pattern were found.\n");
            return;
        }

        foreach (var match in detection.Matches)
            writer.WriteLine($"    Match: {match}{Verdict(match)}");

        writer.WriteLine();
    }

    /// <summary>The reviewer's ruling, or an empty string on an unreviewed match.</summary>
    private static string Verdict(PatternMatch match) => match.Verdict is { } verdict
        ? $" ({verdict.Outcome.ToString().ToLowerInvariant()}: {verdict.Rationale})"
        : "";

    /// <summary>
    /// Every concrete <see cref="IPatternDetector"/> in the assembly that declares the interface, ordered by pattern
    /// name.
    /// </summary>
    public static List<IPatternDetector> DiscoverDetectors() => typeof(IPatternDetector).Assembly
        .GetTypes()
        .Where(type => typeof(IPatternDetector).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
        .Select(type => (IPatternDetector)Activator.CreateInstance(type)!)
        .OrderBy(detector => detector.PatternName)
        .ToList();
}
