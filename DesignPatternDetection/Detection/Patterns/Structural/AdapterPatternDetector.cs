namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// An Adapter implements the client's Target abstraction, overriding its operation,
/// while holding a wrapped instance of an incompatible Adaptee class that does the real work.
/// </summary>
/// <remarks>
/// The defining trait is the single foreign wrapped type: the adapter keeps one reference to a source-declared class
/// or interface from outside the Target hierarchy, and converting one interface into another is the pattern's
/// commonest use. A Decorator looks structurally identical except that it wraps the very abstraction it implements.
/// Because the adaptee may itself be an abstraction, three neighbours have to be held off by the wrapped reference
/// being the adapter's only collaborator and by nothing of the Target being aggregated: a wrapper coordinating
/// several source-declared collaborators is a Facade, one holding two references to the same abstraction is an
/// Interpreter nonterminal, and one collecting the Target is a Composite. A ConcreteCommand is adapter-shaped too -
/// it extends the Command abstraction while wrapping a foreign Receiver - but there an Invoker outside the hierarchy
/// stores the abstraction in a field for later invocation; a <c>FILTER NOT EXISTS</c> keeps that triad out.
/// </remarks>
public sealed class AdapterPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Adapter";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["target"] = "AdapterClientInterface",
        ["adapter"] = "AdapterAdapter",
        ["adaptee"] = "AdapterService"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?target ?adapter ?adaptee WHERE {
            ?target src:hasMethod ?request .
            ?request src:hasModifier src:Abstract .
            ?request src:returnsType ?result .

            ?adapter src:extends ?target .
            ?adapter src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?adapter src:hasField ?adapteeRef .
            ?adapteeRef src:returnsType ?adaptee .
            ?adaptee rdf:type ?adapteeKind .
            FILTER (?adapteeKind IN (src:Class, src:Interface))
            ?override src:delegatesTo ?adapteeRef .

            FILTER (?adaptee != ?target)
            FILTER NOT EXISTS { ?adaptee src:extends+ ?target }

            FILTER NOT EXISTS {
                ?adapter src:hasField ?adapteeRef .
                ?adapter src:hasField ?otherRef .
                ?otherRef src:returnsType ?other .
                ?other rdf:type ?otherKind .
                FILTER (?otherKind IN (src:Class, src:Interface))
                FILTER (STR(?otherRef) != STR(?adapteeRef))
            }

            FILTER NOT EXISTS {
                ?adapter src:hasField|src:hasProperty ?aggregate .
                ?aggregate src:hasTypeArgument ?target .
            }

            FILTER NOT EXISTS {
                ?invoker src:hasField ?invokerRef .
                ?invokerRef src:returnsType ?target .
                FILTER NOT EXISTS { ?invoker src:extends ?target }
            }

            FILTER NOT EXISTS {
                ?adaptee src:hasMethod ?factory .
                ?factory src:instantiates ?adapter .
            }
        }
        """;
}
