using System.Globalization;
using DesignPatternDetection.Detection;
using VDS.RDF;
using VDS.RDF.Writing;

namespace DesignPatternDetection.Reporting;

/// <summary>
/// Writes a <see cref="DetectionReport"/> as RDF (Turtle), as the bridge layer between the scan graph and FDP:
/// <c>det:filledBy</c> names a node of the scan graph, <c>det:playsRole</c> and <c>det:pattern</c> name individuals
/// of FDP, and every role is reified as a <c>det:RoleBinding</c> so an occurrence keeps its own roles.
/// </summary>
public static class RdfFindingsWriter
{
    /// <summary>The findings vocabulary, declared in <c>docs/detection.ttl</c>.</summary>
    public const string Namespace = "https://urga7.github.io/design-pattern-detection/detection.ttl#";

    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private const string RdfsNamespace = "http://www.w3.org/2000/01/rdf-schema#";
    private const string DcTermsNamespace = "http://purl.org/dc/terms/";
    private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema#";

    /// <summary>The node every finding hangs off. Its '-' keeps it clear of type and member fragments.</summary>
    private const string ScanRootFragment = "scan-root";

    public static void Write(string path, DetectionReport report) =>
        new CompressingTurtleWriter().Save(Build(report), path);

    /// <summary>The findings as a graph.</summary>
    public static IGraph Build(DetectionReport report)
    {
        var graph = new Graph();
        graph.NamespaceMap.AddNamespace("det", new Uri(Namespace));
        graph.NamespaceMap.AddNamespace("scan", new Uri(SourceGraphBuilder.ScanNamespace));
        graph.NamespaceMap.AddNamespace("fdp", new Uri(FdpVocabulary.Namespace));
        graph.NamespaceMap.AddNamespace("rdf", new Uri(RdfNamespace));
        graph.NamespaceMap.AddNamespace("rdfs", new Uri(RdfsNamespace));
        graph.NamespaceMap.AddNamespace("dcterms", new Uri(DcTermsNamespace));

        var scan = Scan(graph, ScanRootFragment);
        graph.Assert(scan, "rdf:type", Term(graph, "Scan"));
        graph.Assert(scan, "det:tool", Literal(graph, $"{report.Tool} {report.Version}"));
        graph.Assert(scan, "det:fileCount", Integer(graph, report.FileCount));
        graph.Assert(scan, "dcterms:created", Typed(graph, report.GeneratedAt.ToString("o", CultureInfo.InvariantCulture), "dateTime"));

        // Types are described once however many occurrences name them.
        var described = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pattern in report.Patterns)
        {
            for (var index = 0; index < pattern.Matches.Count; index++)
                AddOccurrence(graph, scan, pattern, pattern.Matches[index], index + 1, described);
        }

        return graph;
    }

    private static void AddOccurrence(
        IGraph graph,
        INode scan,
        PatternReport pattern,
        MatchReport match,
        int ordinal,
        HashSet<string> described)
    {
        // Ordinal within the pattern, in the order the detector reported them.
        var fragment = $"occurrence-{Identifier(pattern)}-{ordinal}";
        var occurrence = Scan(graph, fragment);

        graph.Assert(scan, "det:hasOccurrence", occurrence);
        graph.Assert(occurrence, "rdf:type", Term(graph, "PatternOccurrence"));
        graph.Assert(occurrence, "det:patternName", Literal(graph, pattern.Pattern));

        if (pattern.Iri is not null)
            graph.Assert(occurrence, "det:pattern", graph.CreateUriNode(new Uri(pattern.Iri)));

        if (match.Verdict is { } verdict)
        {
            graph.Assert(occurrence, "det:verdict", Literal(graph, verdict.Outcome));
            graph.Assert(occurrence, "det:rationale", Literal(graph, verdict.Rationale));
            graph.Assert(occurrence, "det:reviewedBy", Literal(graph, verdict.Model));
        }

        foreach (var role in match.Roles)
            AddRole(graph, occurrence, fragment, role, described);
    }

    private static void AddRole(
        IGraph graph,
        INode occurrence,
        string occurrenceFragment,
        RoleBinding role,
        HashSet<string> described)
    {
        var binding = Scan(graph, $"{occurrenceFragment}-{role.Role}");

        graph.Assert(occurrence, "det:hasRole", binding);
        graph.Assert(binding, "rdf:type", Term(graph, "RoleBinding"));
        graph.Assert(binding, "det:role", Literal(graph, role.Role));

        if (role.Iri is not null)
            graph.Assert(binding, "det:playsRole", graph.CreateUriNode(new Uri(role.Iri)));

        // A role bound to a literal or blank node names no scanned type.
        if (role.Fragment is null)
            return;

        var type = Scan(graph, role.Fragment);
        graph.Assert(binding, "det:filledBy", type);

        if (!described.Add(role.Fragment))
            return;

        graph.Assert(type, "rdfs:label", Literal(graph, role.Label));

        if (role.File is null || role.StartLine is null || role.EndLine is null)
            return;

        graph.Assert(type, "det:declaredIn", Typed(graph, new Uri(Path.GetFullPath(role.File)).AbsoluteUri, "anyURI"));
        graph.Assert(type, "det:startLine", Integer(graph, role.StartLine.Value));
        graph.Assert(type, "det:endLine", Integer(graph, role.EndLine.Value));
    }

    /// <summary>
    /// A node of this scan, in <see cref="SourceGraphBuilder.ScanNamespace"/>. The URI is built directly because a
    /// fragment may contain dots, which QName parsing rejects.
    /// </summary>
    private static INode Scan(IGraph graph, string fragment) =>
        graph.CreateUriNode(new Uri(SourceGraphBuilder.ScanNamespace + fragment));

    private static INode Term(IGraph graph, string localName) => graph.CreateUriNode("det:" + localName);

    private static INode Literal(IGraph graph, string value) => graph.CreateLiteralNode(value);

    private static INode Typed(IGraph graph, string value, string xsdType) =>
        graph.CreateLiteralNode(value, new Uri(XsdNamespace + xsdType));

    private static INode Integer(IGraph graph, int value) =>
        Typed(graph, value.ToString(CultureInfo.InvariantCulture), "integer");

    /// <summary>
    /// A pattern as a node fragment: the local name of its FDP individual, or its own name stripped to letters and
    /// digits when FDP does not model it.
    /// </summary>
    private static string Identifier(PatternReport pattern) =>
        pattern.Iri?.Split('#').Last() is { Length: > 0 } localName
            ? localName
            : new string(pattern.Pattern.Where(char.IsLetterOrDigit).ToArray());
}
