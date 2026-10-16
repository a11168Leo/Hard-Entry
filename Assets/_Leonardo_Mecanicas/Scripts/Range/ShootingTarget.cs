using System.Collections;
using UnityEngine;
using HardEntry.Weapons;

namespace HardEntry.Range
{
    /// <summary>
    /// Alvo do stand de tiro.
    ///  - Papel: conta pontos pelos anéis (10 no centro) e mostra o último tiro e o total.
    ///  - Placa: placa de aço que cai para trás quando é atingida e volta a levantar.
    /// Para a Placa, este componente fica no objeto da base (a dobradiça); o colisor pode estar num filho.
    /// </summary>
    public class ShootingTarget : MonoBehaviour, IShootable
    {
        public enum Kind { Papel, Placa }
        public Kind kind = Kind.Papel;

        [Header("Papel")]
        [Tooltip("Raio do anel exterior (m).")]
        public float radius = 0.25f;
        [Tooltip("Número de anéis (o centro vale este número).")]
        public int rings = 10;
        [Tooltip("Texto onde aparecem os pontos (opcional).")]
        public TextMesh scoreText;

        [Header("Placa")]
        [Tooltip("Ângulo para trás quando cai (graus).")]
        public float fallAngle = 85f;
        [Tooltip("Segundos até voltar a levantar.")]
        public float resetTime = 2.5f;

        [Header("Som")]
        public AudioClip hitSound;

        int _total, _shots;
        bool _down;
        Quaternion _upRot;

        void Awake() => _upRot = transform.localRotation;

        void Start() => UpdateText(-1);

        public void OnShot(Vector3 point, Vector3 normal, Vector3 direction)
        {
            if (hitSound) AudioSource.PlayClipAtPoint(hitSound, point, 0.8f);
            if (kind == Kind.Papel) PaperHit(point);
            else if (!_down) StartCoroutine(Fall());
        }

        void PaperHit(Vector3 point)
        {
            float d = Vector3.ProjectOnPlane(point - transform.position, transform.forward).magnitude;
            int score = Mathf.Clamp(rings - Mathf.FloorToInt(d / radius * rings), 0, rings);
            _shots++;
            _total += score;
            UpdateText(score);
        }

        void UpdateText(int last)
        {
            if (scoreText == null || kind != Kind.Papel) return;
            scoreText.text = last < 0 ? "-" : $"{last}\n{_total} / {_shots} tiros";
        }

        /// <summary>Limpa os pontos (papel) ou levanta a placa.</summary>
        public void ResetTarget()
        {
            _total = _shots = 0;
            UpdateText(-1);
            StopAllCoroutines();
            _down = false;
            transform.localRotation = _upRot;
        }

        IEnumerator Fall()
        {
            _down = true;
            Quaternion downRot = _upRot * Quaternion.Euler(fallAngle, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                transform.localRotation = Quaternion.Slerp(_upRot, downRot, t * t);
                yield return null;
            }
            transform.localRotation = downRot;
            yield return new WaitForSeconds(resetTime);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                transform.localRotation = Quaternion.Slerp(downRot, _upRot, t);
                yield return null;
            }
            transform.localRotation = _upRot;
            _down = false;
        }
    }
}
