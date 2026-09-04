namespace DesignPatternDetection.Tests;

/// <summary>A throwaway directory tree, deleted when the test ends.</summary>
internal sealed class TempDirectory(string prefix) : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory(prefix);

    public string Root => _root.FullName;

    /// <summary>Writes a file at a path relative to <see cref="Root"/> and returns its full path.</summary>
    public string Write(string relativePath, string content = "// source")
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    public void Dispose() => _root.Delete(recursive: true);
}
