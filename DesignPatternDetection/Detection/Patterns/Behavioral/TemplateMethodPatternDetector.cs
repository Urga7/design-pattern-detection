namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// An AbstractClass fixes the skeleton of an algorithm in a concrete template
/// method while deferring individual steps to abstract primitive operations, and a ConcreteClass fills those in.
/// </summary>
/// <remarks>
/// The defining trait is the split class: one abstract class carrying both a concrete, non-static method (the
/// template) and an abstract step a subclass overrides - a pure abstract interface has no template to inherit, and a
/// concrete class has no steps to defer. The one neighbour sharing this split is a Factory Method Creator, whose
/// template drives a creation step; a step whose override manufactures a subtype of the step's own return type is
/// that Creator, not this pattern. The split is about which member is concrete and which abstract, not the syntax
/// spelling them, so a property pair counts exactly as a method pair does.
/// </remarks>
public sealed class TemplateMethodPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Template Method";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["abstractClass"] = "TemplateMethodAbstractClass",
        ["concreteClass"] = "TemplateMethodConcreteClass"
    };
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?abstractClass ?concreteClass WHERE {
            ?abstractClass rdf:type src:Class .
            ?abstractClass src:hasModifier src:Abstract .
            ?abstractClass src:hasMethod|src:hasProperty ?template .

            FILTER NOT EXISTS { ?template src:hasModifier src:Abstract }
            FILTER NOT EXISTS { ?template src:hasModifier src:Static }

            ?abstractClass src:hasMethod|src:hasProperty ?step .
            ?step src:hasModifier src:Abstract .
            ?step src:returnsType ?stepResult .
            ?template src:calls ?step .

            ?concreteClass src:extends ?abstractClass .
            ?concreteClass src:hasMethod|src:hasProperty ?stepOverride .
            ?stepOverride src:hasModifier src:Override .
            ?stepOverride src:returnsType ?stepResult .

            FILTER NOT EXISTS {
                ?stepOverride src:instantiates ?created .
                ?created src:extends ?stepResult .
            }

            FILTER NOT EXISTS {
                ?template rdf:type src:Property .
                ?step rdf:type src:Method .
            }
        }
        """;
}
