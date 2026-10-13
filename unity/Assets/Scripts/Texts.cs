using System.Linq;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
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
                    warn = bodyEvent != BodyEvent.Ate && bodyEvent != BodyEvent.Tasted && bodyEvent != BodyEvent.CameTo;
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
                case EventKind.Build:
                    var build = (BuildResult)e.Code;
                    warn = build != BuildResult.Built && build != BuildResult.Removed;
                    return For(build);
                default:
                    warn = false;
                    return null;
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
                default: return result.ToString();
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

            var name = stack.Part == PlantPart.Leaf ? "List" : stack.Part == PlantPart.Root ? "Kořen" : "Plod";
            if (stack.Part == PlantPart.Fruit)
            {
                name += stack.Ripeness == Ripeness.Unripe ? " · zelený" : stack.Ripeness == Ripeness.Overripe ? " · scvrklý, tmavý" : " · zralý";
            }

            if (stack.Cooking == Cooking.Baked) name += " · upečený";
            if (stack.Cooking == Cooking.Boiled) name += " · uvařený";
            return name;
        }

        public static string WaterName(TileType tile)
        {
            return tile == TileType.Bog ? "Bahno" : tile == TileType.Pond ? "Voda" : "Moře";
        }
    }
}
