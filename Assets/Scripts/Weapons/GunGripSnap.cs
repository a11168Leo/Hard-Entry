using UnityEngine;
using UnityEngine.XR;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Faz a arma "saltar" para uma posição fixa na mão quando é agarrada.
    ///
    /// CALIBRAR (dentro do jogo, com o headset):
    ///   1. Agarra a arma até ficar bem na mão (larga e volta a agarrar se for preciso).
    ///   2. Carrega em B (mão direita) ou Y (mão esquerda).
    ///   3. A posição fica gravada no ficheiro de dados (GripData) e é usada sempre a partir daí.
    ///
    /// Coloca este componente no objeto raiz da arma (o que tem o Rigidbody).
    /// </summary>
    [DefaultExecutionOrder(10000)] // corre depois do SDK mover a arma
    public class GunGripSnap : MonoBehaviour
    {
        [Tooltip("Ficheiro onde ficam guardadas as posições de pegada (criado no menu Hard Entry).")]
        public GunGripData gripData;

        [Tooltip("Permite calibrar com B/Y durante o jogo.")]
        public bool allowCalibration = true;

        [Tooltip("Suavidade do 'salto' para a mão (0 = instantâneo).")]
        [Range(0f, 0.2f)] public float snapSmoothing = 0.05f;

        Rigidbody _rb;
        Transform _rightWrist, _leftWrist;
        Transform _holder;          // pulso que está a segurar
        bool _holderIsRight;
        bool _wasGrabbed;
        bool _prevButton;
        float _snapT;

        // Recuo (aplicado por cima da pegada)
        Vector3 _recoilPos, _recoilRot;
        [Tooltip("Velocidade com que a arma volta à posição depois do recuo.")]
        public float recoilReturn = 14f;

        /// <summary>A arma está a ser segurada?</summary>
        public bool IsHeld => IsGrabbed && _holder != null;
        /// <summary>A mão que segura é a direita?</summary>
        public bool HeldByRight => _holderIsRight;
        /// <summary>Pulso (XRHand_Wrist) da mão que segura.</summary>
        public Transform HolderWrist => _holder;
        /// <summary>A arma já terminou o "salto" para a mão?</summary>
        public bool SnapSettled => IsHeld && (_snapT >= 1f || !HasTarget);
        bool HasTarget => (autoGrip && _hasAuto) || (gripData != null && gripData.Has(_holderIsRight));
        /// <summary>Chamado depois de gravar uma pegada com B/Y.</summary>
        public event System.Action Calibrated;

        [Tooltip("Adiciona automaticamente a pose dos dedos (GunHandPose) se não existir.")]
        public bool autoHandPose = true;

        [Header("Pegada automática")]
        [Tooltip("Calcula a posição da arma a partir da forma da mão (punho na palma, cano para a frente). " +
                 "Se carregares em B/Y grava a tua pegada e passa a usar essa.")]
        public bool autoGrip = true;
        [Tooltip("Distância do centro da palma ao centro do punho (m).")]
        public float autoPalmOffset = 0.028f;
        [Tooltip("Sobe (+) ou desce (-) a arma na mão (m).")]
        public float autoHeight = 0f;
        [Tooltip("Avança (+) ou recua (-) a arma na mão (m).")]
        public float autoForward = 0f;
        [Tooltip("Inclinação extra do cano para cima (+) / baixo (-), em graus.")]
        public float autoPitch = 0f;
        [Tooltip("Nome da peça que fica dentro do punho (usada para achar o centro do punho).")]
        public string gripPartName = "Carregador";
        [Tooltip("Nome do gatilho (a altura da mão alinha o indicador com ele).")]
        public string triggerPartName = "Gatilho";

        Vector3 _gripLocal, _triggerLocal;   // no espaço da arma
        bool _hasAuto;
        Vector3 _autoPos; Quaternion _autoRot; // no espaço do pulso

        /// <summary>Adiciona recuo: posição (m, no espaço da arma) e rotação (graus).</summary>
        public void AddRecoil(Vector3 pos, Vector3 rotEuler)
        {
            _recoilPos += pos;
            _recoilRot += rotEuler;
        }

        /// <summary>Todas as armas com pegada na cena.</summary>
        public static readonly System.Collections.Generic.List<GunGripSnap> All = new System.Collections.Generic.List<GunGripSnap>();
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (autoHandPose && GetComponent<GunHandPose>() == null) gameObject.AddComponent<GunHandPose>();

            // pontos de referência no espaço da arma (guardados agora, antes de o carregador poder sair)
            _gripLocal = new Vector3(0f, 0.05f, 0f);
            _triggerLocal = new Vector3(0f, 0.09f, 0.04f);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == gripPartName) _gripLocal = transform.InverseTransformPoint(r.bounds.center);
                else if (r.name == triggerPartName) _triggerLocal = transform.InverseTransformPoint(r.bounds.center);
            }
        }

        /// <summary>Calcula onde a arma fica na mão a partir da forma da mão.</summary>
        bool ComputeAutoGrip()
        {
            Vector3 pos; Quaternion rot;
            var hp = GetComponent<GunHandPose>();
            if (hp == null || !hp.ComputeGunPose(_holder, _holderIsRight, out pos, out rot))
            {
                // sem pose desenhada: estimativa simples a partir da forma da mão
                var j = HandGeo.FindJoints(_holder);
                if (!HandGeo.Frame(j, _holderIsRight, out var palm, out var across, out var fwd, out var normal)) return false;
                rot = Quaternion.LookRotation(fwd, across);
                pos = palm + normal * autoPalmOffset - rot * _gripLocal;
            }

            // afinações (no espaço da arma)
            if (autoPitch != 0f) rot = rot * Quaternion.Euler(-autoPitch, 0f, 0f);
            pos += rot * new Vector3(0f, autoHeight, autoForward);

            _autoPos = _holder.InverseTransformPoint(pos);
            _autoRot = Quaternion.Inverse(_holder.rotation) * rot;
            return true;
        }

        void Start()
        {
            FindWrists();
        }

        void FindWrists()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name != "XRHand_Wrist") continue;
                // Usa os pulsos das mãos visíveis (UnityXRHandVisualRight / Left)
                var p = t.parent;
                string chain = "";
                while (p != null) { chain += p.name + "/"; p = p.parent; }
                if (!chain.Contains("HandVisual")) continue;
                if (chain.Contains("Right")) _rightWrist = t;
                else if (chain.Contains("Left")) _leftWrist = t;
            }
            if (_rightWrist == null || _leftWrist == null)
                Debug.LogWarning($"[GunGrip] Pulsos encontrados: direito={_rightWrist != null}, esquerdo={_leftWrist != null}");
        }

        bool IsGrabbed => _rb != null && _rb.isKinematic; // o SDK põe a arma Kinematic enquanto está agarrada

        void LateUpdate()
        {
            if (_rightWrist == null || _leftWrist == null) { FindWrists(); if (_rightWrist == null && _leftWrist == null) return; }

            bool grabbed = IsGrabbed;

            // Início da pegada: descobrir que mão está a segurar
            if (grabbed && !_wasGrabbed)
            {
                float dR = _rightWrist ? Vector3.Distance(_rightWrist.position, transform.position) : float.MaxValue;
                float dL = _leftWrist ? Vector3.Distance(_leftWrist.position, transform.position) : float.MaxValue;
                _holderIsRight = dR <= dL;
                _holder = _holderIsRight ? _rightWrist : _leftWrist;
                _snapT = 0f;
                _hasAuto = autoGrip && _holder != null && ComputeAutoGrip();
            }
            _wasGrabbed = grabbed;
            if (!grabbed || _holder == null) return;

            // Calibração: B (direita) / Y (esquerda) = botão secundário
            if (allowCalibration && !autoGrip && gripData != null)
            {
                var dev = InputDevices.GetDeviceAtXRNode(_holderIsRight ? XRNode.RightHand : XRNode.LeftHand);
                bool pressed = false;
                if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.secondaryButton, out pressed);
                if (pressed && !_prevButton) Calibrate();
                _prevButton = pressed;
            }

            // Encaixar: pegada automática ou a gravada
            Vector3 lp; Quaternion lr;
            if (autoGrip && _hasAuto) { lp = _autoPos; lr = _autoRot; }
            else if (gripData != null && gripData.Has(_holderIsRight)) gripData.Get(_holderIsRight, out lp, out lr);
            else return;
            Vector3 targetPos = _holder.TransformPoint(lp);
            Quaternion targetRot = _holder.rotation * lr;

            // recuo
            float k = 1f - Mathf.Exp(-recoilReturn * Time.deltaTime);
            _recoilPos = Vector3.Lerp(_recoilPos, Vector3.zero, k);
            _recoilRot = Vector3.Lerp(_recoilRot, Vector3.zero, k);
            targetPos += targetRot * _recoilPos;
            targetRot = targetRot * Quaternion.Euler(_recoilRot);

            if (snapSmoothing > 0f && _snapT < 1f)
            {
                _snapT = Mathf.Min(1f, _snapT + Time.deltaTime / snapSmoothing);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, targetPos, _snapT),
                    Quaternion.Slerp(transform.rotation, targetRot, _snapT));
            }
            else
            {
                transform.SetPositionAndRotation(targetPos, targetRot);
            }
        }

        void Calibrate()
        {
            Vector3 lp = _holder.InverseTransformPoint(transform.position);
            Quaternion lr = Quaternion.Inverse(_holder.rotation) * transform.rotation;
            gripData.Set(_holderIsRight, lp, lr);
            // Se a outra mão ainda não tem pegada, cria uma em espelho como ponto de partida
            if (!gripData.Has(!_holderIsRight)) gripData.SetMirroredFrom(_holderIsRight);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gripData);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(gripData);
#endif
            Debug.Log($"[GunGrip] Pegada {(_holderIsRight ? "DIREITA" : "ESQUERDA")} gravada: pos {lp}, rot {lr.eulerAngles}");
            if (autoGrip) { autoGrip = false; Debug.Log("[GunGrip] A partir de agora usa a pegada gravada. Para voltar à automática, marca 'Auto Grip'."); }
            // Vibração curta para confirmar
            var dev = InputDevices.GetDeviceAtXRNode(_holderIsRight ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid) dev.SendHapticImpulse(0, 0.6f, 0.15f);
            Calibrated?.Invoke();
        }
    }
}
