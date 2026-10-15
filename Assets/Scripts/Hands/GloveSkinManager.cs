using System.Collections.Generic;
using UnityEngine;

namespace HardEntry.Hands
{
    /// <summary>
    /// Veste as mãos do Meta Interaction SDK com uma luva (skin).
    /// - Encontra as mãos da Meta na cena (malhas "RightHand"/"LeftHand" com ossos XRHand_*)
    /// - Cria a luva por cima, ligada aos MESMOS ossos (por nome), por isso os dedos mexem
    /// - Esconde a mão original
    /// Para trocar de skin, muda o material (ou chama SetMaterial em runtime).
    /// </summary>
    public class GloveSkinManager : MonoBehaviour
    {
        [Header("Modelos da luva (os .fbx importados)")]
        public GameObject gloveRightModel;
        public GameObject gloveLeftModel;

        [Header("Skin")]
        public Material gloveMaterial;

        [Tooltip("Mostra no Console o erro de alinhamento entre a luva e o esqueleto.")]
        public bool logInfo = true;

        class Pair { public SkinnedMeshRenderer original, glove; }
        readonly List<Pair> _pairs = new();
        readonly Dictionary<string, Mesh> _meshCache = new();

        void Start()
        {
            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
            {
                if (smr.sharedMesh == null || smr.bones == null || smr.bones.Length == 0) continue;
                if (smr.name.StartsWith("Glove_")) continue;
                bool hasXRBones = false;
                foreach (var b in smr.bones) if (b != null && b.name == "XRHand_Wrist") { hasXRBones = true; break; }
                if (!hasXRBones) continue;

                string meshName = smr.sharedMesh.name;
                bool right = meshName.Contains("Right");
                bool left = meshName.Contains("Left");
                if (!right && !left) continue;

                var model = right ? gloveRightModel : gloveLeftModel;
                if (model == null) continue;
                var src = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (src == null) { Debug.LogWarning("[GloveSkin] O modelo da luva não tem SkinnedMeshRenderer."); continue; }

                CreateGlove(smr, src, right ? "R" : "L");
            }
            if (logInfo) Debug.Log($"[GloveSkin] Luvas criadas: {_pairs.Count}");
        }

        void CreateGlove(SkinnedMeshRenderer original, SkinnedMeshRenderer src, string side)
        {
            // Nomes dos ossos da luva, pela ordem usada nos pesos da malha
            var gloveBoneNames = new string[src.bones.Length];
            for (int i = 0; i < src.bones.Length; i++) gloveBoneNames[i] = src.bones[i].name;

            // Ossos da mão original, por nome
            var origIndex = new Dictionary<string, int>();
            for (int i = 0; i < original.bones.Length; i++)
                if (original.bones[i] != null) origIndex[original.bones[i].name] = i;

            string key = side + "_" + original.sharedMesh.name;
            if (!_meshCache.TryGetValue(key, out var mesh))
            {
                mesh = BuildAlignedMesh(src.sharedMesh, gloveBoneNames, original.sharedMesh, origIndex, side);
                _meshCache[key] = mesh;
            }
            if (mesh == null) return;

            var newBones = new Transform[gloveBoneNames.Length];
            for (int i = 0; i < gloveBoneNames.Length; i++)
                newBones[i] = origIndex.TryGetValue(gloveBoneNames[i], out int j) ? original.bones[j] : original.rootBone;

            var go = new GameObject("Glove_" + side);
            go.transform.SetParent(original.transform.parent, false);
            go.transform.localPosition = original.transform.localPosition;
            go.transform.localRotation = original.transform.localRotation;
            go.transform.localScale = original.transform.localScale;
            go.layer = original.gameObject.layer;

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = newBones;
            smr.rootBone = original.rootBone;
            smr.updateWhenOffscreen = true;
            smr.sharedMaterial = gloveMaterial != null ? gloveMaterial : src.sharedMaterial;

            original.forceRenderingOff = true; // esconde a mão original sem mexer na lógica do SDK
            _pairs.Add(new Pair { original = original, glove = smr });
        }

        /// <summary>
        /// Copia a malha da luva e alinha-a ao espaço da malha original da Meta.
        /// O alinhamento é calculado a partir das posições dos ossos (iguais nos dois modelos),
        /// por isso funciona mesmo que os .fbx tenham eixos/escala diferentes.
        /// </summary>
        Mesh BuildAlignedMesh(Mesh gloveMesh, string[] gloveBoneNames, Mesh origMesh, Dictionary<string, int> origIndex, string side)
        {
            var gBind = gloveMesh.bindposes;
            var oBind = origMesh.bindposes;

            var P = new List<Vector3>(); var Q = new List<Vector3>();
            for (int i = 0; i < gloveBoneNames.Length; i++)
            {
                if (!origIndex.TryGetValue(gloveBoneNames[i], out int j)) continue;
                P.Add(gBind[i].inverse.GetColumn(3));   // osso da luva, no espaço da malha da luva
                Q.Add(oBind[j].inverse.GetColumn(3));   // mesmo osso, no espaço da malha da Meta
            }
            if (P.Count < 4) { Debug.LogWarning($"[GloveSkin] Poucos ossos em comum ({P.Count}). Os nomes dos ossos coincidem?"); return null; }

            Vector3 pc = Vector3.zero, qc = Vector3.zero;
            foreach (var p in P) pc += p; foreach (var q in Q) qc += q;
            pc /= P.Count; qc /= Q.Count;
            float sp = 0, sq = 0;
            for (int i = 0; i < P.Count; i++) { sp += (P[i] - pc).sqrMagnitude; sq += (Q[i] - qc).sqrMagnitude; }
            float s = Mathf.Sqrt(sq / Mathf.Max(sp, 1e-12f));

            // Testa as 24 rotações alinhadas aos eixos e fica com a melhor
            Quaternion bestR = Quaternion.identity; float bestErr = float.MaxValue;
            foreach (var R in AxisRotations())
            {
                float err = 0;
                for (int i = 0; i < P.Count; i++) err += ((R * (P[i] - pc)) * s + qc - Q[i]).sqrMagnitude;
                if (err < bestErr) { bestErr = err; bestR = R; }
            }
            float rms = Mathf.Sqrt(bestErr / P.Count);
            if (logInfo) Debug.Log($"[GloveSkin] Luva {side}: {P.Count} ossos ligados, escala {s:F3}, erro médio {rms * 100f:F2} cm");

            var m = Instantiate(gloveMesh);
            m.name = gloveMesh.name + "_Aligned";
            var v = m.vertices; var n = m.normals; var t = m.tangents;
            for (int i = 0; i < v.Length; i++) v[i] = bestR * (v[i] - pc) * s + qc;
            for (int i = 0; i < n.Length; i++) n[i] = bestR * n[i];
            for (int i = 0; i < t.Length; i++) { var d = bestR * (Vector3)t[i]; t[i] = new Vector4(d.x, d.y, d.z, t[i].w); }
            m.vertices = v; if (n.Length > 0) m.normals = n; if (t.Length > 0) m.tangents = t;

            var bp = new Matrix4x4[gloveBoneNames.Length];
            for (int i = 0; i < bp.Length; i++)
                bp[i] = origIndex.TryGetValue(gloveBoneNames[i], out int j) ? oBind[j] : oBind[0];
            m.bindposes = bp;
            m.RecalculateBounds();
            return m;
        }

        static IEnumerable<Quaternion> AxisRotations()
        {
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in axes)
                foreach (var u in axes)
                    if (Mathf.Abs(Vector3.Dot(f, u)) < 0.5f)
                        yield return Quaternion.LookRotation(f, u);
        }

        void LateUpdate()
        {
            // A luva aparece e desaparece junto com a mão original (o SDK liga/desliga-a)
            foreach (var p in _pairs)
            {
                if (p.original == null || p.glove == null) continue;
                p.glove.enabled = p.original.enabled;
                p.original.forceRenderingOff = true;
            }
        }

        /// <summary>Troca a skin em runtime (ex.: menu de cosméticos).</summary>
        public void SetMaterial(Material mat)
        {
            gloveMaterial = mat;
            foreach (var p in _pairs) if (p.glove != null) p.glove.sharedMaterial = mat;
        }

        void OnDestroy()
        {
            foreach (var p in _pairs) if (p.original != null) p.original.forceRenderingOff = false;
        }
    }
}
