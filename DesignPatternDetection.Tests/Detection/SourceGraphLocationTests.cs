namespace DesignPatternDetection.Tests.Detection;

public class SourceGraphLocationTests
{
    [Fact]
    public void Records_the_file_and_identifier_line_for_a_type()
    {
        var source = TestGraph.Scan("""
        namespace Demo;
        public class C
        {
        }
        """);

        var span = source.Locations["Demo.C"];
        Assert.EndsWith("Source0.cs", span.FilePath);
        Assert.Equal(2, span.StartLine);
        Assert.Equal(2, span.EndLine);
    }

    [Fact]
    public void Records_the_full_declaration_span_for_a_method()
    {
        var source = TestGraph.Scan("""
        namespace Demo;
        public class C
        {
            public void M()
            {
                var x = 1;
            }
        }
        """);

        var span = source.Locations["Demo.C_M_0"];
        Assert.EndsWith("Source0.cs", span.FilePath);
        Assert.Equal(4, span.StartLine);
        Assert.Equal(7, span.EndLine);
    }

    [Fact]
    public void Records_a_span_per_field_declarator()
    {
        var source = TestGraph.Scan("""
        namespace Demo;
        public class C
        {
            private int a, b;
        }
        """);

        Assert.Equal(4, source.Locations["Demo.C_a_0"].StartLine);
        Assert.Equal(4, source.Locations["Demo.C_b_1"].StartLine);
    }

    [Fact]
    public void Uses_the_first_declaration_for_a_partial_type()
    {
        var source = TestGraph.Scan(
            """
            namespace Demo;
            public partial class C { }
            """,
            """
            namespace Demo;
            public partial class C { }
            """);

        Assert.EndsWith("Source0.cs", source.Locations["Demo.C"].FilePath);
    }

    [Fact]
    public void Attributes_types_to_their_own_files()
    {
        var source = TestGraph.Scan(
            """
            namespace Demo;
            public class A { }
            """,
            """
            namespace Demo;
            public class B { }
            """);

        Assert.EndsWith("Source0.cs", source.Locations["Demo.A"].FilePath);
        Assert.EndsWith("Source1.cs", source.Locations["Demo.B"].FilePath);
    }

    [Fact]
    public void Leaves_metadata_only_types_without_a_location()
    {
        var source = TestGraph.Scan("""
        namespace Demo;
        public class C
        {
            private List<string> items = new();
        }
        """);

        // The field's own node has a span; the List<string> node referenced by
        // its returnsType exists only in metadata and must stay absent.
        Assert.True(source.Locations.ContainsKey("Demo.C_items_0"));
        Assert.DoesNotContain(source.Locations.Keys, key => key.Contains("List"));
    }
}
