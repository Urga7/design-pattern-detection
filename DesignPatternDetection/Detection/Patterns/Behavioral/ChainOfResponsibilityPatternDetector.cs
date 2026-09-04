namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A Handler abstraction declares the request operation, the successor reference - typed as the abstraction itself - lives on the
/// abstraction or on a base class implementing it, and ConcreteHandlers override the operation to either resolve the request or
/// pass it on down the chain.
/// </summary>
/// <remarks>
/// The defining trait is the forwarded self-typed successor: a reference typed as the very abstraction whose
/// operation is forwarded through it. There are two idiomatic placements - on the abstraction itself, so every
/// concrete handler inherits the link, or on the abstract base when C# splits the abstraction in two. The split shape
/// overlaps with a Decorator's base wrapper, and what separates them is the absence of a plain component: a Decorator
/// needs an undecorated, reference-free ConcreteComponent, a chain has none. A static self-typed member is a
/// Singleton-style instance holder, not a successor.
/// </remarks>
public sealed class ChainOfResponsibilityPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Chain of Responsibility";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["handler"] = "ChainOfResponsibilityHandler",
        ["concreteHandler"] = "ChainOfResponsibilityConcreteHandler"
    };
    
    protected override string FdpPattern => "ChainOfResponsibility";
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?handler ?concreteHandler WHERE {
            ?handler src:hasMethod ?handle .
            ?handle src:hasModifier src:Abstract .
            ?handle src:returnsType ?result .

            ?concreteHandler src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            {
                ?handler src:hasField|src:hasProperty ?successor .
                ?successor src:returnsType ?handler .
                ?forward src:delegatesTo ?successor .

                ?concreteHandler src:extends ?handler .

                FILTER NOT EXISTS { ?successor src:hasModifier src:Static }
            }
            UNION
            {
                ?linker src:extends ?handler .
                ?linker src:hasField|src:hasProperty ?successor .
                ?successor src:returnsType ?handler .
                ?forward src:delegatesTo ?successor .

                ?concreteHandler src:extends ?linker .

                FILTER (?concreteHandler != ?linker)
                FILTER NOT EXISTS { ?successor src:hasModifier src:Static }
                FILTER NOT EXISTS {
                    ?plain src:extends ?handler .
                    ?plain src:hasMethod ?plainOverride .
                    ?plainOverride src:hasModifier src:Override .
                    FILTER NOT EXISTS { ?plain src:hasField ?anyField }
                }
            }
        }
        """;
}
