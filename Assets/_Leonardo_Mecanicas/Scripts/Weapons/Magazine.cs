using System.Collections.Generic;
using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>Uma arma que aceita carregadores (G17, AWP...).</summary>
    public interface IMagazineWell
    {
        string WeaponId { get; }
        bool HasMagazine { get; }
        /// <summary>Ponto de encaixe (topo do carregador quando está dentro da arma).</summary>
        Transform Socket { get; }
        /// <summary>Direção "para cima" do carregador quando encaixado (mundo).</summary>
        Vector3 MagUpWorld { get; }
        Vector3 MagForwardWorld { get; }
        /// <summary>Quanto o carregador entra na arma (m): do encaixe até à boca do punho.</summary>
        float WellDepth { get; }
        void InsertMagazine(Magazine m);
    }

    /// <summary>
    /// Guarda o "molde" do carregador de cada arma, para as bolsas do colete criarem carregadores novos.
    /// </summary>
    public static class MagazineFactory
    {
        class Entry
        {
            public GameObject template;
            public int capacity;
            public Vector3 topLocal, upLocal, fwdLocal; // no espaço do próprio carregador
        }
        static readonly Dictionary<string, Entry> _db = new Dictionary<string, Entry>();
        public static readonly List<IMagazineWell> Wells = new List<IMagazineWell>();

        /// <summary>Regista o molde. top/up/fwd são no espaço local do carregador.</summary>
        public static void Register(string id, GameObject template, int capacity, Vector3 topLocal, Vector3 upLocal, Vector3 fwdLocal)
        {
            _db[id] = new Entry { template = template, capacity = capacity, topLocal = topLocal, upLocal = upLocal.normalized, fwdLocal = fwdLocal.normalized };
        }

        public static bool Has(string id) => _db.ContainsKey(id);

        /// <summary>Cria um carregador cheio (solto) na posição dada.</summary>
        public static Magazine Create(string id, Vector3 pos, Quaternion rot, int ammo = -1)
        {
            if (!_db.TryGetValue(id, out var e) || e.template == null) return null;
            var go = Object.Instantiate(e.template, pos, rot);
            go.hideFlags = HideFlags.None;
            go.name = "Carregador_" + id;
            go.SetActive(true);
            go.transform.localScale = e.template.transform.lossyScale;
            var m = go.GetComponent<Magazine>();
            if (m == null) m = go.AddComponent<Magazine>();
            m.Setup(id, ammo < 0 ? e.capacity : ammo, e.capacity, e.topLocal, e.upLocal, e.fwdLocal);
            return m;
        }

        /// <summary>Transforma um carregador que estava dentro da arma num carregador solto.</summary>
        public static Magazine MakeLoose(string id, GameObject magObj, int ammo)
        {
            if (!_db.TryGetValue(id, out var e)) return null;
            var m = magObj.GetComponent<Magazine>();
            if (m == null) m = magObj.AddComponent<Magazine>();
            m.Setup(id, ammo, e.capacity, e.topLocal, e.upLocal, e.fwdLocal);
            return m;
        }
    }

    /// <summary>
    /// Carregador solto: pode ser agarrado com o grip (botão lateral) e enfiado na arma.
    /// Encaixa quando o topo chega perto do encaixe da arma e está mais ou menos alinhado.
    /// </summary>
    public class Magazine : MonoBehaviour
    {
        public string weaponId;
        public int ammo;
        public int capacity;

        [Tooltip("Distância (m) abaixo da boca do punho a que o carregador já entra sozinho " +
                 "(assim os comandos não batem um no outro antes de encaixar).")]
        public float insertDistance = 0.045f;
        [Tooltip("Desvio lateral máximo (m) do topo do carregador em relação ao eixo do punho.")]
        public float insertLateral = 0.035f;
        [Tooltip("Ângulo máximo (graus) entre o carregador e o punho para entrar.")]
        public float insertAngle = 45f;
        [Tooltip("Distância da palma para o conseguir agarrar.")]
        public float grabRadius = 0.09f;
        [Tooltip("Segundos até desaparecer quando fica no chão.")]
        public float despawnTime = 45f;

        Vector3 _topLocal, _upLocal, _fwdLocal;
        Rigidbody _rb;
        bool _held, _heldRight, _inserted;
        readonly bool[] _prevGrip = new bool[2];
        float _looseTimer;

        public bool IsHeld => _held;
        public bool IsInserted => _inserted;
        public Vector3 TopWorld => transform.TransformPoint(_topLocal);
        public Vector3 UpWorld => transform.TransformDirection(_upLocal);
        public Vector3 ForwardWorld => transform.TransformDirection(_fwdLocal);

        public void Setup(string id, int ammo, int capacity, Vector3 topLocal, Vector3 upLocal, Vector3 fwdLocal)
        {
            weaponId = id; this.ammo = ammo; this.capacity = capacity;
            _topLocal = topLocal; _upLocal = upLocal; _fwdLocal = fwdLocal;
            _inserted = false;
            enabled = true;
            EnsureBody();
        }

        void EnsureBody()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 0.25f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (GetComponentInChildren<Collider>() == null)
            {
                var bc = gameObject.AddComponent<BoxCollider>();
                var mf = GetComponentInChildren<MeshFilter>();
                if (mf && mf.sharedMesh) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
            }
        }

        /// <summary>Põe o carregador na mão (usado pelas bolsas do colete).</summary>
        public void AttachToHand(bool right)
        {
            if (_inserted) return;
            if (_rb) { _rb.isKinematic = true; }
            _held = true; _heldRight = right;
            HandRig.SetHeld(right, this);
            _prevGrip[right ? 1 : 0] = true;
            HandRig.Buzz(right, 0.3f, 0.04f);
        }

        void Release(bool throwIt, bool makeDynamic = true)
        {
            if (!_held) return;
            HandRig.SetHeld(_heldRight, null);
            _held = false;
            if (_rb && makeDynamic)
            {
                _rb.isKinematic = false;
                if (throwIt) _rb.linearVelocity = _handVel;
            }
            _looseTimer = 0f;
        }

        Vector3 _lastHandPos, _handVel;

        void Update()
        {
            if (_inserted) return;

            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1;
                bool grip = HandRig.Grip(right) > 0.6f;
                bool down = grip && !_prevGrip[i];
                _prevGrip[i] = grip;

                if (_held && _heldRight == right && !grip) { Release(true); continue; }

                // agarrar: mão livre perto do carregador
                if (!_held && down && !HandRig.IsBusy(right))
                {
                    var palm = HandRig.Palm(right);
                    if (palm != null)
                    {
                        var col = GetComponentInChildren<Collider>();
                        Vector3 c = col ? col.ClosestPoint(palm.position) : transform.position;
                        if (Vector3.Distance(c, palm.position) < grabRadius) AttachToHand(right);
                    }
                }
            }

            if (_held)
            {
                if (HandRig.GrabPoint(_heldRight, out var hp, out var hr))
                {
                    // o topo do carregador para o lado dos dedos, a frente para o indicador
                    Quaternion magFrame = Quaternion.LookRotation(_fwdLocal, _upLocal); // frame no espaço do carregador
                    Quaternion rot = hr * Quaternion.Inverse(magFrame);
                    // centro do carregador na pega
                    Vector3 centerLocal = _topLocal - _upLocal * 0.06f;
                    Vector3 pos = hp - rot * Vector3.Scale(centerLocal, transform.lossyScale);
                    _handVel = (hp - _lastHandPos) / Mathf.Max(Time.deltaTime, 1e-4f);
                    _lastHandPos = hp;
                    transform.SetPositionAndRotation(pos, rot);
                }
                TryInsert();
            }
            else if (_rb != null && !_rb.isKinematic)
            {
                _looseTimer += Time.deltaTime;
                if (_looseTimer > despawnTime) Destroy(gameObject);
            }
        }

        void TryInsert()
        {
            foreach (var w in MagazineFactory.Wells)
            {
                if (w == null || w.HasMagazine || w.WeaponId != weaponId || w.Socket == null) continue;
                // topo do carregador perto da boca do punho, em linha com o eixo
                // (de insertDistance abaixo da boca até 4 cm para dentro; não entra pelos lados do punho)
                Vector3 up = w.MagUpWorld;
                Vector3 v = TopWorld - w.Socket.position;
                float along = Vector3.Dot(v, up);              // 0 = encaixe, -WellDepth = boca do punho
                if (along > -w.WellDepth + 0.04f || along < -(w.WellDepth + insertDistance)) continue;
                if ((v - up * along).magnitude > insertLateral) continue;
                if (Vector3.Angle(UpWorld, w.MagUpWorld) > insertAngle) continue;
                if (Vector3.Angle(ForwardWorld, w.MagForwardWorld) > 70f) continue;
                bool hand = _heldRight;
                Release(false, false);
                _inserted = true;
                if (_rb) Destroy(_rb);
                _rb = null;
                w.InsertMagazine(this);
                HandRig.Buzz(hand, 0.7f, 0.06f);
                enabled = false;
                return;
            }
        }

        /// <summary>Chamado pela arma quando o carregador sai (ejetado).</summary>
        public void OnEjected()
        {
            _inserted = false;
            enabled = true;
            EnsureBody();
            _rb.isKinematic = false;
            _looseTimer = 0f;
        }

        void OnDestroy()
        {
            if (_held) HandRig.SetHeld(_heldRight, null);
        }
    }
}
