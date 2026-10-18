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
                    warn = fire != FireResult.Lit && fire != FireResult.Fed && fire != FireResult.Relit && fire != FireResult.Baked;
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
            if (stack.Kind == ItemKind.Meat) return (stack.Cooking == Cooking.Baked ? "Maso · upečené" : "Maso · syrové") + (stack.Spoiled ? " · zapáchá" : string.Empty);
            if (stack.Kind == ItemKind.Fish) return (stack.Cooking == Cooking.Baked ? "Ryba · upečená" : "Ryba · syrová") + (stack.Spoiled ? " · zapáchá" : string.Empty);

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

        public static string WaterName(TileType tile)
        {
            return tile == TileType.Bog ? "Bahno" : tile == TileType.Pond ? "Voda" : "Moře";
        }
    }
}
