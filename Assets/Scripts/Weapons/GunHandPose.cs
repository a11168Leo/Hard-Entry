using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Oculus.Interaction;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Põe a mão da Meta na pose de pegada desenhada no Blender (HandPoseLibrary).
    ///
    /// - Coloca a arma na mão de forma a que a palma e os nós dos dedos fiquem como na pose desenhada.
    /// - Roda cada osso dos dedos para apontar como na pose (dedos à volta do punho,
    ///   indicador no gatilho, polegar ao longo do lado da armação).
    /// - O indicador dobra mais quando carregas no gatilho do comando.
    /// A pose é da mão direita; para a esquerda é espelhada automaticamente.
    /// </summary>
    [DefaultExecutionOrder(10010)] // depois do GunGripSnap
    public class GunHandPose : MonoBehaviour
    {
        [Tooltip("Nome da pose em HandPoseLibrary.")]
        public string poseName = "G17";

        [Header("Indicador / gatilho")]
        [Tooltip("Dobra extra do indicador com o gatilho do comando carregado a fundo (graus).")]
        public float triggerExtraCurl = 18f;

        [Header("Geral")]
        [Tooltip("Tempo para os dedos fecharem ao agarrar (s).")]
        public float blendTime = 0.08f;

        static readonly string[][] Chains =
        {
            new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" },
            new[] { "IndexProximal", "IndexIntermediate", "IndexDistal", "IndexTip" },
            new[] { "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "MiddleTip" },
            new[] { "RingProximal", "RingIntermediate", "RingDistal", "RingTip" },
            new[] { "LittleProximal", "LittleIntermediate", "LittleDistal", "LittleTip" },
        };

        GunGripSnap _snap;
        Dictionary<string, Vector3> _pose;

        Transform _wrist;
        HandVisual _visual;
        bool _isRight;
        Dictionary<string, Transform> _joints = new Dictionary<string, Transform>();
        float _weight;
        int _appliedFrame = -1;

        void Awake()
        {
            _snap = GetComponent<GunGripSnap>();
            _pose = HandPoseLibrary.Get(poseName);
        }

        void OnDisable() => Release();

        // ------------------------------------------------------------------ pose desenhada (espaço da arma)

        /// <summary>Posição de uma articulação da pose, no espaço local da arma (espelhada para a mão esquerda).</summary>
        Vector3 P(string n, bool right)
        {
            Vector3 v = _pose[n];
            if (!right) v.x = -v.x;
            return v;
        }

        static Quaternion Frame(Vector3 wrist, Vector3 indexProx, Vector3 littleProx, Vector3 middleProx)
        {
            Vector3 across = (indexProx - littleProx).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(middleProx - wrist, across).normalized;
            return Quaternion.LookRotation(fwd, across);
        }

        /// <summary>
        /// Calcula onde a arma deve ficar para a mão (com o tracking atual) coincidir com a pose desenhada.
        /// </summary>
        public bool ComputeGunPose(Transform wrist, bool right, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            if (_pose == null) _pose = HandPoseLibrary.Get(poseName);
            var j = HandGeo.FindJoints(wrist);
            if (!j.TryGetValue("Wrist", out var w) || !j.TryGetValue("IndexProximal", out var ip) ||
                !j.TryGetValue("LittleProximal", out var lp) || !j.TryGetValue("MiddleProximal", out var mp))
                return false;

            Quaternion tracked = Frame(w.position, ip.position, lp.position, mp.position);
            Quaternion authored = Frame(P("Wrist", right), P("IndexProximal", right), P("LittleProximal", right), P("MiddleProximal", right));
            rot = tracked * Quaternion.Inverse(authored);
            // alinhar pelos nós dos dedos (não pela articulação "Palm": na mão da Meta ela fica ~15 mm
            // mais perto do pulso do que na pose desenhada, o que punha os dedos a fechar à frente do punho)
            Vector3 knTracked = (ip.position + mp.position + lp.position) / 3f;
            Vector3 knAuthored = (P("IndexProximal", right) + P("MiddleProximal", right) + P("LittleProximal", right)) / 3f;
            if (j.TryGetValue("RingProximal", out var rp))
            {
                knTracked = (knTracked * 3f + rp.position) / 4f;
                knAuthored = (knAuthored * 3f + P("RingProximal", right)) / 4f;
            }
            pos = knTracked - rot * knAuthored;
            return true;
        }

        // ------------------------------------------------------------------ ciclo

        void LateUpdate()
        {
            if (_snap == null || _pose == null) return;

            if (!_snap.IsHeld || !_snap.SnapSettled)
            {
                if (!_snap.IsHeld && _wrist != null) Release();
                return;
            }

            if (_snap.HolderWrist != _wrist)
            {
                Release();
                Attach(_snap.HolderWrist, _snap.HeldByRight);
            }

            _weight = blendTime > 0f ? Mathf.Min(1f, _weight + Time.deltaTime / blendTime) : 1f;
            Apply();
        }

        void Attach(Transform wrist, bool right)
        {
            _wrist = wrist;
            _isRight = right;
            _weight = 0f;
            _joints = HandGeo.FindJoints(wrist);
            _visual = wrist != null ? wrist.GetComponentInParent<HandVisual>() : null;
            if (_visual != null) _visual.WhenHandVisualUpdated += OnVisualUpdated;
        }

        void Release()
        {
            if (_visual != null) _visual.WhenHandVisualUpdated -= OnVisualUpdated;
            _visual = null;
            _wrist = null;
            _weight = 0f;
            _appliedFrame = -1;
        }

        // o SDK acabou de pôr a mão com os dados do comando: aplicar a pose por cima
        void OnVisualUpdated()
        {
            _appliedFrame = -1;
            Apply();
        }

        void Apply()
        {
            if (_wrist == null || _weight <= 0f) return;
            if (_appliedFrame == Time.frameCount) return; // já aplicado sobre os dados deste frame
            _appliedFrame = Time.frameCount;

            Quaternion gunRot = transform.rotation;

            foreach (var ch in Chains)
            {
                for (int i = 0; i < ch.Length - 1; i++)
                {
                    if (!_joints.TryGetValue(ch[i], out var a) || !_joints.TryGetValue(ch[i + 1], out var b)) break;
                    Vector3 cur = b.position - a.position;
                    Vector3 want = gunRot * (P(ch[i + 1], _isRight) - P(ch[i], _isRight));
                    if (cur.sqrMagnitude < 1e-10f || want.sqrMagnitude < 1e-10f) continue;
                    Quaternion full = Quaternion.FromToRotation(cur, want);
                    a.rotation = Quaternion.Slerp(Quaternion.identity, full, _weight) * a.rotation;
                }
            }

            // indicador dobra com o gatilho do comando
            float trig = 0f;
            var dev = InputDevices.GetDeviceAtXRNode(_isRight ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.trigger, out trig);
            if (trig > 0.01f &&
                _joints.TryGetValue("IndexProximal", out var p0) && _joints.TryGetValue("IndexIntermediate", out var p1) &&
                _joints.TryGetValue("IndexDistal", out var p2))
            {
                Vector3 axis = Vector3.Cross(p1.position - p0.position, p2.position - p1.position);
                if (axis.sqrMagnitude > 1e-10f)
                {
                    axis.Normalize();
                    float a = trig * triggerExtraCurl * _weight;
                    p1.rotation = Quaternion.AngleAxis(a * 0.6f, axis) * p1.rotation;
                    p2.rotation = Quaternion.AngleAxis(a * 0.4f, axis) * p2.rotation;
                }
            }
        }
    }
}
