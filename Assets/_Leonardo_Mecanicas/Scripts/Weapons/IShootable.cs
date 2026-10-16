using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>Qualquer coisa que reage a ser atingida por um tiro (alvos, placas, inimigos...).</summary>
    public interface IShootable
    {
        /// <param name="point">Ponto de impacto (mundo).</param>
        /// <param name="normal">Normal da superfície atingida.</param>
        /// <param name="direction">Direção do tiro.</param>
        void OnShot(Vector3 point, Vector3 normal, Vector3 direction);
    }
}
