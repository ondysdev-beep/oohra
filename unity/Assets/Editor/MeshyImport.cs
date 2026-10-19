using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Oohra.Client.Editor
{
    /// <summary>
    /// Příprava modelu z Meshy (návod v docs/Grafika.md): ve složce leží jmeno.fbx, jmeno.png (barvy) a
    /// jmeno_normal.png (normály). Nastaví textury (barva se sRGB, normálová jako Normal map, nejvýš maxSize),
    /// vytvoří materiál jmeno.mat (URP Lit jako OohraLit, bez kovu, hladkost 0,2) a dá ho všem materiálům modelu.
    /// </summary>
    public static class MeshyImport
    {
        [MenuItem("Oohra/Připravit model z Meshy (vybraný FBX)")]
        public static void PrepareSelected()
        {
            foreach (var path in Selection.objects.Select(AssetDatabase.GetAssetPath).Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)))
            {
                Prepare(path);
                Debug.Log("Model připravený: " + path);
            }
        }

        /// <summary>Připraví model a vrátí jeho kořen (pro katalog vzhledu).</summary>
        public static GameObject Prepare(string fbxPath, int maxSize = 1024, float smoothness = 0.2f)
        {
            var folder = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var color = SetTexture(folder + "/" + name + ".png", false, maxSize);
            var normal = SetTexture(folder + "/" + name + "_normal.png", true, maxSize);

            var materialPath = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/OohraLit.mat");
                material = new Material(lit.shader) { name = name };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", color);
            material.SetTexture("_MainTex", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);

            // Všechny materiály z FBX nahradit tím jedním (Meshy dává jednu texturu na celý model).
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>())
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        }

        private static Texture2D SetTexture(string path, bool normal, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
