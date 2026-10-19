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

/// <summary>Úmysly hráčů: příjem zpráv, sběr, jídlo, stavby, oheň, lov, nové narození.</summary>
public sealed partial class GameServer
{
    // ---------- úmysly hráčů ----------

    /// <summary>
    /// Zpráva od klienta. Chyba při jejím zpracování (třeba poškozená zpráva) se jen zapíše do konzole,
    /// server kvůli ní nespadne a ostatní hrají dál.
    /// </summary>
    private void OnReceive(NetPeer peer, NetDataReader reader)
    {
        try
        {
            HandleMessage(peer, reader);
        }
        catch (Exception error)
        {
            var who = _byPeer.TryGetValue(peer.Id, out var player) ? player.Nickname : "spojení " + peer.Id;
            Log?.Invoke($"Chyba při zprávě od {who}: {error}");
        }
    }

    private void HandleMessage(NetPeer peer, NetDataReader reader)
    {
        var type = (MessageType)reader.GetByte();
        if (type == MessageType.Login)
        {
            if (!_byPeer.ContainsKey(peer.Id))
            {
                HandleLogin(peer, reader);
            }

            return;
        }

        if (!_byPeer.TryGetValue(peer.Id, out var player))
        {
            return;
        }

        if (type != MessageType.Input)
        {
            // Pohyb chodí každý snímek, ten se nepočítá.
            PlayStats.Intent(player.Nickname, type);
        }

        if (player.Character.Sleeping && type is not (MessageType.Input or MessageType.Sleep))
        {
            // Spící postava nic nedělá. Probudí ji pohyb nebo tlačítko Vstát.
            return;
        }

        switch (type)
        {
            case MessageType.Input:
                SetInput(player, Messages.ReadInput(reader));
                break;
            case MessageType.Sleep:
                ToggleSleep(player);
                break;
            case MessageType.Harvest:
                Harvest(player, Messages.ReadHarvest(reader));
                break;
            case MessageType.Drink:
                Drink(player, Messages.ReadDrink(reader));
                break;
            case MessageType.Eat:
            case MessageType.Taste:
            case MessageType.Drop:
                UseStack(player, type, Messages.ReadStackAction(reader));
                break;
            case MessageType.Reborn:
                Reborn(player, Messages.ReadReborn(reader));
                break;
            case MessageType.GatherSticks:
            case MessageType.TakeStone:
                GatherMaterial(player, type, Messages.ReadTileAction(reader));
                break;
            case MessageType.ChopTree:
                var tree = Messages.ReadTileAction(reader);
                WoodAction(player, () => Island.Forestry.Chop(Island, tree.TileX, tree.TileY, player.Id, player.X, player.Y, player.Inventory));
                break;
            case MessageType.ChopTimber:
                var timber = Messages.ReadTimberAction(reader);
                WoodAction(player, () => Island.Forestry.ChopTimber(timber, player.Id, player.X, player.Y, player.Inventory));
                break;
            case MessageType.PickUpTimber:
                var piece = Messages.ReadTimberAction(reader);
                WoodAction(player, () => Island.Forestry.PickUp(piece, player.X, player.Y, player.Inventory));
                break;
            case MessageType.DropTimber:
                WoodAction(player, () => Island.Forestry.Drop(Island, player.Inventory, player.X, player.Y, player.Facing));
                break;
            case MessageType.SplitWood:
                var wood = Messages.ReadStackAction(reader);
                WoodAction(player, () => Forestry.SplitWood(player.Inventory, wood));
                break;
            case MessageType.MakeAxe:
                CraftAction(player, "axe");
                break;
            case MessageType.Craft:
                CraftAction(player, Messages.ReadCraft(reader));
                break;
            case MessageType.Smear:
                var ash = Messages.ReadSmear(reader);
                if (!Hazards.Smear(player.Character, Island, player.Inventory, player.X, player.Y, ash, Clock.TotalMinutes))
                {
                    SendEvent(player, new GameEvent(EventKind.Craft, (byte)(ash ? CraftResult.NeedMaterials : CraftResult.NeedWater), default, []));
                }

                SendBodyEvents(player);
                SendInventory(player);
                SendSelfState(player);
                break;
            case MessageType.RemoveLeeches:
                if (!Hazards.RemoveLeeches(player.Character, player.Inventory, Fires.WarmsAt(Island, player.X, player.Y), Clock.TotalMinutes))
                {
                    SendEvent(player, new GameEvent(EventKind.Craft, (byte)CraftResult.NeedFire, default, []));
                }

                SendBodyEvents(player);
                SendInventory(player);
                SendSelfState(player);
                break;
            case MessageType.InspectBody:
                if (!player.Character.IsDead)
                {
                    Hazards.Inspect(player.Character, Clock.TotalMinutes);
                    SendBodyEvents(player);
                }

                break;
            case MessageType.SmokeOutNest:
                var nest = Messages.ReadSmokeOutNest(reader);
                if (!player.Character.IsDead)
                {
                    var smoked = Island.Wasps.SmokeOut(nest, player.Character, player.X, player.Y, Clock.TotalMinutes);
                    SendEvent(player, new GameEvent(EventKind.Wasp, (byte)smoked, default, []));
                    SendBodyEvents(player);
                }

                break;
            case MessageType.TeachRecipe:
                TeachAction(player, Messages.ReadTeachRecipe(reader));
                break;
            case MessageType.ReadJournal:
                SendJournal(player, Messages.ReadStackAction(reader));
                break;
            case MessageType.DrawJournal:
                var page = Messages.ReadDrawJournal(reader);
                if (JournalOf(player, page.StackIndex) is { } drawn && Journals.Draw(drawn, page.Page, page.Bits))
                {
                    SendJournal(player, page.StackIndex);
                }

                break;
            case MessageType.WearBag:
                player.Inventory.WearBag(Messages.ReadStackAction(reader));
                SendInventory(player);
                break;
            case MessageType.TakeOffBag:
                player.Inventory.TakeOffBag();
                SendInventory(player);
                break;
            case MessageType.Combine:
                CombineAction(player, Messages.ReadCombine(reader));
                break;
            case MessageType.StripBark:
                var bark = Messages.ReadTimberAction(reader);
                WoodAction(player, () => Island.Forestry.StripBark(bark, player.X, player.Y, player.Inventory));
                break;
            case MessageType.TakeAsh:
                var ashFire = Messages.ReadTimberAction(reader);
                FireAction(player, () => Fires.TakeAsh(ashFire, player.Inventory, player.X, player.Y, Clock.TotalMinutes));
                break;
            case MessageType.FeedFire:
                var fireId = Messages.ReadFeedFire(reader);
                FireAction(player, () => Fires.Feed(fireId, player.Inventory, player.X, player.Y));
                break;
            case MessageType.Bake:
                var stackIndex = Messages.ReadStackAction(reader);
                FireAction(player, () => Fires.Bake(Island, player.Inventory, stackIndex, player.X, player.Y, player.Character));
                break;
            case MessageType.Place:
                Place(player, Messages.ReadPlace(reader));
                break;
            case MessageType.RemoveStructure:
                var structureId = Messages.ReadRemoveStructure(reader);
                BuildAction(player, () => Island.Structures.Remove(structureId, player.Inventory, player.X, player.Y, player.Id, Tribes));
                break;
            case MessageType.Attack:
                var target = Messages.ReadAnimalAction(reader);
                HuntAction(player, target, () => Animals.Attack(target, new AnimalTarget(player.Id, player.X, player.Y, player.Character), player.Inventory, Clock.TotalMinutes));
                break;
            case MessageType.Butcher:
                var corpse = Messages.ReadAnimalAction(reader);
                HuntAction(player, corpse, () => Animals.Butcher(corpse, player.Inventory, player.X, player.Y, Clock.TotalMinutes));
                break;
            case MessageType.MakeSpear:
                CraftAction(player, "spear");
                break;
            case MessageType.Fish:
                var (waterX, waterY) = Messages.ReadTileAction(reader);
                HuntAction(player, -1, () => Animals.Fish(Island, waterX, waterY, new AnimalTarget(player.Id, player.X, player.Y, player.Character), player.Inventory, Clock.TotalMinutes));
                break;
            case MessageType.TakeCatch:
                var trapId = Messages.ReadAnimalAction(reader);
                HuntAction(player, -1, () => Island.Structures.TakeCatch(trapId, player.Inventory, player.X, player.Y, Clock.TotalMinutes, player.Id, Tribes));
                break;
            case MessageType.Say:
                Say(player, Messages.ReadSay(reader));
                break;
            case MessageType.Gesture:
                MakeGesture(player, Messages.ReadGesture(reader));
                break;
            case MessageType.DrawSign:
                var (signX, signY, bits) = Messages.ReadDrawSign(reader);
                SignAction(player, () => Island.Signs.Draw(Island, player.Id, player.X, player.Y, signX, signY, bits, Clock.TotalMinutes, player.Signature, player.Character.Traits.IsAncient));
                break;
            case MessageType.EraseSign:
                var signId = Messages.ReadEraseSign(reader);
                SignAction(player, () => Island.Signs.Erase(signId, player.Id, player.X, player.Y, Tribes));
                break;
            case MessageType.TakeFromCorpse:
                var (corpseId, corpseIndex) = Messages.ReadStockAction(reader);
                CorpseAction(player, corpseId, corpseIndex);
                break;
            case MessageType.SetSignature:
                SetSignature(player, Messages.ReadSetSignature(reader));
                break;
            case MessageType.Report:
                Report(player, Messages.ReadReport(reader));
                break;
            case MessageType.DreamOver:
                player.DreamUntil = double.NegativeInfinity;
                break;
            case MessageType.LightTorch:
                FireAction(player, () => Torch.Light(Island, Fires, player.Character, player.Inventory, player.X, player.Y));
                break;
            case MessageType.ShowItem:
                ShowItem(player, Messages.ReadShowItem(reader));
                break;
            case MessageType.GiveToStranger:
                var give = Messages.ReadGiveToStranger(reader);
                StrangerAction(player, () => Strangers.Give(Island, player.Id, give.StrangerId, player.X, player.Y, player.Inventory, give.StackIndex, Clock));
                break;
            case MessageType.InviteToTribe:
                InviteToTribe(player, Messages.ReadInviteToTribe(reader));
                break;
            case MessageType.AnswerInvite:
                AnswerInvite(player, Messages.ReadAnswerInvite(reader));
                break;
            case MessageType.LeaveTribe:
                SendEvent(player, TribeEvent(Tribes.Leave(player.Id)));
                break;
            case MessageType.StoreItem:
                var (storeId, storeIndex) = Messages.ReadStockAction(reader);
                StockAction(player, () => Island.Structures.StoreItem(storeId, player.Inventory, storeIndex, player.X, player.Y, player.Id, Tribes));
                break;
            case MessageType.TakeItem:
                var (takeId, takeIndex) = Messages.ReadStockAction(reader);
                StockAction(player, () => Island.Structures.TakeItem(takeId, player.Inventory, takeIndex, player.X, player.Y, player.Id, Tribes));
                break;
            case MessageType.SetMeaning:
                var (word, meaning) = Messages.ReadSetMeaning(reader);
                if (Words.IsValid(word) && (meaning is null || Enum.IsDefined(meaning.Value)))
                {
                    player.Character.Vocabulary.SetMeaning(word, meaning);
                }

                break;
        }
    }

    /// <summary>Režim stavby: zeď jde do staveb, oheň mezi ohně. Kam a pod jakým úhlem, určil hráč.</summary>
    private void Place(PlayerSession player, (BuildKind Kind, float X, float Y, float Angle, float Length, string Decoration) place)
    {
        if (place.Kind == BuildKind.Campfire)
        {
            FireAction(player, () => Fires.Light(Island, player.Inventory, player.X, player.Y, place.X, place.Y, player.Character));
            return;
        }

        if (place.Kind != BuildKind.Door && GuardedBirthPlace(player, place.Kind, place.X, place.Y, place.Angle, place.Length))
        {
            BuildAction(player, () => BuildResult.BirthPlaceProtected);
            return;
        }

        BuildAction(player, () => Island.Structures.Build(
            Island, Fires, player.Inventory, player.X, player.Y, place.Kind, place.X, place.Y, place.Angle, place.Length, PlayerPositions(), player.Character, player.Id, place.Decoration));
    }

    /// <summary>Zasahuje stavba do cizího chráněného místa narození? U zdi se hlídá střed i oba konce.</summary>
    private bool GuardedBirthPlace(PlayerSession player, BuildKind kind, double x, double y, double angle, double length)
    {
        var points = new List<Position> { new(x, y) };
        if (kind is BuildKind.StickWall or BuildKind.DoorFrame)
        {
            var ends = Structure.WallEnds(x, y, angle, kind == BuildKind.StickWall ? length : GameData.Building.Wall.Length);
            points.Add(new Position(ends.X1, ends.Y1));
            points.Add(new Position(ends.X2, ends.Y2));
        }

        return points.Any(p => Island.BirthGuards.Blocking(p.X, p.Y, player.Id, Tribes, Clock.TotalMinutes) is not null);
    }

    /// <summary>Lov, stažení masa, výroba kopí. Hláška nese druh zvířete (pro text „Divočák padl.“).</summary>
    private void HuntAction(PlayerSession player, int animalId, Func<HuntResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var kind = Animals.Find(animalId)?.Kind ?? AnimalKind.Rabbit;
        var result = action();
        SendEvent(player, new GameEvent(EventKind.Hunt, (byte)result, default, [], Subject: (byte)kind));
        SendInventory(player);
        SendSelfState(player);
    }

    /// <summary>Výroba podle receptu (i neznámého: může se nepovést). V hlášce je číslo receptu.</summary>
    private void CraftAction(PlayerSession player, string recipeId)
    {
        var recipe = GameData.Crafting.Find(recipeId);
        if (player.Character.IsDead || recipe is null)
        {
            return;
        }

        var result = Crafting.Make(recipe, player.Inventory, player.Character, PlaceOf(player));
        SendCraftResult(player, result, recipe);
    }

    /// <summary>Zkoušení: spojit vybrané hromádky. Když z nich nejde žádný recept, nic se nestane.</summary>
    private void CombineAction(PlayerSession player, int[] stackIndices)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var (result, recipe) = Crafting.Combine(stackIndices, player.Inventory, player.Character, PlaceOf(player));
        SendCraftResult(player, result, recipe);
    }

    private void SendCraftResult(PlayerSession player, CraftResult result, Recipe? recipe)
    {
        var subject = recipe is null ? (byte)255 : (byte)GameData.Crafting.IndexOf(recipe);
        SendEvent(player, new GameEvent(EventKind.Craft, (byte)result, default, [], Subject: subject));
        SendInventory(player);
        SendRecipes(player);
    }

    /// <summary>Ukázat jinému hráči, jak se dělá recept. Oba dostanou hlášku, ten druhý i nový seznam receptů.</summary>
    private void TeachAction(PlayerSession teacher, (int PlayerId, string RecipeId) teach)
    {
        var recipe = GameData.Crafting.Find(teach.RecipeId);
        var learner = InWorld.FirstOrDefault(p => p.Id == teach.PlayerId);
        if (teacher.Character.IsDead || recipe is null || learner is null || learner == teacher)
        {
            return;
        }

        var distance = Math.Sqrt((teacher.X - learner.X) * (teacher.X - learner.X) + (teacher.Y - learner.Y) * (teacher.Y - learner.Y));
        var result = Crafting.Teach(teacher.Character, learner.Character, recipe.Id, distance);
        var subject = (byte)GameData.Crafting.IndexOf(recipe);
        SendEvent(teacher, new GameEvent(EventKind.Craft, (byte)result, default, [], Subject: subject));
        if (result == CraftResult.Taught)
        {
            SendRecipes(learner);
            SendEvent(learner, new GameEvent(EventKind.Craft, (byte)CraftResult.LearnedFromOther, default, [], Subject: subject));
        }
    }

    /// <summary>Číslo deníku na hromádce v batohu hráče, nebo null (není to deník).</summary>
    private static int? JournalOf(PlayerSession player, int stackIndex)
    {
        var stacks = player.Inventory.Stacks;
        return stackIndex >= 0 && stackIndex < stacks.Count && stacks[stackIndex].Item is { Kind: ItemKind.Journal, Ref: > 0 } item ? item.Ref : null;
    }

    /// <summary>Pošle hráči obsah deníku, který drží (jen svému hráči, deník čte jen ten, kdo ho má).</summary>
    private void SendJournal(PlayerSession player, int stackIndex)
    {
        if (player.Peer is null || JournalOf(player, stackIndex) is not { } id || Journals.Pages(id) is not { } pages)
        {
            return;
        }

        var view = new JournalView(stackIndex, pages.ToArray());
        SendTo(player.Peer, w => Messages.WriteJournal(w, view));
    }

    /// <summary>Kde hráč stojí: u sladké nebo bahenní vody (činění) a u ohně.</summary>
    private CraftPlace PlaceOf(PlayerSession player)
    {
        var reach = GameData.Gathering.Reach.Water;
        var nearWater = false;
        var (cx, cy) = ((int)Math.Floor(player.X), (int)Math.Floor(player.Y));
        for (var y = cy - 2; y <= cy + 2 && !nearWater; y++)
        {
            for (var x = cx - 2; x <= cx + 2; x++)
            {
                if (Island.InBounds(x, y) && Island.TileAt(x, y) is TileType.Pond or TileType.Ford or TileType.Bog
                    && Math.Sqrt((x + 0.5 - player.X) * (x + 0.5 - player.X) + (y + 0.5 - player.Y) * (y + 0.5 - player.Y)) <= reach)
                {
                    nearWater = true;
                    break;
                }
            }
        }

        return new CraftPlace(nearWater, Fires.WarmsAt(Island, player.X, player.Y), Clock.TotalMinutes, Journals);
    }

    /// <summary>Kácení, dřevo a sekera. Stav stromů a dřeva na zemi se všem pošle v příštím ticku.</summary>
    private void WoodAction(PlayerSession player, Func<WoodResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = action();
        SendEvent(player, new GameEvent(EventKind.Wood, (byte)result, default, []));
        SendInventory(player);
    }

    /// <summary>Stavba: postavit nebo rozebrat. Seznam staveb se všem pošle v příštím ticku.</summary>
    private void BuildAction(PlayerSession player, Func<BuildResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = action();
        SendEvent(player, new GameEvent(EventKind.Build, (byte)result, default, []));
        SendInventory(player);
    }

    private void SignAction(PlayerSession player, Func<SignResult> action)
    {
        if (!player.Character.IsDead)
        {
            SendEvent(player, new GameEvent(EventKind.Sign, (byte)action(), default, []));
        }
    }

    /// <summary>Kde stojí hráči ve hře (zeď se nesmí postavit na nikoho z nich).</summary>
    private List<Position> PlayerPositions()
    {
        return InWorld.Select(p => new Position(p.X, p.Y)).ToList();
    }

    private void GatherMaterial(PlayerSession player, MessageType type, (int TileX, int TileY) tile)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = type == MessageType.GatherSticks
            ? Materials.GatherSticks(Island, tile.TileX, tile.TileY, Clock, player.Inventory, _rng, player.X, player.Y)
            : Materials.TakeStone(Island, tile.TileX, tile.TileY, player.Inventory, player.X, player.Y, Clock.TotalMinutes);
        SendEvent(player, new GameEvent(EventKind.Material, (byte)result, default, []));
        if (result is MaterialResult.GotSticks or MaterialResult.GotStone)
        {
            SendInventory(player);
        }
    }

    /// <summary>Oheň: rozdělat, přiložit nebo péct. Seznam ohňů se ostatním pošle v příštím ticku.</summary>
    private void FireAction(PlayerSession player, Func<FireResult> action)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = action();
        SendEvent(player, new GameEvent(EventKind.Fire, (byte)result, default, []));
        SendInventory(player);
    }

    /// <summary>Směr chůze. Když spící postava dostane pokyn k chůzi, probudí se.</summary>
    private void SetInput(PlayerSession player, (float DirX, float DirY) input)
    {
        var length = Math.Sqrt(input.DirX * input.DirX + input.DirY * input.DirY);
        player.InputX = length > 0.01 ? input.DirX / length : 0;
        player.InputY = length > 0.01 ? input.DirY / length : 0;
        if (length > 0.01 && player.Character.Sleeping)
        {
            player.Character.WakeUp(Clock.TotalMinutes, BodyEvent.WokeUp);
            SendBodyEvents(player);
            SendSelfState(player);
        }
    }

    /// <summary>Spát, nebo vstát. Usnout jde jen unavený a v teple (rozhodne Core).</summary>
    private void ToggleSleep(PlayerSession player)
    {
        var c = player.Character;
        if (c.IsDead)
        {
            return;
        }

        if (c.Sleeping)
        {
            c.WakeUp(Clock.TotalMinutes, BodyEvent.WokeUp);
        }
        else if (c.TryFallAsleep(Clock.TotalMinutes))
        {
            player.InputX = player.InputY = 0;
        }

        SendBodyEvents(player);
        SendSelfState(player);
    }

    private void Harvest(PlayerSession player, (int PlantId, PlantPart Part) request)
    {
        if (player.Character.IsDead || request.PlantId < 0 || request.PlantId >= Island.Plants.Count)
        {
            return;
        }

        var result = Gathering.Harvest(Island, Plants, Island.Plants[request.PlantId], request.Part, Clock, player.Inventory, player.X, player.Y, player.Character);
        SendEvent(player, new GameEvent(EventKind.Harvest, (byte)result, request.Part, []));
        if (result == HarvestResult.Picked)
        {
            SendInventory(player);
            BroadcastPlantChanges();
        }
    }

    private void Drink(PlayerSession player, (int TileX, int TileY) request)
    {
        if (player.Character.IsDead)
        {
            return;
        }

        var result = Drinking.Drink(player.Character, Island, request.TileX, request.TileY, player.X, player.Y, Clock);
        SendEvent(player, new GameEvent(EventKind.Drink, (byte)result, default, []));
        SendSelfState(player);
    }

    private void UseStack(PlayerSession player, MessageType action, int index)
    {
        var inventory = player.Inventory;
        if (player.Character.IsDead || index < 0 || index >= inventory.Stacks.Count)
        {
            return;
        }

        var stack = inventory.Stacks[index].Item;
        var item = stack.Plant;
        var spoiled = Spoilage.IsSpoiled(stack, Clock.TotalMinutes);
        switch (action)
        {
            case MessageType.Eat when item is not null:
                inventory.TakeOne(index);
                Eating.Eat(player.Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock, spoiled);
                break;
            case MessageType.Eat when stack.IsFlesh:
                inventory.TakeOne(index);
                Eating.EatMeat(player.Character, stack.Cooking, Clock, spoiled: spoiled);
                break;
            case MessageType.Taste when stack.IsFlesh:
                Eating.EatMeat(player.Character, stack.Cooking, Clock, taste: true, spoiled: spoiled);
                break;
            case MessageType.Taste when item is not null:
                Eating.Taste(player.Character, item, Island.SpeciesSecrets[item.SpeciesId], Clock, spoiled);
                break;
            case MessageType.Drop when stack.Kind == ItemKind.LogPiece:
                // Klad nezmizí, položí se na zem.
                WoodAction(player, () => Island.Forestry.Drop(Island, inventory, player.X, player.Y, player.Facing));
                break;
            case MessageType.Drop:
                inventory.Drop(index);
                break;
        }

        SendInventory(player);
        SendBodyEvents(player);
        SendSelfState(player);
    }

    /// <summary>
    /// Po smrti: aktuální generace světa, aspoň Nový (návrh 8.2 a 8.6), s prázdným batohem. Narodí se v divočině daleko od místa smrti,
    /// nebo, když si to hráč vybral, na okraji území svého kmene (návrh 4.1).
    /// </summary>
    private void Reborn(PlayerSession player, BirthPlace place)
    {
        if (!player.Character.IsDead)
        {
            return;
        }

        var generation = GenerationTraits.AfterDeath(WorldGeneration);
        var deathPlace = new Position(player.X, player.Y);
        player.Lives++;
        player.Character = NewCharacter(generation);
        player.BodyLeft = false;
        player.SentVocabularyVersion = -1;
        player.SentRecipes = null;
        player.Inventory = NewInventory(player.Character);
        var atHome = place == BirthPlace.Tribe ? SpawnPoint.PickNearTribe(Island, _rng, TribeHome(player)) : null;
        var spawn = atHome ?? SpawnPoint.PickWild(Island, _rng, firstLife: false, avoid: deathPlace);
        player.X = spawn.X;
        player.Y = spawn.Y;
        player.InputX = player.InputY = 0;
        Island.BirthGuards.Add(spawn.X, spawn.Y, player.Id, Clock.TotalMinutes);
        player.BornAt = Clock.TotalMinutes;

        Broadcast(w => Messages.WritePlayerJoined(w, InfoOf(player)));
        SendInventory(player);
        SendSelfState(player);
        if (place == BirthPlace.Tribe)
        {
            SendEvent(player, TribeEvent(atHome is null ? TribeResult.NoHome : TribeResult.BornAtHome));
        }
    }

    /// <summary>
    /// Místa kmene, u kterých se dá narodit: stavby členů kmene. Když kmen nic nepostavil, živí členové.
    /// Hráč bez kmene žádná místa nemá.
    /// </summary>
    private List<Position> TribeHome(PlayerSession player)
    {
        if (Tribes.TribeOf(player.Id) is null)
        {
            return [];
        }

        var members = Tribes.MembersWith(player.Id);
        var home = Island.Structures.All
            .Where(s => members.Contains(s.OwnerId))
            .OrderBy(s => s.Id)
            .Select(s => new Position(s.X, s.Y))
            .ToList();
        if (home.Count > 0)
        {
            return home;
        }

        return InWorld
            .Where(p => p.Id != player.Id && members.Contains(p.Id) && !p.Character.IsDead)
            .OrderBy(p => p.Id)
            .Select(p => new Position(p.X, p.Y))
            .ToList();
    }
}
