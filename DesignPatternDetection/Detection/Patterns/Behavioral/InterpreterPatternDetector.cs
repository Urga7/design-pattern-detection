namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A grammar's AbstractExpression declares the interpret operation,
/// TerminalExpressions implement it for the literals of the language, and NonterminalExpressions implement it by
/// composing two or more sub-expressions typed as the abstraction and combining their results.
/// </summary>
/// <remarks>
/// The defining trait is the nonterminal composing several single references of its own abstraction: two distinct
/// references both typed as the very Expression the class extends. That places it between its neighbours in the
/// self-wrapping family - a Decorator holds exactly one such reference, a Composite a collection of them, whereas a
/// grammar rule has a fixed arity of named operands. Demanding a TerminalExpression, a sibling overriding the
/// operation while holding no sub-expression, pins down the recursion's base case.
/// </remarks>
public sealed class InterpreterPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Interpreter";
    
    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>

        SELECT DISTINCT ?expression ?terminalExpression ?nonterminalExpression WHERE {
            ?expression src:hasMethod ?interpret .
            ?interpret src:hasModifier src:Abstract .
            ?interpret src:returnsType ?result .

            ?nonterminalExpression src:extends ?expression .
            ?nonterminalExpression src:hasMethod ?compose .
            ?compose src:hasModifier src:Override .
            ?compose src:returnsType ?result .

            {
                ?nonterminalExpression src:hasField ?leftRef .
                ?leftRef src:returnsType ?expression .
                ?nonterminalExpression src:hasField ?rightRef .
                ?rightRef src:returnsType ?expression .
            }
            UNION
            {
                ?nonterminalExpression src:hasProperty ?leftRef .
                ?leftRef src:returnsType ?expression .
                ?nonterminalExpression src:hasProperty ?rightRef .
                ?rightRef src:returnsType ?expression .
            }

            FILTER (STR(?leftRef) < STR(?rightRef))

            ?terminalExpression src:extends ?expression .
            ?terminalExpression src:hasMethod ?leafOverride .
            ?leafOverride src:hasModifier src:Override .
            ?leafOverride src:returnsType ?result .

            FILTER (?terminalExpression != ?nonterminalExpression)
            FILTER NOT EXISTS {
                ?terminalExpression src:hasField ?operand .
                ?operand src:returnsType ?expression .
            }
            FILTER NOT EXISTS {
                ?terminalExpression src:hasProperty ?operandProperty .
                ?operandProperty src:returnsType ?expression .
            }
        }
        """;
}
