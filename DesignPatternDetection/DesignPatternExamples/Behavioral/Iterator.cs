using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Iterator: the abstract traversal protocol clients walk collections with.
[UsedImplicitly]
public abstract class BookIterator
{
    public abstract bool HasNext();

    public abstract string Next();
}

// ConcreteIterator: wraps the aggregate it walks and keeps its own cursor.
[UsedImplicitly]
public sealed class ShelfIterator : BookIterator
{
    private readonly BookShelf _shelf;
    private int _position;

    public ShelfIterator(BookShelf shelf) => _shelf = shelf;

    public override bool HasNext() => _position < _shelf.Count;

    public override string Next() => _shelf.BookAt(_position++);
}

// Aggregate: stores the elements and hands out the iterator that wraps it, keeping the traversal logic outside the collection itself.
[UsedImplicitly]
public sealed class BookShelf
{
    private readonly List<string> _books = [];

    public int Count => _books.Count;

    public void Add(string book) => _books.Add(book);

    public string BookAt(int index) => _books[index];

    public BookIterator CreateIterator() => new ShelfIterator(this);
}
