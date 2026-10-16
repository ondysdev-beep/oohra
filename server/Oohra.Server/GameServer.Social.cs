using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Social;
using Oohra.Core.Time;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>Těla, podpisy, nahlášení, kmeny a zásoby.</summary>
public sealed partial class GameServer
{
    // ---------- těla a podpisy ----------

    /// <summary>Postava zemřela: na místě zůstane tělo s věcmi z batohu (návrh 7.4), batoh je prázdný.</summary>
    private void LeaveBody(PlayerSession player)
    {
        player.BodyLeft = true;
        PlayStats.Died(player.Nickname, player.Character.DeathCause, Clock.TotalMinutes - player.BornAt);
        Island.Corpses.Add(player.X, player.Y, player.Id, player.Character.Traits.Generation, Clock.TotalMinutes, player.Inventory, player.Signature);
        SendInventory(player);
    }

    private void CorpseAction(PlayerSession player, int corpseId, int stackIndex)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = Island.Corpses.Take(corpseId, stackIndex, player.Inventory, player.X, player.Y, player.Id, Tribes, Clock.TotalMinutes);
        SendEvent(player, new GameEvent(EventKind.Corpse, (byte)result, default, []));
        SendInventory(player);
    }

    // ---------- tlačítko Nahlásit ----------

    /// <summary>
    /// Hráč něco nahlásil (návrh 14.3). Server zapíše záznam do souboru a do konzole, rozhoduje správce.
    /// Jedno nahlášení za reportCooldownSeconds, sebe nahlásit nejde.
    /// </summary>
    private void Report(PlayerSession reporter, (ReportTarget Target, int TargetId, ReportReason Reason) report)
    {
        if (!Enum.IsDefined(report.Target) || !Enum.IsDefined(report.Reason))
        {
            return;
        }

        if (_seconds - reporter.LastReportAt < _config.ReportCooldownSeconds)
        {
            SendEvent(reporter, new GameEvent(EventKind.Report, (byte)ReportResult.TooOften, default, []));
            return;
        }

        var entry = ReportEntryFor(reporter, report.Target, report.TargetId, report.Reason);
        if (entry is null)
        {
            SendEvent(reporter, new GameEvent(EventKind.Report, (byte)ReportResult.NotFound, default, []));
            return;
        }

        reporter.LastReportAt = _seconds;
        Reports.Write(entry);
        var what = entry.Player ?? (entry.SignId is { } sign ? $"značku č. {sign}" : "problém");
        Log?.Invoke($"NAHLÁŠENÍ: {reporter.Nickname} nahlásil {what} ({entry.Reason}). Záznam je v {Reports.Path}.");
        SendEvent(reporter, new GameEvent(EventKind.Report, (byte)ReportResult.Sent, default, []));
    }

    /// <summary>Záznam pro správce: kdo, kdy, kde, koho nebo co. Null, když cíl není (nebo je to sám nahlašující).</summary>
    private ReportEntry? ReportEntryFor(PlayerSession reporter, ReportTarget target, int targetId, ReportReason reason)
    {
        var entry = new ReportEntry
        {
            Time = DateTime.UtcNow,
            GameMinutes = Math.Round(Clock.TotalMinutes, 1),
            Reporter = reporter.Nickname,
            ReporterId = reporter.Id,
            ReporterX = Math.Round(reporter.X, 2),
            ReporterY = Math.Round(reporter.Y, 2),
            Target = target.ToString(),
            Reason = reason.ToString(),
        };

        switch (target)
        {
            case ReportTarget.Player:
                var player = _byNickname.Values.FirstOrDefault(p => p.Id == targetId);
                return player is null || player.Id == reporter.Id ? null : entry with
                {
                    Player = player.Nickname,
                    PlayerId = player.Id,
                    PlayerX = Math.Round(player.X, 2),
                    PlayerY = Math.Round(player.Y, 2),
                    PlayerOnline = player.Online,
                };
            case ReportTarget.Sign:
                var sign = Island.Signs.Find(targetId);
                return sign is null ? null : entry with
                {
                    SignId = sign.Id,
                    SignX = Math.Round(sign.X, 2),
                    SignY = Math.Round(sign.Y, 2),
                    SignAuthor = _byNickname.Values.FirstOrDefault(p => p.Id == sign.AuthorId)?.Nickname,
                    SignRows = SignRows(sign.Bits),
                };
            default:
                return entry;
        }
    }

    /// <summary>Kresba značky jako 12 řádků textu (# = bod, . = nic), aby ji správce viděl v souboru.</summary>
    private static string[] SignRows(byte[] bits)
    {
        var text = SignBits.ToText(bits);
        return Enumerable.Range(0, SignBits.Size)
            .Select(row => text.Substring(row * SignBits.Size, SignBits.Size).Replace('1', '#').Replace('0', '.'))
            .ToArray();
    }

    /// <summary>Podpis: jméno ze slabik (1 až 3) a znak 12 × 12. Neplatný se ignoruje.</summary>
    private void SetSignature(PlayerSession player, (byte[] Name, byte[] Mark) signature)
    {
        if (!Signature.IsValid(signature.Name, signature.Mark))
        {
            return;
        }

        player.Signature = new Signature(signature.Name, signature.Mark);
        SendSignature(player);
    }

    private void SendSignature(PlayerSession player)
    {
        var signature = player.Signature;
        SendTo(player.Peer!, w => Messages.WriteSignature(w, signature?.Name ?? [], signature?.Mark ?? []));
    }

    private CorpseView[] CorpseViews()
    {
        return Island.Corpses.All.Select(c => new CorpseView(
            c.Id, (float)c.X, (float)c.Y, c.OwnerId, c.Generation, c.DiedAt, c.TribeOnlyUntil,
            new InventoryView(c.Items.Capacity, c.Items.Stacks.Select(StackViewOf).ToArray()),
            c.Signature?.Name ?? [], c.Signature?.Mark ?? [])).ToArray();
    }

    // ---------- kmeny a zásoby ----------

    /// <summary>
    /// Pozvání do kmene (návrh 14.1): okolí vidí gesto, pozvaný dostane pozvánku a odpoví.
    /// </summary>
    private void InviteToTribe(PlayerSession inviter, int inviteeId)
    {
        var invitee = _byPeer.Values.FirstOrDefault(p => p.Id == inviteeId);
        if (inviter.Character.IsDead || invitee is null || invitee == inviter)
        {
            return;
        }

        var result = Tribes.Invite(inviter.Id, inviter.X, inviter.Y, invitee.Id, invitee.X, invitee.Y, Clock.TotalMinutes);
        SendEvent(inviter, TribeEvent(result));
        if (result == TribeResult.Invited)
        {
            MakeGesture(inviter, (Gesture.Invite, (float)invitee.X, (float)invitee.Y));
            SendTo(invitee.Peer!, w => Messages.WriteTribeInvite(w, inviter.Id));
        }
    }

    private void AnswerInvite(PlayerSession invitee, (int InviterId, bool Accept) answer)
    {
        var result = Tribes.Answer(invitee.Id, answer.InviterId, answer.Accept, Clock.TotalMinutes);
        SendEvent(invitee, TribeEvent(result));
        var inviter = _byPeer.Values.FirstOrDefault(p => p.Id == answer.InviterId);
        if (inviter is not null && result is TribeResult.Joined or TribeResult.Declined)
        {
            SendEvent(inviter, TribeEvent(result == TribeResult.Joined ? TribeResult.TheyJoined : TribeResult.TheyDeclined));
        }
    }

    private void StockAction(PlayerSession player, Func<TribeResult> action)
    {
        if (!player.Character.IsDead)
        {
            SendEvent(player, TribeEvent(action()));
            SendInventory(player);
        }
    }

    private static GameEvent TribeEvent(TribeResult result)
    {
        return new GameEvent(EventKind.Tribe, (byte)result, default, []);
    }

    /// <summary>Pošle hráči jeho kmen a zásoby, když se od posledně něco změnilo.</summary>
    private void SendTribeState(PlayerSession player)
    {
        if (Tribes.Version != player.SentTribeVersion)
        {
            player.SentTribeVersion = Tribes.Version;
            var members = Tribes.TribeOf(player.Id) is null ? [] : Tribes.MembersWith(player.Id);
            SendTo(player.Peer!, w => Messages.WriteTribe(w, members));
        }

        var stockKey = $"{Island.Structures.Version}/{Island.Structures.StockVersion}/{Tribes.Version}/{_stockSpoilage}";
        if (stockKey != player.SentStockKey)
        {
            player.SentStockKey = stockKey;
            var stockpiles = Island.Structures.All
                .Where(s => s.Store is not null && Structures.MayTouch(s, player.Id, Tribes))
                .Select(s => new StockpileView(s.Id, new InventoryView(s.Store!.Capacity, s.Store.Stacks.Select(StackViewOf).ToArray())))
                .ToArray();
            SendTo(player.Peer!, w => Messages.WriteStockpiles(w, stockpiles));
        }
    }

    /// <summary>Které věci v zásobách jsou zkažené (když se něco zkazí, zásoby se pošlou znovu).</summary>
    private string StockSpoilage()
    {
        return string.Concat(Island.Structures.All.Where(s => s.Store is not null)
            .SelectMany(s => s.Store!.Stacks.Select(i => Spoilage.IsSpoiled(i.Item, Clock.TotalMinutes) ? '1' : '0')));
    }
}
