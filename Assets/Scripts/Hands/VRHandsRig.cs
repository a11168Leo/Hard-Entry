using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace HardEntry.Hands
{
    /// <summary>
    /// Liga o modelo "fps-hands" aos comandos do Quest:
    /// - o pulso fica onde a mão real segura o comando (o comando em si fica escondido)
    /// - o antebraço acompanha a mão
    /// - gatilho dobra o indicador, grip fecha os restantes dedos + polegar
    /// Coloca este componente no objeto "fps-hands".
    /// </summary>
    [DefaultExecutionOrder(1000)] // corre depois do tracking dos comandos
    public class VRHandsRig : MonoBehaviour
    {
        [System.Serializable]
        public class FingerBone
        {
            public Transform bone;
            [HideInInspector] public Quaternion rest;
            public float maxAngle;
        }

        [System.Serializable]
        public class HandSide
        {
            [Tooltip("L para a esquerda, R para a direita (sufixo dos ossos)")]
            public string suffix = "L";
            public XRNode node = XRNode.LeftHand;

            [Header("Ligações (preenchidas sozinhas se ficarem vazias)")]
            public Transform controller;
            [Tooltip("Osso raiz da mão, ex.: forearm.L.001")]
            public Transform handRoot;
            [Tooltip("Osso do pulso, ex.: hand.L (é este que fica preso ao comando)")]
            public Transform wrist;
            [Tooltip("Osso raiz do braço, ex.: clavicle.L (segue a mão de forma rígida)")]
            public Transform armRoot;

            [Header("Ajuste fino (afina em Play Mode)")]
            [Tooltip("Posição do pulso em relação ao comando, em metros (Z negativo = para trás).")]
            public Vector3 wristOffset = new Vector3(0f, -0.02f, -0.08f);
            [Tooltip("Rotação extra da mão, em graus.")]
            public Vector3 rotationOffset;
            [Tooltip("Liga se a palma ficar virada para o lado errado.")]
            public bool flipPalm;

            [Header("Estado (só leitura)")]
            [Range(0, 1)] public float grip;
            [Range(0, 1)] public float trigger;

            [HideInInspector] public Vector3 wristLocalPos;      // pulso em relação a handRoot
            [HideInInspector] public Quaternion wristLocalRot;
            [HideInInspector] public Quaternion boneAlign;       // eixos do osso -> eixos "dedos/palma"
            [HideInInspector] public Vector3 armLocalPos;
            [HideInInspector] public Quaternion armLocalRot;
            [HideInInspector] public List<FingerBone> index = new();
            [HideInInspector] public List<FingerBone> fingers = new();
            [HideInInspector] public List<FingerBone> thumb = new();
            [HideInInspector] public bool ready;
        }

        public HandSide left = new HandSide { suffix = "L", node = XRNode.LeftHand };
        public HandSide right = new HandSide { suffix = "R", node = XRNode.RightHand };

        [Header("Comandos")]
        [Tooltip("Esconde o modelo branco dos comandos (fica só a mão).")]
        public bool hideControllerModels = true;

        [Header("Dedos")]
        [Tooltip("Eixo local em que os ossos dos dedos dobram. Se dobrarem para o lado errado, experimenta (-1,0,0), (0,0,1) ou (0,0,-1).")]
        public Vector3 curlAxis = new Vector3(1, 0, 0);
        [Tooltip("Eixo de dobra do polegar. Se o polegar dobrar para o lado errado, experimenta (-1,0,0) ou (0,0,1).")]
        public Vector3 thumbAxis = new Vector3(1, 0, 0);
        [Tooltip("Velocidade com que os dedos acompanham o comando.")]
        public float fingerSpeed = 20f;

        [Header("Braços")]
        [Tooltip("Se ligado, o antebraço acompanha a mão. Se desligado, o braço fica parado.")]
        public bool followArms = true;

        [Header("Teste sem headset")]
        [Tooltip("Liga para testar os dedos com os sliders Grip/Trigger, sem comandos.")]
        public bool useDebugValues = false;

        float _hideTimer;

        void Awake()
        {
            Setup(left);
            Setup(right);

            // Os ossos vão para longe da origem do modelo; sem isto o Unity pode esconder a malha.
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
        }

        void Setup(HandSide h)
        {
            string s = h.suffix;
            if (h.handRoot == null) h.handRoot = FindDeep(transform, $"forearm.{s}.001");
            if (h.wrist == null) h.wrist = FindDeep(transform, $"hand.{s}");
            if (h.armRoot == null) h.armRoot = FindDeep(transform, $"clavicle.{s}");
            if (h.controller == null)
            {
                // Inclui objetos desativados: o XR Input Modality Manager desliga os
                // comandos enquanto não estão a ser seguidos pelo headset.
                string wanted = s == "L" ? "Left Controller" : "Right Controller";
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name == wanted) { h.controller = t; break; }
                }
            }

            if (h.handRoot == null || h.wrist == null || h.controller == null)
            {
                Debug.LogWarning($"[VRHandsRig] Lado {s}: antebraço={(h.handRoot ? h.handRoot.name : "EM FALTA")}, pulso={(h.wrist ? h.wrist.name : "EM FALTA")}, comando={(h.controller ? h.controller.name : "EM FALTA")}. Liga-os no Inspector.");
                return;
            }

            // Pulso em relação à raiz da mão (pose original do modelo)
            Quaternion invRoot = Quaternion.Inverse(h.handRoot.rotation);
            h.wristLocalPos = invRoot * (h.wrist.position - h.handRoot.position);
            h.wristLocalRot = invRoot * h.wrist.rotation;

            // Braço em relação à raiz da mão
            if (h.armRoot != null)
            {
                h.armLocalPos = invRoot * (h.armRoot.position - h.handRoot.position);
                h.armLocalRot = invRoot * h.armRoot.rotation;
            }

            // Descobrir, a partir da geometria, para onde apontam os dedos e a palma
            // no espaço local do osso do pulso.
            var middle = FindDeep(transform, $"f_middle.01.{s}");
            var indexB = FindDeep(transform, $"f_index.01.{s}");
            var pinkyB = FindDeep(transform, $"f_pinky.01.{s}");
            if (middle != null && indexB != null && pinkyB != null)
            {
                Quaternion invWrist = Quaternion.Inverse(h.wrist.rotation);
                Vector3 fingerDir = invWrist * (middle.position - h.wrist.position).normalized;
                Vector3 across = invWrist * (indexB.position - pinkyB.position).normalized;
                Vector3 palmDir = Vector3.Cross(fingerDir, across).normalized;
                if (s == "L") palmDir = -palmDir; // espelho (na mão esquerda o produto vetorial aponta para as costas da mão)
                // boneAlign: rotação que leva (Z = dedos, Y = palma) para os eixos do osso
                h.boneAlign = Quaternion.LookRotation(fingerDir, palmDir);
            }
            else
            {
                h.boneAlign = Quaternion.identity;
            }

            AddChain(h.index, "f_index", s, 70f, 90f, 60f);
            AddChain(h.fingers, "f_middle", s, 75f, 95f, 65f);
            AddChain(h.fingers, "f_ring", s, 80f, 100f, 65f);
            AddChain(h.fingers, "f_pinky", s, 85f, 100f, 65f);
            // Polegar: só as duas últimas falanges dobram, e pouco (o polegar fica em cima do comando)
            AddChain(h.thumb, "thumb", s, 0f, 25f, 35f);

            h.ready = true;
        }

        void AddChain(List<FingerBone> list, string baseName, string s, float a1, float a2, float a3)
        {
            float[] angles = { a1, a2, a3 };
            for (int i = 1; i <= 3; i++)
            {
                var t = FindDeep(transform, $"{baseName}.0{i}.{s}");
                if (t == null) continue;
                list.Add(new FingerBone { bone = t, rest = t.localRotation, maxAngle = angles[i - 1] });
            }
        }

        void LateUpdate()
        {
            if (hideControllerModels)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f)
                {
                    _hideTimer = 0.5f; // os modelos podem aparecer mais tarde, por isso verifica de vez em quando
                    HideModels(left.controller);
                    HideModels(right.controller);
                }
            }

            UpdateHand(left);
            UpdateHand(right);
        }

        void HideModels(Transform controller)
        {
            if (controller == null) return;
            // Só malhas 3D: os raios (LineRenderer) continuam visíveis.
            foreach (var r in controller.GetComponentsInChildren<MeshRenderer>(true))
                if (!r.transform.IsChildOf(transform)) r.enabled = false;
            foreach (var r in controller.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!r.transform.IsChildOf(transform)) r.enabled = false;
        }

        void UpdateHand(HandSide h)
        {
            if (!h.ready) return;

            // 1) Onde o pulso deve ficar: dedos para a frente do comando,
            //    palma virada para o punho do comando.
            Vector3 palmTarget = h.suffix == "L" ? h.controller.right : -h.controller.right;
            if (h.flipPalm) palmTarget = -palmTarget;
            Quaternion target = Quaternion.LookRotation(h.controller.forward, palmTarget)
                                * Quaternion.Euler(h.rotationOffset);
            Quaternion wristRot = target * Quaternion.Inverse(h.boneAlign);
            Vector3 wristPos = h.controller.TransformPoint(h.wristOffset);

            // 2) Calcular a raiz da mão a partir do pulso
            Quaternion rootRot = wristRot * Quaternion.Inverse(h.wristLocalRot);
            Vector3 rootPos = wristPos - rootRot * h.wristLocalPos;
            h.handRoot.SetPositionAndRotation(rootPos, rootRot);

            // 3) Braço segue a mão (rígido)
            if (followArms && h.armRoot != null)
            {
                h.armRoot.SetPositionAndRotation(
                    rootPos + rootRot * h.armLocalPos,
                    rootRot * h.armLocalRot);
            }

            // 4) Ler gatilho e grip
            if (!useDebugValues)
            {
                var device = InputDevices.GetDeviceAtXRNode(h.node);
                float g = 0f, t = 0f;
                if (device.isValid)
                {
                    device.TryGetFeatureValue(CommonUsages.grip, out g);
                    device.TryGetFeatureValue(CommonUsages.trigger, out t);
                }
                float k = 1f - Mathf.Exp(-fingerSpeed * Time.deltaTime);
                h.grip = Mathf.Lerp(h.grip, g, k);
                h.trigger = Mathf.Lerp(h.trigger, t, k);
            }

            // 5) Dobrar os dedos
            Curl(h.index, h.trigger, curlAxis);
            Curl(h.fingers, h.grip, curlAxis);
            Curl(h.thumb, h.grip, thumbAxis);
        }

        static void Curl(List<FingerBone> bones, float amount, Vector3 axis)
        {
            foreach (var f in bones)
                f.bone.localRotation = f.rest * Quaternion.AngleAxis(f.maxAngle * amount, axis);
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
