namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// A Facade fronts a complex subsystem by keeping references to several of its classes
/// and exposing its own simple entry points that coordinate them.
/// </summary>
/// <remarks>
/// The defining trait is the freestanding aggregate: the facade holds references to two or more distinct
/// source-declared classes while extending nothing at all. Wrapping without conforming to any abstraction is what
/// separates it from Adapter, Decorator and Proxy - each of those extends the very Target its wrapper stands in for -
/// and from a Bridge Abstraction, which wraps a single hierarchy head, not a family of subsystem classes.
/// </remarks>
public sealed class FacadePatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Facade";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["facade"] = "FacadeFacade",
        ["subsystemA"] = "FacadeSubsystemClass",
        ["subsystemB"] = "FacadeSubsystemClass"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?facade ?subsystemA ?subsystemB WHERE {
            ?facade rdf:type src:Class .

            ?facade src:hasField ?refA .
            ?refA src:returnsType ?subsystemA .
            ?subsystemA rdf:type src:Class .
            ?facade src:hasMethod ?opA .
            ?opA src:delegatesTo ?refA .

            ?facade src:hasField ?refB .
            ?refB src:returnsType ?subsystemB .
            ?subsystemB rdf:type src:Class .
            ?facade src:hasMethod ?opB .
            ?opB src:delegatesTo ?refB .

            FILTER (STR(?subsystemA) < STR(?subsystemB))
            FILTER (?subsystemA != ?facade && ?subsystemB != ?facade)

            FILTER NOT EXISTS { ?facade src:extends ?base }
        }
        """;
}
