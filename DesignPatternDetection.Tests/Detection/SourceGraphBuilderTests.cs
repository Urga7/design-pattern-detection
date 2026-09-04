using DesignPatternDetection.Detection.Patterns.Behavioral;
using VDS.RDF;
using VDS.RDF.Query;

namespace DesignPatternDetection.Tests.Detection;

public class SourceGraphBuilderTests
{
    private const string Prefixes = """
        PREFIX src: <https://urga7.github.io/design-pattern-detection/vocab.ttl#>
        PREFIX scan: <https://urga7.github.io/design-pattern-detection/scan#>
        PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
        """;

    private static bool Ask(IGraph graph, string whereClause) =>
        ((SparqlResultSet)graph.ExecuteQuery($"{Prefixes}\nASK {{ {whereClause} }}")).Result;

    /// <summary>A full IRI for a type node, since qualified fragments contain dots.</summary>
    private static string Type(string qualifiedName) =>
        $"<https://urga7.github.io/design-pattern-detection/scan#{qualifiedName}>";

    [Fact]
    public void Emits_the_type_kind_for_classes_interfaces_and_structs()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class C { }
        public interface I { }
        public struct S { }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.C")} rdf:type src:Class"));
        Assert.True(Ask(graph, $"{Type("Demo.I")} rdf:type src:Interface"));
        Assert.True(Ask(graph, $"{Type("Demo.S")} rdf:type src:Struct"));
    }

    [Fact]
    public void Emits_extends_edges_for_both_base_classes_and_interfaces()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface IShape { }
        public abstract class Shape { }
        public sealed class Circle : Shape, IShape { }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.Circle")} src:extends {Type("Demo.Shape")}"));
        Assert.True(Ask(graph, $"{Type("Demo.Circle")} src:extends {Type("Demo.IShape")}"));
    }

    [Fact]
    public void Emits_member_modifiers()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class C
        {
            private C() { }
            public abstract void M();
        }
        """);

        Assert.True(Ask(graph, "?ctor rdf:type src:Constructor . ?ctor src:hasModifier src:Private"));
        Assert.True(Ask(graph, "?m rdf:type src:Method . ?m src:hasModifier src:Abstract"));
    }

    [Fact]
    public void Emits_return_types_and_instantiations()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Product { }
        public class Factory
        {
            public Product Create() => new Product();
        }
        """);

        Assert.True(Ask(graph, $"?m src:returnsType {Type("Demo.Product")}"));
        Assert.True(Ask(graph, $"?m src:instantiates {Type("Demo.Product")}"));
    }

    [Fact]
    public void Emits_parameter_types_for_methods_and_constructors()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Visitor { }
        public class Element
        {
            public Element(Visitor initial) { }
            public void Accept(Visitor visitor, int depth) { }
        }
        """);

        Assert.True(Ask(graph, $"?ctor rdf:type src:Constructor . ?ctor src:hasParameterType {Type("Demo.Visitor")}"));
        Assert.True(Ask(graph, $"?m rdf:type src:Method . ?m src:hasParameterType {Type("Demo.Visitor")} . ?m src:hasParameterType scan:int"));
    }

    [Fact]
    public void Resolves_qualified_uses_to_the_same_node_as_the_declaration()
    {
        // The return type is written fully qualified, but should resolve to the
        // same node as the class declaration.
        var graph = TestGraph.From("""
        namespace Demo;
        public class Product { }
        public class Factory
        {
            public Demo.Product Create() => new Demo.Product();
        }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.Product")} rdf:type src:Class . ?m src:returnsType {Type("Demo.Product")}"));
    }

    [Fact]
    public void Same_named_classes_in_different_namespaces_stay_distinct()
    {
        var graph = TestGraph.From(
            """
            namespace First;
            public class Component { public string Operation() => "first"; }
            """,
            """
            namespace Second;
            public class Component { }
            """);

        Assert.True(Ask(graph, $"{Type("First.Component")} rdf:type src:Class"));
        Assert.True(Ask(graph, $"{Type("Second.Component")} rdf:type src:Class"));
        // The method belongs to First.Component only.
        Assert.False(Ask(graph, $"{Type("Second.Component")} src:hasMethod ?operation"));
    }

    [Fact]
    public void Unresolved_type_references_fall_back_to_the_simple_name_and_unify()
    {
        // Widget is never declared: the field's and the method's references
        // must still land on one shared node.
        var graph = TestGraph.From("""
        namespace Demo;
        public class Holder
        {
            private Widget _widget;
            public Widget Current() => _widget;
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Holder")} src:hasField ?field .
            ?field src:returnsType {Type("Widget")} .
            {Type("Demo.Holder")} src:hasMethod ?method .
            ?method src:returnsType {Type("Widget")} .
            """));
    }

    [Fact]
    public void A_class_without_a_base_list_extends_nothing()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Plain { public string Speak() => "plain"; }
        """);

        Assert.False(Ask(graph, "?type src:extends ?base"));
    }

    [Fact]
    public void Collection_type_arguments_resolve_to_the_declared_element_type()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Graphic { }
        public class Canvas
        {
            private List<Graphic> _children;
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Canvas")} src:hasField ?children .
            ?children src:hasTypeArgument {Type("Demo.Graphic")} .
            """));
    }

    [Fact]
    public void Nested_generic_type_arguments_are_recorded_at_every_depth()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class TreeType { }
        public class TreeFactory
        {
            private List<Tuple<TreeType, string>> _pool;
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.TreeFactory")} src:hasField ?pool .
            ?pool src:hasTypeArgument {Type("System.Tuple_TreeType_string_")} .
            ?pool src:hasTypeArgument {Type("Demo.TreeType")} .
            ?pool src:hasTypeArgument scan:string .
            """));
    }

    [Fact]
    public void Memberwise_clone_counts_as_instantiating_the_declaring_type()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Person
        {
            public Person ShallowCopy() => (Person)this.MemberwiseClone();
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Person")} src:hasMethod ?copy .
            ?copy src:instantiates {Type("Demo.Person")} .
            """));
    }

    [Fact]
    public void Interface_member_carries_abstract()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface INotifier { string Send(string message); }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.INotifier")} src:hasMethod ?send . ?send src:hasModifier src:Abstract"));
    }

    [Fact]
    public void Interface_property_carries_abstract()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface IShape { int Area { get; } }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.IShape")} src:hasProperty ?area . ?area src:hasModifier src:Abstract"));
    }

    [Fact]
    public void Implicit_interface_implementation_carries_override()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface INotifier { string Send(string message); }
        public sealed class EmailNotifier : INotifier { public string Send(string message) => "email"; }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.EmailNotifier")} src:hasMethod ?send . ?send src:hasModifier src:Override"));
    }

    [Fact]
    public void Explicit_interface_implementation_carries_override()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface INotifier { string Send(string message); }
        public sealed class SmsNotifier : INotifier { string INotifier.Send(string message) => "sms"; }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.SmsNotifier")} src:hasMethod ?send . ?send src:hasModifier src:Override"));
    }

    [Fact]
    public void Member_implementing_an_inherited_interface_carries_override()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface IReader { string Read(); }
        public interface IBufferedReader : IReader { }
        public sealed class FileReader : IBufferedReader { public string Read() => "data"; }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.FileReader")} src:hasMethod ?read . ?read src:hasModifier src:Override"));
    }

    [Fact]
    public void Default_interface_method_is_neither_abstract_nor_override()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public interface ILogger { string Log(string message) => message; }
        """);

        Assert.False(Ask(graph, $"""
            {Type("Demo.ILogger")} src:hasMethod ?log .
            ?log src:hasModifier ?modifier .
            FILTER (?modifier IN (src:Abstract, src:Override))
            """));
    }

    [Fact]
    public void Constructor_that_is_private_by_omission_carries_private()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Registry
        {
            Registry() { }
        }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.Registry")} src:hasConstructor ?ctor . ?ctor src:hasModifier src:Private"));
    }

    [Fact]
    public void Method_invoking_another_type_emits_invokes()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Printer { public void Print() { } }
        public class Report
        {
            public void Render(Printer printer) { printer.Print(); }
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Report")} src:hasMethod ?render .
            ?render src:invokes {Type("Demo.Printer")} .
            """));
    }

    [Fact]
    public void Method_forwarding_to_an_own_field_emits_delegatesTo()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Engine { public void Start() { } }
        public class Car
        {
            private Engine _engine;
            public void Drive() => _engine.Start();
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Car")} src:hasMethod ?drive .
            {Type("Demo.Car")} src:hasField ?engine .
            ?drive src:delegatesTo ?engine .
            """));
    }

    [Fact]
    public void Call_on_a_local_or_parameter_is_not_delegation()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Engine { public void Start() { } }
        public class Garage
        {
            public void Test(Engine engine) { engine.Start(); }
        }
        """);

        Assert.False(Ask(graph, "?m src:delegatesTo ?anything"));
    }

    [Fact]
    public void Delegation_through_an_unresolved_field_type_still_emits_both_facts()
    {
        // Widget is never declared - sources only need to parse. The receiver
        // still resolves as a field symbol, and its written type still names
        // who is being called into.
        var graph = TestGraph.From("""
        namespace Demo;
        public class Wrapper
        {
            private Widget _widget;
            public void Run() => _widget.Spin();
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Wrapper")} src:hasMethod ?run .
            {Type("Demo.Wrapper")} src:hasField ?widget .
            ?run src:delegatesTo ?widget .
            ?run src:invokes {Type("Widget")} .
            """));
    }

    [Fact]
    public void Method_calling_an_own_method_emits_calls()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public abstract class Recipe
        {
            public void Cook() { Prepare(); }
            protected abstract void Prepare();
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Recipe")} src:hasMethod ?cook .
            {Type("Demo.Recipe")} src:hasMethod ?prepare .
            ?prepare src:hasModifier src:Abstract .
            ?cook src:calls ?prepare .
            """));
    }

    [Fact]
    public void Method_returning_this_emits_returnsSelf()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class QueryBuilder
        {
            public QueryBuilder WithFilter() { return this; }
            public QueryBuilder WithSort() => this;
            public QueryBuilder Clone() => new QueryBuilder();
        }
        """);

        Assert.True(Ask(graph, $"{Type("Demo.QueryBuilder_WithFilter_0")} src:returnsSelf {Type("Demo.QueryBuilder")}"));
        Assert.True(Ask(graph, $"{Type("Demo.QueryBuilder_WithSort_1")} src:returnsSelf {Type("Demo.QueryBuilder")}"));
        Assert.False(Ask(graph, $"{Type("Demo.QueryBuilder_Clone_2")} src:returnsSelf ?anything"));
    }

    [Fact]
    public void Constructor_and_method_assigning_an_own_field_emit_assignsField()
    {
        var graph = TestGraph.From("""
        namespace Demo;
        public class Counter
        {
            private int _count;
            public Counter() { _count = 0; }
            public void Bump() { _count += 1; }
        }
        """);

        Assert.True(Ask(graph, $"""
            {Type("Demo.Counter")} src:hasConstructor ?ctor .
            {Type("Demo.Counter")} src:hasField ?count .
            ?ctor src:assignsField ?count .
            {Type("Demo.Counter")} src:hasMethod ?bump .
            ?bump src:assignsField ?count .
            """));
    }

    [Fact]
    public void Detects_an_interface_based_strategy_end_to_end()
    {
        // Idiomatic C# declares the abstraction as an interface, so the detectors must still see abstract
        // operations and their overrides.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface ISortStrategy { string Sort(string items); }
        public sealed class BubbleSort : ISortStrategy { public string Sort(string items) => "bubble"; }
        public sealed class QuickSort : ISortStrategy { public string Sort(string items) => "quick"; }

        public sealed class Sorter
        {
            private ISortStrategy _strategy;
            public Sorter(ISortStrategy strategy) => _strategy = strategy;
            public string Sort(string items) => _strategy.Sort(items);
        }
        """);

        var matches = new StrategyPatternDetector().Detect(graph);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal("Sorter", match.Bindings["context"]);
            Assert.Equal("ISortStrategy", match.Bindings["strategy"]);
        });
    }
}
