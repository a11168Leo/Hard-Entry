using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>Posições de pegada (mão direita e esquerda) de uma arma, relativas ao pulso.</summary>
    [CreateAssetMenu(menuName = "Hard Entry/Gun Grip Data", fileName = "GripData")]
    public class GunGripData : ScriptableObject
    {
        public bool hasRight, hasLeft;
        public Vector3 rightPos, leftPos;
        public Quaternion rightRot = Quaternion.identity, leftRot = Quaternion.identity;

        public bool Has(bool right) => right ? hasRight : hasLeft;

        public void Get(bool right, out Vector3 pos, out Quaternion rot)
        {
            pos = right ? rightPos : leftPos;
            rot = right ? rightRot : leftRot;
        }

        public void Set(bool right, Vector3 pos, Quaternion rot)
        {
            if (right) { rightPos = pos; rightRot = rot; hasRight = true; }
            else { leftPos = pos; leftRot = rot; hasLeft = true; }
        }

        /// <summary>Cria a pegada da outra mão em espelho (plano X do pulso). Pode precisar de recalibrar.</summary>
        public void SetMirroredFrom(bool fromRight)
        {
            Get(fromRight, out var p, out var r);
            var mp = new Vector3(-p.x, p.y, p.z);
            var mr = new Quaternion(r.x, -r.y, -r.z, r.w);
            Set(!fromRight, mp, mr);
        }

        [ContextMenu("Apagar pegadas")]
        public void Clear() { hasRight = hasLeft = false; }
    }
}
