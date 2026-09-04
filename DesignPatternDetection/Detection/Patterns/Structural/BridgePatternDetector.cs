namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// An Abstraction delegates its work to a separate Implementor hierarchy through a
/// wrapped reference, and both sides vary independently - a RefinedAbstraction extends the Abstraction while
/// ConcreteImplementors extend the Implementor.
/// </summary>
/// <remarks>
/// The defining trait is the pair of parallel hierarchies joined by composition: the wrapping side must have its own
/// subclass and the wrapped side its own. A lone class delegating to an abstract hierarchy is Strategy-like
/// composition, and wrapping a class with no hierarchy of its own is Adapter-shaped - in a Bridge, both sides vary. A
/// Decorator is kept out by requiring the two hierarchies to stay unrelated, since its wrapper extends what it wraps.
/// </remarks>
public sealed class BridgePatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Bridge";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["abstraction"] = "BridgeAbstraction",
        ["refinedAbstraction"] = "BridgeRefinedAbstraction",
        ["implementor"] = "BridgeImplementation",
        ["concreteImplementor"] = "BridgeConcreteImplementation"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?abstraction ?refinedAbstraction ?implementor ?concreteImplementor WHERE {
            ?implementor src:hasMethod ?operation .
            ?operation src:hasModifier src:Abstract .
            ?operation src:returnsType ?result .

            ?concreteImplementor src:extends ?implementor .
            ?concreteImplementor src:hasMethod ?impl .
            ?impl src:hasModifier src:Override .
            ?impl src:returnsType ?result .

            ?abstraction src:hasField ?implementorRef .
            ?implementorRef src:returnsType ?implementor .

            ?abstraction src:hasMethod ?absOperation .
            ?absOperation src:delegatesTo ?implementorRef .

            FILTER (?abstraction != ?implementor)
            FILTER NOT EXISTS { ?abstraction src:extends ?implementor }

            ?refinedAbstraction src:extends ?abstraction .
        }
        """;
}
