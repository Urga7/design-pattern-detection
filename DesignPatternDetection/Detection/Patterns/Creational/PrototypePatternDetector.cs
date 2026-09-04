namespace DesignPatternDetection.Detection.Patterns.Creational;

/// <summary>
/// An object produces fresh copies of its own kind - through an abstract Prototype
/// whose ConcretePrototype overrides the clone, through a concrete class copying itself (copy constructor or
/// <c>MemberwiseClone</c>), or through <c>ICloneable</c>.
/// </summary>
/// <remarks>
/// The defining trait is the self-producing clone: a method declared on a type that manufactures a fresh instance of
/// that very type. The exact self-type return separates it from a Factory Method, whose creation method lives on a
/// separate Creator and returns a distinct Product, and from a Builder, whose product is not the builder itself.
/// Requiring the clone to be non-static keeps out a Singleton's static self-typed accessor, and a fluent member's
/// <c>return this</c> emits <c>src:returnsSelf</c> rather than <c>src:instantiates</c>.
/// </remarks>
public sealed class PrototypePatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Prototype";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["prototype"] = "PrototypePrototype",
        ["concretePrototype"] = "PrototypeConcretePrototype"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX scan: <https://urga7.github.io/design-pattern-detection/scan#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?prototype ?concretePrototype WHERE {
            {
                ?prototype src:hasMethod ?clone .
                ?clone src:hasModifier src:Abstract .
                ?clone src:returnsType ?prototype .

                ?concretePrototype src:extends ?prototype .
                ?concretePrototype src:hasMethod ?override .
                ?override src:hasModifier src:Override .
                ?override src:returnsType ?prototype .
                ?override src:instantiates ?concretePrototype .
            }
            UNION
            {
                ?prototype src:hasMethod ?clone .
                ?concretePrototype src:hasMethod ?clone .
                ?concretePrototype rdf:type src:Class .
                ?clone src:returnsType ?concretePrototype .
                ?clone src:instantiates ?concretePrototype .
                FILTER NOT EXISTS { ?clone src:hasModifier src:Static }
            }
            UNION
            {
                ?prototype src:hasMethod ?clone .
                ?concretePrototype src:hasMethod ?clone .
                ?concretePrototype rdf:type src:Class .
                ?concretePrototype src:extends <https://urga7.github.io/design-pattern-detection/scan#System.ICloneable> .
                ?clone src:returnsType scan:object .
                ?clone src:instantiates ?concretePrototype .
                FILTER NOT EXISTS { ?clone src:hasModifier src:Static }
            }
        }
        """;
}
