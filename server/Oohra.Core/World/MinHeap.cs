namespace Oohra.Core.World;

/// <summary>
/// Jednoduchá prioritní fronta (nejmenší priorita první) pro hledání cest. .NET Standard 2.1 nemá PriorityQueue.
/// Při stejné prioritě rozhoduje menší index, takže výsledek je vždy stejný.
/// </summary>
public sealed class MinHeap
{
    private readonly List<(int Item, double Priority)> _items = new();

    public int Count => _items.Count;

    public void Push(int item, double priority)
    {
        _items.Add((item, priority));
        var child = _items.Count - 1;
        while (child > 0)
        {
            var parent = (child - 1) / 2;
            if (!Less(child, parent))
            {
                break;
            }

            Swap(child, parent);
            child = parent;
        }
    }

    public (int Item, double Priority) Pop()
    {
        var top = _items[0];
        var last = _items.Count - 1;
        _items[0] = _items[last];
        _items.RemoveAt(last);
        var parent = 0;
        while (true)
        {
            var left = parent * 2 + 1;
            var right = left + 1;
            var smallest = parent;
            if (left < _items.Count && Less(left, smallest)) smallest = left;
            if (right < _items.Count && Less(right, smallest)) smallest = right;
            if (smallest == parent)
            {
                break;
            }

            Swap(parent, smallest);
            parent = smallest;
        }

        return top;
    }

    private bool Less(int a, int b)
    {
        var (x, y) = (_items[a], _items[b]);
        return x.Priority < y.Priority || (x.Priority == y.Priority && x.Item < y.Item);
    }

    private void Swap(int a, int b)
    {
        (_items[a], _items[b]) = (_items[b], _items[a]);
    }
}
