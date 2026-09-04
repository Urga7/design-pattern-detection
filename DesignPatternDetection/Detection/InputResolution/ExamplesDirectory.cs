namespace DesignPatternDetection.Detection.InputResolution;

public static class ExamplesDirectory
{
    private const string FolderName = "DesignPatternExamples";

    public static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string[] candidates =
            [
                Path.Combine(directory.FullName, FolderName),
                Path.Combine(directory.FullName, "DesignPatternDetection", FolderName)
            ];

            if (Array.Find(candidates, Directory.Exists) is { } found)
                return found;
        }

        throw new DirectoryNotFoundException($"Could not locate the '{FolderName}' directory.");
    }
}
