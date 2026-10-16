using UnityEngine;

namespace HardEntry.Gear
{
    /// <summary>
    /// Mantém o colete no peito do jogador: segue a cabeça (câmara do headset),
    /// mas só roda à volta do eixo vertical e com algum atraso, como o tronco.
    /// </summary>
    public class PlayerVest : MonoBehaviour
    {
        [Tooltip("Câmara do headset. Se ficar vazio, procura a CenterEyeAnchor / Camera.main.")]
        public Transform head;
        [Tooltip("Posição do colete em relação aos olhos (x = lado, y = altura, z = frente).")]
        public Vector3 offset = new Vector3(0f, -0.24f, -0.09f);
        [Tooltip("Tempo que o tronco demora a acompanhar a rotação da cabeça (s).")]
        public float turnSmooth = 0.25f;
        [Tooltip("Só roda o tronco quando a cabeça vira mais do que isto (graus).")]
        public float turnDeadZone = 25f;

        float _yaw, _yawVel;
        bool _init;

        void FindHead()
        {
            if (head != null) return;
            var c = GameObject.Find("CenterEyeAnchor");
            if (c != null) head = c.transform;
            else if (Camera.main != null) head = Camera.main.transform;
        }

        void LateUpdate()
        {
            FindHead();
            if (head == null) return;

            Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(head.up, Vector3.up); // a olhar para baixo/cima
            float headYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

            if (!_init) { _yaw = headYaw; _init = true; }
            float diff = Mathf.DeltaAngle(_yaw, headYaw);
            float target = Mathf.Abs(diff) > turnDeadZone ? headYaw - Mathf.Sign(diff) * turnDeadZone * 0.5f : _yaw;
            _yaw = Mathf.SmoothDampAngle(_yaw, target, ref _yawVel, turnSmooth);

            Quaternion rot = Quaternion.Euler(0f, _yaw, 0f);
            transform.SetPositionAndRotation(head.position + rot * offset, rot);
        }
    }
}
