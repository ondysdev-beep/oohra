using System.Linq;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Plants;
using Oohra.Core.World;

namespace Oohra.Client
{
    /// <summary>České texty k výsledkům pravidel (hlášky z prototypu). Core posílá jen kódy.</summary>
    public static class Texts
    {
        public static string For(BodyMessage message)
        {
            switch (message.Event)
            {
                case BodyEvent.Ate: return "Snědl jsi to.";
                case BodyEvent.Tasted: return "Ochutnal jsi. " + string.Join(" ", message.Notes.Select(For));
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
                case BodyEvent.Died: return "Zemřel jsi.";
                default: return message.Event.ToString();
            }
        }

        public static bool IsWarning(BodyEvent bodyEvent)
        {
            return bodyEvent != BodyEvent.Ate && bodyEvent != BodyEvent.Tasted && bodyEvent != BodyEvent.CameTo;
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
        public static string Describe(PlantItem item)
        {
            var name = item.Part == PlantPart.Leaf ? "List" : item.Part == PlantPart.Root ? "Kořen" : "Plod";
            if (item.Part == PlantPart.Fruit)
            {
                name += item.Ripeness == Ripeness.Unripe ? " · zelený" : item.Ripeness == Ripeness.Overripe ? " · scvrklý, tmavý" : " · zralý";
            }

            if (item.Cooking == Cooking.Baked) name += " · upečený";
            if (item.Cooking == Cooking.Boiled) name += " · uvařený";
            return name;
        }

        public static string WaterName(TileType tile)
        {
            return tile == TileType.Bog ? "Bahno" : tile == TileType.Pond ? "Voda" : "Moře";
        }
    }
}
