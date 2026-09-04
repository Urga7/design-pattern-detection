namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// Colleagues that never talk to each other directly hold a reference to a Mediator
/// abstraction and report through its abstract notification, while the ConcreteMediator knows every colleague and
/// coordinates their interaction.
/// </summary>
/// <remarks>
/// The defining trait is the bidirectional coupling through the abstraction, fanned out over several colleagues: the
/// ConcreteMediator holds references to two or more distinct colleague classes, and each colleague reports back by
/// delegating through a reference typed as the Mediator abstraction while standing outside its hierarchy. The
/// back-reference separates this from Command, whose Receiver knows nothing about the command abstraction; the
/// fan-out over two colleagues separates it from Iterator's one-to-one association with its Aggregate.
/// </remarks>
public sealed class MediatorPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Mediator";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["mediator"] = "MediatorMediator",
        ["concreteMediator"] = "MediatorConcreteMediator",
        ["colleagueA"] = "MediatorComponent",
        ["colleagueB"] = "MediatorComponent"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?mediator ?concreteMediator ?colleagueA ?colleagueB WHERE {
            ?mediator src:hasMethod ?notify .
            ?notify src:hasModifier src:Abstract .
            ?notify src:returnsType ?result .

            ?concreteMediator src:extends ?mediator .
            ?concreteMediator src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?concreteMediator src:hasField ?refA .
            ?refA src:returnsType ?colleagueA .
            ?colleagueA rdf:type src:Class .

            ?concreteMediator src:hasField ?refB .
            ?refB src:returnsType ?colleagueB .
            ?colleagueB rdf:type src:Class .

            FILTER (STR(?colleagueA) < STR(?colleagueB))
            FILTER (?colleagueA != ?mediator && ?colleagueB != ?mediator)

            ?mediatorRefA src:returnsType ?mediator .
            ?colleagueA src:hasMethod ?reportA .
            ?reportA src:delegatesTo ?mediatorRefA .
            FILTER NOT EXISTS { ?colleagueA src:extends ?mediator }

            ?mediatorRefB src:returnsType ?mediator .
            ?colleagueB src:hasMethod ?reportB .
            ?reportB src:delegatesTo ?mediatorRefB .
            FILTER NOT EXISTS { ?colleagueB src:extends ?mediator }
        }
        """;
}
