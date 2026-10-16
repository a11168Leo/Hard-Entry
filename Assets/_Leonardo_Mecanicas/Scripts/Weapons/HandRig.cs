using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Acesso simples às duas mãos: palma (posição/rotação), botão de agarrar (grip) e vibração.
    /// Usa as mãos visíveis da Meta (XRHand_* debaixo de "HandVisual").
    /// Também sabe o que cada mão está a segurar (arma ou carregador).
    /// </summary>
    public static class HandRig
    {
        static Transform _rWrist, _lWrist, _rPalm, _lPalm;
        static int _lastSearchFrame = -100;

        /// <summary>Objeto que cada mão segura com o nosso sistema (carregadores, etc.).</summary>
        static readonly Dictionary<bool, Object> _held = new Dictionary<bool, Object> { { true, null }, { false, null } };

        static void Ensure()
        {
            if (_rPalm != null && _lPalm != null) return;
            if (Time.frameCount - _lastSearchFrame < 30) return; // não procurar todos os frames
            _lastSearchFrame = Time.frameCount;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name != "XRHand_Wrist" && t.name != "XRHand_Palm") continue;
                string chain = "";
                for (var p = t.parent; p != null; p = p.parent) chain += p.name + "/";
                if (!chain.Contains("HandVisual")) continue;
                bool right = chain.Contains("Right");
                if (!right && !chain.Contains("Left")) continue;
                if (t.name == "XRHand_Wrist") { if (right) _rWrist = t; else _lWrist = t; }
                else { if (right) _rPalm = t; else _lPalm = t; }
            }
        }

        public static Transform Wrist(bool right) { Ensure(); return right ? _rWrist : _lWrist; }
        public static Transform Palm(bool right) { Ensure(); return right ? _rPalm : _lPalm; }

        public static InputDevice Device(bool right) =>
            InputDevices.GetDeviceAtXRNode(right ? XRNode.RightHand : XRNode.LeftHand);

        public static float Grip(bool right)
        {
            var d = Device(right);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.grip, out float g) ? g : 0f;
        }

        public static void Buzz(bool right, float amp = 0.4f, float dur = 0.05f)
        {
            var d = Device(right);
            if (d.isValid) d.SendHapticImpulse(0, amp, dur);
        }

        /// <summary>Posição de "pega" à frente da palma (onde fica um objeto agarrado).</summary>
        public static bool GrabPoint(bool right, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            var w = Wrist(right);
            if (w == null) return false;
            var j = HandGeo.FindJointsCached(w);
            if (!HandGeo.Frame(j, right, out var palm, out var across, out var fwd, out var normal)) return false;
            pos = palm + normal * 0.03f + fwd * 0.01f;
            rot = Quaternion.LookRotation(across, fwd); // z = lado do indicador, y = dedos
            return true;
        }

        // ---------------- ocupação das mãos ----------------

        public static Object HeldBy(bool right) => _held[right];
        public static void SetHeld(bool right, Object o) => _held[right] = o;

        /// <summary>A mão está ocupada (arma agarrada pelo SDK ou objeto nosso)?</summary>
        public static bool IsBusy(bool right)
        {
            if (_held[right] != null) return true;
            foreach (var g in GunGripSnap.All)
                if (g != null && g.IsHeld && g.HeldByRight == right) return true;
            return false;
        }
    }
}
