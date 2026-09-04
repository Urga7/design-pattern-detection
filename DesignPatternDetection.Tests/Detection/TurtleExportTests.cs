using DesignPatternDetection.Detection;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace DesignPatternDetection.Tests.Detection;

/// <summary>The <c>--turtle</c> dump: the source graph must survive the round trip intact.</summary>
public class TurtleExportTests
{
    [Fact]
    public void The_source_graph_round_trips_through_turtle()
    {
        var source = TestGraph.Scan("namespace Demo; public class C { }");
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            TurtleGraphWriter.Save(source.Graph, path);

            Assert.Contains("scan:Demo.C", File.ReadAllText(path));

            var reloaded = new Graph();
            new TurtleParser().Load(reloaded, path);

            Assert.Equal(source.Graph.Triples.Count, reloaded.Triples.Count);
            Assert.Contains(reloaded.Triples, triple =>
                triple.Subject is IUriNode { Uri.Fragment: "#Demo.C" }
                && triple.Object is IUriNode { Uri.Fragment: "#Class" });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
