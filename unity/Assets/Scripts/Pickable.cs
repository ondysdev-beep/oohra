using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Na co se dá kliknout: strom, rostlina, hromada kamenů, stavba, zvíře, hráč… Na objektu je neviditelná
    /// kolizní krabice (jen pro kliknutí, pohyb a srážky počítá server) a údaje, čím objekt je.
    /// PlayerController paprskem z kamery najde nejbližší takový objekt, takže klik na korunu stromu
    /// označí strom, ne zem za ním. Zprůhledněný objekt (SeeThrough) klik propustí.
    /// </summary>
    public class Pickable : MonoBehaviour
    {
        public TargetKind Kind;
        public int Id;
        public int TileX;
        public int TileY;
        public double X;
        public double Y;

        /// <summary>
        /// Přidá objektu krabici přes všechny jeho viditelné části a údaje o cíli. Krabice se dá na kořen objektu
        /// (v jeho souřadnicích), takže se hýbe a otáčí s ním.
        /// </summary>
        public static Pickable Attach(GameObject go, InteractionTarget target)
        {
            // Pozor: v Unity nefunguje ?? s komponentami (chybějící komponenta není „opravdové“ null).
            var pickable = go.GetComponent<Pickable>();
            if (pickable == null)
            {
                pickable = go.AddComponent<Pickable>();
            }

            pickable.Kind = target.Kind;
            pickable.Id = target.Id;
            pickable.TileX = target.TileX;
            pickable.TileY = target.TileY;
            pickable.X = target.X;
            pickable.Y = target.Y;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                // Vždy nová krabice: tvary z kódu mají svou kolizi, ta se ale na konci snímku maže (IslandView.Primitive).
                var box = go.AddComponent<BoxCollider>();
                var bounds = LocalBounds(go.transform, renderers);
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
            }

            return pickable;
        }

        /// <summary>Cíl pro ClientSession (u zvířat a hráčů se živá poloha bere ze serveru, viz PositionOf).</summary>
        public InteractionTarget Target()
        {
            switch (Kind)
            {
                case TargetKind.Plant: return InteractionTarget.ForPlant(Id, X, Y);
                case TargetKind.Fire: return InteractionTarget.ForFire(Id, X, Y);
                case TargetKind.Structure: return InteractionTarget.ForStructure(Id, X, Y);
                case TargetKind.Animal: return InteractionTarget.ForAnimal(Id, X, Y);
                case TargetKind.Sign: return InteractionTarget.ForSign(Id, X, Y);
                case TargetKind.Player: return InteractionTarget.ForPlayer(Id, X, Y);
                case TargetKind.Corpse: return InteractionTarget.ForCorpse(Id, X, Y);
                case TargetKind.Stranger: return InteractionTarget.ForStranger(Id, X, Y);
                default: return InteractionTarget.ForTile(Kind, TileX, TileY);
            }
        }

        /// <summary>Kvádr kolem všech částí v souřadnicích kořene (přes rohy jejich krabic, i když je kořen otočený).</summary>
        private static Bounds LocalBounds(Transform root, Renderer[] renderers)
        {
            var first = true;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = root.InverseTransformPoint(corner);
                    if (first)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return bounds;
        }
    }
}
