using Oohra.Core.Data;
using Oohra.Core.Persistence;

namespace Oohra.Core.Social;

/// <summary>
/// Kmeny hráčů (návrh 14.1). Kmen vzniká pozvánkou: člen (nebo kdokoli bez kmene) pozve hráče poblíž,
/// ten přijme nebo odmítne. Žádná správa, žádná listina. Kmen patří hráči, ne postavě: přežije smrt.
/// Kmen, ve kterém zůstane jen jeden člověk, zanikne.
/// </summary>
public sealed class Tribes
{
    private readonly Dictionary<int, int> _tribeOf = [];
    private readonly Dictionary<int, Invitation> _invites = [];
    private int _nextTribe = 1;

    /// <summary>Roste s každou změnou členství (server podle toho pošle členům, kdo k nim patří).</summary>
    public int Version { get; private set; }

    /// <summary>Kdo patří do kterého kmene (uložení světa). Pozvánky se neukládají, za chvíli by stejně vypršely.</summary>
    public TribesSave Export()
    {
        return new TribesSave { TribeOf = new Dictionary<int, int>(_tribeOf), NextTribe = _nextTribe };
    }

    public void Restore(TribesSave saved)
    {
        foreach (var (player, tribe) in saved.TribeOf)
        {
            _tribeOf[player] = tribe;
        }

        _nextTribe = Math.Max(_nextTribe, saved.NextTribe);
        Version++;
    }

    /// <summary>Číslo kmene hráče, nebo null.</summary>
    public int? TribeOf(int playerId)
    {
        return _tribeOf.TryGetValue(playerId, out var tribe) ? tribe : null;
    }

    /// <summary>Členové kmene, ve kterém je hráč (bez kmene jen on sám).</summary>
    public IReadOnlyList<int> MembersWith(int playerId)
    {
        var tribe = TribeOf(playerId);
        return tribe is null ? [playerId] : _tribeOf.Where(p => p.Value == tribe).Select(p => p.Key).OrderBy(id => id).ToList();
    }

    /// <summary>Je to tentýž hráč, nebo jsou ve stejném kmeni? (Kdo smí sáhnout na čí stavbu a zásoby.)</summary>
    public bool SameTribe(int a, int b)
    {
        return a == b || (TribeOf(a) is { } tribe && TribeOf(b) == tribe);
    }

    /// <summary>Pozvat hráče do kmene. Pozvaný musí stát do inviteDistance, pozvánka platí inviteMinutes.</summary>
    public TribeResult Invite(int inviterId, double inviterX, double inviterY, int inviteeId, double inviteeX, double inviteeY, double now)
    {
        var rules = GameData.Tribes;
        if (SameTribe(inviterId, inviteeId))
        {
            return TribeResult.AlreadyTogether;
        }

        var distance = Math.Sqrt((inviterX - inviteeX) * (inviterX - inviteeX) + (inviterY - inviteeY) * (inviterY - inviteeY));
        if (distance > rules.InviteDistance)
        {
            return TribeResult.TooFar;
        }

        _invites[inviteeId] = new Invitation(inviterId, now + rules.InviteMinutes);
        return TribeResult.Invited;
    }

    /// <summary>
    /// Pozvaný odpoví. Když přijme, přejde do kmene toho, kdo ho zval (ze starého kmene odejde).
    /// Když zvoucí kmen neměl, vznikne nový.
    /// </summary>
    public TribeResult Answer(int inviteeId, int inviterId, bool accept, double now)
    {
        if (!_invites.TryGetValue(inviteeId, out var invite) || invite.InviterId != inviterId || now > invite.Until)
        {
            return TribeResult.NoInvite;
        }

        _invites.Remove(inviteeId);
        if (!accept)
        {
            return TribeResult.Declined;
        }

        if (SameTribe(inviterId, inviteeId))
        {
            return TribeResult.AlreadyTogether;
        }

        var tribe = TribeOf(inviterId) ?? NewTribe(inviterId);
        RemoveMember(inviteeId);
        _tribeOf[inviteeId] = tribe;
        Version++;
        return TribeResult.Joined;
    }

    public TribeResult Leave(int playerId)
    {
        if (TribeOf(playerId) is null)
        {
            return TribeResult.NotInTribe;
        }

        RemoveMember(playerId);
        Version++;
        return TribeResult.Left;
    }

    private int NewTribe(int founderId)
    {
        var tribe = _nextTribe++;
        _tribeOf[founderId] = tribe;
        return tribe;
    }

    /// <summary>Odebere člena. Když v kmeni zůstane jen jeden, kmen zanikne.</summary>
    private void RemoveMember(int playerId)
    {
        if (!_tribeOf.Remove(playerId, out var tribe))
        {
            return;
        }

        var rest = _tribeOf.Where(p => p.Value == tribe).Select(p => p.Key).ToList();
        if (rest.Count == 1)
        {
            _tribeOf.Remove(rest[0]);
        }
    }

    private sealed record Invitation(int InviterId, double Until);
}
