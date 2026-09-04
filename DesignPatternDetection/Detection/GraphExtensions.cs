using VDS.RDF;

namespace DesignPatternDetection.Detection;

internal static class GraphExtensions
{
    /// <summary>Asserts a triple from a subject, a predicate QName and an object.</summary>
    public static void Assert(this IGraph graph, INode subject, string predicateQName, INode @object) =>
        graph.Assert(new Triple(subject, graph.CreateUriNode(predicateQName), @object));
}
