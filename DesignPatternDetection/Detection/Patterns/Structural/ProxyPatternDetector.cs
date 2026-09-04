namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// A Proxy implements the Subject abstraction and controls access to a wrapped RealSubject
/// - a concrete sibling from the same hierarchy - overriding the operation to decide when and how the real work
/// happens.
/// </summary>
/// <remarks>
/// The defining trait is the concrete sibling: the wrapped field's type is another subclass of the very Subject the
/// proxy extends. That places it between its two neighbours - a Decorator wraps the abstraction itself, so decorators
/// stack, and an Adapter wraps a class from outside the hierarchy. A Proxy commits to one specific RealSubject, which
/// is what lets it manage that object's lifecycle or access.
/// </remarks>
public sealed class ProxyPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Proxy";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["subject"] = "ProxyServiceInterface",
        ["realSubject"] = "ProxyService",
        ["proxy"] = "ProxyProxy"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?subject ?realSubject ?proxy WHERE {
            ?subject src:hasMethod ?operation .
            ?operation src:hasModifier src:Abstract .
            ?operation src:returnsType ?result .

            ?realSubject src:extends ?subject .
            ?realSubject src:hasMethod ?realWork .
            ?realWork src:hasModifier src:Override .
            ?realWork src:returnsType ?result .

            ?proxy src:extends ?subject .
            ?proxy src:hasField ?wrapped .
            ?wrapped src:returnsType ?realSubject .

            ?proxy src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .
            ?override src:delegatesTo ?wrapped .

            FILTER (?proxy != ?realSubject)
        }
        """;
}
