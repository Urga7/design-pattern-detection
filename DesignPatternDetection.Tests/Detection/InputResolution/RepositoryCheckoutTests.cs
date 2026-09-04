using System.Diagnostics;
using DesignPatternDetection.Detection.InputResolution;

namespace DesignPatternDetection.Tests.Detection.InputResolution;

/// <summary>
/// Exercises the real clone-and-clean-up path against a throwaway local repository, so no network access is needed -
/// but the <c>git</c> executable is, exactly as the feature itself requires it.
/// </summary>
public class RepositoryCheckoutTests
{
    [Fact]
    public void Clones_the_sources_and_deletes_them_again_when_disposed()
    {
        using var origin = new Origin();
        string root;

        using (var checkout = RepositoryCheckout.Clone(origin.Path))
        {
            root = checkout.Root;
            Assert.Equal(["// origin"], SourceFileResolver.Resolve(root).Select(File.ReadAllText));
        }

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Clones_the_requested_branch()
    {
        using var origin = new Origin();
        origin.CommitOnNewBranch("feature", "Feature.cs");

        using var checkout = RepositoryCheckout.Clone(origin.Path, "feature");

        Assert.Equal(
            ["Feature.cs", "Widget.cs"],
            SourceFileResolver.Resolve(checkout.Root).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Leaves_nothing_behind_when_the_clone_fails()
    {
        using var origin = new Origin();
        var before = TemporaryCheckouts();

        Assert.Throws<InvalidOperationException>(
            () => RepositoryCheckout.Clone(Path.Combine(origin.Path, "no-such-repository")));

        Assert.Equal(before, TemporaryCheckouts());
    }

    /// <summary>Disposing twice must not throw - the scan disposes in a finally.</summary>
    [Fact]
    public void Can_be_disposed_twice()
    {
        using var origin = new Origin();
        var checkout = RepositoryCheckout.Clone(origin.Path);

        checkout.Dispose();
        checkout.Dispose();

        Assert.False(Directory.Exists(checkout.Root));
    }

    private static string[] TemporaryCheckouts() =>
        Directory.GetDirectories(Path.GetTempPath(), "dpd-clone-*").Order().ToArray();

    /// <summary>A one-commit repository to clone from, deleted when the test ends.</summary>
    private sealed class Origin : IDisposable
    {
        public Origin()
        {
            Path = Directory.CreateTempSubdirectory("dpd-origin-").FullName;
            Git("init", "-b", "main", Path);
            Commit("Widget.cs", "// origin");
        }

        public string Path { get; }

        public void CommitOnNewBranch(string branch, string fileName)
        {
            Git("-C", Path, "checkout", "-b", branch);
            Commit(fileName, "// branch");
        }

        public void Dispose()
        {
            // Git writes its objects read-only, which blocks a plain delete.
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(Path, recursive: true);
        }

        private void Commit(string fileName, string content)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), content);
            Git("-C", Path, "add", fileName);
            Git("-C", Path,
                "-c", "user.name=Test", "-c", "user.email=test@example.com",
                "commit", "-m", $"Add {fileName}");
        }

        private static void Git(params string[] arguments)
        {
            var git = new ProcessStartInfo("git") { UseShellExecute = false };
            foreach (var argument in arguments)
                git.ArgumentList.Add(argument);

            using var process = Process.Start(git)!;
            process.WaitForExit();

            Assert.Equal(0, process.ExitCode);
        }
    }
}
