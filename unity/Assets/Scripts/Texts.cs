using System.Linq;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Client
{
    /// <summary>České texty ke zprávám od serveru (hlášky z prototypu). Server posílá jen kódy.</summary>
    public static class Texts
    {
        /// <summary>Text hlášky ze serveru, nebo null, když se nemá ukazovat.</summary>
        public static string For(GameEvent e, out bool warn)
        {
            switch (e.Kind)
            {
                case EventKind.Body:
                    var bodyEvent = (BodyEvent)e.Code;
                    warn = IsBad(bodyEvent);
                    if (bodyEvent == BodyEvent.SkillLevelUp)
                    {
                        return "Po spánku ti věci dávají větší smysl: " + SkillName((SkillKind)e.Skill) + " " + e.Level + ".";
                    }

                    return bodyEvent == BodyEvent.Died ? null : For(bodyEvent, e.Notes);
                case EventKind.Harvest:
                    var harvest = (HarvestResult)e.Code;
                    warn = harvest != HarvestResult.Picked;
                    return For(harvest, e.Part);
                case EventKind.Drink:
                    var drink = (DrinkResult)e.Code;
                    warn = drink == DrinkResult.Salty || drink == DrinkResult.TooFar;
                    return For(drink);
                case EventKind.Material:
                    var material = (MaterialResult)e.Code;
                    warn = material != MaterialResult.GotSticks && material != MaterialResult.GotStone;
                    return For(material);
                case EventKind.Fire:
                    var fire = (FireResult)e.Code;
                    warn = fire != FireResult.Lit && fire != FireResult.Fed && fire != FireResult.Relit && fire != FireResult.Baked && fire != FireResult.TorchLit;
                    return For(fire);
                case EventKind.Hunt:
                    var hunt = (HuntResult)e.Code;
                    warn = hunt != HuntResult.Killed && hunt != HuntResult.CaughtSmall && hunt != HuntResult.Butchered && hunt != HuntResult.SpearMade && hunt != HuntResult.HitSpear && hunt != HuntResult.FishCaught;
                    return For(hunt, (AnimalKind)e.Subject);
                case EventKind.Build:
                    var build = (BuildResult)e.Code;
                    warn = build != BuildResult.Built && build != BuildResult.Removed;
                    return For(build);
                case EventKind.Corpse:
                    var corpse = (CorpseResult)e.Code;
                    warn = corpse != CorpseResult.Taken;
                    return For(corpse);
                case EventKind.Tribe:
                    var tribe = (TribeResult)e.Code;
                    warn = !(tribe is TribeResult.Invited or TribeResult.Joined or TribeResult.Left or TribeResult.Stored or TribeResult.Taken or TribeResult.TheyJoined or TribeResult.BornAtHome);
                    return For(tribe);
                case EventKind.Craft:
                    var craft = (CraftResult)e.Code;
                    warn = !(craft is CraftResult.Made or CraftResult.Learned or CraftResult.Taught or CraftResult.LearnedFromOther);
                    return For(craft, RecipeName(e.Subject));
                case EventKind.Wood:
                    var wood = (WoodResult)e.Code;
                    warn = !(wood is WoodResult.AxeMade or WoodResult.Chopped or WoodResult.TreeFalling or WoodResult.PieceCut
                        or WoodResult.PieceSplit or WoodResult.WoodSplit or WoodResult.PickedUp or WoodResult.Dropped or WoodResult.BastStripped);
                    return For(wood);
                case EventKind.Climb:
                    var climb = (ClimbResult)e.Code;
                    warn = !(climb is ClimbResult.Up or ClimbResult.Down);
                    return For(climb);
                case EventKind.Cave:
                    var cave = (CaveResult)e.Code;
                    warn = !(cave is CaveResult.Entered or CaveResult.Left);
                    return For(cave);
                case EventKind.Preserve:
                    var preserve = (PreserveResult)e.Code;
                    warn = !(preserve is PreserveResult.Hung or PreserveResult.Taken);
                    return For(preserve);
                case EventKind.Wasp:
                    var wasp = (WaspResult)e.Code;
                    warn = wasp != WaspResult.SmokedOut;
                    return For(wasp);
                case EventKind.Stranger:
                    var stranger = (StrangerResult)e.Code;
                    warn = stranger != StrangerResult.Gift && stranger != StrangerResult.Accepted;
                    return For(stranger);
                case EventKind.Report:
                    var report = (ReportResult)e.Code;
                    warn = report != ReportResult.Sent;
                    return For(report);
                case EventKind.Sign:
                    var sign = (SignResult)e.Code;
                    warn = sign != SignResult.Drawn && sign != SignResult.Erased;
                    return For(sign);
                default:
                    warn = false;
                    return null;
            }
        }

        /// <summary>Text k braní věcí z těla.</summary>
        public static string For(CorpseResult result)
        {
            switch (result)
            {
                case CorpseResult.Taken: return "Vzal sis to.";
                case CorpseResult.TooFar: return "Jsi od těla moc daleko.";
                case CorpseResult.NotFound: return "To už tu není.";
                case CorpseResult.InventoryFull: return "Batoh je plný.";
                case CorpseResult.TribeOnly: return "Prvních 24 hodin si věci smí vzít jen jeho kmen.";
                default: return "U těla už nic není.";
            }
        }

        /// <summary>Text k pozvání do kmene, odpovědi, odchodu a zásobám kmene.</summary>
        public static string For(TribeResult result)
        {
            switch (result)
            {
                case TribeResult.Invited: return "Pozval jsi ho do kmene. Teď je to na něm.";
                case TribeResult.Joined: return "Jsi v kmeni. Zásoby kmene jsou teď i tvoje.";
                case TribeResult.Declined: return "Pozvánku jsi odmítl.";
                case TribeResult.Left: return "Odešel jsi z kmene.";
                case TribeResult.TooFar: return "Je moc daleko. Musí stát kousek od tebe, aby gesto viděl.";
                case TribeResult.AlreadyTogether: return "Už jste spolu v kmeni.";
                case TribeResult.NoInvite: return "Pozvánka už neplatí.";
                case TribeResult.NotInTribe: return "Nejsi v žádném kmeni.";
                case TribeResult.Stored: return "Uloženo do zásob.";
                case TribeResult.Taken: return "Vzal jsi to ze zásob.";
                case TribeResult.StockFull: return "Koš je plný.";
                case TribeResult.InventoryFull: return "Batoh je plný.";
                case TribeResult.NotYours: return "Cizí zásoby. Brát z nich smí jen jejich kmen.";
                case TribeResult.NotFound: return "To už tu není.";
                case TribeResult.TheyDeclined: return "Pozvánku do kmene odmítl.";
                case TribeResult.BornAtHome: return "Narodil ses u svého kmene. Nic nevíš, ale jsi mezi svými.";
                case TribeResult.NoHome: return "U kmene nebylo kde se narodit (nemá stavby ani živé členy). Probouzíš se v divočině.";
                default: return "Přijal pozvánku. Je s tebou v kmeni.";
            }
        }

        /// <summary>Text k daru od cizince nebo k tomu, co udělal s darovanou věcí (prototyp giftBack a giveTo).</summary>
        public static string For(StrangerResult result)
        {
            switch (result)
            {
                case StrangerResult.Gift: return "Cizinec ti podal upečenou rostlinu.";
                case StrangerResult.GiftNoRoom: return "Cizinec ti chtěl něco dát, ale nemáš v batohu místo.";
                case StrangerResult.Accepted: return "Cizinec se spokojeně usmívá.";
                case StrangerResult.Discarded: return "Cizinec zavrtěl hlavou a věc zahodil.";
                case StrangerResult.Puzzled: return "Cizinec neví, co s tím.";
                case StrangerResult.TooFar: return "Cizinec je moc daleko. Dojdi k němu.";
                default: return "Cizinec už tu není.";
            }
        }

        /// <summary>Název generace: Pradávný (0), Nový (1), od třetí generace jen pořadí („3. generace“).</summary>
        public static string GenerationName(int generation)
        {
            if (generation <= 0) return "Pradávný";
            if (generation == 1) return "Nový";
            return (generation + 1) + ". generace";
        }

        /// <summary>Text k výsledku nahlášení.</summary>
        public static string For(ReportResult result)
        {
            switch (result)
            {
                case ReportResult.Sent: return "Nahlášeno. Správce se na to podívá.";
                case ReportResult.TooOften: return "Nahlašuješ moc často. Chvíli počkej.";
                default: return "Tohle nahlásit nejde (už tu není, nebo jsi to ty).";
            }
        }

        /// <summary>Důvod nahlášení na tlačítku.</summary>
        public static string ReasonName(ReportReason reason)
        {
            switch (reason)
            {
                case ReportReason.Behavior: return "Obtěžuje nebo uráží";
                case ReportReason.Cheating: return "Podvádí";
                case ReportReason.OffensiveSign: return "Nevhodná kresba";
                case ReportReason.Bug: return "Chyba ve hře";
                default: return "Něco jiného";
            }
        }

        /// <summary>Text k výsledku kreslení a mazání značky.</summary>
        public static string For(SignResult result)
        {
            switch (result)
            {
                case SignResult.Drawn: return "Nakreslil jsi značku.";
                case SignResult.Erased: return "Smazal jsi značku.";
                case SignResult.Empty: return "Nakresli aspoň pár bodů.";
                case SignResult.TooFar: return "Jsi moc daleko.";
                case SignResult.CannotDrawHere: return "Tady kreslit nejde (voda nebo jiná značka hned vedle).";
                case SignResult.NotFound: return "Značka už tu není.";
                case SignResult.NotYours: return "Cizí značku smazat nemůžeš.";
                default: return "Ve světě máš už moc značek. Nějakou smaž.";
            }
        }

        /// <summary>Poznámka na staré rytině (prototyp TAGS).</summary>
        public static string TagName(CarvingTag tag)
        {
            switch (tag)
            {
                case CarvingTag.Filling: return "syté";
                case CarvingTag.Bitter: return "hořké";
                case CarvingTag.Burns: return "pálí";
                case CarvingTag.Vomit: return "zvracení";
                case CarvingTag.Diarrhea: return "průjem";
                case CarvingTag.Fever: return "horečka";
                case CarvingTag.Tremor: return "třes";
                case CarvingTag.Faint: return "omdlení";
                case CarvingTag.Strengthened: return "posílilo";
                default: return "léčí";
            }
        }

        /// <summary>Jak dávno to bylo (prototyp lt): „právě teď“, „před 20 min“, „před 3 h“, „před 1,5 dne“.</summary>
        public static string Ago(double minutes)
        {
            if (minutes < 1) return "právě teď";
            if (minutes < 60) return "před " + System.Math.Round(minutes) + " min";
            if (minutes < 1440) return "před " + System.Math.Round(minutes / 60) + " h";
            return "před " + (System.Math.Round(minutes / 1440 * 10) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " dny";
        }

        // Názvy receptů podle čísla (Subject v hlášce Craft). Plní je ClientSession ze seznamu receptů ze serveru.
        private static readonly System.Collections.Generic.Dictionary<int, string> _recipeNames = new System.Collections.Generic.Dictionary<int, string>();

        public static void RememberRecipes(RecipeView[] recipes)
        {
            foreach (var recipe in recipes) _recipeNames[recipe.Index] = recipe.Name;
        }

        private static string RecipeName(int index)
        {
            string name;
            return _recipeNames.TryGetValue(index, out name) ? name : null;
        }

        /// <summary>Text k výrobě a zkoušení receptů (název receptu, když ho klient zná).</summary>
        public static string For(CraftResult result, string recipe)
        {
            var what = recipe != null ? recipe.ToLower() : "to";
            switch (result)
            {
                case CraftResult.Made: return "Hotovo: " + what + ".";
                case CraftResult.Learned: return "Povedlo se! Teď umíš " + what + ". (Smrtí to zapomeneš.)";
                case CraftResult.Failed: return "Nepovedlo se, kus materiálu přišel vniveč. Zkus to znovu.";
                case CraftResult.NothingCame: return "Z tohohle nic nebude.";
                case CraftResult.NeedMaterials: return "Na to ti chybí materiál.";
                case CraftResult.NeedTool: return "Na to potřebuješ nástroj (třeba nůž).";
                case CraftResult.NeedWater: return "Tohle se dělá u vody.";
                case CraftResult.NeedFire: return "Tohle se dělá u ohně.";
                case CraftResult.AlreadyHave: return "Tohle už máš.";
                case CraftResult.NothingToRepair: return "Není co opravovat.";
                case CraftResult.Rotten: return "Kůže už zahnila. Syrovou kůži je potřeba vyčinit do dvou dnů.";
                case CraftResult.InventoryFull: return "Nemáš kam to dát. Něco odlož.";
                case CraftResult.Taught: return "Ukázal jsi, jak se dělá " + what + ".";
                case CraftResult.AlreadyKnows: return "To už umí.";
                case CraftResult.TooFarToTeach: return "Musíš stát blíž, aby viděl, jak to děláš.";
                case CraftResult.DontKnow: return "Co neumíš, neukážeš.";
                case CraftResult.LearnedFromOther: return "Někdo ti ukázal, jak se dělá " + what + ". Teď to umíš.";
                default: return result.ToString();
            }
        }

        /// <summary>Co recept potřebuje (nápověda u tlačítka).</summary>
        public static string RecipeInputs(RecipeView recipe)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var input in recipe.Inputs) parts.Add(input.Count + "× " + ItemName(input.Kind).ToLower());
            if (recipe.Tool.HasValue) parts.Add("nástroj: " + ItemName(recipe.Tool.Value).ToLower());
            if (recipe.NearWater) parts.Add("u vody");
            if (recipe.NearFire) parts.Add("u ohně");
            return string.Join(", ", parts);
        }

        /// <summary>Název věci podle druhu (bez podrobností hromádky).</summary>
        public static string ItemName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Stick: return "Klacek";
                case ItemKind.Stone: return "Kámen";
                case ItemKind.Spear: return "Kopí";
                case ItemKind.Meat: return "Maso";
                case ItemKind.Fish: return "Ryba";
                case ItemKind.Axe: return "Sekera";
                case ItemKind.Wood: return "Dřevo";
                case ItemKind.Splinter: return "Tříska";
                case ItemKind.LogPiece: return "Klad";
                case ItemKind.SmallBag: return "Malý batoh z lýka";
                case ItemKind.LargeBag: return "Velký batoh z kůže";
                case ItemKind.Bast: return "Lýko";
                case ItemKind.Hide: return "Syrová kůže";
                case ItemKind.TannedHide: return "Vyčiněná kůže";
                case ItemKind.Bone: return "Kost";
                case ItemKind.Ash: return "Popel";
                case ItemKind.Knife: return "Nůž";
                case ItemKind.Flint: return "Pazourek";
                case ItemKind.Needle: return "Kostěná jehla";
                case ItemKind.Hook: return "Kostěný háček";
                case ItemKind.Journal: return "Deník";
                default: return "Rostlina";
            }
        }

        /// <summary>Text k vykuřování vosího hnízda.</summary>
        public static string For(WaspResult result)
        {
            switch (result)
            {
                case WaspResult.SmokedOut: return "Kouř vosy vyhnal. Hnízdo je na pár dní prázdné.";
                case WaspResult.NeedTorch: return "Vosy odežene jen kouř. Zapal si pochodeň u ohně.";
                case WaspResult.TooFar: return "K hnízdu musíš blíž.";
                default: return "Hnízdo tu už není.";
            }
        }

        /// <summary>Text ke kácení, dřevu a sekeře. Obyčejné seknutí nic nepíše (je ho slyšet).</summary>
        public static string For(WoodResult result)
        {
            switch (result)
            {
                case WoodResult.AxeMade: return "Vyrobil jsi kamennou sekeru. Strom padá na stranu od tebe, nestůj pod ním.";
                case WoodResult.AlreadyHasAxe: return "Sekeru už máš.";
                case WoodResult.NeedMaterials: return "Na sekeru potřebuješ klacek a dva kameny.";
                case WoodResult.NeedAxe: return "Bez sekery to nepůjde. Rukama nasbíráš jen klacky.";
                case WoodResult.Chopped: return null;
                case WoodResult.TreeFalling: return "Strom praská a padá!";
                case WoodResult.PieceCut: return "Odsekl jsi z kmene klad.";
                case WoodResult.PieceSplit: return "Rozštípal jsi klad na dřevo.";
                case WoodResult.WoodSplit: return "Naštípal jsi třísky.";
                case WoodResult.AxeBroke: return "Sekera se rozpadla. Vyrob si novou.";
                case WoodResult.PickedUp: return "Neseš klad. Je těžký, jdeš pomaleji.";
                case WoodResult.Dropped: return "Položil jsi klad.";
                case WoodResult.AlreadyCarrying: return "Už neseš klad. Víc neuneseš.";
                case WoodResult.TooHeavy: return "Celý kmen neuneseš. Rozsekej ho na klady.";
                case WoodResult.NotCarrying: return "Nic neneseš.";
                case WoodResult.TooFar: return "Je to moc daleko.";
                case WoodResult.NotFound: return "Už tu nic není.";
                case WoodResult.TooSoon: return null;
                case WoodResult.InventoryFull: return "Nemáš kam to dát. Něco z batohu zahoď.";
                case WoodResult.CannotSplit: return "Štípat jde jen dřevo.";
                case WoodResult.BastStripped: return "Sloupl jsi z kůry lýko.";
                case WoodResult.NoBark: return "Lýko tu už není (nebo ho tenhle strom nedá).";
                case WoodResult.NeedKnife: return "Na lýko potřebuješ nůž.";
                default: return result.ToString();
            }
        }

        /// <summary>Text k výsledku lovu (prototyp attack a nabídka mrtvého zvířete).</summary>
        public static string For(HuntResult result, AnimalKind animal)
        {
            switch (result)
            {
                case HuntResult.HitFist: return "Praštil jsi ho pěstí. Spíš ho to rozzuřilo.";
                case HuntResult.HitSpear: return "Zasáhl jsi ho kopím.";
                case HuntResult.Killed: return AnimalViews.NameOf(animal) + " padl.";
                case HuntResult.CaughtSmall: return "Chytil jsi malé zvíře.";
                case HuntResult.TooFar: return "Je moc daleko.";
                case HuntResult.TooSoon: return "Ještě se napřahuješ.";
                case HuntResult.NotFound: return "Zvíře už tu není.";
                case HuntResult.Butchered: return "Získal jsi maso.";
                case HuntResult.NotDead: return "Zvíře ještě žije.";
                case HuntResult.InventoryFull: return "Nemáš kam to dát. Něco z batohu zahoď.";
                case HuntResult.SpearMade: return "Ostrý kámen ke klacku. Máš kopí.";
                case HuntResult.AlreadyHasSpear: return "Kopí už máš.";
                case HuntResult.NeedMaterials: return "Na kopí potřebuješ klacek a kámen.";
                case HuntResult.TrapEmpty: return "V pasti nic není.";
                case HuntResult.FishCaught: return "Bodl jsi rybu.";
                case HuntResult.FishEscaped: return "Ryba ti proklouzla.";
                case HuntResult.NeedSpear: return "Na rybaření potřebuješ kopí.";
                case HuntResult.NotWater: return "Tady se rybařit nedá.";
                case HuntResult.NotYours: return "Cizí past. Úlovek patří tomu, kdo ji postavil, a jeho kmeni.";
                default: return result.ToString();
            }
        }

        public static string For(BuildResult result)
        {
            switch (result)
            {
                case BuildResult.Built: return "Postaveno.";
                case BuildResult.Removed: return "Rozebráno. Část klacků ti zůstala.";
                case BuildResult.NeedMaterials: return "Na tu stavbu nemáš dost klacků.";
                case BuildResult.CannotBuildHere: return "Tady stavět nejde (voda, strom, oheň nebo tam někdo stojí).";
                case BuildResult.TooFar: return "Je to moc daleko.";
                case BuildResult.NotFound: return "Ta stavba už tu není.";
                case BuildResult.NoDoorway: return "Dveře patří do zdi s otvorem. Miř na otvor.";
                case BuildResult.NotYours: return "Cizí stavbu může rozebrat jen ten, kdo ji postavil, a jeho kmen.";
                case BuildResult.NotEmpty: return "V koši něco je. Napřed ho vyber.";
                case BuildResult.BirthPlaceProtected: return "Tady se nedávno někdo narodil. Den po narození tu smí stavět jen on a jeho kmen.";
                case BuildResult.NotInCave: return "V jeskyni stěny dává skála. Zeď ani dveře tu nepostavíš, ohniště, zásoby a ozdoby ano.";
                case BuildResult.NoCaveEntrance: return "Zátaras patří k vchodu do jeskyně. Tady žádný není, nebo už zatarasený je.";
                case BuildResult.NoSupport: return "Střecha by se neměla o co opřít. Potřebuje dvě podpory: zdi, sloupky nebo sousední střechu.";
                case BuildResult.NoFloor: return "V patře se staví jen na podlahu patra. Žebřík patří k jejímu okraji.";
                case BuildResult.Repaired: return "Střecha je opravená. Zase nezatéká.";
                case BuildResult.CannotRepair: return "Opravit jde jen střecha, která nehoří.";
                default: return result.ToString();
            }
        }

        public static string For(MaterialResult result)
        {
            switch (result)
            {
                case MaterialResult.GotSticks: return "Nasbíral jsi klacky.";
                case MaterialResult.GotStone: return "Vzal jsi kámen.";
                case MaterialResult.TooFar: return "Je to moc daleko.";
                case MaterialResult.NothingLeft: return "Spadlé větve jsou pryč. Zkus to zítra.";
                case MaterialResult.InventoryFull: return "Nemáš kam to dát. Něco z batohu zahoď.";
                default: return "Tady nic není.";
            }
        }

        public static string For(FireResult result)
        {
            switch (result)
            {
                case FireResult.Lit: return "Oheň hoří. Je u něj teplo.";
                case FireResult.Fed: return "Oheň se rozhořel.";
                case FireResult.Relit: return "Oheň znovu chytil.";
                case FireResult.Baked: return "Upekl jsi to na ohni.";
                case FireResult.NeedMaterials: return "Na oheň potřebuješ 3 klacky a kámen.";
                case FireResult.NoStick: return "Nemáš klacek.";
                case FireResult.CannotBuildHere: return "Tady oheň nerozdělíš (voda, strom, zeď nebo jiný oheň).";
                case FireResult.TooFar: return "Je to moc daleko.";
                case FireResult.NoFireNear: return "Musíš být u ohně.";
                case FireResult.CannotBake: return "Tohle se péct nedá.";
                case FireResult.InventoryFull: return "Nemáš kam to dát. Něco z batohu zahoď.";
                case FireResult.TorchLit: return "Klacek chytl. Pochodeň ti svítí do tmy a zvěř se jí bojí.";
                case FireResult.TorchAlreadyLit: return "Pochodeň už hoří.";
                case FireResult.AshTaken: return "Nabral jsi popel. Hodí se na činění kůže.";
                case FireResult.NoAsh: return "Popel tu ještě není (oheň musí chvíli hořet), nebo už je dnes vybraný.";
                default: return result.ToString();
            }
        }

        /// <summary>Špatná zpráva o těle (zvýrazní se). Jídlo, spánek a učení jsou dobré zprávy.</summary>
        public static bool IsBad(BodyEvent bodyEvent)
        {
            switch (bodyEvent)
            {
                case BodyEvent.Ate:
                case BodyEvent.Tasted:
                case BodyEvent.CameTo:
                case BodyEvent.FellAsleep:
                case BodyEvent.WokeRested:
                case BodyEvent.WokeUp:
                case BodyEvent.NotTired:
                case BodyEvent.LearnedInSleep:
                case BodyEvent.IronStomach:
                case BodyEvent.SkillLevelUp:
                case BodyEvent.LeechesRemoved:
                case BodyEvent.Smeared:
                case BodyEvent.NoTicks:
                case BodyEvent.NestSmokedOut:
                case BodyEvent.FoundCave:
                case BodyEvent.Relief:
                case BodyEvent.Recovered:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Název dovednosti (prototyp SKN).</summary>
        public static string SkillName(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.Gather: return "Sbírání";
                case SkillKind.Hunt: return "Lov";
                case SkillKind.Craft: return "Řemeslo";
                default: return "Stavění";
            }
        }

        public static string For(BodyEvent bodyEvent, TasteNote[] notes)
        {
            switch (bodyEvent)
            {
                case BodyEvent.Ate: return "Snědl jsi to.";
                case BodyEvent.Tasted: return "Ochutnal jsi. " + string.Join(" ", notes.Select(For));
                case BodyEvent.BitternessStopped: return "Hořkost tě zastaví. Polkl jsi jen část.";
                case BodyEvent.Vomited: return "Žaludek se ti zvedá. Vyzvracíš se.";
                case BodyEvent.Diarrhea: return "Břicho se ti svírá křečemi.";
                case BodyEvent.HeavyFeeling: return "Cítíš se divně, jako by tě něco zevnitř tížilo.";
                case BodyEvent.Tremor: return "Ruce se ti třesou a svět se začíná vlnit.";
                case BodyEvent.Fainted: return "Zrak se ti rozostřuje. Padáš na zem.";
                case BodyEvent.CameTo: return "Probíráš se. Hlava ti třeští.";
                case BodyEvent.Crash: return "Ten příval sil je pryč. Cítíš prázdnotu.";
                case BodyEvent.FeverStarted: return "Hoří ti čelo.";
                case BodyEvent.FeltSick: return "Je ti horko a špatně od žaludku. Něco v tobě se pokazilo.";
                case BodyEvent.FellAsleep: return "Ležíš a spíš. Čas neběží rychleji, ale síly se vracejí.";
                case BodyEvent.WokeRested: return "Probudil ses odpočatý.";
                case BodyEvent.WokeUp: return "Probudil ses.";
                case BodyEvent.WokeCold: return "Zimou nemůžeš spát.";
                case BodyEvent.NotTired: return "Nejsi dost unavený.";
                case BodyEvent.TooColdToSleep: return "Je ti moc zima na spaní.";
                case BodyEvent.LearnedInSleep: return "Ve spánku se ti uložilo, co jsi dělal.";
                case BodyEvent.WoundedByBoar: return "Kly tě zasáhly. Krvácíš a bolí tě každý krok.";
                case BodyEvent.WoundedByWolf: return "Vlk tě pokousal. Krvácíš a bolí tě každý krok.";
                case BodyEvent.WokeByAnimal: return "Něco tě vzbudilo. Divočák je blízko.";
                case BodyEvent.Stalked: return "Něco tě sleduje ze tmy. Oheň ho odradí.";
                case BodyEvent.BoarAlert: return "Divočák se na tebe zaměřil. Až se rozběhne, uhni na stranu.";
                case BodyEvent.WoundInflamed: return "Rána se zanítila. Pálí a je ti horko.";
                case BodyEvent.IronStomach: return "Železný žaludek to zvládl.";
                case BodyEvent.ColdInstinct: return "Zebe tě do kostí. Tělo ti říká: najdi oheň nebo úkryt, než přijde mráz.";
                case BodyEvent.TorchLow: return "Pochodeň slábne. Brzy dohoří.";
                case BodyEvent.TorchOut: return "Pochodeň zhasla. Kolem je zase tma.";
                case BodyEvent.HitByTree: return "Zasáhl tě padající strom!";
                case BodyEvent.MosquitoBite: return "Štípou tě komáři. Kouř z ohně je odežene, nebo se natři bahnem či popelem.";
                case BodyEvent.Leech: return "Něco se ti přisálo na nohu. Pijavice.";
                case BodyEvent.LeechesRemoved: return "Sundal jsi pijavice.";
                case BodyEvent.Smeared: return "Natřel ses. Komáři si tě teď chvíli nevšimnou.";
                case BodyEvent.Chills: return "Třese tebou zima, i když je ti horko. Přichází to ve vlnách.";
                case BodyEvent.BellyAche: return "Bolí tě břicho a pořád musíš odbíhat.";
                case BodyEvent.Relief: return "Po jídle se ti trochu ulevilo.";
                case BodyEvent.Recovered: return "Je ti zase dobře. Nemoc odešla.";
                case BodyEvent.TicksFound: return "Prohlédl ses a našel přisátá klíšťata. Vytáhl jsi je.";
                case BodyEvent.NoTicks: return "Prohlédl ses. Nic na tobě není.";
                case BodyEvent.AchingJoints: return "Bolí tě klouby a pálí tě čelo. Něco v tobě roste už pár dní.";
                case BodyEvent.SnakeBite: return "Had tě uštkl! Noha pálí.";
                case BodyEvent.SnakeWarning: return "Had se vztyčil a syčí. Pomalu ustup.";
                case BodyEvent.VenomSpreads: return "Noha otéká a točí se ti hlava. Jed se rozlévá.";
                case BodyEvent.WaspSting: return "Vosy! Štípou tě. Uteč od hnízda, kouř je odežene.";
                case BodyEvent.NestSmokedOut: return "Vosy prchají před kouřem.";
                case BodyEvent.FoundCave: return "Za převislou skálou je úzká puklina. Vede dovnitř.";
                case BodyEvent.Smoke: return "Kouř se drží pod stropem a dusí tě. Tahle jeskyně nemá kudy větrat.";
                case BodyEvent.RoofCracking: return "Střecha nad tebou praská! Uteč, nebo ji rychle podepři.";
                case BodyEvent.HitByRoof: return "Spadla na tebe střecha!";
                case BodyEvent.Fell: return "Podlaha pod tebou povolila a spadl jsi dolů.";
                case BodyEvent.JumpedDown: return "Seskočil jsi dolů. Kotníky to trochu cítí.";
                case BodyEvent.BuildingOnFire: return "Stavba vedle tebe hoří! Žár pálí, uteč.";
                default: return "Zemřel jsi.";
            }
        }

        public static string For(TasteNote note)
        {
            switch (note)
            {
                case TasteNote.Sweet: return "Sladká, sytá chuť.";
                case TasteNote.Bland: return "Mdlá, trochu sytá chuť.";
                case TasteNote.Tasteless: return "Skoro bez chuti.";
                case TasteNote.Burns: return "Pálí tě v ústech.";
                case TasteNote.Irritates: return "Trochu tě to dráždí v hrdle.";
                case TasteNote.VeryBitter: return "Zvláštně hořké a svíravé.";
                case TasteNote.Bitter: return "Má hořkou pachuť.";
                case TasteNote.Soapy: return "Divně sladká, skoro mýdlová.";
                case TasteNote.Fresh: return "Štiplavě svěží, zaostří ti to smysly.";
                case TasteNote.Herbal: return "Svíravá, bylinná chuť.";
                case TasteNote.Rotten: return "Páchne to a chutná odporně.";
                default: return note.ToString();
            }
        }

        public static string For(HarvestResult result, PlantPart part)
        {
            switch (result)
            {
                case HarvestResult.Picked:
                    return part == PlantPart.Leaf ? "Utrhl jsi list."
                        : part == PlantPart.Fruit ? "Utrhl jsi plod."
                        : "Vykopal jsi kořen. Rostlina je pryč.";
                case HarvestResult.TooFar: return "Je to moc daleko.";
                case HarvestResult.PlantGone: return "Rostlina už tu není.";
                case HarvestResult.NoLeavesLeft: return "Tady už žádné listy nezbyly.";
                case HarvestResult.NoFruit: return "Žádné plody tu nejsou.";
                case HarvestResult.InventoryFull: return "Nemáš kam to dát. Něco z batohu zahoď.";
                default: return result.ToString();
            }
        }

        public static string For(DrinkResult result)
        {
            switch (result)
            {
                case DrinkResult.Clean: return "Čistá, studená voda.";
                case DrinkResult.Muddy: return "Voda je kalná a zapáchá, ale uhasila žízeň.";
                case DrinkResult.MuddySip: return "Zápach tě odpuzuje. Napil ses jen trochu.";
                case DrinkResult.Salty: return "Je slaná. Žízeň to jen zhoršilo.";
                case DrinkResult.TooFar: return "Je to moc daleko.";
                default: return "Tady voda není.";
            }
        }

        public static string For(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Toxin: return "Zemřel jsi na otravu, která se v těle hromadila.";
                case DeathCause.Neurotoxin: return "Zemřel jsi na otravu, která ti vzala vědomí.";
                case DeathCause.Fever: return "Zemřel jsi na horečku.";
                case DeathCause.Infection: return "Nemoc tě vyčerpala horečkou.";
                case DeathCause.Cold: return "Zemřel jsi podchlazením.";
                case DeathCause.Thirst: return "Zemřel jsi žízní.";
                case DeathCause.Hunger: return "Zemřel jsi hladem.";
                case DeathCause.Bleeding: return "Vykrvácel jsi po zranění od zvířete.";
                case DeathCause.Wound: return "Zemřel jsi na následky zranění od zvířete.";
                case DeathCause.FallingTree: return "Zabil tě padající strom.";
                case DeathCause.Smoke: return "Udusil ses kouřem v jeskyni.";
                case DeathCause.Collapse: return "Zavalila tě spadlá střecha.";
                case DeathCause.Fall: return "Pád z patra byl poslední.";
                case DeathCause.Burned: return "Uhořel jsi u hořící stavby.";
                default: return "Zemřel jsi.";
            }
        }

        public static string For(Symptom symptom)
        {
            switch (symptom)
            {
                case Symptom.HeavyBody: return "ztěžklé tělo";
                case Symptom.Fever: return "horečka";
                case Symptom.Tremor: return "třes";
                case Symptom.Agitated: return "rozjitřený";
                case Symptom.Diarrhea: return "průjem";
                case Symptom.Inflammation: return "zánět";
                case Symptom.Unconscious: return "bezvědomí";
                case Symptom.Bleeding: return "krvácení";
                case Symptom.Limping: return "kulhání";
                case Symptom.Chills: return "zimnice (horečka chodí ve vlnách)";
                case Symptom.BellyAche: return "bolí tě břicho";
                case Symptom.Leeches: return "na noze ti visí pijavice";
                case Symptom.Smeared: return "natřený (komáři neštípou)";
                case Symptom.AchingJoints: return "bolí tě klouby";
                case Symptom.Swelling: return "oteklá noha";
                default: return symptom.ToString();
            }
        }

        public static string SeasonName(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "Jaro";
                case Season.Summer: return "Léto";
                case Season.Autumn: return "Podzim";
                default: return "Zima";
            }
        }

        /// <summary>Popis věci v batohu, jak ji vidí hráč (prototyp describe).</summary>
        public static string Describe(StackView stack)
        {
            if (stack.Kind == ItemKind.Stick) return "Klacek";
            if (stack.Kind == ItemKind.Stone) return "Kámen";
            if (stack.Kind == ItemKind.Spear) return "Kopí";
            if (stack.Kind == ItemKind.Axe) return "Sekera · zbývá " + stack.Uses + " seknutí";
            if (stack.Kind == ItemKind.Knife) return "Nůž · zbývá " + stack.Uses + " použití";
            if (stack.Kind == ItemKind.Needle) return "Kostěná jehla · zbývá " + stack.Uses + " použití";
            if (stack.Kind == ItemKind.Hook) return "Kostěný háček · zbývá " + stack.Uses + " použití";
            if (stack.Kind == ItemKind.LogPiece) return "Klad · " + TreeName(stack.Tree).ToLower();
            if (stack.Kind == ItemKind.Hide) return "Syrová kůže" + (stack.Spoiled ? " · zahnilá" : string.Empty);
            if (stack.Kind == ItemKind.Meat || stack.Kind == ItemKind.Fish) return FleshName(stack.Kind, stack.Cooking) + (stack.Spoiled ? " · zapáchá" : string.Empty);
            if (stack.Kind != ItemKind.Plant) return ItemName(stack.Kind);

            var name = stack.Part == PlantPart.Leaf ? "List" : stack.Part == PlantPart.Root ? "Kořen" : "Plod";
            if (stack.Part == PlantPart.Fruit)
            {
                name += stack.Ripeness == Ripeness.Unripe ? " · zelený" : stack.Ripeness == Ripeness.Overripe ? " · scvrklý, tmavý" : " · zralý";
            }

            if (stack.Cooking == Cooking.Baked) name += " · upečený";
            if (stack.Cooking == Cooking.Boiled) name += " · uvařený";
            if (stack.Spoiled) name += " · plesnivý";
            return name;
        }

        /// <summary>Maso nebo ryba i s úpravou (syrové, upečené, uzené, sušené).</summary>
        public static string FleshName(ItemKind kind, Cooking cooking)
        {
            var fish = kind == ItemKind.Fish;
            switch (cooking)
            {
                case Cooking.Baked: return fish ? "Ryba · upečená" : "Maso · upečené";
                case Cooking.Smoked: return fish ? "Ryba · uzená" : "Maso · uzené";
                case Cooking.Dried: return fish ? "Ryba · sušená" : "Maso · sušené";
                default: return fish ? "Ryba · syrová" : "Maso · syrové";
            }
        }

        /// <summary>Název střechy podle materiálu.</summary>
        public static string RoofName(string material)
        {
            switch (material)
            {
                case "Bark": return "Střecha z kůry";
                case "Hide": return "Střecha z kůže";
                case "Shingles": return "Střecha ze šindelů";
                default: return "Střecha z větví";
            }
        }

        /// <summary>Text k lezení po žebříku.</summary>
        public static string For(ClimbResult result)
        {
            switch (result)
            {
                case ClimbResult.Up: return "Vylezl jsi nahoru. Zvířata sem nedosáhnou.";
                case ClimbResult.Down: return "Slezl jsi dolů.";
                case ClimbResult.TooFar: return "K žebříku musíš blíž.";
                default: return "Ten žebřík nikam nevede.";
            }
        }

        /// <summary>Text k průchodu vchodem do jeskyně.</summary>
        public static string For(CaveResult result)
        {
            switch (result)
            {
                case CaveResult.Entered: return "Vešel jsi do tmy. Bez ohně tu skoro nic neuvidíš.";
                case CaveResult.Left: return "Vyšel jsi ven na světlo.";
                case CaveResult.TooFar: return "Musíš blíž k otvoru.";
                case CaveResult.Barricaded: return "Vchod je zatarasený. Za zátarasem je cizí kmen.";
                default: return "Tudy cesta nevede.";
            }
        }

        /// <summary>Text k uzení a sušení masa.</summary>
        public static string For(PreserveResult result)
        {
            switch (result)
            {
                case PreserveResult.Hung: return "Maso visí. Hotové bude, až ztmavne a ztvrdne.";
                case PreserveResult.Taken: return "Vzal jsi maso.";
                case PreserveResult.NotMeat: return "Pověsit jde jen syrové maso nebo ryba.";
                case PreserveResult.Spoiled: return "Tohle už zapáchá. Kouř ho nezachrání.";
                case PreserveResult.RackFull: return "Víc se sem nevejde.";
                case PreserveResult.NothingHere: return "Nic tu nevisí.";
                case PreserveResult.InventoryFull: return "Nemáš volné ruce ani místo v batohu.";
                case PreserveResult.TooFar: return "Musíš blíž.";
                case PreserveResult.FireOut: return "Bez ohně není kouř. Nejdřív oheň rozdělej.";
                default: return "Cizí sušák. Sahat na něj smí jen ten, kdo ho postavil, a jeho kmen.";
            }
        }

        public static string TreeName(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.Oak: return "Dub";
                case TreeKind.Conifer: return "Jehličnan";
                case TreeKind.Birch: return "Bříza";
                case TreeKind.Willow: return "Vrba";
                default: return "Suchý strom";
            }
        }

        public static string WaterName(TileType tile)
        {
            return tile == TileType.Bog ? "Bahno" : tile == TileType.Pond ? "Voda" : tile == TileType.Ford ? "Brod" : "Moře";
        }
    }
}
