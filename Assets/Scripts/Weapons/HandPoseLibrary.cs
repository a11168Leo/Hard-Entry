using System.Collections.Generic;
using UnityEngine;

namespace HardEntry.Weapons
{
    /// <summary>
    /// Poses de mão desenhadas no Blender (mão direita da Meta a segurar a arma).
    /// Posições das articulações no espaço local da arma (Unity), em metros.
    /// G17: mão rodada para os nós dos dedos acompanharem o punho e dedos médio/anelar/mindinho fechados
    /// à volta da frente do punho real (medido no G17.fbx) — antes ficavam ~1,5 cm à frente dele.
    /// </summary>
    public static class HandPoseLibrary
    {
        public static readonly Dictionary<string, Vector3> G17 = new Dictionary<string, Vector3>
        {
            { "IndexDistal", new Vector3(0.01552f, 0.09058f, -0.00845f) },
            { "IndexIntermediate", new Vector3(0.03077f, 0.08761f, -0.02713f) },
            { "IndexMetacarpal", new Vector3(0.00847f, 0.07877f, -0.12153f) },
            { "IndexProximal", new Vector3(0.01728f, 0.07665f, -0.06084f) },
            { "IndexTip", new Vector3(0.00471f, 0.09167f, -0.00443f) },
            { "LittleDistal", new Vector3(0.01216f, 0.00196f, -0.04393f) },
            { "LittleIntermediate", new Vector3(0.02582f, 0.00894f, -0.05725f) },
            { "LittleMetacarpal", new Vector3(0.01305f, 0.03672f, -0.12710f) },
            { "LittleProximal", new Vector3(0.02289f, 0.02045f, -0.08558f) },
            { "LittleTip", new Vector3(0.00031f, 0.00095f, -0.04447f) },
            { "MiddleDistal", new Vector3(-0.00969f, 0.04475f, -0.02311f) },
            { "MiddleIntermediate", new Vector3(0.01746f, 0.04922f, -0.02444f) },
            { "MiddleMetacarpal", new Vector3(0.02055f, 0.06579f, -0.12507f) },
            { "MiddleProximal", new Vector3(0.02757f, 0.05734f, -0.06536f) },
            { "MiddleTip", new Vector3(-0.01425f, 0.04583f, -0.03437f) },
            { "Palm", new Vector3(0.02412f, 0.05637f, -0.09860f) },
            { "RingDistal", new Vector3(-0.00723f, 0.02272f, -0.03438f) },
            { "RingIntermediate", new Vector3(0.01877f, 0.02710f, -0.03777f) },
            { "RingMetacarpal", new Vector3(0.02008f, 0.05247f, -0.12738f) },
            { "RingProximal", new Vector3(0.02739f, 0.03844f, -0.07406f) },
            { "RingTip", new Vector3(-0.01353f, 0.02468f, -0.04434f) },
            { "ThumbDistal", new Vector3(-0.02221f, 0.07434f, -0.05119f) },
            { "ThumbMetacarpal", new Vector3(-0.00946f, 0.08216f, -0.11579f) },
            { "ThumbProximal", new Vector3(-0.01576f, 0.07837f, -0.08412f) },
            { "ThumbTip", new Vector3(-0.02506f, 0.07258f, -0.03672f) },
            { "Wrist", new Vector3(0.00731f, 0.06406f, -0.15865f) },
        };

        public static Dictionary<string, Vector3> Get(string name)
        {
            switch (name)
            {
                case "G17": return G17;
                default: return G17;
            }
        }
    }
}
