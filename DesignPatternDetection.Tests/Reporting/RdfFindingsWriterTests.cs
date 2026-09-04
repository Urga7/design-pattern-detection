using DesignPatternDetection.Detection;
using DesignPatternDetection.Detection.Patterns.Structural;
using DesignPatternDetection.Reporting;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace DesignPatternDetection.Tests.Reporting;

/// <summary>Every assertion compares whole IRIs: an IRI that is merely close still parses and still queries.</summary>
public class RdfFindingsWriterTests
{
    private const string Det = "https://urga7.github.io/design-pattern-detection/detection.ttl#";
    private const string Scan = "https://urga7.github.io/design-pattern-detection/scan#";
    private const string Fdp = "https://dejanl.github.io/FDP/FDP.ttl#";
    private const string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    private const string RdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";

    private const string AdapterSource = """
        namespace Demo;

        public interface INotifier { void Send(string message); }

        public class LegacyPager
        {
            public void Page(string text) { }
        }

        public class PagerAdapter : INotifier
        {
            private readonly LegacyPager _pager;

            public PagerAdapter(LegacyPager pager) { _pager = pager; }

            public void Send(string message) { _pager.Page(message); }
        }
        """;

    private static IEnumerable<INode> Objects(IGraph graph, string subject, string predicate) =>
        graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(new Uri(subject)),
                graph.CreateUriNode(new Uri(predicate)))
            .Select(triple => triple.Object);

    private static IEnumerable<string> Uris(IGraph graph, string subject, string predicate) =>
        Objects(graph, subject, predicate).OfType<IUriNode>().Select(node => node.Uri.AbsoluteUri);

    private static IEnumerable<string> Values(IGraph graph, string subject, string predicate) =>
        Objects(graph, subject, predicate).OfType<ILiteralNode>().Select(node => node.Value);

    private static DetectionReport Report(string pattern, string? iri, params MatchReport[] matches) =>
        new("DesignPatternDetection", "1.0.0", DateTimeOffset.UtcNow, 1, [new PatternReport(pattern, matches, iri)]);

    /// <summary>One Adapter match over a real scan, so nothing about the alignment is hand-written.</summary>
    private static IGraph AdapterFindings()
    {
        var source = TestGraph.Scan(AdapterSource);
        var matches = new AdapterPatternDetector().Detect(source.Graph);

        return RdfFindingsWriter.Build(
            DetectionReport.From(new ScanResult(1, [new PatternDetection("Adapter", matches)], source)));
    }

    [Fact]
    public void An_occurrence_names_the_pattern_in_fdp_and_by_its_own_name()
    {
        var graph = AdapterFindings();
        const string occurrence = $"{Scan}occurrence-Adapter-1";

        Assert.Equal([$"{Det}PatternOccurrence"], Uris(graph, occurrence, RdfType));
        Assert.Equal([$"{Fdp}Adapter"], Uris(graph, occurrence, $"{Det}pattern"));
        Assert.Equal(["Adapter"], Values(graph, occurrence, $"{Det}patternName"));
    }

    /// <summary>The bridge itself: one node pointing at a scanned type and at the FDP participant it fills.</summary>
    [Fact]
    public void A_role_binding_points_at_the_scanned_type_and_at_the_fdp_participant()
    {
        var graph = AdapterFindings();
        const string binding = $"{Scan}occurrence-Adapter-1-adaptee";

        Assert.Equal([$"{Det}RoleBinding"], Uris(graph, binding, RdfType));
        Assert.Equal(["adaptee"], Values(graph, binding, $"{Det}role"));
        Assert.Equal([$"{Scan}Demo.LegacyPager"], Uris(graph, binding, $"{Det}filledBy"));
        Assert.Equal([$"{Fdp}AdapterService"], Uris(graph, binding, $"{Det}playsRole"));
    }

    [Fact]
    public void The_scan_root_reaches_every_occurrence_and_records_the_run()
    {
        var graph = AdapterFindings();
        const string root = $"{Scan}scan-root";

        Assert.Equal([$"{Det}Scan"], Uris(graph, root, RdfType));
        Assert.Equal([$"{Scan}occurrence-Adapter-1"], Uris(graph, root, $"{Det}hasOccurrence"));
        Assert.Equal(["1"], Values(graph, root, $"{Det}fileCount"));
    }

    [Fact]
    public void A_scanned_type_carries_its_label_and_its_declaration_span()
    {
        var graph = AdapterFindings();
        const string type = $"{Scan}Demo.PagerAdapter";

        Assert.Equal(["PagerAdapter"], Values(graph, type, $"{Rdfs}label"));
        Assert.Equal(["10"], Values(graph, type, $"{Det}startLine"));
        Assert.StartsWith("file:///", Assert.Single(Values(graph, type, $"{Det}declaredIn")));
    }

    /// <summary>
    /// The query the export enables: two hops from a role binding to the FDP catalogue entry it belongs to. FDP
    /// itself is not vendored, so a stand-in stands in.
    /// </summary>
    [Fact]
    public void Findings_merged_with_fdp_answer_a_cross_graph_query()
    {
        var merged = AdapterFindings();
        new TurtleParser().Load(merged, new StringReader($"""
            @prefix fdp: <{Fdp}> .

            fdp:Adapter fdp:hasStructure fdp:AdapterStructure .
            fdp:AdapterStructure fdp:hasStructureElement fdp:AdapterAdapter , fdp:AdapterService .
            """));

        var results = (SparqlResultSet)merged.ExecuteQuery($$"""
            PREFIX det: <{{Det}}>
            PREFIX fdp: <{{Fdp}}>

            SELECT ?type ?participant WHERE {
                ?occurrence det:pattern ?pattern ;
                            det:hasRole ?binding .
                ?binding det:playsRole ?participant ;
                         det:filledBy ?type .
                ?pattern fdp:hasStructure/fdp:hasStructureElement ?participant .
            }
            """);

        Assert.Equal(
            [
                $"{Scan}Demo.LegacyPager -> {Fdp}AdapterService",
                $"{Scan}Demo.PagerAdapter -> {Fdp}AdapterAdapter"
            ],
            results.Select(row => $"{row["type"]} -> {row["participant"]}").Order());
    }

    /// <summary>
    /// A pattern FDP does not model still exports; it just makes no claim about the ontology, rather than being
    /// aligned onto a neighbour's participants.
    /// </summary>
    [Fact]
    public void An_unaligned_pattern_exports_without_any_fdp_terms()
    {
        var graph = RdfFindingsWriter.Build(Report("Interpreter", null,
            new MatchReport([new RoleBinding("expression", "Expression", null, null, null, null, "Demo.Expression")])));

        Assert.DoesNotContain(graph.Triples, triple =>
            triple.Object is IUriNode node && node.Uri.AbsoluteUri.StartsWith(Fdp, StringComparison.Ordinal));

        Assert.Equal(["Interpreter"], Values(graph, $"{Scan}occurrence-Interpreter-1", $"{Det}patternName"));
        Assert.Equal(
            [$"{Scan}Demo.Expression"],
            Uris(graph, $"{Scan}occurrence-Interpreter-1-expression", $"{Det}filledBy"));
    }

    [Fact]
    public void A_reviewed_match_carries_its_ruling_and_an_unreviewed_one_carries_none()
    {
        const string occurrence = $"{Scan}occurrence-Adapter-1";
        var role = new RoleBinding("adapter", "A", null, null, null, null, "Demo.A");

        var reviewed = RdfFindingsWriter.Build(Report("Adapter", null,
            new MatchReport([role], new VerdictReport("accepted", "Wraps a foreign class.", "gemini-3.7-flash"))));

        Assert.Equal(["accepted"], Values(reviewed, occurrence, $"{Det}verdict"));
        Assert.Equal(["Wraps a foreign class."], Values(reviewed, occurrence, $"{Det}rationale"));
        Assert.Equal(["gemini-3.7-flash"], Values(reviewed, occurrence, $"{Det}reviewedBy"));

        var unreviewed = RdfFindingsWriter.Build(Report("Adapter", null, new MatchReport([role])));
        Assert.Empty(Values(unreviewed, occurrence, $"{Det}verdict"));
    }

    /// <summary>
    /// Two occurrences that share a type keep their own role bindings, so a join cannot pair either occurrence with
    /// the other's participant.
    /// </summary>
    [Fact]
    public void Two_occurrences_of_one_pattern_keep_their_roles_apart()
    {
        var graph = RdfFindingsWriter.Build(Report("Adapter", $"{Fdp}Adapter",
            new MatchReport([new RoleBinding("adapter", "Shared", null, null, null, $"{Fdp}AdapterAdapter", "Demo.Shared")]),
            new MatchReport([new RoleBinding("adaptee", "Shared", null, null, null, $"{Fdp}AdapterService", "Demo.Shared")])));

        Assert.Equal([$"{Fdp}AdapterAdapter"], Uris(graph, $"{Scan}occurrence-Adapter-1-adapter", $"{Det}playsRole"));
        Assert.Equal([$"{Fdp}AdapterService"], Uris(graph, $"{Scan}occurrence-Adapter-2-adaptee", $"{Det}playsRole"));
    }
}
