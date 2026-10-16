using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vzhled postavy podle generace (návrh 8.4), parametrický: pár čísel a doplňků, ne kreslená postava pro každou generaci.
    /// Každá generace světa (půl roku) vypadá jinak. Pradávní (0) jsou mohutní, v kožešinovém plášti, s ochrou
    /// a kostěnými korálky. Noví (1) jsou vyšší a štíhlejší, v prostém světlém oděvu. Třetí generace (2) je ještě
    /// štíhlejší, v tkaném zeleném oděvu s páskou přes hruď a čelenkou s pírkem. Další generace k tomu přidají
    /// korálky (od 4) a malbu (od 5). Hra to nikde nevysvětluje.
    /// Vlastní model z katalogu dostane jen postavu (šířku a výšku) a barvu generace (části se SpeciesTint),
    /// doplňky z kódu jen tvar z kódu. V katalogu může mít vlastní model Pradávný (PlayerAncient) a třetí
    /// generace (PlayerThird), ostatní Player.
    /// </summary>
    public static class GenerationLook
    {
        public const string FigureName = "Figura";

        // Rozměry postavy z kódu (1 dlaždice = 1 m): výška 1,8 m, poloměr jako kolize na serveru.
        private const float Height = 1.8f;
        private const float Radius = 0.28f;

        private static readonly Color Fur = Palette.Hex("#5a4330");
        private static readonly Color Ochre = Palette.Hex("#c0612b");
        private static readonly Color Bone = Palette.Hex("#e9e0c9");
        private static readonly Color Headband = Palette.Hex("#8c3b2a");
        private static readonly Color Bead = Palette.Hex("#4f8fa6");
        private static readonly Color Paint = Palette.Hex("#ece6d6");
        private static readonly Color Sash = Palette.Hex("#d9c38a");
        private static readonly Color Feather = Palette.Hex("#f2efe6");

        /// <summary>Jak postava dané generace vypadá.</summary>
        public struct Look
        {
            /// <summary>Násobek šířky (Pradávný 1,15, Noví štíhlejší).</summary>
            public float Width;

            /// <summary>Násobek výšky (Pradávný o kousek menší, Noví vyšší).</summary>
            public float Height;

            /// <summary>Barva těla a oděvu.</summary>
            public Color Body;

            public bool Cloak;
            public bool Ochre;
            public bool BoneBeads;
            public bool Headband;
            public bool Beads;
            public bool Paint;

            /// <summary>Páska přes hruď (třetí generace a další).</summary>
            public bool Sash;

            /// <summary>Pírko za čelenkou (třetí generace a další).</summary>
            public bool Feather;
        }

        public static Look For(int generation)
        {
            if (generation <= 0)
            {
                return new Look { Width = 1.15f, Height = 0.97f, Body = Palette.Hex("#8a6a45"), Cloak = true, Ochre = true, BoneBeads = true };
            }

            var third = generation >= 2;
            return new Look
            {
                Width = 1f - 0.03f * Mathf.Min(generation, 3),
                Height = 1f + 0.015f * Mathf.Min(generation, 4),
                // Noví světlý prostý oděv, třetí generace a další tkaný zelený.
                Body = third ? Palette.Hex("#6f8a6a") : Palette.Hex("#c5a97e"),
                Headband = third,
                Sash = third,
                Feather = third,
                Beads = generation >= 4,
                Paint = generation >= 5,
            };
        }

        /// <summary>
        /// Model z katalogu pro generaci: Pradávný a třetí generace mají vlastní, když je vyplněný, jinak Player.
        /// Null = tvar z kódu.
        /// </summary>
        public static GameObject ModelFor(int generation)
        {
            var catalog = VisualCatalog.Current;
            // Pozor: v Unity nefunguje ?? s objekty (prázdné políčko není „opravdové“ null).
            if (generation <= 0 && catalog.PlayerAncient != null)
            {
                return catalog.PlayerAncient;
            }

            if (generation >= 2 && catalog.PlayerThird != null)
            {
                return catalog.PlayerThird;
            }

            return catalog.Player != null ? catalog.Player : null;
        }

        /// <summary>
        /// Postava dané generace pod parent, nohy dole v počátku, obličej ve směru osy Z: vlastní model z katalogu,
        /// jinak tvar z kódu s doplňky. Šířka a výška se nastaví zvětšením celé postavy.
        /// </summary>
        public static GameObject Figure(Transform parent, int generation)
        {
            var look = For(generation);
            var figure = new GameObject(FigureName);
            figure.transform.SetParent(parent, false);
            var model = ModelFor(generation);
            if (model != null)
            {
                VisualCatalog.Spawn(model, figure.transform, "Model");
                SpeciesTint.ApplyMarked(figure, look.Body);
            }
            else
            {
                BuildShape(figure.transform, look);
            }

            figure.transform.localScale = new Vector3(look.Width, look.Height, look.Width);
            return figure;
        }

        /// <summary>Tvar z kódu: kapsle s „nosem“ (kam se postava dívá) a doplňky generace.</summary>
        private static void BuildShape(Transform figure, Look look)
        {
            // Tělo nese SpeciesTint, aby šlo přebarvit (tělo mrtvého je tmavší).
            Part(PrimitiveType.Capsule, "Tělo", figure, look.Body, new Vector3(0f, Height / 2, 0f), new Vector3(Radius * 2, Height / 2, Radius * 2)).AddComponent<SpeciesTint>();
            Part(PrimitiveType.Cube, "Směr", figure, Palette.Hex("#dba23f"), new Vector3(0f, 1.35f, 0.28f), new Vector3(0.17f, 0.13f, 0.22f));

            if (look.Cloak)
            {
                // Kožešina přes ramena a dolů po zádech.
                Part(PrimitiveType.Cylinder, "Plášť", figure, Fur, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.2f, 0.7f));
                Part(PrimitiveType.Cube, "Plášť vzadu", figure, Fur, new Vector3(0f, 0.85f, -0.25f), new Vector3(0.52f, 0.85f, 0.07f));
            }

            if (look.Ochre)
            {
                Part(PrimitiveType.Cube, "Ochra", figure, Ochre, new Vector3(0f, 1.62f, 0.25f), new Vector3(0.3f, 0.04f, 0.04f));
            }

            if (look.BoneBeads)
            {
                for (var i = -1; i <= 1; i++)
                {
                    Part(PrimitiveType.Sphere, "Kostěný korálek", figure, Bone, new Vector3(i * 0.1f, 1.42f, 0.36f), Vector3.one * 0.06f);
                }
            }

            if (look.Headband)
            {
                Part(PrimitiveType.Cylinder, "Čelenka", figure, Headband, new Vector3(0f, 1.63f, 0f), new Vector3(0.57f, 0.025f, 0.57f));
            }

            if (look.Sash)
            {
                // Tkaná páska šikmo přes hruď, z ramene k boku.
                Part(PrimitiveType.Cube, "Páska", figure, Sash, new Vector3(0f, 1.12f, 0.27f), new Vector3(0.09f, 0.62f, 0.04f), Quaternion.Euler(0f, 0f, 38f));
            }

            if (look.Feather)
            {
                Part(PrimitiveType.Cube, "Pírko", figure, Feather, new Vector3(0.18f, 1.8f, -0.12f), new Vector3(0.04f, 0.3f, 0.03f), Quaternion.Euler(-20f, 0f, -18f));
            }

            if (look.Beads)
            {
                for (var i = 0; i < 4; i++)
                {
                    Part(PrimitiveType.Sphere, "Korálek", figure, Bead, new Vector3(-0.12f + i * 0.08f, 1.4f, 0.29f), Vector3.one * 0.05f);
                }
            }

            if (look.Paint)
            {
                Part(PrimitiveType.Cube, "Malba", figure, Paint, new Vector3(-0.08f, 1.57f, 0.26f), new Vector3(0.03f, 0.12f, 0.02f));
                Part(PrimitiveType.Cube, "Malba", figure, Paint, new Vector3(0.08f, 1.57f, 0.26f), new Vector3(0.03f, 0.12f, 0.02f));
            }
        }

        private static GameObject Part(PrimitiveType type, string name, Transform figure, Color color, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var part = IslandView.Primitive(type, name, figure, color);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.transform.localScale = scale;
            return part;
        }
    }
}
