namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// A FlyweightFactory pools shared Flyweight instances in a collection and hands them
/// out through a get-or-create method, while the Flyweight keeps its intrinsic state immutable so sharing is safe.
/// </summary>
/// <remarks>
/// The defining trait is the pool of shared immutables: the factory holds a collection-typed member whose element is
/// the flyweight class, at any nesting depth, together with a method that both returns and instantiates that same
/// class - the get-or-create shape. The immutable intrinsic state is a field that is readonly or assigned only during
/// construction, which separates the pattern from a plain registry of mutable objects. A Composite also holds a
/// collection of a source class, but of its own abstraction, so the factory must stand outside the flyweight's
/// hierarchy.
/// </remarks>
public sealed class FlyweightPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Flyweight";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["flyweightFactory"] = "FlyweightFlyweightFactory",
        ["flyweight"] = "FlyweightFlyweight"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?flyweightFactory ?flyweight WHERE {
            ?flyweight rdf:type src:Class .
            ?flyweight src:hasField ?intrinsic .
            { ?intrinsic src:hasModifier src:ReadOnly }
            UNION
            { ?flyweight src:hasConstructor ?ctor . ?ctor src:assignsField ?intrinsic }
            FILTER NOT EXISTS {
                ?flyweight src:hasMethod ?mutator .
                ?mutator src:assignsField ?intrinsic .
            }

            ?flyweightFactory src:hasField ?pool .
            ?pool src:hasTypeArgument ?flyweight .

            ?flyweightFactory src:hasMethod ?getFlyweight .
            ?getFlyweight src:returnsType ?flyweight .
            ?getFlyweight src:instantiates ?flyweight .

            FILTER (?flyweightFactory != ?flyweight)
            FILTER NOT EXISTS { ?flyweightFactory src:extends ?flyweight }
        }
        """;
}
