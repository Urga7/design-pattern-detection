namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// An Aggregate hands out an Iterator for walking its elements, and the
/// ConcreteIterator wraps that very aggregate while overriding the abstract traversal operations.
/// </summary>
/// <remarks>
/// The defining trait is the mutual association: the aggregate's creation method returns the Iterator abstraction
/// while instantiating the ConcreteIterator, and that concrete iterator holds a reference typed as the aggregate it
/// walks - the product points back at its creator. A Factory Method product never references the creator that built
/// it, and an Adapter's Adaptee never manufactures its own wrapper. The creation method may declare either the
/// iterator abstraction or a supertype it explicitly extends, since idiomatic C# returns the BCL <c>IEnumerator</c>.
/// </remarks>
public sealed class IteratorPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Iterator";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["aggregate"] = "IteratorIterableCollection",
        ["iterator"] = "IteratorIterator",
        ["concreteIterator"] = "IteratorConcreteIterator"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?aggregate ?iterator ?concreteIterator WHERE {
            ?iterator src:hasMethod ?next .
            ?next src:hasModifier src:Abstract .
            ?next src:returnsType ?result .

            ?concreteIterator src:extends ?iterator .
            ?concreteIterator src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?concreteIterator src:hasField ?aggregateRef .
            ?aggregateRef src:returnsType ?aggregate .
            ?aggregate rdf:type src:Class .

            FILTER (?aggregate != ?iterator)
            FILTER (?aggregate != ?concreteIterator)
            FILTER NOT EXISTS { ?aggregate src:extends ?iterator }

            ?aggregate src:hasMethod ?createIterator .
            ?createIterator src:instantiates ?concreteIterator .
            {
                ?createIterator src:returnsType ?iterator .
            }
            UNION
            {
                ?createIterator src:returnsType ?iteratorBase .
                ?iterator src:extends ?iteratorBase .
            }
        }
        """;
}
