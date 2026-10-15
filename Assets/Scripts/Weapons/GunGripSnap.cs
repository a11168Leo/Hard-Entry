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

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
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
            }
            _wasGrabbed = grabbed;
            if (!grabbed || _holder == null) return;

            // Calibração: B (direita) / Y (esquerda) = botão secundário
            if (allowCalibration && gripData != null)
            {
                var dev = InputDevices.GetDeviceAtXRNode(_holderIsRight ? XRNode.RightHand : XRNode.LeftHand);
                bool pressed = false;
                if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.secondaryButton, out pressed);
                if (pressed && !_prevButton) Calibrate();
                _prevButton = pressed;
            }

            // Encaixar na posição gravada
            if (gripData == null || !gripData.Has(_holderIsRight)) return;
            gripData.Get(_holderIsRight, out var lp, out var lr);
            Vector3 targetPos = _holder.TransformPoint(lp);
            Quaternion targetRot = _holder.rotation * lr;

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
            // Vibração curta para confirmar
            var dev = InputDevices.GetDeviceAtXRNode(_holderIsRight ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid) dev.SendHapticImpulse(0, 0.6f, 0.15f);
        }
    }
}
