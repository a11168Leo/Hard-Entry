#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HardEntry.Hands;

namespace HardEntry.EditorTools
{
    /// <summary>Configura automaticamente a importação dos ficheiros das luvas.</summary>
    public class GloveImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/_Leonardo_Mecanicas/Maos/Gloves/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = true;                                   // o script precisa de ler a malha
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_MetallicSmoothness.png")) ti.sRGBTexture = false;
        }
    }

    public static class GloveSetupMenu
    {
        const string Folder = "Assets/_Leonardo_Mecanicas/Maos/Gloves/";

        [MenuItem("Hard Entry/Luvas/Configurar luvas na cena")]
        public static void Setup()
        {
            // 1) Material URP/Lit com as texturas da luva
            string matPath = Folder + "Luva_Skin01.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_BaseColor.png");
            var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_Normal.png");
            var msTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Luva_MetallicSmoothness.png");
            if (baseTex) mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            if (normTex) { mat.SetTexture("_BumpMap", normTex); mat.EnableKeyword("_NORMALMAP"); }
            if (msTex)
            {
                mat.SetTexture("_MetallicGlossMap", msTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            // 2) Gestor das luvas na cena
            var mgr = Object.FindAnyObjectByType<GloveSkinManager>();
            if (mgr == null)
            {
                var go = new GameObject("GloveSkinManager");
                mgr = go.AddComponent<GloveSkinManager>();
                Undo.RegisterCreatedObjectUndo(go, "Criar GloveSkinManager");
            }
            mgr.gloveRightModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Luva_Direita.fbx");
            mgr.gloveLeftModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Luva_Esquerda.fbx");
            mgr.gloveMaterial = mat;
            EditorUtility.SetDirty(mgr);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
            Selection.activeObject = mgr.gameObject;

            Debug.Log($"[GloveSkin] Configurado. Direita={(mgr.gloveRightModel != null)}, Esquerda={(mgr.gloveLeftModel != null)}, Material={matPath}");
        }
    }
}
#endif
