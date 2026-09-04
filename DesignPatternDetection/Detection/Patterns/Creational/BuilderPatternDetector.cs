namespace DesignPatternDetection.Detection.Patterns.Creational;

/// <summary>
/// An abstract Builder splits object construction into several steps, and a
/// ConcreteBuilder implements those steps and, in one of them, assembles and returns the finished Product.
/// </summary>
/// <remarks>
/// Two traits set this apart from the Factory patterns. First, the Builder declares a family of steps - two or more
/// abstract methods. Second, the ConcreteBuilder both instantiates the Product and returns the same concrete type
/// from a result method: it constructs the very product it hands out, whereas a Factory Method or Abstract Factory
/// returns an abstract product while instantiating a different subtype. The instantiation and the hand-out may sit in
/// different methods, since idiomatic builders reset the product in one and return it from another.
/// </remarks>
public sealed class BuilderPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Builder";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["builder"] = "BuilderBuilder",
        ["concreteBuilder"] = "BuilderConcreteBuilder",
        ["product"] = "BuilderProduct"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?builder ?concreteBuilder ?product WHERE {
            ?builder src:hasMethod ?stepA .
            ?stepA src:hasModifier src:Abstract .

            ?builder src:hasMethod ?stepB .
            ?stepB src:hasModifier src:Abstract .

            FILTER (STR(?stepA) < STR(?stepB))

            ?concreteBuilder src:extends ?builder .

            ?concreteBuilder src:hasMethod ?assembler .
            ?assembler src:instantiates ?product .
            ?product rdf:type src:Class .

            ?concreteBuilder src:hasMethod ?result .
            ?result src:returnsType ?product .
        }
        """;
}
