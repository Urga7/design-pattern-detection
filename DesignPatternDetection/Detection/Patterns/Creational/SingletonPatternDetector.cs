namespace DesignPatternDetection.Detection.Patterns.Creational;

/// <summary>
/// A class that hides its constructor and exposes a single instance of itself through a static member.
/// </summary>
/// <remarks>
/// The defining trait is the self-typed static behind a hidden constructor: the class denies callers the <c>new</c>
/// keyword and then hands them the one instance it made itself. That shape is shared with a static cache or a
/// well-known default instance, and structure alone cannot separate them - the question is whether the type intends
/// to be the only one of its kind, which is one for the semantic pass rather than the query.
/// </remarks>
public sealed class SingletonPatternDetector : SparqlPatternDetector
{
    public override string PatternName => "Singleton";

    protected override IReadOnlyDictionary<string, string> FdpRoles => new Dictionary<string, string>
    {
        ["class"] = "SingletonSingleton"
    };

    protected override string SparqlQuery => """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>

        SELECT DISTINCT ?class WHERE {
            ?class rdf:type src:Class .

            ?class src:hasConstructor ?constructor .
            ?constructor src:hasModifier src:Private .

            { ?class src:hasProperty ?accessor } UNION { ?class src:hasMethod ?accessor }
            ?accessor src:hasModifier src:Static .
            ?accessor src:returnsType ?class .
        }
        """;
}
