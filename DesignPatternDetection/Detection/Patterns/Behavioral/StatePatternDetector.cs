namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A Context delegates behaviour to a wrapped State abstraction, and each ConcreteState
/// both overrides the abstract handle operation and drives the machine onward by creating the sibling state that
/// takes over next.
/// </summary>
/// <remarks>
/// The wrapper side is identical to Strategy - a context storing the abstraction from outside its hierarchy - so the
/// defining trait is the transition: a ConcreteState instantiates a sibling from the same hierarchy, creating oneself
/// being Prototype's clone rather than a handover. Interchangeable strategies never reference one another. A
/// ConcreteState must also not keep a field typed as the sibling it creates: caching the created instance is a lazily
/// initialising Proxy, whereas a state hands control over and lets go.
/// </remarks>
public sealed class StatePatternDetector : SparqlPatternDetector
{
    public override string PatternName => "State";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["context"] = "StateContext",
        ["state"] = "StateState",
        ["concreteState"] = "StateConcreteState",
        ["nextState"] = "StateConcreteState"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?context ?state ?concreteState ?nextState WHERE {
            ?state src:hasMethod ?handle .
            ?handle src:hasModifier src:Abstract .
            ?handle src:returnsType ?result .

            ?concreteState src:extends ?state .
            ?concreteState src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?concreteState src:hasMethod ?transition .
            ?transition src:instantiates ?nextState .
            ?nextState src:extends ?state .

            FILTER (?nextState != ?concreteState)

            FILTER NOT EXISTS {
                ?concreteState src:hasField ?cached .
                ?cached src:returnsType ?nextState .
            }

            ?context rdf:type src:Class .
            ?context src:hasField ?stateRef .
            ?stateRef src:returnsType ?state .

            FILTER (?context != ?state)
            FILTER NOT EXISTS { ?context src:extends ?state }
        }
        """;
}
