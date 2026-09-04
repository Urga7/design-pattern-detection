using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DesignPatternDetection.Detection.Verification;

/// <summary>Supplies the prose a reviewer judges a pattern against.</summary>
public interface IRubricSource
{
    /// <summary>The rubric for a pattern, or null when none is available.</summary>
    string? Rubric(string patternName);
}

/// <summary>Reads each detector's rubric out of the XML documentation on its own type.</summary>
public sealed partial class XmlDocRubricSource : IRubricSource
{
    /// <summary>Stands in for a paragraph boundary while the text is still being assembled.</summary>
    private const string ParagraphMarker = "@@PARA@@";

    /// <summary>The doc elements a rubric is assembled from, in order.</summary>
    private static readonly string[] RubricElements = ["summary", "remarks"];

    private readonly Dictionary<string, string> _rubrics;

    private XmlDocRubricSource(Dictionary<string, string> rubrics) => _rubrics = rubrics;

    public string? Rubric(string patternName) => _rubrics.GetValueOrDefault(patternName);

    /// <summary>Pairs every discovered detector with the documentation on its own type.</summary>
    public static XmlDocRubricSource Load(IEnumerable<IPatternDetector> detectors)
    {
        var documentation = LoadDocumentation(typeof(IPatternDetector).Assembly);
        var rubrics = new Dictionary<string, string>();

        if (documentation is null)
            return new XmlDocRubricSource(rubrics);

        foreach (var detector in detectors)
        {
            if (documentation.TryGetValue($"T:{detector.GetType().FullName}", out var member)
                && Describe(member) is { Length: > 0 } rubric)
            {
                rubrics[detector.PatternName] = rubric;
            }
        }

        return new XmlDocRubricSource(rubrics);
    }

    private static Dictionary<string, XElement>? LoadDocumentation(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");

        try
        {
            if (!File.Exists(path))
                return null;

            return XDocument.Load(path)
                .Descendants("member")
                .Where(member => member.Attribute("name") is not null)
                .ToDictionary(member => member.Attribute("name")!.Value, member => member);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string Describe(XElement member)
    {
        var parts = RubricElements.Select(name => member.Element(name))
            .OfType<XElement>()
            .Select(Flatten)
            .Where(text => text.Length > 0);

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Renders a doc element as plain prose: <c>&lt;see cref="X.Y"/&gt;</c> becomes <c>Y</c>, <c>&lt;para&gt;</c>
    /// becomes a blank line, every other inline tag contributes only its text, and the hard line wrapping of the
    /// source comment is collapsed.
    /// </summary>
    private static string Flatten(XElement element)
    {
        var text = new StringBuilder();
        Render(element, text);

        var collapsed = CollapseWhitespace().Replace(text.ToString(), " ");

        return ParagraphBreak().Replace(collapsed, "\n\n").Trim();
    }

    private static void Render(XElement element, StringBuilder text)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText content:
                    text.Append(content.Value);
                    break;

                case XElement { Name.LocalName: "see" or "seealso" } reference:
                    text.Append(ReferenceName(reference));
                    break;

                case XElement { Name.LocalName: "para" } paragraph:
                    text.Append(ParagraphMarker);
                    Render(paragraph, text);
                    text.Append(ParagraphMarker);
                    break;

                case XElement nested:
                    Render(nested, text);
                    break;
            }
        }
    }

    /// <summary>The readable part of a cref: <c>T:Namespace.Decorator</c> renders as <c>Decorator</c>.</summary>
    private static string ReferenceName(XElement reference)
    {
        var target = reference.Attribute("cref")?.Value
                     ?? reference.Attribute("langword")?.Value
                     ?? reference.Value;

        var afterPrefix = target.Contains(':') ? target[(target.IndexOf(':') + 1)..] : target;

        return afterPrefix.Contains('.') ? afterPrefix[(afterPrefix.LastIndexOf('.') + 1)..] : afterPrefix;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();

    [GeneratedRegex(@" ?(?:@@PARA@@)+ ?")]
    private static partial Regex ParagraphBreak();
}
