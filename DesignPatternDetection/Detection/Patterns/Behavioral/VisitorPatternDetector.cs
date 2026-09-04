namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// An Element hierarchy accepts a Visitor through an abstract accept operation, the
/// Visitor declares one abstract visit per concrete element type, and a ConcreteVisitor overrides those visits to
/// bundle one operation's logic across the whole hierarchy.
/// </summary>
/// <remarks>
/// The defining trait is the closed double-dispatch loop between two hierarchies, expressed in parameters: the
/// element's abstract accept takes the Visitor abstraction, while the visitor's abstract operations each take a
/// distinct concrete element of that same hierarchy. No other pattern couples two hierarchies through mutual
/// parameter types - a State's abstract handle also takes a foreign class, but that context declares no abstract
/// operation pointing back. Requiring two distinct visited element types pins down the fan-out that makes double
/// dispatch worthwhile: a visit over a single type is a plain callback.
/// </remarks>
public sealed class VisitorPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Visitor";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["visitor"] = "VisitorVisitor",
        ["concreteVisitor"] = "VisitorConcreteVisitor",
        ["element"] = "VisitorElement",
        ["concreteElementA"] = "VisitorConcreteElement",
        ["concreteElementB"] = "VisitorConcreteElement"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?visitor ?concreteVisitor ?element ?concreteElementA ?concreteElementB WHERE {
            ?element src:hasMethod ?accept .
            ?accept src:hasModifier src:Abstract .
            ?accept src:hasParameterType ?visitor .

            FILTER (?visitor != ?element)

            ?visitor src:hasMethod ?visitA .
            ?visitA src:hasModifier src:Abstract .
            ?visitA src:hasParameterType ?concreteElementA .
            ?concreteElementA src:extends ?element .

            ?visitor src:hasMethod ?visitB .
            ?visitB src:hasModifier src:Abstract .
            ?visitB src:hasParameterType ?concreteElementB .
            ?concreteElementB src:extends ?element .

            FILTER (?visitA != ?visitB)
            FILTER (STR(?concreteElementA) < STR(?concreteElementB))

            ?concreteElementA src:hasMethod ?acceptA .
            ?acceptA src:hasModifier src:Override .
            ?acceptA src:hasParameterType ?visitor .

            ?concreteElementB src:hasMethod ?acceptB .
            ?acceptB src:hasModifier src:Override .
            ?acceptB src:hasParameterType ?visitor .

            ?concreteVisitor src:extends ?visitor .

            ?concreteVisitor src:hasMethod ?visitImplA .
            ?visitImplA src:hasModifier src:Override .
            ?visitImplA src:hasParameterType ?concreteElementA .

            ?concreteVisitor src:hasMethod ?visitImplB .
            ?visitImplB src:hasModifier src:Override .
            ?visitImplB src:hasParameterType ?concreteElementB .
        }
        """;
}
