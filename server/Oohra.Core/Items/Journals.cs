using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Items;

/// <summary>Uložený deník: číslo a stránky (každá kresba 12 × 12 bodů).</summary>
public sealed record JournalSave(int Id, List<byte[]> Pages);

/// <summary>
/// Deníky (návrh 0.8, kap. 3): věc z kůže a lýka, ušitá jehlou. Má journalPages stránek, na každé kresba
/// 12 × 12 bodů (poznámky, kresby, mapy). Deník je věc jako každá jiná: po smrti zůstane u těla a kdo ho vezme,
/// může ho číst i do něj kreslit. Znalost tak přežije smrt. Obsah je tady podle čísla deníku (Item.Ref).
/// </summary>
public sealed class Journals
{
    private readonly Dictionary<int, byte[][]> _pages = [];
    private int _nextId = 1;

    /// <summary>Nový prázdný deník (věc s číslem).</summary>
    public Item Create()
    {
        var id = _nextId++;
        _pages[id] = Enumerable.Range(0, GameData.Crafting.JournalPages).Select(_ => SignBits.Empty()).ToArray();
        return new Item(ItemKind.Journal, Ref: id);
    }

    /// <summary>Stránky deníku, nebo null (neznámé číslo).</summary>
    public IReadOnlyList<byte[]>? Pages(int journalId)
    {
        return _pages.TryGetValue(journalId, out var pages) ? pages : null;
    }

    /// <summary>Nakreslí stránku (celá kresba se nahradí). Vrací false, když deník nebo stránka není.</summary>
    public bool Draw(int journalId, int page, byte[] bits)
    {
        if (!_pages.TryGetValue(journalId, out var pages) || page < 0 || page >= pages.Length || bits.Length != SignBits.ByteCount)
        {
            return false;
        }

        pages[page] = (byte[])bits.Clone();
        return true;
    }

    public List<JournalSave> Export()
    {
        return _pages.Select(p => new JournalSave(p.Key, p.Value.Select(b => (byte[])b.Clone()).ToList())).ToList();
    }

    public int NextId => _nextId;

    public void Restore(IEnumerable<JournalSave> saved, int nextId)
    {
        foreach (var journal in saved)
        {
            _pages[journal.Id] = journal.Pages.ToArray();
        }

        _nextId = Math.Max(nextId, _pages.Count == 0 ? 1 : _pages.Keys.Max() + 1);
    }
}
