using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Lógica da pistola (G17):
    ///  - Gatilho do comando  -> dispara (semi-automática)
    ///  - Botão A / X         -> carrega no botão do carregador e o carregador cai
    ///  - Recarregar           -> tira um carregador do colete com a outra mão e enfia-o no punho
    ///  - Botão B / Y          -> solta a corrediça trancada atrás (retém)
    ///  - Outra mão na corrediça + grip -> puxa a corrediça atrás (municia / tira bala)
    /// Encontra as peças pelo nome: Corredica, Gatilho, BotaoCarregador, Carregador,
    /// BalaCamara, PontaCano, SaidaCapsula, EncaixeCarregador.
    /// Coloca no objeto raiz da arma (o mesmo do GunGripSnap).
    /// </summary>
    [RequireComponent(typeof(GunGripSnap))]
    public class PistolController : MonoBehaviour, IMagazineWell
    {
        [Header("Arma")]
        [Tooltip("Identificador dos carregadores desta arma (as bolsas do colete usam o mesmo).")]
        public string weaponId = "G17";

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

        [Tooltip("Distância da palma à traseira da corrediça para a conseguir puxar (m).")]
        public float rackReach = 0.07f;

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
        bool _prevTrigger, _prevA, _prevB;
        Vector3 _slideRearLocal;
        Vector3 _magUpLocalGun, _magFwdLocalGun;   // eixo do carregador no espaço da arma
        float _wellDepthLocal = 0.1f;              // quanto o carregador entra (espaço da arma)
        // puxar a corrediça com a outra mão
        bool _racking, _rackHandRight, _rackReachedBack, _rackWasLocked;
        Vector3 _rackStartHand; float _rackStartOffset;
        readonly bool[] _prevOffGrip = new bool[2];
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

            if (_slide)
            {
                _slideRest = _slide.localPosition;
                var sr = _slide.GetComponentInChildren<Renderer>();
                if (sr)
                {
                    // ponto mais atrás da corrediça (onde se agarra para a puxar)
                    Vector3 c = transform.InverseTransformPoint(sr.bounds.center);
                    float minZ = float.MaxValue;
                    var b = sr.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        minZ = Mathf.Min(minZ, transform.InverseTransformPoint(corner).z);
                    }
                    _slideRearLocal = new Vector3(c.x, c.y, minZ + 0.01f);
                }
            }
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

                // eixo do carregador (do centro para o encaixe) e frente, no espaço da arma
                var mr = _mag.GetComponentInChildren<Renderer>();
                Vector3 centerGun = transform.InverseTransformPoint(mr ? mr.bounds.center : _mag.position);
                Vector3 topGun = _magSocket ? transform.InverseTransformPoint(_magSocket.position) : _magRestPos;
                _magUpLocalGun = (topGun - centerGun).normalized;
                _magFwdLocalGun = Vector3.ProjectOnPlane(Vector3.forward, _magUpLocalGun).normalized;
                // profundidade do poço: do encaixe até ao fundo do carregador (ao longo do eixo)
                _wellDepthLocal = 0.1f;
                if (mr)
                {
                    var bb = mr.bounds; float deepest = 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, (i & 2) == 0 ? bb.min.y : bb.max.y, (i & 4) == 0 ? bb.min.z : bb.max.z);
                        deepest = Mathf.Max(deepest, Vector3.Dot(topGun - transform.InverseTransformPoint(corner), _magUpLocalGun));
                    }
                    if (deepest > 0.02f) _wellDepthLocal = deepest;
                }
                // mesmos vetores no espaço do próprio carregador
                Quaternion inv = Quaternion.Inverse(_magRestRot);
                Vector3 s = _mag.localScale;
                Vector3 topMag = inv * (topGun - _magRestPos);
                topMag = new Vector3(topMag.x / s.x, topMag.y / s.y, topMag.z / s.z);
                MagazineFactory.Register(weaponId, _magTemplate, magCapacity, topMag, inv * _magUpLocalGun, inv * _magFwdLocalGun);
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

            // só uma mão pode agarrar a arma (a outra fica livre para recarregar / puxar a corrediça)
            var grab = GetComponent<Oculus.Interaction.Grabbable>();
            if (grab != null)
            {
                var f = typeof(Oculus.Interaction.Grabbable).GetField("_maxGrabPoints",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(grab, 1);
            }

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
                if (_racking) EndRack();
                _prevTrigger = false; _prevA = false; _prevB = false;
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

            // B / Y: retém da corrediça (solta a corrediça trancada)
            bool b = false;
            if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.secondaryButton, out b);
            if (b && !_prevB && _slideLocked && _mag != null && !_racking) ReleaseSlide();
            _prevB = b;

            UpdateRack();
        }

        void OnEnable() { if (!MagazineFactory.Wells.Contains(this)) MagazineFactory.Wells.Add(this); }
        void OnDisable() { MagazineFactory.Wells.Remove(this); }

        // ---------------- IMagazineWell ----------------
        public string WeaponId => weaponId;
        public bool HasMagazine => _mag != null;
        public Transform Socket => _magSocket;
        public Vector3 MagUpWorld => transform.TransformDirection(_magUpLocalGun);
        public Vector3 MagForwardWorld => transform.TransformDirection(_magFwdLocalGun);
        public float WellDepth => _wellDepthLocal * transform.lossyScale.y;

        public void InsertMagazine(Magazine m)
        {
            _mag = m.transform;
            _mag.SetParent(transform, true);
            magAmmo = m.ammo;
            _mag.gameObject.name = "Carregador";
            StartCoroutine(SeatMag(_mag));
            Play(magInSound, 1f);
            // sem colisões entre o carregador e a arma enquanto entra
            foreach (var c in _mag.GetComponentsInChildren<Collider>())
                foreach (var gc in _gunColliders) if (gc && gc != c) Physics.IgnoreCollision(c, gc, true);
        }

        IEnumerator SeatMag(Transform mag)
        {
            Vector3 p0 = mag.localPosition; Quaternion r0 = mag.localRotation;
            float t = 0f, d = 0.06f;
            while (t < d && mag != null && mag.parent == transform)
            {
                t += Time.deltaTime;
                mag.localPosition = Vector3.Lerp(p0, _magRestPos, t / d);
                mag.localRotation = Quaternion.Slerp(r0, _magRestRot, t / d);
                yield return null;
            }
            if (mag != null && mag.parent == transform) { mag.localPosition = _magRestPos; mag.localRotation = _magRestRot; }
        }

        void MarkInserted()
        {
            if (_mag == null) return;
            var m = _mag.GetComponent<Magazine>();
            if (m != null) { m.enabled = false; }
            var rb = _mag.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
        }

        // ---------------- PUXAR A CORREDIÇA (outra mão) ----------------
        void UpdateRack()
        {
            if (_slide == null) return;
            bool holder = _grip.HeldByRight;
            bool off = !holder;
            int oi = off ? 1 : 0;
            bool grip = HandRig.Grip(off) > 0.6f;
            bool down = grip && !_prevOffGrip[oi];
            _prevOffGrip[oi] = grip;
            var palm = HandRig.Palm(off);
            if (palm == null) return;

            if (!_racking)
            {
                if (!down || HandRig.HeldBy(off) != null) return;
                Vector3 rear = transform.TransformPoint(_slideRearLocal);
                if (Vector3.Distance(palm.position, rear) > rackReach) return;
                _racking = true;
                _rackHandRight = off;
                _rackStartHand = palm.position;
                _rackWasLocked = _slideLocked;
                _rackStartOffset = Vector3.Dot(_slideRest - _slide.localPosition, Vector3.forward); // 0 = à frente
                _rackReachedBack = false;
                if (_slideRoutine != null) { StopCoroutine(_slideRoutine); _slideRoutine = null; }
                HandRig.SetHeld(off, this);
                HandRig.Buzz(off, 0.3f, 0.03f);
                return;
            }

            if (!grip)
            {
                EndRack();
                return;
            }

            float pull = _rackStartOffset + Vector3.Dot(palm.position - _rackStartHand, -transform.forward);
            pull = Mathf.Clamp(pull, 0f, slideTravel);
            _slide.localPosition = _slideRest + Vector3.back * pull;
            if (!_rackReachedBack && pull >= slideTravel * 0.9f)
            {
                _rackReachedBack = true;
                // a bala que estava na câmara salta fora
                if (roundChambered) { roundChambered = false; EjectCasing(); UpdateChamberVisual(); }
                HandRig.Buzz(off, 0.5f, 0.04f);
            }
        }

        void EndRack()
        {
            _racking = false;
            HandRig.SetHeld(_rackHandRight, null);
            bool pulledFromLock = _rackWasLocked && Vector3.Dot(_slideRest - _slide.localPosition, Vector3.forward) < slideTravel * 0.98f;
            if (_rackReachedBack || pulledFromLock || _slideLocked)
            {
                // carregador vazio: o retém tranca a corrediça atrás
                if (_mag != null && magAmmo <= 0 && _rackReachedBack)
                {
                    _slide.localPosition = _slideRest + Vector3.back * slideTravel;
                    _slideLocked = true;
                    return;
                }
                _slideLocked = true; // ReleaseSlide municia e manda a corrediça à frente
                ReleaseSlide();
            }
            else
            {
                // largou a meio: volta à frente sem mexer na câmara
                if (_slideRoutine != null) StopCoroutine(_slideRoutine);
                _slideRoutine = StartCoroutine(SlideForward());
            }
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
            if (!roundChambered && _mag != null && magAmmo > 0) { magAmmo--; roundChambered = true; }
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
            Vector3 dropDir = -MagUpWorld;
            mag.SetParent(null, true);
            var loose = MagazineFactory.MakeLoose(weaponId, mag.gameObject, magAmmo); // as balas ficam no carregador
            if (loose != null) loose.OnEjected();
            var rb = mag.GetComponent<Rigidbody>();
            var parentRb = GetComponent<Rigidbody>();
            if (rb) rb.linearVelocity = dropDir * 1.2f + (parentRb ? parentRb.linearVelocity : Vector3.zero);
            foreach (var col in mag.GetComponentsInChildren<Collider>()) StartCoroutine(IgnoreForAWhile(col, 0.6f));
            mag.gameObject.name = "Carregador_Ejetado";
            _mag = null;
            magAmmo = 0;
            Play(magOutSound, 1f);
        }

        IEnumerator IgnoreForAWhile(Collider c, float time)
        {
            foreach (var gc in _gunColliders) if (gc && gc != c) Physics.IgnoreCollision(c, gc, true);
            yield return new WaitForSeconds(time);
            foreach (var gc in _gunColliders) if (gc && c && gc != c) Physics.IgnoreCollision(c, gc, false);
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
