using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Tests.Detection.InputResolution;

public class SourceFileResolverTests
{
    [Fact]
    public void Resolves_a_single_source_file_to_itself()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        var file = fixture.Write("Widget.cs");

        Assert.Equal([file], SourceFileResolver.Resolve(file));
    }

    [Fact]
    public void Resolves_a_directory_recursively_skipping_build_output()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        var visible = fixture.Write("Widget.cs");
        var nested = fixture.Write(Path.Combine("Nested", "Gadget.cs"));
        fixture.Write(Path.Combine("bin", "Debug", "Widget.cs"));
        fixture.Write(Path.Combine("obj", "Widget.AssemblyInfo.cs"));

        Assert.Equal([nested, visible], SourceFileResolver.Resolve(fixture.Root));
    }

    [Fact]
    public void Resolves_a_project_to_the_sources_beside_it()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        var project = fixture.Write(Path.Combine("App", "App.csproj"), "<Project />");
        var source = fixture.Write(Path.Combine("App", "Widget.cs"));
        var nested = fixture.Write(Path.Combine("App", "Detection", "Gadget.cs"));
        fixture.Write(Path.Combine("App", "obj", "App.GlobalUsings.g.cs"));
        fixture.Write("Outside.cs");

        Assert.Equal([nested, source], SourceFileResolver.Resolve(project));
    }

    [Fact]
    public void Resolves_a_sln_solution_to_the_sources_of_its_projects()
    {
        // The solution-folder entry has no .csproj path and must be ignored.
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        fixture.Write(Path.Combine("App", "App.csproj"), "<Project />");
        fixture.Write(Path.Combine("Lib", "Lib.csproj"), "<Project />");
        var appSource = fixture.Write(Path.Combine("App", "Widget.cs"));
        var libSource = fixture.Write(Path.Combine("Lib", "Gadget.cs"));
        fixture.Write(Path.Combine("Unreferenced", "Ignored.cs"));

        var solution = fixture.Write("All.sln", """
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Items", "Items", "{22222222-2222-2222-2222-222222222222}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Lib", "Lib\Lib.csproj", "{33333333-3333-3333-3333-333333333333}"
        EndProject
        """);

        Assert.Equal([appSource, libSource], SourceFileResolver.Resolve(solution));
    }

    [Fact]
    public void Resolves_a_slnx_solution_to_the_sources_of_its_projects()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        fixture.Write(Path.Combine("App", "App.csproj"), "<Project />");
        fixture.Write(Path.Combine("Lib", "Lib.csproj"), "<Project />");
        var appSource = fixture.Write(Path.Combine("App", "Widget.cs"));
        var libSource = fixture.Write(Path.Combine("Lib", "Gadget.cs"));

        var solution = fixture.Write("All.slnx", """
        <Solution>
          <Project Path="App/App.csproj" />
          <Project Path="Lib/Lib.csproj" />
        </Solution>
        """);

        Assert.Equal([appSource, libSource], SourceFileResolver.Resolve(solution));
    }

    [Fact]
    public void Deduplicates_sources_when_projects_share_a_directory()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        fixture.Write(Path.Combine("App", "App.csproj"), "<Project />");
        fixture.Write(Path.Combine("App", "App.Variant.csproj"), "<Project />");
        var source = fixture.Write(Path.Combine("App", "Widget.cs"));

        var solution = fixture.Write("All.slnx", """
        <Solution>
          <Project Path="App/App.csproj" />
          <Project Path="App/App.Variant.csproj" />
        </Solution>
        """);

        Assert.Equal([source], SourceFileResolver.Resolve(solution));
    }

    [Fact]
    public void Throws_for_a_missing_path_and_an_unsupported_extension()
    {
        using var fixture = new TempDirectory("dpd-resolver-tests-");
        var unsupported = fixture.Write("Notes.txt", "not code");

        Assert.Throws<FileNotFoundException>(
            () => SourceFileResolver.Resolve(Path.Combine(fixture.Root, "Missing.cs")));
        Assert.Throws<ArgumentException>(() => SourceFileResolver.Resolve(unsupported));
    }
}
