namespace DesignPatternDetection.Detection.Patterns.Structural;

/// <summary>
/// A Decorator implements the Component abstraction, wraps a single instance of that
/// same abstraction, and overrides the operation to add behaviour around the wrapped call, while a ConcreteComponent
/// provides the plain object being decorated.
/// </summary>
/// <remarks>
/// The defining trait is the self-wrapping: the type the wrapper holds is exactly the abstraction the wrapper itself
/// extends. An Adapter looks structurally identical but wraps a class from outside the hierarchy, and a Composite
/// wraps a collection rather than a single reference. The wrapped reference must also be the only one: a subclass
/// composing two or more references to the abstraction is an Interpreter's NonterminalExpression. Requiring a
/// ConcreteComponent that implements the operation without wrapping ensures there is an undecorated object to
/// enhance. The forwarding override may sit on the wrapper itself or on a subclass of it, since C# idiomatically
/// splits the decorator in two - an abstract base holding the wrappee, and concrete decorators forwarding through
/// that inherited member.
/// </remarks>
public sealed class DecoratorPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Decorator";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["component"] = "DecoratorComponent",
        ["concreteComponent"] = "DecoratorConcreteComponent",
        ["decorator"] = "DecoratorBaseDecorator"
    };
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?component ?concreteComponent ?decorator WHERE {
            ?component src:hasMethod ?operation .
            ?operation src:hasModifier ?operationModifier .
            FILTER (?operationModifier IN (src:Abstract, src:Virtual))
            ?operation src:returnsType ?result .

            ?decorator src:extends ?component .
            { ?decorator src:hasField ?wrapped } UNION { ?decorator src:hasProperty ?wrapped }
            ?wrapped src:returnsType ?component .

            { ?decorator src:hasMethod ?override }
            UNION
            {
                ?concreteDecorator src:extends+ ?decorator .
                ?concreteDecorator src:hasMethod ?override .
            }

            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .
            ?override src:delegatesTo ?wrapped .

            FILTER NOT EXISTS {
                ?decorator src:hasField ?operandA .
                ?operandA src:returnsType ?component .
                ?decorator src:hasField ?operandB .
                ?operandB src:returnsType ?component .
                FILTER (STR(?operandA) < STR(?operandB))
            }
            FILTER NOT EXISTS {
                ?decorator src:hasProperty ?operandC .
                ?operandC src:returnsType ?component .
                ?decorator src:hasProperty ?operandD .
                ?operandD src:returnsType ?component .
                FILTER (STR(?operandC) < STR(?operandD))
            }

            ?concreteComponent src:extends ?component .
            ?concreteComponent src:hasMethod ?plain .
            ?plain src:hasModifier src:Override .
            ?plain src:returnsType ?result .

            FILTER (?concreteComponent != ?decorator)
            FILTER NOT EXISTS {
                ?concreteComponent src:hasField ?otherWrapped .
                ?otherWrapped src:returnsType ?component .
            }
        }
        """;
}
