#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HardEntry.Weapons;

namespace HardEntry.EditorTools
{
    public static class PistolSetupMenu
    {
        [MenuItem("Hard Entry/Armas/Ativar disparo na G17")]
        public static void Setup()
        {
            var gun = GameObject.Find("G17");
            if (gun == null) { Debug.LogError("[Pistola] Não encontrei o objeto 'G17' na cena."); return; }
            var pc = gun.GetComponent<PistolController>();
            if (pc == null) pc = Undo.AddComponent<PistolController>(gun);

            // Tenta ligar sons automaticamente se existirem em Assets/Audio/Armas
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g).ToLower();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
                if (path.Contains("tiro") || path.Contains("shot") || path.Contains("fire")) { if (pc.shotSound == null) pc.shotSound = clip; }
                else if (path.Contains("click") || path.Contains("clic") || path.Contains("empty") || path.Contains("dry")) { if (pc.emptyClickSound == null) pc.emptyClickSound = clip; }
                else if (path.Contains("magout") || path.Contains("mag_out") || path.Contains("carregador_sai")) { if (pc.magOutSound == null) pc.magOutSound = clip; }
                else if (path.Contains("magin") || path.Contains("mag_in") || path.Contains("carregador_entra")) { if (pc.magInSound == null) pc.magInSound = clip; }
                else if (path.Contains("slide") || path.Contains("rack") || path.Contains("corredica")) { if (pc.slideReleaseSound == null) pc.slideReleaseSound = clip; }
            }

            EditorUtility.SetDirty(pc);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.scene);
            Selection.activeObject = gun;
            Debug.Log("[Pistola] Disparo ativado na G17. Gatilho = disparar, A/X = carregador.");
        }
    }
}
#endif
