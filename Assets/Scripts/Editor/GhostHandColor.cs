#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HardEntry.EditorTools
{
    /// <summary>
    /// Pinta a "mão fantasma" dos HandGrabPose (Meta Interaction SDK) com um material sólido,
    /// para ser fácil de ver e ajustar no editor. Só afeta o editor, não o jogo.
    /// Liga/desliga em: Hard Entry > Armas > Mão fantasma sólida
    /// </summary>
    [InitializeOnLoad]
    public static class GhostHandColor
    {
        const string MatPath = "Assets/Hands/Luva_Tatica_Preta.mat";
        const string PrefKey = "HardEntry_GhostSolid";
        const string MenuPath = "Hard Entry/Armas/Mão fantasma sólida";
        static double _next;
        static Material _mat;

        static GhostHandColor()
        {
            EditorApplication.update += Tick;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        static void Toggle() { Enabled = !Enabled; SceneView.RepaintAll(); }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Enabled); return true; }

        static void Tick()
        {
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;

            if (_mat == null) _mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (_mat == null) return;

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null || mb.GetType().Name != "HandGrabPose") continue;
                foreach (var r in mb.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != _mat) { mats[i] = _mat; changed = true; }
                    if (changed) r.sharedMaterials = mats;
                }
            }
        }
    }
}
#endif
