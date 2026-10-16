using UnityEngine;

namespace HardEntry.Gear
{
    /// <summary>
    /// Põe uma manga (casaco) em cada pulso das mãos da Meta, para não se ver o "corte" da mão no pulso.
    /// A malha é gerada por código: um tubo que começa por cima do punho da luva e vai até ao antebraço.
    /// </summary>
    public class HandSleeves : MonoBehaviour
    {
        public Color color = new Color(0.10f, 0.11f, 0.10f);
        [Range(0f, 1f)] public float smoothness = 0.12f;
        [Tooltip("Comprimento da manga (m).")]
        public float length = 0.20f;
        [Tooltip("Onde começa a manga em relação ao pulso (m, negativo = mais para a mão).")]
        public float start = -0.006f;
        [Tooltip("Raio da manga no pulso (x = largura, y = altura).")]
        public Vector2 wristRadius = new Vector2(0.031f, 0.024f);
        [Tooltip("Raio da manga no antebraço.")]
        public Vector2 forearmRadius = new Vector2(0.041f, 0.036f);

        static Material _mat;
        bool _doneR, _doneL;

        /// <summary>Cria sozinho o objeto "Mangas" quando o jogo arranca (se ainda não houver um na cena).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<HandSleeves>() != null) return;
            new GameObject("Mangas").AddComponent<HandSleeves>();
        }

        void Update()
        {
            if (!_doneR) _doneR = TryAttach(true);
            if (!_doneL) _doneL = TryAttach(false);
            if (_doneR && _doneL) enabled = false;
        }

        bool TryAttach(bool right)
        {
            var w = HardEntry.Weapons.HandRig.Wrist(right);
            if (w == null) return false;
            if (w.Find("Manga") != null) return true;
            Build(w, right, this);
            return true;
        }

        /// <summary>Cria a manga como filha do pulso (espaço do pulso OpenXR: -Z = dedos, +Y = costas da mão).</summary>
        public static GameObject Build(Transform wrist, bool right, HandSleeves cfg = null)
        {
            float len = cfg ? cfg.length : 0.20f, z0 = cfg ? cfg.start : -0.006f;
            Vector2 r0 = cfg ? cfg.wristRadius : new Vector2(0.031f, 0.024f);
            Vector2 r1 = cfg ? cfg.forearmRadius : new Vector2(0.041f, 0.036f);
            Color col = cfg ? cfg.color : new Color(0.10f, 0.11f, 0.10f);

            var go = new GameObject("Manga");
            go.transform.SetParent(wrist, false);
            go.layer = wrist.gameObject.layer;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            // eixos da mão no espaço do pulso (não dependem da convenção do rig)
            var j = HardEntry.Weapons.HandGeo.FindJoints(wrist);
            Vector3 fwd = Vector3.forward, across = Vector3.right;
            if (j.TryGetValue("MiddleProximal", out var mp) && j.TryGetValue("IndexProximal", out var ip) && j.TryGetValue("LittleProximal", out var lp))
            {
                fwd = wrist.InverseTransformDirection(mp.position - wrist.position).normalized;
                across = Vector3.ProjectOnPlane(wrist.InverseTransformDirection(ip.position - lp.position), fwd).normalized;
            }
            mf.sharedMesh = Tube(fwd, across, z0, len, r0, r1);
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _mat.SetColor("_BaseColor", col);
                _mat.SetFloat("_Smoothness", cfg ? cfg.smoothness : 0.12f);
                _mat.SetTexture("_BaseMap", FabricTexture());
                _mat.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
            }
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Mesh Tube(Vector3 fwd, Vector3 across, float z0, float len, Vector2 r0, Vector2 r1)
        {
            const int seg = 28, rings = 8;
            Vector3 side = across, up = Vector3.Cross(fwd, across).normalized; // up = espessura da mão
            Vector3 back = -fwd;                                                // do pulso para o cotovelo
            Vector3 P(float ang, Vector2 rad, float z) => side * (Mathf.Cos(ang) * rad.x) + up * (Mathf.Sin(ang) * rad.y) + back * z;
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var tri = new System.Collections.Generic.List<int>();

            // superfície exterior
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float z = z0 + len * t;
                // punho da manga um pouco mais largo no início (dobra do tecido)
                float bulge = 1f + 0.06f * Mathf.Exp(-t * 18f);
                Vector2 rad = Vector2.Lerp(r0, r1, Mathf.SmoothStep(0f, 1f, t)) * bulge;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    v.Add(P(a, rad, z));
                    uv.Add(new Vector2(s / (float)seg, t));
                }
            }
            int row = seg + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                    tri.AddRange(new[] { a, b, c, b, d, c });
                }

            // aba interior no início (para não se ver por dentro da manga)
            int baseIn = v.Count;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                Vector2 rad = r0 * 0.80f;
                v.Add(P(a, rad, z0 + 0.012f));
                uv.Add(new Vector2(s / (float)seg, 0f));
            }
            for (int s = 0; s < seg; s++)
            {
                int o = s, o1 = s + 1, i0 = baseIn + s, i1 = baseIn + s + 1;
                tri.AddRange(new[] { o, i0, o1, o1, i0, i1 });
            }

            // tampa no fim (cotovelo)
            int center = v.Count;
            v.Add(back * (z0 + len)); uv.Add(new Vector2(0.5f, 1f));
            int last = rings * row;
            for (int s = 0; s < seg; s++) tri.AddRange(new[] { last + s, last + s + 1, center });

            // se os eixos formarem um sistema "ao contrário", inverte os triângulos para as faces ficarem para fora
            if (Vector3.Dot(Vector3.Cross(side, up), back) < 0f)
                for (int k = 0; k < tri.Count; k += 3) { int tmp = tri[k + 1]; tri[k + 1] = tri[k + 2]; tri[k + 2] = tmp; }
            var m = new Mesh { name = "Manga" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Texture2D FabricTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "TecidoManga" };
            var px = new Color[n * n];
            var rnd = new System.Random(3);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float weave = 0.5f + 0.5f * Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f);
                    float g = 0.82f + 0.12f * weave + 0.08f * (float)rnd.NextDouble();
                    px[y * n + x] = new Color(g, g, g, 1f);
                }
            tex.SetPixels(px); tex.Apply(true);
            return tex;
        }
    }
}
