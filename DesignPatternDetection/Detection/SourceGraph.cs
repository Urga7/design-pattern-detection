using VDS.RDF;

namespace DesignPatternDetection.Detection;

/// <summary>The source file and the 1-based, inclusive line range.</summary>
public sealed record SourceSpan(string FilePath, int StartLine, int EndLine);

/// <summary>
/// The RDF graph of structural facts plus a side table mapping node URI fragments to the declaration that minted
/// them. Nodes for metadata-only or unresolved types have no entry.
/// </summary>
public sealed record SourceGraph(IGraph Graph, IReadOnlyDictionary<string, SourceSpan> Locations);
