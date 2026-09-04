using DesignPatternDetection.Detection.Patterns.Creational;

namespace DesignPatternDetection.Tests.Detection.Patterns.Creational;

public class SingletonPatternDetectorTests
{
    private readonly SingletonPatternDetector _detector = new();

    [Fact]
    public void Detects_a_private_ctor_with_a_static_self_typed_property()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class Singleton
        {
            private Singleton() { }
            public static Singleton Instance { get; } = new Singleton();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Singleton", match.Bindings["class"]);
    }

    [Fact]
    public void Detects_a_singleton_exposed_through_a_static_method()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class Registry
        {
            private static Registry _instance = new Registry();
            private Registry() { }
            public static Registry GetInstance() => _instance;
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Registry", match.Bindings["class"]);
    }

    /// <summary>
    /// Neither sealing nor pattern naming is part of the rule. Whether an unsealed <c>Cache</c> intends to be the only
    /// one of its kind is a question about intent, which the semantic pass answers and a query cannot.
    /// </summary>
    [Fact]
    public void Matches_a_class_that_is_neither_sealed_nor_named_for_the_pattern()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Cache
        {
            private Cache() { }
            public static Cache Instance { get; } = new Cache();
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Cache", match.Bindings["class"]);
    }

    [Fact]
    public void Ignores_a_class_with_a_public_constructor()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class NotASingleton
        {
            public NotASingleton() { }
            public static NotASingleton Instance { get; } = new NotASingleton();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_private_ctor_without_a_self_typed_static_accessor()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public sealed class Util
        {
            private Util() { }
            public static string Version => "1.0";
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }
}
