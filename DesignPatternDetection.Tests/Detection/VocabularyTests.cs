using System.Text.RegularExpressions;
using DesignPatternDetection.Detection;
using DesignPatternDetection.Reporting;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace DesignPatternDetection.Tests.Detection;

/// <summary>
/// Holds <c>docs/vocab.ttl</c> to the graph and the queries. Nothing in RDF or SPARQL can catch a mistyped term:
/// <c>src:hasMethods</c> parses, executes, and returns zero rows forever, which is indistinguishable from "the
/// pattern is not in this code".
/// </summary>
public class VocabularyTests
{
    private const string VocabularyNamespace = "https://urga7.github.io/design-pattern-detection/vocab.ttl#";
    private const string DetectionNamespace = "https://urga7.github.io/design-pattern-detection/detection.ttl#";

    /// <summary>Every term declared in docs/vocab.ttl, as local names.</summary>
    private static readonly Lazy<HashSet<string>> Declared = new(() => LocalNames("vocab.ttl", VocabularyNamespace));

    /// <summary>Every term declared in docs/detection.ttl, as local names.</summary>
    private static readonly Lazy<HashSet<string>> DeclaredFindings =
        new(() => LocalNames("detection.ttl", DetectionNamespace));

    private static HashSet<string> LocalNames(string fileName, string ns)
    {
        var graph = new Graph();
        new TurtleParser().Load(graph, Path.Combine(AppContext.BaseDirectory, fileName));

        return graph.Triples
            .SelectMany(triple => new[] { triple.Subject, triple.Predicate, triple.Object })
            .OfType<IUriNode>()
            .Where(node => node.Uri.AbsoluteUri.StartsWith(ns, StringComparison.Ordinal))
            .Select(node => node.Uri.AbsoluteUri[ns.Length..])
            .Where(localName => localName.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A term the builder emits but never declared is an undocumented fact detectors may come to rely on.
    /// </summary>
    [Fact]
    public void Every_term_emitted_into_a_graph_is_declared_in_the_vocabulary()
    {
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IHandler { void Handle(string message); }

        public struct Token { public int Value; }

        public abstract class Base
        {
            protected const int Limit = 4;
            protected readonly List<IHandler> Handlers = new();
            public abstract Base Clone();
        }

        public sealed class Handler : Base, IHandler
        {
            private static Handler _instance;
            private IHandler _next;

            private Handler(IHandler next) { _next = next; }

            public void Handle(string message) { _next.Handle(message); }

            public override Base Clone() => new Handler(_next);

            public Handler WithLimit(int limit) { _instance = this; return this; }
        }
        """);

        var emitted = graph.Triples
            .SelectMany(triple => new[] { triple.Predicate, triple.Object })
            .OfType<IUriNode>()
            .Where(node => node.Uri.AbsoluteUri.StartsWith(VocabularyNamespace, StringComparison.Ordinal))
            .Select(node => node.Uri.AbsoluteUri[VocabularyNamespace.Length..])
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(emitted);
        var undeclared = emitted.Except(Declared.Value).Order().ToList();
        Assert.True(undeclared.Count == 0, $"emitted but not declared in docs/vocab.ttl: {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void Every_term_used_by_a_detector_query_is_declared_in_the_vocabulary()
    {
        var offenders = new List<string>();

        foreach (var detector in DetectorReflection.Detectors)
        {
            var used = Regex.Matches(DetectorReflection.QueryOf(detector), @"\bsrc:([A-Za-z_][A-Za-z0-9_]*)")
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal);

            offenders.AddRange(used
                .Where(term => !Declared.Value.Contains(term))
                .Select(term => $"{detector.PatternName} uses src:{term}"));
        }

        Assert.True(offenders.Count == 0, string.Join("; ", offenders.Order()));
    }

    /// <summary>A query may only reference the two namespaces this project defines.</summary>
    [Fact]
    public void No_detector_query_references_the_fdp_namespace()
    {
        foreach (var detector in DetectorReflection.Detectors)
            Assert.DoesNotContain(FdpVocabulary.Namespace, DetectorReflection.QueryOf(detector), StringComparison.Ordinal);
    }

    /// <summary>The same guarantee for the findings vocabulary.</summary>
    [Fact]
    public void Every_term_emitted_into_a_findings_graph_is_declared_in_the_detection_vocabulary()
    {
        var report = new DetectionReport("DesignPatternDetection", "1.0.0", DateTimeOffset.UtcNow, 1,
        [
            new PatternReport("Adapter",
                [
                    new MatchReport(
                        [
                            new RoleBinding("adapter", "PagerAdapter", "Adapter.cs", 10, 18,
                                FdpVocabulary.Role("AdapterAdapter"), "Demo.PagerAdapter"),
                            new RoleBinding("collection", "List_string_", null, null, null, null, null)
                        ],
                        new VerdictReport("accepted", "Wraps a foreign class.", "gemini-3.7-flash"))
                ],
                FdpVocabulary.Pattern("Adapter"))
        ]);

        var emitted = RdfFindingsWriter.Build(report).Triples
            .SelectMany(triple => new[] { triple.Predicate, triple.Object })
            .OfType<IUriNode>()
            .Where(node => node.Uri.AbsoluteUri.StartsWith(DetectionNamespace, StringComparison.Ordinal))
            .Select(node => node.Uri.AbsoluteUri[DetectionNamespace.Length..])
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(emitted);
        var undeclared = emitted.Except(DeclaredFindings.Value).Order().ToList();
        Assert.True(undeclared.Count == 0, $"emitted but not declared in docs/detection.ttl: {string.Join(", ", undeclared)}");
    }
}
