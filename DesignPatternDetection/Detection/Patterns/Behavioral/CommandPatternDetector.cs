namespace DesignPatternDetection.Detection.Patterns.Behavioral;

/// <summary>
/// A request is reified as a Command object with an abstract Execute operation, a
/// ConcreteCommand binds that operation to a wrapped Receiver that performs the actual work, and an Invoker stores
/// the command through its abstraction to trigger it later.
/// </summary>
/// <remarks>
/// The defining trait is the invoker - command - receiver triad. A ConcreteCommand on its own is adapter-shaped, so
/// the receiver alone cannot separate the two; what does is the Invoker, a class outside the command hierarchy that
/// stores the abstraction in a field for later invocation. Requiring the receiver to sit outside the command
/// hierarchy keeps Decorator and Proxy out, and the receiver must know nothing about the command abstraction: a
/// wrapped class that talks back through a field typed as it is a Mediator's colleague, not a Receiver.
/// </remarks>
public sealed class CommandPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Command";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["invoker"] = "CommandInvoker",
        ["command"] = "CommandCommand",
        ["concreteCommand"] = "CommandConcreteCommand",
        ["receiver"] = "CommandReceiver"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?invoker ?command ?concreteCommand ?receiver WHERE {
            ?command src:hasMethod ?execute .
            ?execute src:hasModifier src:Abstract .
            ?execute src:returnsType ?result .

            ?concreteCommand src:extends ?command .
            ?concreteCommand src:hasMethod ?override .
            ?override src:hasModifier src:Override .
            ?override src:returnsType ?result .

            ?concreteCommand src:hasField ?receiverRef .
            ?receiverRef src:returnsType ?receiver .
            ?receiver rdf:type src:Class .
            ?override src:delegatesTo ?receiverRef .

            FILTER (?receiver != ?command)
            FILTER NOT EXISTS { ?receiver src:extends ?command }

            FILTER NOT EXISTS {
                ?receiver src:hasField ?backRef .
                ?backRef src:returnsType ?command .
            }

            FILTER NOT EXISTS {
                ?concreteCommand src:hasMethod ?maker .
                ?maker src:instantiates ?receiver .
            }

            ?invoker rdf:type src:Class .
            ?invoker src:hasField ?commandRef .
            ?commandRef src:returnsType ?command .

            ?invoker src:hasMethod ?trigger .
            ?trigger src:delegatesTo ?commandRef .

            FILTER (?invoker != ?command)
            FILTER NOT EXISTS { ?invoker src:extends ?command }
        }
        """;
}
