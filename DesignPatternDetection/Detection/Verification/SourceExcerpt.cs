using System.Text;

namespace DesignPatternDetection.Detection.Verification;

/// <summary>
/// Pulls the source text behind a <see cref="SourceSpan"/>, caching each file's lines. A single-line span - which is
/// what a type node records - is widened by matching braces forward from the declaration, so the whole class comes
/// back; members already span their full declaration and are taken as-is. The result is capped at
/// <paramref name="maxLines"/>.
/// </summary>
public sealed class SourceExcerptReader(int maxLines = 400)
{
    private readonly Dictionary<string, string[]?> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The declaration at <paramref name="span"/>, or null when the file cannot be read.</summary>
    public string? Read(SourceSpan span)
    {
        var lines = Lines(span.FilePath);
        if (lines is null || span.StartLine < 1 || span.StartLine > lines.Length)
            return null;

        var start = span.StartLine - 1;
        var end = span.EndLine > span.StartLine
            ? Math.Min(span.EndLine, lines.Length) - 1
            : MatchBraces(lines, start);

        end = Math.Min(end, start + maxLines - 1);

        var text = new StringBuilder();
        for (var line = start; line <= end; line++)
            text.AppendLine(lines[line]);

        return text.ToString().TrimEnd();
    }

    /// <summary>Walks forward from a declaration line to the line closing its body.</summary>
    private static int MatchBraces(string[] lines, int start)
    {
        var depth = 0;
        var opened = false;

        for (var line = start; line < lines.Length && line < start + 2000; line++)
        {
            foreach (var character in lines[line])
            {
                switch (character)
                {
                    case '{':
                        depth++;
                        opened = true;
                        break;
                    case '}':
                        depth--;
                        break;
                }
            }

            if (opened && depth <= 0)
                return line;
        }

        // No balanced body found: the declaration line alone.
        return start;
    }

    private string[]? Lines(string path)
    {
        if (_files.TryGetValue(path, out var cached))
            return cached;

        string[]? lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lines = null;
        }

        _files[path] = lines;
        return lines;
    }
}
