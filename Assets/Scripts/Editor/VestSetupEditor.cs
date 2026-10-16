#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HardEntry.Gear;
using HardEntry.Weapons;

namespace HardEntry.EditorTools
{
    public class VestImport : AssetPostprocessor
    {
        const string Folder = "Assets/Gear/Colete/";
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            ((TextureImporter)assetImporter).maxTextureSize = 2048;
        }
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    public static class VestSetupMenu
    {
        const string Folder = "Assets/Gear/Colete/";

        [MenuItem("Hard Entry/Equipamento/Montar colete com bolsas")]
        public static void Build()
        {
            string fbx = Folder + "Colete.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) { Debug.LogError("[Colete] Não encontrei " + fbx); return; }

            // material URP
            string matPath = Folder + "Colete.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Colete_BaseColor.jpg");
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.15f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Colete"), m);
            importer.SaveAndReimport();

            // na cena
            var old = GameObject.Find("Colete_Jogador");
            if (old) Undo.DestroyObjectImmediate(old);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "Colete_Jogador";
            Undo.RegisterCreatedObjectUndo(go, "Colete");
            go.AddComponent<PlayerVest>();
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int n = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Bolsa_")) continue;
                var parts = t.name.Split('_'); // Bolsa_G17_1
                var p = t.gameObject.AddComponent<MagazinePouch>();
                p.weaponId = parts.Length > 1 ? parts[1] : "G17";
                p.count = -1;
                // y = para cima, z = para a frente do colete
                t.rotation = go.transform.rotation;
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            Debug.Log($"[Colete] Colete montado com {n} bolsas. No jogo fica no teu peito; tira carregadores com a mão livre + grip.");
        }
    }
}
#endif
