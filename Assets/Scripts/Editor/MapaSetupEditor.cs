#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HardEntry.EditorTools
{
    /// <summary>Cria a estrutura de pastas e a cena do mapa (para o colega que faz o mundo).</summary>
    public static class MapaSetupMenu
    {
        const string Root = "Assets/Mapa";
        const string ScenePath = Root + "/Mapa.unity";

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        [MenuItem("Hard Entry/Equipa/Criar pasta e cena do Mapa")]
        public static void Create()
        {
            // 1) Pastas
            EnsureFolder("Assets", "Mapa");
            foreach (var f in new[] { "Modelos", "Materiais", "Texturas", "Prefabs", "Iluminacao" })
                EnsureFolder(Root, f);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.LogWarning("[Mapa] A cena " + ScenePath + " já existe. Não mexi em nada.");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
                return;
            }

            // Guardar a cena atual antes de trocar
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;

            // 2) Cena nova com o básico
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Luz_Sol");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Global Volume (pós-processamento) se o projeto já tiver um perfil
            var profile = AssetDatabase.FindAssets("t:VolumeProfile")
                .Select(g => AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(p => p != null);
            if (profile != null)
            {
                var vol = new GameObject("Global Volume").AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Chao_Temporario";
            chao.transform.localScale = new Vector3(5f, 1f, 5f); // 50 x 50 m

            var mundo = new GameObject("--- MUNDO (o mapa vai aqui dentro) ---");
            chao.transform.SetParent(mundo.transform, true);

            var spawn = new GameObject("PontoInicio_Jogador");
            spawn.transform.position = Vector3.zero;

            new GameObject("--- JOGADOR E ARMAS (prefabs do Leonardo) ---");

            EditorSceneManager.SaveScene(scene, ScenePath);

            // 3) Adicionar às Build Settings
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            // 4) Voltar à cena onde estavas
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log("[Mapa] Criado: " + Root + " (Modelos, Materiais, Texturas, Prefabs, Iluminacao) + cena Mapa.unity.");
        }
    }
}
#endif
