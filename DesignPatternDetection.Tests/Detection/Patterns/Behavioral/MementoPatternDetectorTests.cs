using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class MementoPatternDetectorTests
{
    private readonly MementoPatternDetector _detector = new();

    private const string UndoableEditor = """
    namespace Demo;

    public sealed class EditorMemento
    {
        private readonly string _text;
        public EditorMemento(string text) => _text = text;
        public string Text => _text;
    }

    public sealed class TextEditor
    {
        private string _text = "";
        public void Type(string text) => _text += text;
        public EditorMemento Save() => new EditorMemento(_text);
        public void Restore(EditorMemento memento) => _text = memento.Text;
    }

    public sealed class EditorHistory
    {
        private readonly List<EditorMemento> _history = new();
        public void Push(EditorMemento memento) => _history.Add(memento);
        public EditorMemento Pop()
        {
            var memento = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            return memento;
        }
    }
    """;

    [Fact]
    public void Detects_the_originator_memento_and_caretaker()
    {
        var graph = TestGraph.From(UndoableEditor);

        var matches = _detector.Detect(graph);

        var match = Assert.Single(matches);
        Assert.Equal("TextEditor", match.Bindings["originator"]);
        Assert.Equal("EditorMemento", match.Bindings["memento"]);
        Assert.Equal("EditorHistory", match.Bindings["caretaker"]);
    }

    [Fact]
    public void Ignores_a_flyweight_factory_that_pools_what_it_creates()
    {
        // A Flyweight factory also creates and returns an immutable class,
        // but keeps the instances in a pool; an originator hands its
        // snapshots out and lets go.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class TreeType
        {
            private readonly string _name;
            public TreeType(string name) => _name = name;
            public string Draw(int x, int y) => _name;
        }

        public sealed class TreeFactory
        {
            private readonly Dictionary<string, TreeType> _pool = new();

            public TreeType GetTreeType(string name)
            {
                if (!_pool.TryGetValue(name, out var type))
                {
                    type = new TreeType(name);
                    _pool[name] = type;
                }

                return type;
            }
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_builder_that_assembles_through_an_overridden_step()
    {
        // A ConcreteBuilder also instantiates and returns one type, but by
        // overriding an abstract construction step; an originator snapshots
        // through a method of its own.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class ReportBuilder
        {
            public abstract void AddSection(string title);
            public abstract Report Build();
        }

        public sealed class Report
        {
            private readonly string _body;
            public Report(string body) => _body = body;
        }

        public sealed class PdfReportBuilder : ReportBuilder
        {
            private string _body = "";
            public override void AddSection(string title) => _body += title;
            public override Report Build() => new Report(_body);
        }

        public sealed class ReportArchive
        {
            private readonly List<Report> _reports = new();
            public void Keep(Report report) => _reports.Add(report);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_store_that_creates_its_own_entries()
    {
        // A keeper that manufactures what it stores is a factory over a pool;
        // a caretaker only receives snapshots made by the originator.
        var graph = TestGraph.From("""
        namespace Demo;

        public sealed class Snapshot
        {
            private readonly string _data;
            public Snapshot(string data) => _data = data;
        }

        public sealed class SnapshotService
        {
            public Snapshot Capture(string data) => new Snapshot(data);
        }

        public sealed class SnapshotStore
        {
            private readonly List<Snapshot> _snapshots = new();
            public void Record(string data) => _snapshots.Add(new Snapshot(data));
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Detects_a_memento_concealed_behind_an_interface()
    {
        // Idiomatic C# hides the concrete snapshot behind a memento
        // interface: the originator returns and takes the abstraction while
        // instantiating the concrete memento.
        var graph = TestGraph.From("""
        namespace Demo;

        public interface IMemento { string GetState(); }

        public sealed class ConcreteMemento : IMemento
        {
            private string _state;
            public ConcreteMemento(string state) => _state = state;
            public string GetState() => _state;
        }

        public sealed class Originator
        {
            private string _state;
            public IMemento Save() => new ConcreteMemento(_state);
            public void Restore(IMemento memento) => _state = memento.GetState();
        }

        public sealed class Caretaker
        {
            private readonly List<IMemento> _mementos = new();
            public void Backup(IMemento memento) => _mementos.Add(memento);
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("Originator", match.Bindings["originator"]);
        Assert.Equal("IMemento", match.Bindings["memento"]);
        Assert.Equal("Caretaker", match.Bindings["caretaker"]);
    }
}
