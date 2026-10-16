#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HardEntry.EditorTools
{
    /// <summary>Importação das texturas da luva tática.</summary>
    public class GloveTacticalImport : AssetPostprocessor
    {
        const string Folder = "Assets/Hands/Gloves/Tatica/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_MetallicSmoothness.png") || assetPath.EndsWith("_AO.png")) ti.sRGBTexture = false;
        }
    }

    public static class GloveTacticalMenu
    {
        const string Folder = "Assets/Hands/Gloves/Tatica/";
        const string MatPath = Folder + "Luva_Tatica.mat";
        const string OldMatPath = "Assets/Hands/Luva_Tatica_Preta.mat";

        [MenuItem("Hard Entry/Luvas/Aplicar luva tática")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MatPath);
            }
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_BaseColor.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_MetallicSmoothness.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_AO.png");
            if (bc == null) { Debug.LogError("[Luva] Texturas em falta em " + Folder); return; }

            m.SetTexture("_BaseMap", bc);
            m.SetColor("_BaseColor", Color.white);
            if (nm) { m.SetTexture("_BumpMap", nm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (ms) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); }
            if (ao) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            int n = Swap(AssetDatabase.LoadAssetAtPath<Material>(OldMatPath), m);
            Debug.Log($"[Luva] Luva tática aplicada em {n} mão(s). Para voltar: Hard Entry > Luvas > Voltar à luva preta lisa.");
        }

        [MenuItem("Hard Entry/Luvas/Voltar à luva preta lisa")]
        public static void Revert()
        {
            int n = Swap(AssetDatabase.LoadAssetAtPath<Material>(MatPath), AssetDatabase.LoadAssetAtPath<Material>(OldMatPath));
            Debug.Log($"[Luva] Luva preta lisa reposta em {n} mão(s).");
        }

        // troca o material das mãos da Meta (SkinnedMeshRenderer debaixo de "HandVisual")
        static int Swap(Material from, Material to)
        {
            if (to == null) { Debug.LogError("[Luva] Material de destino não encontrado."); return 0; }
            int count = 0;
            foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
            {
                bool isHand = false;
                for (var p = smr.transform; p != null; p = p.parent)
                    if (p.name.Contains("HandVisual")) { isHand = true; break; }
                if (!isHand) continue;

                var mats = smr.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == to) continue;
                    if (from == null || mats[i] == from || mats.Length == 1) { mats[i] = to; changed = true; }
                }
                if (!changed) continue;
                Undo.RecordObject(smr, "Luva");
                smr.sharedMaterials = mats;
                EditorUtility.SetDirty(smr);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(smr.gameObject.scene);
                count++;
            }
            return count;
        }
    }
}
#endif
