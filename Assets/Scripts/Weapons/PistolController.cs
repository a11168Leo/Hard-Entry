using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Lógica da pistola (G17):
    ///  - Gatilho do comando  -> dispara (semi-automática)
    ///  - Botão A / X         -> carrega no botão do carregador e o carregador cai
    ///                           (TEMPORÁRIO: sem carregador, A / X mete um novo)
    /// Encontra as peças pelo nome: Corredica, Gatilho, BotaoCarregador, Carregador,
    /// BalaCamara, PontaCano, SaidaCapsula, EncaixeCarregador.
    /// Coloca no objeto raiz da arma (o mesmo do GunGripSnap).
    /// </summary>
    [RequireComponent(typeof(GunGripSnap))]
    public class PistolController : MonoBehaviour
    {
        [Header("Munição")]
        public int magCapacity = 17;
        public int magAmmo = 17;
        public bool roundChambered = true;

        [Header("Disparo")]
        public float range = 150f;
        public float impactForce = 6f;
        [Tooltip("Valor do gatilho a partir do qual dispara (0-1).")]
        public float triggerThreshold = 0.75f;
        public float triggerMaxAngle = 18f;

        [Header("Corrediça")]
        public float slideTravel = 0.028f;
        public float slideCycleTime = 0.07f;

        [Header("Recuo")]
        public Vector3 recoilKickPos = new Vector3(0f, 0.004f, -0.025f);
        public Vector3 recoilKickRot = new Vector3(-7f, 0f, 0f);

        [Header("Cápsula")]
        public float casingSpeed = 2.2f;
        public float casingLifetime = 8f;

        [Header("Sons (arrasta os áudios para aqui)")]
        public AudioClip shotSound;
        public AudioClip emptyClickSound;
        public AudioClip magOutSound;
        public AudioClip magInSound;
        public AudioClip slideReleaseSound;
        [Range(0f, 1f)] public float volume = 1f;

        // peças
        Transform _slide, _trigger, _magButton, _mag, _chamberRound, _muzzle, _ejectPort, _magSocket;
        Vector3 _slideRest, _magButtonRest, _magRestPos;
        Quaternion _triggerRest, _magRestRot;
        Vector3 _triggerAxis;
        GameObject _magTemplate, _casingTemplate;
        GunGripSnap _grip;
        AudioSource _audio;
        Light _flash;
        Collider[] _gunColliders;

        bool _slideLocked;
        bool _prevTrigger, _prevA;
        float _triggerValue;
        Coroutine _slideRoutine;

        void Awake()
        {
            _grip = GetComponent<GunGripSnap>();
            _slide = Find("Corredica");
            _trigger = Find("Gatilho");
            _magButton = Find("BotaoCarregador");
            _mag = Find("Carregador");
            _chamberRound = Find("BalaCamara");
            _muzzle = Find("PontaCano");
            _ejectPort = Find("SaidaCapsula");
            _magSocket = Find("EncaixeCarregador");

            if (_slide) _slideRest = _slide.localPosition;
            if (_magButton) _magButtonRest = _magButton.localPosition;
            if (_trigger)
            {
                _triggerRest = _trigger.localRotation;
                _triggerAxis = Quaternion.Inverse(_triggerRest) * Vector3.right; // eixo X da arma
            }
            if (_mag)
            {
                _magRestPos = _mag.localPosition; _magRestRot = _mag.localRotation;
                // modelo guardado para criar carregadores novos
                _magTemplate = Instantiate(_mag.gameObject);
                _magTemplate.name = "Carregador";
                _magTemplate.SetActive(false);
                _magTemplate.transform.SetParent(transform, false);
                _magTemplate.hideFlags = HideFlags.HideInHierarchy;
            }
            if (_chamberRound)
            {
                _casingTemplate = Instantiate(_chamberRound.gameObject);
                _casingTemplate.name = "Capsula";
                _casingTemplate.SetActive(false);
                _casingTemplate.transform.SetParent(transform, false);
                _casingTemplate.hideFlags = HideFlags.HideInHierarchy;
                foreach (var c in _casingTemplate.GetComponentsInChildren<Collider>()) Destroy(c);
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 1f;
            _audio.maxDistance = 60f;

            if (_muzzle)
            {
                var lo = new GameObject("FlashLuz");
                lo.transform.SetParent(_muzzle, false);
                lo.transform.localPosition = Vector3.forward * 0.03f;
                _flash = lo.AddComponent<Light>();
                _flash.type = LightType.Point;
                _flash.color = new Color(1f, 0.75f, 0.4f);
                _flash.range = 4f;
                _flash.intensity = 6f;
                _flash.enabled = false;
            }

            _gunColliders = GetComponentsInChildren<Collider>();
            UpdateChamberVisual();

            foreach (var n in new[] { "Corredica", "Gatilho", "BotaoCarregador", "Carregador", "BalaCamara", "PontaCano", "SaidaCapsula" })
                if (Find(n) == null) Debug.LogWarning($"[Pistola] Peça não encontrada: {n}");
        }

        Transform Find(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }

        void Update()
        {
            if (!_grip.IsHeld)
            {
                _prevTrigger = false; _prevA = false;
                AnimateTrigger(0f);
                return;
            }

            var dev = InputDevices.GetDeviceAtXRNode(_grip.HeldByRight ? XRNode.RightHand : XRNode.LeftHand);
            float trig = 0f; bool a = false;
            if (dev.isValid)
            {
                dev.TryGetFeatureValue(CommonUsages.trigger, out trig);
                dev.TryGetFeatureValue(CommonUsages.primaryButton, out a);
            }

            AnimateTrigger(trig);

            bool pulled = trig >= triggerThreshold;
            if (pulled && !_prevTrigger) TryFire(dev);
            // histerese: o gatilho tem de voltar atrás para disparar outra vez
            if (trig < triggerThreshold * 0.6f) _prevTrigger = false;
            else if (pulled) _prevTrigger = true;

            if (a && !_prevA) MagButton();
            _prevA = a;
        }

        void AnimateTrigger(float v)
        {
            _triggerValue = Mathf.Lerp(_triggerValue, v, 1f - Mathf.Exp(-30f * Time.deltaTime));
            if (_trigger) _trigger.localRotation = _triggerRest * Quaternion.AngleAxis(triggerMaxAngle * _triggerValue, _triggerAxis);
        }

        // ---------------- DISPARO ----------------
        void TryFire(InputDevice dev)
        {
            if (!roundChambered || _slideLocked)
            {
                Play(emptyClickSound, 0.8f);
                return;
            }

            roundChambered = false;
            Play(shotSound, 1f);
            if (dev.isValid) dev.SendHapticImpulse(0, 1f, 0.08f);
            _grip.AddRecoil(recoilKickPos, recoilKickRot + new Vector3(0f, Random.Range(-1.2f, 1.2f), 0f));
            StartCoroutine(Flash());
            Raycast();
            EjectCasing();

            // municiar a próxima bala
            if (_mag != null && _mag.parent == transform && magAmmo > 0)
            {
                magAmmo--;
                roundChambered = true;
                CycleSlide(lockBack: false);
            }
            else
            {
                CycleSlide(lockBack: true);   // vazia: corrediça fica atrás
            }
            UpdateChamberVisual();
        }

        IEnumerator Flash()
        {
            if (_flash == null) yield break;
            _flash.enabled = true;
            yield return new WaitForSeconds(0.035f);
            _flash.enabled = false;
        }

        void Raycast()
        {
            if (_muzzle == null) return;
            var origin = _muzzle.position;
            var dir = transform.forward;
            var hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic)
                    h.rigidbody.AddForceAtPosition(dir * impactForce, h.point, ForceMode.Impulse);
                SpawnImpact(h.point, h.normal, h.collider.transform);
                break;
            }
        }

        void SpawnImpact(Vector3 p, Vector3 n, Transform parent)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(d.GetComponent<Collider>());
            d.name = "MarcaTiro";
            d.transform.position = p + n * 0.002f;
            d.transform.rotation = Quaternion.LookRotation(-n);
            d.transform.localScale = Vector3.one * 0.012f;
            var r = d.GetComponent<Renderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.04f));
            r.sharedMaterial = m;
            d.transform.SetParent(parent, true);
            Destroy(d, 20f);
        }

        void EjectCasing()
        {
            if (_casingTemplate == null || _ejectPort == null) return;
            var c = Instantiate(_casingTemplate, _ejectPort.position, _chamberRound ? _chamberRound.rotation : transform.rotation);
            c.hideFlags = HideFlags.None;
            c.SetActive(true);
            c.transform.localScale = _chamberRound ? _chamberRound.lossyScale : Vector3.one;
            var col = c.AddComponent<CapsuleCollider>();
            col.radius = 0.0045f; col.height = 0.02f; col.direction = 2;
            var rb = c.AddComponent<Rigidbody>();
            rb.mass = 0.012f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var gc in _gunColliders) if (gc) Physics.IgnoreCollision(col, gc);
            // lado da janela de ejeção (direita da arma), para cima e um pouco para trás
            float side = Mathf.Sign(transform.InverseTransformPoint(_ejectPort.position).x);
            if (side == 0) side = 1;
            Vector3 v = transform.right * side * casingSpeed + transform.up * casingSpeed * 0.9f - transform.forward * 0.4f;
            var parentRb = GetComponent<Rigidbody>();
            rb.linearVelocity = v + (parentRb ? parentRb.linearVelocity : Vector3.zero);
            rb.angularVelocity = Random.insideUnitSphere * 25f;
            Destroy(c, casingLifetime);
        }

        void CycleSlide(bool lockBack)
        {
            if (_slide == null) return;
            if (_slideRoutine != null) StopCoroutine(_slideRoutine);
            _slideRoutine = StartCoroutine(SlideRoutine(lockBack));
        }

        IEnumerator SlideRoutine(bool lockBack)
        {
            Vector3 back = _slideRest + Vector3.back * slideTravel;   // para trás no espaço da arma
            float t = 0f, half = slideCycleTime * 0.4f;
            while (t < half) { t += Time.deltaTime; _slide.localPosition = Vector3.Lerp(_slideRest, back, t / half); yield return null; }
            _slide.localPosition = back;
            if (lockBack) { _slideLocked = true; yield break; }
            t = 0f; float ret = slideCycleTime * 0.6f;
            while (t < ret) { t += Time.deltaTime; _slide.localPosition = Vector3.Lerp(back, _slideRest, t / ret); yield return null; }
            _slide.localPosition = _slideRest;
        }

        void ReleaseSlide()
        {
            if (!_slideLocked || _slide == null) return;
            _slideLocked = false;
            if (_mag != null && magAmmo > 0) { magAmmo--; roundChambered = true; }
            UpdateChamberVisual();
            Play(slideReleaseSound, 1f);
            if (_slideRoutine != null) StopCoroutine(_slideRoutine);
            _slideRoutine = StartCoroutine(SlideForward());
        }

        IEnumerator SlideForward()
        {
            Vector3 from = _slide.localPosition; float t = 0f, d = 0.05f;
            while (t < d) { t += Time.deltaTime; _slide.localPosition = Vector3.Lerp(from, _slideRest, t / d); yield return null; }
            _slide.localPosition = _slideRest;
        }

        // ---------------- CARREGADOR ----------------
        void MagButton()
        {
            StartCoroutine(PressButton());
            if (_mag != null && _mag.parent == transform) EjectMag();
            else InsertNewMag();  // TEMPORÁRIO até termos a recarga manual
        }

        IEnumerator PressButton()
        {
            if (_magButton == null) yield break;
            // o botão afunda para dentro do punho (em direção ao centro da arma)
            float sx = Mathf.Abs(_magButtonRest.x) > 0.0005f ? -Mathf.Sign(_magButtonRest.x) : -1f;
            Vector3 inward = new Vector3(sx * 0.0025f, 0f, 0f);
            _magButton.localPosition = _magButtonRest + inward;
            yield return new WaitForSeconds(0.12f);
            _magButton.localPosition = _magButtonRest;
        }

        void EjectMag()
        {
            var mag = _mag;
            Vector3 dropDir = (_magSocket != null)
                ? (mag.GetComponentInChildren<Renderer>().bounds.center - _magSocket.position).normalized
                : -transform.up;
            mag.SetParent(null, true);
            var col = mag.GetComponentInChildren<Collider>();
            var rb = mag.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var parentRb = GetComponent<Rigidbody>();
            rb.linearVelocity = dropDir * 1.2f + (parentRb ? parentRb.linearVelocity : Vector3.zero);
            if (col) StartCoroutine(IgnoreForAWhile(col, 0.4f));
            mag.gameObject.name = "Carregador_Ejetado";
            Destroy(mag.gameObject, 30f);
            _mag = null;
            // as balas iam no carregador; a da câmara fica
            magAmmo = 0;
            Play(magOutSound, 1f);
        }

        IEnumerator IgnoreForAWhile(Collider c, float time)
        {
            foreach (var gc in _gunColliders) if (gc && gc != c) Physics.IgnoreCollision(c, gc, true);
            yield return new WaitForSeconds(time);
            foreach (var gc in _gunColliders) if (gc && c && gc != c) Physics.IgnoreCollision(c, gc, false);
        }

        void InsertNewMag()
        {
            if (_magTemplate == null) return;
            var m = Instantiate(_magTemplate, transform);
            m.hideFlags = HideFlags.None;
            m.name = "Carregador";
            m.transform.localPosition = _magRestPos;
            m.transform.localRotation = _magRestRot;
            m.SetActive(true);
            _mag = m.transform;
            magAmmo = magCapacity;
            Play(magInSound, 1f);
            // como numa Glock real (slingshot simplificado): se a corrediça estava presa, solta
            if (_slideLocked) ReleaseSlide();
        }

        void UpdateChamberVisual()
        {
            if (_chamberRound) _chamberRound.gameObject.SetActive(roundChambered);
        }

        void Play(AudioClip clip, float vol)
        {
            if (clip == null || _audio == null) return;
            _audio.pitch = Random.Range(0.97f, 1.03f);
            _audio.PlayOneShot(clip, vol * volume);
        }
    }
}
