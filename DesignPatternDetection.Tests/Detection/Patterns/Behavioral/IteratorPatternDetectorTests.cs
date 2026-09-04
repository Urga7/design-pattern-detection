using DesignPatternDetection.Detection.Patterns.Behavioral;

namespace DesignPatternDetection.Tests.Detection.Patterns.Behavioral;

public class IteratorPatternDetectorTests
{
    private readonly IteratorPatternDetector _detector = new();

    private const string Bookshelf = """
    namespace Demo;

    public abstract class BookIterator
    {
        public abstract bool HasNext();
        public abstract string Next();
    }

    public sealed class ShelfIterator : BookIterator
    {
        private readonly BookShelf _shelf;
        private int _position;

        public ShelfIterator(BookShelf shelf) => _shelf = shelf;

        public override bool HasNext() => _position < _shelf.Count;
        public override string Next() => _shelf.BookAt(_position++);
    }

    public sealed class BookShelf
    {
        private readonly List<string> _books = new();

        public int Count => _books.Count;
        public string BookAt(int index) => _books[index];

        public BookIterator CreateIterator() => new ShelfIterator(this);
    }
    """;

    [Fact]
    public void Detects_the_aggregate_iterator_pair()
    {
        var graph = TestGraph.From(Bookshelf);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("BookShelf", match.Bindings["aggregate"]);
        Assert.Equal("BookIterator", match.Bindings["iterator"]);
        Assert.Equal("ShelfIterator", match.Bindings["concreteIterator"]);
    }

    [Fact]
    public void Detects_an_iterator_built_on_the_bcl_enumerator_protocol()
    {
        // Idiomatic C#: the source abstraction implements the BCL IEnumerator
        // protocol and the creation method declares that protocol - not the
        // source abstraction - as its return type (the RefactoringGuru shape).
        var graph = TestGraph.From("""
        using System.Collections;

        namespace Demo;

        public abstract class Iterator : IEnumerator
        {
            object IEnumerator.Current => Current();
            public abstract object Current();
            public abstract bool MoveNext();
            public abstract void Reset();
        }

        public sealed class AlphabeticalOrderIterator : Iterator
        {
            private readonly WordsCollection _collection;
            private int _position = -1;

            public AlphabeticalOrderIterator(WordsCollection collection) => _collection = collection;

            public override object Current() => _collection.WordAt(_position);
            public override bool MoveNext() { _position++; return _position < _collection.Count; }
            public override void Reset() => _position = -1;
        }

        public sealed class WordsCollection : IEnumerable
        {
            private readonly List<string> _words = new();

            public int Count => _words.Count;
            public string WordAt(int index) => _words[index];

            public IEnumerator GetEnumerator() => new AlphabeticalOrderIterator(this);
        }
        """);

        var match = Assert.Single(_detector.Detect(graph));
        Assert.Equal("WordsCollection", match.Bindings["aggregate"]);
        Assert.Equal("Iterator", match.Bindings["iterator"]);
        Assert.Equal("AlphabeticalOrderIterator", match.Bindings["concreteIterator"]);
    }

    [Fact]
    public void Ignores_a_factory_method_whose_product_never_wraps_its_creator()
    {
        // A creation method alone is Factory Method; the iterator's product
        // must point back at the aggregate it traverses.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Transport { public abstract string Deliver(); }
        public sealed class Truck : Transport { public override string Deliver() => "land"; }

        public abstract class Logistics
        {
            public abstract Transport CreateTransport();
        }

        public sealed class RoadLogistics : Logistics
        {
            public override Transport CreateTransport() => new Truck();
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_an_adapter_the_wrapped_class_never_creates()
    {
        // Wrapping alone is Adapter-shaped; without the aggregate handing
        // out its own wrapper the create-and-wrap loop never closes.
        var graph = TestGraph.From("""
        namespace Demo;

        public abstract class Notifier { public abstract string Send(string message); }

        public sealed class LegacyPager { public string Page(string text) => text; }

        public sealed class PagerAdapter : Notifier
        {
            private readonly LegacyPager _pager;
            public PagerAdapter(LegacyPager pager) => _pager = pager;
            public override string Send(string message) => _pager.Page(message);
        }
        """);

        Assert.Empty(_detector.Detect(graph));
    }

    [Fact]
    public void Ignores_a_flyweight_factory_pooling_a_concrete_class()
    {
        // The factory creates what it pools, but the pooled class extends no
        // abstract traversal protocol and never wraps the factory back.
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
}
