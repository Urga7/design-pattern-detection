namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A Context delegates an algorithm to a wrapped Strategy abstraction, and
/// interchangeable ConcreteStrategies override that algorithm so the behaviour can be swapped without touching the
/// context.
/// </summary>
/// <remarks>
/// The defining trait is the lone wrapper outside the hierarchy: the context stores the Strategy abstraction without
/// belonging to it - which keeps Decorator, Proxy and Adapter out, since their wrapper extends the very type it
/// stands in for - and heads no hierarchy of its own, since once the wrapping side varies too the shape is a Bridge.
/// Requiring the concrete strategies to be self-contained keeps Command out, whose concrete commands wrap a Receiver.
/// The strategies must also be independent of one another: a subclass that instantiates a sibling is a State
/// transitioning the machine onward.
/// </remarks>
public sealed class StrategyPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Strategy";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["context"] = "StrategyContext",
        ["strategy"] = "StrategyStrategy",
        ["concreteStrategy"] = "StrategyConcreteStrategy"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?context ?strategy ?concreteStrategy WHERE {
            ?strategy src:hasMethod ?algorithm .
            ?algorithm src:hasModifier src:Abstract .
            ?algorithm src:returnsType ?result .

            ?concreteStrategy src:extends ?strategy .
            ?concreteStrategy src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?context rdf:type src:Class .
            ?context src:hasField ?strategyRef .
            ?strategyRef src:returnsType ?strategy .

            ?context src:hasMethod ?contextOperation .
            ?contextOperation src:delegatesTo ?strategyRef .

            FILTER (?context != ?strategy)
            FILTER NOT EXISTS { ?context src:extends ?strategy }

            FILTER NOT EXISTS { ?refinedContext src:extends ?context }

            FILTER NOT EXISTS {
                ?armedSibling src:extends ?strategy .
                ?armedSibling src:hasField ?receiverRef .
                ?receiverRef src:returnsType ?receiver .
                ?receiver rdf:type src:Class .
            }

            FILTER NOT EXISTS {
                ?transitioningState src:extends ?strategy .
                ?transitioningState src:hasMethod ?transition .
                ?transition src:instantiates ?nextState .
                ?nextState src:extends ?strategy .
                FILTER (?nextState != ?transitioningState)
            }
        }
        """;
}
