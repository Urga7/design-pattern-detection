using VDS.RDF;

namespace DesignPatternDetection.Detection;

/// <summary>Detects a single design pattern within an RDF model of source code.</summary>
public interface IPatternDetector
{
    /// <summary>Human-readable name of the pattern, e.g. "Singleton".</summary>
    string PatternName { get; }

    /// <summary>Returns every occurrence of the pattern found in the graph.</summary>
    IReadOnlyList<PatternMatch> Detect(IGraph graph);
}
