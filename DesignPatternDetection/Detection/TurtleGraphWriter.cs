using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace DesignPatternDetection.Detection;

/// <summary>Writes a scan graph as Turtle 1.1, where a dot is legal inside a prefixed name's local part.</summary>
public static class TurtleGraphWriter
{
    public static void Save(IGraph graph, string path) =>
        new CompressingTurtleWriter(TurtleSyntax.W3C).Save(graph, path);
}
