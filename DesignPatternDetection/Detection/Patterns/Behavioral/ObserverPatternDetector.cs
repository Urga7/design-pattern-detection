namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A Subject keeps a collection of Observers registered from outside and notifies each
/// of them through the abstract update operation that every ConcreteObserver overrides with its own reaction.
/// </summary>
/// <remarks>
/// The defining trait is the collection of a foreign abstraction: the subject holds a member whose type argument is
/// the Observer abstraction while standing outside its hierarchy - a Composite also collects an abstraction, but the
/// very one it extends. The subject must also never instantiate what it collects: a holder that creates its own
/// elements is a factory or pool, whereas observers subscribe from outside. Requiring the abstract update on the
/// element type additionally rules out pools of plain concrete classes.
/// </remarks>
public sealed class ObserverPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Observer";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["subject"] = "ObserverPublisher",
        ["observer"] = "ObserverSubscriber",
        ["concreteObserver"] = "ObserverConcreteSubscriber"
    };
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?subject ?observer ?concreteObserver WHERE {
            {
                SELECT DISTINCT ?subject ?observer WHERE {
                    ?subject rdf:type src:Class .
                    ?subject src:hasField ?observers .
                    ?observers src:hasTypeArgument ?observer .
                    ?subject src:hasMethod ?notify .
                    ?notify src:invokes ?observer .
                    ?subject src:hasMethod ?attach .
                    ?attach src:hasParameterType ?observer .
                }
            }

            ?observer src:hasMethod ?update .
            ?update src:hasModifier src:Abstract .
            ?update src:returnsType ?result .

            ?concreteObserver src:extends ?observer .
            ?concreteObserver src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            FILTER (?subject != ?observer)
            FILTER NOT EXISTS { ?subject src:extends+ ?observer }

            FILTER NOT EXISTS {
                ?subject src:hasMethod ?creator .
                ?creator src:instantiates ?created .
                ?created src:extends ?observer .
            }
        }
        """;
}
