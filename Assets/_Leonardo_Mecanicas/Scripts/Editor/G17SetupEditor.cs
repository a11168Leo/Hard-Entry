#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HardEntry.Weapons;

namespace HardEntry.EditorTools
{
    /// <summary>Importação automática das texturas/modelo da G17.</summary>
    public class G17ImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/_Leonardo_Mecanicas/Arsenal/G17/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_MetallicSmoothness.png") || assetPath.EndsWith("_AO.png")) ti.sRGBTexture = false;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importAnimation = false;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    public static class G17SetupMenu
    {
        const string Folder = "Assets/_Leonardo_Mecanicas/Arsenal/G17/";
        const string Tex = Folder + "Textures/";

        static Material MakeMat(string name, bool emissive = false)
        {
            string path = Folder + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder + "Materials")) AssetDatabase.CreateFolder("Assets/_Leonardo_Mecanicas/Arsenal/G17", "Materials");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + name + "_BaseColor.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + name + "_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + name + "_MetallicSmoothness.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + name + "_AO.png");
            if (bc) m.SetTexture("_BaseMap", bc);
            m.SetColor("_BaseColor", Color.white);
            if (nm) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); }
            if (ms) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); }
            if (ao) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (emissive)
            {
                var em = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "G17_Slide_Emissive.png");
                if (em)
                {
                    m.SetTexture("_EmissionMap", em);
                    m.SetColor("_EmissionColor", Color.white * 1.5f);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeLens()
        {
            string path = Folder + "Materials/Olight_Lens.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f));
            m.SetFloat("_Metallic", 0.2f);
            m.SetFloat("_Smoothness", 0.95f);
            EditorUtility.SetDirty(m);
            return m;
        }

        [MenuItem("Hard Entry/Armas/Montar G17")]
        public static void Build()
        {
            string modelPath = Folder + "G17.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[G17] Não encontrei " + modelPath); return; }

            // 1) Materiais URP
            var frame = MakeMat("G17_Frame");
            var slide = MakeMat("G17_Slide", emissive: true);
            var mag = MakeMat("G17_Mag");
            var light = MakeMat("Olight");
            var lens = MakeLens();
            AssetDatabase.SaveAssets();

            // 2) Ligar os materiais do .fbx aos nossos
            void Remap(string src, Material dst) => importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src), dst);
            Remap("g17_gen5_frame", frame);
            Remap("g17_gen5_slide", slide);
            Remap("g17_gen5_mag_fde", mag);
            Remap("olight_pl2_valkyrie", light);
            Remap("olight_pl2_valkyrie_lens", lens);
            importer.SaveAndReimport();

            // 3) Prefab com física + pegada fixa
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "G17";

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.9f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            foreach (var name in new[] { "Armacao", "Corredica", "Carregador" })
            {
                var t = go.transform.Find(name);
                if (t == null) { Debug.LogWarning("[G17] Peça em falta: " + name); continue; }
                var mf = t.GetComponent<MeshFilter>();
                if (mf && !t.GetComponent<Collider>())
                {
                    var mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }

            const string gripPath = "Assets/_Leonardo_Mecanicas/Arsenal/GripData_G17.asset";
            var grip = AssetDatabase.LoadAssetAtPath<GunGripData>(gripPath);
            if (grip == null) { grip = ScriptableObject.CreateInstance<GunGripData>(); AssetDatabase.CreateAsset(grip, gripPath); }
            var snap = go.GetComponent<GunGripSnap>();
            if (snap == null) snap = go.AddComponent<GunGripSnap>();
            snap.gripData = grip;

            string prefabPath = Folder + "G17.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.UserAction);

            // 4) Pôr na mesa de teste e esconder a pistola antiga
            go.transform.position = new Vector3(0.15f, 1.0f, 0.55f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var old = GameObject.Find("Pistola_Teste");
            if (old) old.SetActive(false);
            Undo.RegisterCreatedObjectUndo(go, "G17");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;

            // Informação de orientação (para confirmar que o cano aponta para a frente, +Z)
            var muzzle = go.transform.Find("PontaCano");
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Debug.Log($"[G17] Prefab criado em {prefabPath}. Tamanho (m): {bounds.size}. Ponta do cano (local): {(muzzle ? muzzle.localPosition.ToString("F3") : "?")}");
        }
    }
}
#endif
