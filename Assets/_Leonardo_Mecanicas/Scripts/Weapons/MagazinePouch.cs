using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Bolsa de carregadores (no colete). Leva a mão livre à bolsa, carrega no grip
    /// e sai um carregador cheio na mão.
    /// </summary>
    public class MagazinePouch : MonoBehaviour
    {
        [Tooltip("Para que arma são os carregadores (G17, AWP...).")]
        public string weaponId = "G17";
        [Tooltip("Quantos carregadores tem (-1 = infinitos, para testes).")]
        public int count = -1;
        [Tooltip("Distância da palma à bolsa para tirar um carregador (m).")]
        public float reach = 0.10f;
        [Tooltip("Mostrar o carregador a sair da bolsa.")]
        public bool showMagInPouch = true;

        readonly bool[] _prevGrip = new bool[2];
        GameObject _preview;

        void Update()
        {
            UpdatePreview();
            if (count == 0 || !MagazineFactory.Has(weaponId)) return;

            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1;
                bool grip = HandRig.Grip(right) > 0.6f;
                bool down = grip && !_prevGrip[i];
                _prevGrip[i] = grip;
                if (!down || HandRig.IsBusy(right)) continue;

                var palm = HandRig.Palm(right);
                if (palm == null || Vector3.Distance(palm.position, transform.position) > reach) continue;

                var m = MagazineFactory.Create(weaponId, transform.position, transform.rotation);
                if (m == null) continue;
                m.AttachToHand(right);
                if (count > 0) count--;
            }
        }

        // um carregador "espetado" na bolsa, só para se ver que há munição
        void UpdatePreview()
        {
            if (!showMagInPouch) return;
            bool want = count != 0 && MagazineFactory.Has(weaponId);
            if (want && _preview == null)
            {
                var m = MagazineFactory.Create(weaponId, Vector3.zero, Quaternion.identity);
                if (m == null) return;
                Vector3 up = m.UpWorld, fwd = m.ForwardWorld, top = m.TopWorld; // com rotação zero
                var go = m.gameObject;
                Destroy(m);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                var rb = go.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
                go.name = "CarregadorNaBolsa";
                // eixos do carregador alinhados com a bolsa (y = para cima, z = frente), topo 1,5 cm fora
                Quaternion rot = transform.rotation * Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
                go.transform.SetPositionAndRotation(transform.position - rot * top + transform.up * 0.015f, rot);
                go.transform.SetParent(transform, true);
                _preview = go;
            }
            else if (!want && _preview != null)
            {
                Destroy(_preview);
            }
        }
    }
}
