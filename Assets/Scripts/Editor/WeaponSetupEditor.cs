#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HardEntry.EditorTools
{
    public static class WeaponSetupMenu
    {
        const string Root = "Assets/Nokobot/Modern Guns - Handgun/";

        [MenuItem("Hard Entry/Armas/Converter materiais da pistola para URP")]
        public static void ConvertMaterials()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "Materials" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == urpLit) continue;
                var albedo = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                var color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                var normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                var metal = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                float gloss = m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : 1f;

                m.shader = urpLit;
                if (albedo) m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", color);
                if (normal) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                if (metal) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", gloss); }
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Armas] Materiais convertidos para URP: {n}");
        }

        [MenuItem("Hard Entry/Armas/Pôr pistola de teste na cena")]
        public static void SpawnTestPistol()
        {
            ConvertMaterials();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "_Prefabs/Handgun Black/M1911 Handgun_Black.prefab");
            if (prefab == null) { Debug.LogError("[Armas] Não encontrei o prefab da pistola."); return; }

            // Mesa simples à frente do jogador
            var table = GameObject.Find("Mesa_Teste");
            if (table == null)
            {
                table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Mesa_Teste";
                table.transform.position = new Vector3(0f, 0.45f, 0.6f);
                table.transform.localScale = new Vector3(1.0f, 0.9f, 0.5f);
                Undo.RegisterCreatedObjectUndo(table, "Mesa");
            }

            var gun = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            gun.name = "Pistola_Teste";
            gun.transform.position = new Vector3(0f, 0.93f, 0.55f);
            gun.transform.rotation = Quaternion.Euler(0f, 90f, 90f); // deitada na mesa
            Undo.RegisterCreatedObjectUndo(gun, "Pistola");

            // Física básica para poder pousar e agarrar
            if (!gun.GetComponent<Rigidbody>())
            {
                var rb = gun.AddComponent<Rigidbody>();
                rb.mass = 1.1f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            if (!gun.GetComponentInChildren<Collider>())
            {
                foreach (var mf in gun.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
            }

            Selection.activeObject = gun;
            SceneView.lastActiveSceneView?.FrameSelected();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.scene);
            Debug.Log("[Armas] Pistola de teste colocada na cena (em cima da Mesa_Teste).");
        }
    }
}
#endif
