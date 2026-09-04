namespace DesignPatternDetection.Detection.Patterns.Creational;

/// <summary>
/// An abstract Creator declares a factory method that returns an abstract
/// Product, and a ConcreteCreator overrides it to instantiate a concrete subtype of that Product.
/// </summary>
/// <remarks>
/// The defining trait is the lone factory method for a foreign product: the creator centres on a single product
/// hierarchy, since one declaring abstract factory methods for several distinct products is an Abstract Factory
/// assembling a family, and that product is a hierarchy other than the creator's own, since a factory method
/// returning the creator's own type is a Prototype clone. The product must also be indifferent to who made it: one
/// that keeps a reference back to the creator that built it is an Iterator's ConcreteIterator, and the method that
/// handed it out is that pattern's CreateIterator rather than a factory method.
/// </remarks>
public sealed class FactoryMethodPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Factory Method";
    
    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["creator"] = "FactoryMethodCreator",
        ["product"] = "FactoryMethodProduct",
        ["concreteCreator"] = "FactoryMethodConcreteCreator",
        ["concreteProduct"] = "FactoryMethodConcreteProduct"
    };
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?creator ?factoryMethod ?product ?concreteCreator ?concreteProduct WHERE {
            ?creator src:hasMethod ?factoryMethod .
            ?factoryMethod src:hasModifier src:Abstract .
            ?factoryMethod src:returnsType ?product .

            ?concreteCreator src:extends ?creator .
            ?concreteCreator src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?product .

            ?override src:instantiates ?concreteProduct .
            ?concreteProduct src:extends+ ?product .

            FILTER (?creator != ?product)

            FILTER NOT EXISTS {
                ?concreteProduct src:hasField ?creatorRef .
                ?creatorRef src:returnsType ?concreteCreator .
            }

            FILTER NOT EXISTS {
                ?factoryMethod src:returnsType ?product .
                ?creator src:hasMethod ?familyMethod .
                ?familyMethod src:hasModifier src:Abstract .
                ?familyMethod src:returnsType ?familyProduct .
                ?familyConcrete src:extends ?familyProduct .
                FILTER (?familyProduct != ?product)
            }
        }
        """;
}
