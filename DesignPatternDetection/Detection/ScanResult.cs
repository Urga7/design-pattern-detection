namespace DesignPatternDetection.Detection;

/// <summary>One detector's matches over a scan.</summary>
public sealed record PatternDetection(string PatternName, IReadOnlyList<PatternMatch> Matches);

/// <summary>
/// Everything a scan produced: one <see cref="PatternDetection"/> per detector, the <see cref="SourceGraph"/> it ran
/// over, and how long the graph and each detector took.
/// </summary>
public sealed record ScanResult(
    int FileCount,
    IReadOnlyList<PatternDetection> Detections,
    SourceGraph Source,
    TimeSpan GraphDuration = default,
    IReadOnlyDictionary<string, TimeSpan>? DetectorDurations = null)
{
    public IReadOnlyDictionary<string, TimeSpan> DetectorDurations { get; init; } =
        DetectorDurations ?? new Dictionary<string, TimeSpan>();

    public IReadOnlyDictionary<string, SourceSpan> Locations => Source.Locations;
}
