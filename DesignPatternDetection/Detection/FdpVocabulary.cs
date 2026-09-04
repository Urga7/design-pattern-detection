namespace DesignPatternDetection.Detection;

/// <summary>
/// The Formal Design Patterns ontology (FDP) of Dejan Lavbič and Marko Poženel. FDP names every GoF pattern as an
/// individual (<c>fdp:Adapter</c>) and every participant of every pattern as another (<c>fdp:AdapterAdapter</c>), the
/// two joined by <c>:hasStructure</c>/<c>:hasStructureElement</c>.
/// </summary>
public static class FdpVocabulary
{
    /// <summary>Namespace of FDP 1.0.3.</summary>
    public const string Namespace = "https://dejanl.github.io/FDP/FDP.ttl#";

    /// <summary>Expands an FDP participant's local name (<c>AdapterAdapter</c>) to its full IRI.</summary>
    public static string Role(string localName) => Namespace + localName;

    /// <summary>Expands an FDP pattern's local name (<c>Adapter</c>) to its full IRI.</summary>
    public static string Pattern(string localName) => Namespace + localName;
}
