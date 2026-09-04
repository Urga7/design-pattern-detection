using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Visitor: one abstract visit per concrete element type, so a new operation over the whole hierarchy is a single new subclass.
[UsedImplicitly]
public abstract class DocumentVisitor
{
    public abstract string VisitPlainText(PlainText text);

    public abstract string VisitHyperlink(Hyperlink link);
}

// Element: accepts a visitor, letting it dispatch on the concrete type.
[UsedImplicitly]
public abstract class DocumentPart
{
    public abstract string Accept(DocumentVisitor visitor);
}

// Concrete elements: each accept hands the visitor its own concrete type - the second dispatch of the double dispatch.
[UsedImplicitly]
public sealed class PlainText : DocumentPart
{
    private readonly string _content;

    public PlainText(string content) => _content = content;

    public string Content => _content;

    public override string Accept(DocumentVisitor visitor) => visitor.VisitPlainText(this);
}

[UsedImplicitly]
public sealed class Hyperlink : DocumentPart
{
    private readonly string _url;

    public Hyperlink(string url) => _url = url;

    public string Url => _url;

    public override string Accept(DocumentVisitor visitor) => visitor.VisitHyperlink(this);
}

// ConcreteVisitor: bundles one operation's logic for every element type.
[UsedImplicitly]
public sealed class HtmlExportVisitor : DocumentVisitor
{
    public override string VisitPlainText(PlainText text) => $"<p>{text.Content}</p>";

    public override string VisitHyperlink(Hyperlink link) => $"<a href=\"{link.Url}\">{link.Url}</a>";
}
