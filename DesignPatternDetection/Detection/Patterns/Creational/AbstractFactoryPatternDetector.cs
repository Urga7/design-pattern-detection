namespace DesignPatternDetection.Detection.Patterns.Creational;

/// <summary>
/// An abstract Factory declares a family of creation methods (two or more),
/// each returning a different abstract Product, and a ConcreteFactory overrides every one of them to instantiate a
/// concrete product from a single, matching family.
/// </summary>
/// <remarks>
/// The defining trait that sets this apart from a plain Factory Method is the family: the factory produces several
/// related products through more than one creation method, so the query binds two distinct creators returning two
/// distinct products.
/// </remarks>
public sealed class AbstractFactoryPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Abstract Factory";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["factory"] = "AbstractFactoryAbstractFactory",
        ["concreteFactory"] = "AbstractFactoryConcreteFactory",
        ["productA"] = "AbstractFactoryAbstractProduct",
        ["productB"] = "AbstractFactoryAbstractProduct"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?factory ?concreteFactory ?productA ?productB WHERE {
            ?factory src:hasMethod ?createA .
            ?createA src:hasModifier src:Abstract .
            ?createA src:returnsType ?productA .

            ?factory src:hasMethod ?createB .
            ?createB src:hasModifier src:Abstract .
            ?createB src:returnsType ?productB .

            FILTER (STR(?productA) < STR(?productB))

            ?concreteFactory src:extends ?factory .

            ?concreteFactory src:hasMethod ?overrideA .
            ?overrideA src:hasModifier src:Override .
            ?overrideA src:returnsType ?productA .
            ?overrideA src:instantiates ?concreteProductA .
            ?concreteProductA src:extends ?productA .

            ?concreteFactory src:hasMethod ?overrideB .
            ?overrideB src:hasModifier src:Override .
            ?overrideB src:returnsType ?productB .
            ?overrideB src:instantiates ?concreteProductB .
            ?concreteProductB src:extends ?productB .
        }
        """;
}
