using Oohra.Core.Characters;

namespace Oohra.TestClient;

/// <summary>České texty ke zprávám o těle (hlášky z prototypu, part4.js).</summary>
public static class Texts
{
    public static string For(BodyMessage message)
    {
        return message.Event switch
        {
            BodyEvent.Ate => "Snědl jsi to.",
            BodyEvent.Tasted => "Ochutnal jsi. " + string.Join(" ", message.Notes!.Select(For)),
            BodyEvent.BitternessStopped => "Hořkost tě zastaví. Polkl jsi jen část.",
            BodyEvent.Vomited => "Žaludek se ti zvedá. Vyzvracíš se.",
            BodyEvent.Diarrhea => "Břicho se ti svírá křečemi.",
            BodyEvent.HeavyFeeling => "Cítíš se divně, jako by tě něco zevnitř tížilo.",
            BodyEvent.Tremor => "Ruce se ti třesou a svět se začíná vlnit.",
            BodyEvent.Fainted => "Zrak se ti rozostřuje. Padáš na zem.",
            BodyEvent.CameTo => "Probíráš se. Hlava ti třeští.",
            BodyEvent.Crash => "Ten příval sil je pryč. Cítíš prázdnotu.",
            BodyEvent.FeverStarted => "Hoří ti čelo.",
            BodyEvent.FeltSick => "Je ti horko a špatně od žaludku. Něco v tobě se pokazilo.",
            BodyEvent.Died => "Zemřel jsi.",
            _ => message.Event.ToString(),
        };
    }

    public static string For(TasteNote note)
    {
        return note switch
        {
            TasteNote.Sweet => "Sladká, sytá chuť.",
            TasteNote.Bland => "Mdlá, trochu sytá chuť.",
            TasteNote.Tasteless => "Skoro bez chuti.",
            TasteNote.Burns => "Pálí tě v ústech.",
            TasteNote.Irritates => "Trochu tě to dráždí v hrdle.",
            TasteNote.VeryBitter => "Zvláštně hořké a svíravé.",
            TasteNote.Bitter => "Má hořkou pachuť.",
            TasteNote.Soapy => "Divně sladká, skoro mýdlová.",
            TasteNote.Fresh => "Štiplavě svěží, zaostří ti to smysly.",
            TasteNote.Herbal => "Svíravá, bylinná chuť.",
            _ => note.ToString(),
        };
    }

    public static string For(Symptom symptom)
    {
        return symptom switch
        {
            Symptom.HeavyBody => "ztěžklé tělo",
            Symptom.Fever => "horečka",
            Symptom.Tremor => "třes",
            Symptom.Agitated => "rozjitřený",
            Symptom.Diarrhea => "průjem",
            Symptom.Inflammation => "zánět",
            Symptom.Unconscious => "bezvědomí",
            _ => symptom.ToString(),
        };
    }

    public static string For(DeathCause cause)
    {
        return cause switch
        {
            DeathCause.Toxin => "Zemřel jsi na otravu, která se v těle hromadila.",
            DeathCause.Neurotoxin => "Zemřel jsi na otravu, která ti vzala vědomí.",
            DeathCause.Fever => "Zemřel jsi na horečku.",
            DeathCause.Infection => "Rána nebo nemoc tě vyčerpala horečkou.",
            DeathCause.Cold => "Zemřel jsi podchlazením.",
            DeathCause.Thirst => "Zemřel jsi žízní.",
            DeathCause.Hunger => "Zemřel jsi hladem.",
            _ => cause.ToString(),
        };
    }

    public static string SeasonName(Oohra.Core.Season season)
    {
        return season switch
        {
            Oohra.Core.Season.Spring => "Jaro",
            Oohra.Core.Season.Summer => "Léto",
            Oohra.Core.Season.Autumn => "Podzim",
            _ => "Zima",
        };
    }
}
