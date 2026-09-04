namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// An Originator snapshots its state into a Memento object it manufactures and hands
/// out, restores itself from a memento handed back, and a separate Caretaker keeps the history of those snapshots
/// without ever creating or inspecting them.
/// </summary>
/// <remarks>
/// The defining trait is the save/restore pair on the creator: one method returns the memento abstraction while
/// instantiating the snapshot, and a second takes the abstraction as a parameter and assigns the originator's own
/// state from it. Restoring is what no structural neighbour shares - a Flyweight factory or ConcreteBuilder also
/// creates and returns one type, but nothing hands the object back to mutate the maker's state. The Caretaker
/// completes the split: it collects the abstraction yet never instantiates a snapshot.
/// </remarks>
public sealed class MementoPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Memento";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["originator"] = "MementoOriginator",
        ["memento"] = "MementoMemento",
        ["caretaker"] = "MementoCaretaker"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?originator ?memento ?caretaker WHERE {
            ?originator rdf:type src:Class .
            ?originator src:hasMethod ?save .
            ?save src:returnsType ?memento .
            ?save src:instantiates ?snapshot .

            FILTER (?snapshot = ?memento || EXISTS { ?snapshot src:extends ?memento })
            FILTER (?originator != ?memento && ?originator != ?snapshot)

            ?originator src:hasMethod ?restore .
            ?restore src:hasParameterType ?memento .
            ?restore src:assignsField ?state .

            FILTER (?restore != ?save)

            ?caretaker src:hasField ?history .
            ?history src:hasTypeArgument ?memento .

            FILTER (?caretaker != ?originator)
            FILTER NOT EXISTS {
                ?caretaker src:hasMethod ?creator .
                ?creator src:instantiates ?created .
                FILTER (?created = ?memento || EXISTS { ?created src:extends ?memento })
            }
        }
        """;
}
