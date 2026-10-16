#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HardEntry.Weapons;

namespace HardEntry.EditorTools
{
    /// <summary>
    /// Mostra, sem headset, como fica a mão da Meta na pose de pegada de uma arma.
    /// Cria uma cópia da mão direita ("PREVIEW_Pegada") colada à arma selecionada (ou G17).
    /// </summary>
    public static class HandPosePreviewMenu
    {
        static readonly string[][] Chains =
        {
            new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" },
            new[] { "IndexProximal", "IndexIntermediate", "IndexDistal", "IndexTip" },
            new[] { "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "MiddleTip" },
            new[] { "RingProximal", "RingIntermediate", "RingDistal", "RingTip" },
            new[] { "LittleProximal", "LittleIntermediate", "LittleDistal", "LittleTip" },
        };

        static Quaternion Frame(Vector3 wrist, Vector3 ip, Vector3 lp, Vector3 mp)
        {
            Vector3 across = (ip - lp).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(mp - wrist, across).normalized;
            return Quaternion.LookRotation(fwd, across);
        }

        [MenuItem("Hard Entry/Armas/Pré-visualizar pegada da G17")]
        public static void Preview() => Make("G17", "G17", true);

        // Em Play: finge que a mão direita agarrou a G17 (para testar sem headset na mão)
        [MenuItem("Hard Entry/Debug/Simular agarrar G17 (em Play)")]
        public static void SimGrab()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Debug] Isto só funciona em Play."); return; }
            var gun = GameObject.Find("G17");
            var w = HandRig.Wrist(true);
            if (gun == null || w == null) { Debug.LogError("[Debug] Falta a G17 ou a mão direita."); return; }
            var rb = gun.GetComponent<Rigidbody>();
            gun.transform.position = w.position;
            rb.isKinematic = !rb.isKinematic;
            Debug.Log("[Debug] G17 " + (rb.isKinematic ? "agarrada (simulado)" : "largada"));
        }

        [MenuItem("Hard Entry/Armas/Apagar pré-visualização da pegada")]
        public static void Clear()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == "PREVIEW_Pegada") Object.DestroyImmediate(t.gameObject);
        }

        static void StripScripts(GameObject go)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
                if (mbs.Length == 0) break;
                for (int i = mbs.Length - 1; i >= 0; i--)
                {
                    try { if (mbs[i] != null) Object.DestroyImmediate(mbs[i]); } catch { }
                }
            }
        }

        public static void Make(string gunName, string poseName, bool right)
        {
            Clear();
            var gunSrc = GameObject.Find(gunName);
            if (gunSrc == null) { Debug.LogError("[Preview] Não encontrei " + gunName); return; }
            // cópia da arma, direita e num sítio limpo (cano para +Z, topo para +Y)
            var root = new GameObject("PREVIEW_Pegada");
            root.transform.position = new Vector3(0f, 2.0f, -3f);
            var gun = Object.Instantiate(gunSrc, root.transform);
            gun.name = gunName + "_preview";
            StripScripts(gun);
            foreach (var rb in gun.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in gun.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            gun.transform.localPosition = Vector3.zero;
            gun.transform.localRotation = Quaternion.identity;
            var pose = HandPoseLibrary.Get(poseName);

            // mão visível da Meta (direita)
            Transform srcWrist = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name != "XRHand_Wrist") continue;
                string chain = "";
                for (var p = t.parent; p != null; p = p.parent) chain += p.name + "/";
                if (chain.Contains("HandVisual") && chain.Contains(right ? "Right" : "Left")) { srcWrist = t; break; }
            }
            if (srcWrist == null) { Debug.LogError("[Preview] Não encontrei a mão da Meta na cena."); return; }
            var visual = srcWrist.GetComponentInParent<Oculus.Interaction.HandVisual>(true);
            var src = visual != null ? visual.gameObject : srcWrist.parent.gameObject;

            var copy = Object.Instantiate(src);
            copy.name = "Mao_preview";
            copy.SetActive(true);
            copy.transform.SetParent(root.transform, true);
            StripScripts(copy);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

            var J = new Dictionary<string, Transform>();
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("XRHand_")) J[t.name.Substring(7)] = t;

            Vector3 P(string n) { var v = pose[n]; if (!right) v.x = -v.x; return v; }

            // 1) mão inteira: frame e palma como na pose
            Quaternion cur = Frame(J["Wrist"].position, J["IndexProximal"].position, J["LittleProximal"].position, J["MiddleProximal"].position);
            Quaternion want = gun.transform.rotation * Frame(P("Wrist"), P("IndexProximal"), P("LittleProximal"), P("MiddleProximal"));
            Quaternion d = want * Quaternion.Inverse(cur);
            copy.transform.rotation = d * copy.transform.rotation;
            // alinhar pelos nós dos dedos (como o GunHandPose no jogo)
            string[] kn = { "IndexProximal", "MiddleProximal", "RingProximal", "LittleProximal" };
            Vector3 knWant = Vector3.zero, knCur = Vector3.zero;
            foreach (var k in kn) { knWant += gun.transform.TransformPoint(P(k)); knCur += J[k].position; }
            copy.transform.position += (knWant - knCur) / kn.Length;

            // 2) dedos
            foreach (var ch in Chains)
                for (int i = 0; i < ch.Length - 1; i++)
                {
                    var a = J[ch[i]]; var b = J[ch[i + 1]];
                    Vector3 c = b.position - a.position;
                    Vector3 w = gun.transform.rotation * (P(ch[i + 1]) - P(ch[i]));
                    a.rotation = Quaternion.FromToRotation(c, w) * a.rotation;
                }

            // manga no pulso (como no jogo)
            HardEntry.Gear.HandSleeves.Build(J["Wrist"], right);

            // erros (mm) entre a mão e a pose
            float max = 0f; string worst = "";
            foreach (var kv in pose)
                if (J.TryGetValue(kv.Key, out var t))
                {
                    float e = Vector3.Distance(t.position, gun.transform.TransformPoint(P(kv.Key))) * 1000f;
                    if (e > max) { max = e; worst = kv.Key; }
                }
            Debug.Log($"[Preview] Pegada {poseName} mostrada. Maior diferença: {max:0} mm ({worst}). Escala da mão: {copy.transform.lossyScale.x:0.000}");

            Selection.activeGameObject = null;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                var bounds = new Bounds(gun.transform.position, Vector3.one * 0.05f);
                foreach (var r in gun.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                foreach (var r in copy.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                sv.Frame(bounds, false);
            }
        }
    }
}
#endif
