using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DesignPatternDetection.Detection.Verification;

/// <summary>Remembers rulings so an unchanged scan is never re-adjudicated.</summary>
public interface IVerdictCache
{
    bool TryGet(string key, out MatchVerdict verdict);

    void Set(string key, MatchVerdict verdict);
}

/// <summary>A cache that remembers nothing - every match is re-adjudicated.</summary>
public sealed class NullVerdictCache : IVerdictCache
{
    public bool TryGet(string key, out MatchVerdict verdict)
    {
        verdict = null!;
        return false;
    }

    public void Set(string key, MatchVerdict verdict) { }
}

/// <summary>
/// A JSON file of rulings keyed by the content that produced them: model, system prompt, pattern, role fragments and
/// a hash of the source text handed to the reviewer. An unreadable cache loads empty, and a cache that cannot be
/// written is dropped silently.
/// </summary>
public sealed class FileVerdictCache : IVerdictCache
{
    /// <summary>Where both CLIs keep rulings when <c>--verify-cache</c> names no file.</summary>
    public const string DefaultPath = ".verdict-cache.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly Dictionary<string, MatchVerdict> _entries;

    private FileVerdictCache(string path, Dictionary<string, MatchVerdict> entries)
    {
        _path = path;
        _entries = entries;
    }

    public static FileVerdictCache Load(string path)
    {
        try
        {
            if (File.Exists(path)
                && JsonSerializer.Deserialize<Dictionary<string, MatchVerdict>>(File.ReadAllText(path), JsonOptions)
                    is { } loaded)
            {
                return new FileVerdictCache(path, loaded);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable cache is a cold cache.
        }

        return new FileVerdictCache(path, []);
    }

    public bool TryGet(string key, out MatchVerdict verdict) => _entries.TryGetValue(key, out verdict!);

    public void Set(string key, MatchVerdict verdict) => _entries[key] = verdict;

    public void Save()
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(_path)) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A cache that cannot be written is dropped.
        }
    }

    /// <summary>
    /// The identity of a ruling: everything that could change the answer. Role order does not matter, and the source
    /// text is hashed rather than stored, so an edit anywhere inside a participating declaration invalidates the
    /// entry.
    /// </summary>
    public static string Key(
        string model,
        string systemPrompt,
        string patternName,
        IEnumerable<string> roleFragments,
        string sourceText)
    {
        var material = string.Join(
            "",
            [
                model,
                systemPrompt,
                patternName,
                string.Join(",", roleFragments.Order(StringComparer.Ordinal)),
                sourceText
            ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
