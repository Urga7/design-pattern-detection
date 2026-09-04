namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// A Component declares an operation shared by the whole hierarchy, a Composite
/// implements it while holding a collection of child Components it delegates to, and a Leaf implements it with no
/// children of its own.
/// </summary>
/// <remarks>
/// The defining trait is the collection of the own abstraction: the Composite keeps a member whose element type is
/// the very Component it extends. A Decorator also wraps its own abstraction but holds a single reference, so
/// requiring the collection keeps the two apart. Demanding a separate Leaf - a sibling implementing the same
/// operation but holding no such collection - pins down the part-whole tree.
/// </remarks>
public sealed class CompositePatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Composite";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["component"] = "CompositeComponent",
        ["composite"] = "CompositeContainer",
        ["leaf"] = "CompositeLeaf"
    };
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?component ?composite ?leaf WHERE {
            ?component src:hasMethod ?operation .
            ?operation src:hasModifier ?operationModifier .
            FILTER (?operationModifier IN (src:Abstract, src:Virtual))
            ?operation src:returnsType ?result .

            ?composite src:extends ?component .
            ?composite src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            { ?composite src:hasField ?children } UNION { ?composite src:hasProperty ?children }
            ?children src:hasTypeArgument ?component .

            ?leaf src:extends ?component .
            ?leaf src:hasMethod ?leafOverride .
            ?leafOverride src:hasModifier src:Override .
            ?leafOverride src:returnsType ?result .

            FILTER (?leaf != ?composite)
            FILTER NOT EXISTS {
                ?leaf src:hasField ?leafChildren .
                ?leafChildren src:hasTypeArgument ?component .
            }
            FILTER NOT EXISTS {
                ?leaf src:hasProperty ?leafChildrenProperty .
                ?leafChildrenProperty src:hasTypeArgument ?component .
            }
        }
        """;
}
