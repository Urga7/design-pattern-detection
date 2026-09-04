using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Memento: an immutable snapshot of the editor's state, opaque to everyone but the originator that made it.
[UsedImplicitly]
public sealed class EditorMemento
{
    private readonly string _text;

    public EditorMemento(string text) => _text = text;

    public string Text => _text;
}

// Originator: owns the mutable state, snapshots it on demand and restores it
// from a snapshot handed back - it creates mementos but never keeps them.
[UsedImplicitly]
public sealed class TextEditor
{
    private string _text = "";

    public void Type(string text) => _text += text;

    public EditorMemento Save() => new EditorMemento(_text);

    public void Restore(EditorMemento memento) => _text = memento.Text;
}

// Caretaker: keeps the history of snapshots without ever creating or looking inside one.
[UsedImplicitly]
public sealed class EditorHistory
{
    private readonly List<EditorMemento> _history = [];

    public void Push(EditorMemento memento) => _history.Add(memento);

    public EditorMemento Pop()
    {
        var memento = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        return memento;
    }
}
