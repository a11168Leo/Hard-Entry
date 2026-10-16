#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace HardEntry.Weapons
{
    /// <summary>
    /// SÓ NO EDITOR: quando seguras uma arma no Play, tira fotos da mão + arma de vários lados
    /// e guarda-as na pasta "Capturas" do projeto (fora de Assets), com um ficheiro de dados.
    /// Serve para ver a pegada sem teres de tirar prints.
    /// </summary>
    public class GripDebugCapture : MonoBehaviour
    {
        public float holdSecondsBeforeCapture = 1.5f;
        public float minSecondsBetweenCaptures = 15f;
        public int resolution = 768;

        float _heldTime, _lastCapture = -999f;
        int _count;
        bool _capturedThisGrab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<GripDebugCapture>() != null) return;
            var go = new GameObject("CapturaPegada (editor)");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<GripDebugCapture>();
        }

        void LateUpdate()
        {
            GunGripSnap held = null;
            foreach (var g in GunGripSnap.All) if (g != null && g.IsHeld && g.SnapSettled) { held = g; break; }
            if (held == null) { _heldTime = 0f; _capturedThisGrab = false; return; }
            _heldTime += Time.unscaledDeltaTime;
            if (_capturedThisGrab || _count >= 10) return; // uma vez por pegada, no máximo 10 por sessão
            if (_heldTime < holdSecondsBeforeCapture || Time.unscaledTime - _lastCapture < minSecondsBetweenCaptures) return;
            _lastCapture = Time.unscaledTime;
            _capturedThisGrab = true;
            Capture(held);
        }

        void Capture(GunGripSnap gun)
        {
            string dir = Path.Combine(Application.dataPath, "..", "Capturas");
            Directory.CreateDirectory(dir);
            string stamp = System.DateTime.Now.ToString("HHmmss") + "_" + (_count++);

            // vista do headset (Game view)
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, stamp + "_headset.png"));

            // centro: arma + mão
            var b = new Bounds(gun.transform.position, Vector3.one * 0.05f);
            foreach (var r in gun.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            if (gun.HolderWrist != null) b.Encapsulate(gun.HolderWrist.position);
            Vector3 c = b.center;
            Transform t = gun.transform;

            var views = new (string name, Vector3 dir)[]
            {
                ("lado_direito", t.right),
                ("lado_esquerdo", -t.right),
                ("atras_cima", (-t.forward + t.up * 0.5f).normalized),
                ("cima", (t.up + t.right * 0.2f).normalized),
            };

            var camGo = new GameObject("CamCaptura");
            var cam = camGo.AddComponent<Camera>();
                        cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.75f, 0.77f, 0.8f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.enabled = false;
            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);

            foreach (var v in views)
            {
                float dist = Mathf.Max(0.35f, b.extents.magnitude * 3.2f);
                cam.transform.position = c + v.dir * dist;
                cam.transform.LookAt(c, Mathf.Abs(Vector3.Dot(v.dir, t.up)) > 0.8f ? t.forward : t.up);
                cam.targetTexture = rt;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, stamp + "_" + v.name + ".png"), tex.EncodeToPNG());
            }
            cam.targetTexture = null;
            Destroy(camGo); rt.Release(); Destroy(rt); Destroy(tex);

            // dados: articulações da mão no espaço da arma
            var sb = new StringBuilder();
            sb.AppendLine($"arma={gun.name} maoDireita={gun.HeldByRight}");
            string path = ""; for (var p = gun.HolderWrist; p != null; p = p.parent) path = p.name + "/" + path;
            sb.AppendLine("pulso=" + path);
            foreach (var kv in HandGeo.FindJoints(gun.HolderWrist))
            {
                Vector3 lp = t.InverseTransformPoint(kv.Value.position);
                sb.AppendLine($"{kv.Key} {lp.x:F4} {lp.y:F4} {lp.z:F4} ativo={kv.Value.gameObject.activeInHierarchy}");
            }
            foreach (var r in gun.HolderWrist.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sb.AppendLine($"render {r.name} ativo={r.gameObject.activeInHierarchy && r.enabled} mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")}");
            File.WriteAllText(Path.Combine(dir, stamp + "_dados.txt"), sb.ToString());
            Debug.Log($"[Captura] Fotos da pegada guardadas em {Path.GetFullPath(dir)} ({stamp})");
        }
    }
}
#endif
