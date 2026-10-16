#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HardEntry.Weapons;

namespace HardEntry.EditorTools
{
    public static class GunGripSetupMenu
    {
        [MenuItem("Hard Entry/Armas/Adicionar pegada fixa à pistola")]
        public static void Setup()
        {
            var gun = GameObject.Find("Pistola_Teste");
            if (gun == null) { Debug.LogError("[GunGrip] Não encontrei 'Pistola_Teste' na cena."); return; }

            const string folder = "Assets/_Leonardo_Mecanicas/Arsenal";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Leonardo_Mecanicas", "Arsenal");
            const string path = folder + "/GripData_M1911.asset";
            var data = AssetDatabase.LoadAssetAtPath<GunGripData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<GunGripData>();
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
            }

            var snap = gun.GetComponent<GunGripSnap>();
            if (snap == null) snap = Undo.AddComponent<GunGripSnap>(gun);
            snap.gripData = data;
            EditorUtility.SetDirty(snap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.scene);
            Selection.activeObject = gun;
            Debug.Log("[GunGrip] Pegada fixa adicionada. No jogo: agarra a arma como quiseres e carrega em B (direita) / Y (esquerda) para gravar.");
        }
    }
}
#endif
