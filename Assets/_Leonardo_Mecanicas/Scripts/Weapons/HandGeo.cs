using System.Collections.Generic;
using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>Utilitários para ler a geometria da mão da Meta (articulações XRHand_*).</summary>
    public static class HandGeo
    {
        /// <summary>Encontra todas as articulações "XRHand_*" da mão a que pertence este pulso.</summary>
        public static Dictionary<string, Transform> FindJoints(Transform wrist)
        {
            var d = new Dictionary<string, Transform>();
            if (wrist == null) return d;
            var visual = wrist.GetComponentInParent<Oculus.Interaction.HandVisual>();
            Transform root = visual != null ? visual.transform : wrist.root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("XRHand_")) d[t.name.Substring(7)] = t;
            return d;
        }

        static readonly Dictionary<Transform, Dictionary<string, Transform>> _cache = new Dictionary<Transform, Dictionary<string, Transform>>();

        /// <summary>Igual a FindJoints, mas guarda o resultado (para usar todos os frames).</summary>
        public static Dictionary<string, Transform> FindJointsCached(Transform wrist)
        {
            if (wrist == null) return new Dictionary<string, Transform>();
            if (!_cache.TryGetValue(wrist, out var d) || d.Count == 0) { d = FindJoints(wrist); _cache[wrist] = d; }
            return d;
        }

        /// <summary>
        /// Eixos da mão em coordenadas do mundo:
        /// across = do mindinho para o indicador, fwd = do pulso para os nós dos dedos,
        /// normal = para onde a palma está virada.
        /// </summary>
        public static bool Frame(Dictionary<string, Transform> j, bool right,
            out Vector3 palm, out Vector3 across, out Vector3 fwd, out Vector3 normal)
        {
            palm = across = fwd = normal = Vector3.zero;
            if (!j.TryGetValue("Wrist", out var w) || !j.TryGetValue("IndexProximal", out var ip) ||
                !j.TryGetValue("LittleProximal", out var lp) || !j.TryGetValue("MiddleProximal", out var mp))
                return false;

            palm = j.TryGetValue("Palm", out var pt) ? pt.position : Vector3.Lerp(w.position, mp.position, 0.5f);
            across = (ip.position - lp.position).normalized;
            fwd = Vector3.ProjectOnPlane(mp.position - w.position, across).normalized;
            // mão direita em "aperto de mão" (polegar para cima, dedos para a frente) tem a palma virada para a esquerda
            normal = Vector3.Cross(across, fwd).normalized * (right ? -1f : 1f);
            return true;
        }
    }
}
