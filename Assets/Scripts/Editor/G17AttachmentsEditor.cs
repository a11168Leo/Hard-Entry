#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HardEntry.EditorTools
{
    public static class G17AttachmentsMenu
    {
        [MenuItem("Hard Entry/Armas/Separar lanterna da G17")]
        public static void SeparateLight()
        {
            var gun = GameObject.Find("G17");
            if (gun == null) { Debug.LogError("[G17] Não encontrei 'G17' na cena."); return; }
            Transform light = null;
            foreach (var t in gun.GetComponentsInChildren<Transform>(true))
                if (t.name == "Lanterna") { light = t; break; }
            if (light == null) { Debug.LogError("[G17] A G17 não tem 'Lanterna'."); return; }

            // 1) ponto de encaixe no trilho (para encaixar a lanterna mais tarde)
            var socket = gun.transform.Find("EncaixeLanterna");
            if (socket == null)
            {
                socket = new GameObject("EncaixeLanterna").transform;
                Undo.RegisterCreatedObjectUndo(socket.gameObject, "Encaixe lanterna");
                socket.SetParent(gun.transform, false);
            }
            socket.localPosition = light.localPosition;
            socket.localRotation = light.localRotation;

            // 2) lanterna solta, como objeto próprio
            var mf = light.GetComponent<MeshFilter>();
            var mr = light.GetComponent<MeshRenderer>();
            var loose = new GameObject("Lanterna");
            Undo.RegisterCreatedObjectUndo(loose, "Lanterna solta");
            loose.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            loose.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            loose.transform.localScale = light.lossyScale;
            loose.transform.SetPositionAndRotation(gun.transform.position + new Vector3(0.25f, 0.05f, 0f), Quaternion.identity);

            var bc = loose.AddComponent<BoxCollider>();
            bc.center = mf.sharedMesh.bounds.center;
            bc.size = mf.sharedMesh.bounds.size;
            var rb = loose.AddComponent<Rigidbody>();
            rb.mass = 0.13f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 3) esconder a lanterna que estava presa à arma
            Undo.RecordObject(light.gameObject, "Esconder lanterna");
            light.gameObject.SetActive(false);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.scene);
            Selection.activeObject = loose;
            Debug.Log("[G17] Lanterna separada. Para a poderes agarrar: GameObject > Interaction SDK > Add Grab Interaction (com a Lanterna selecionada).");
        }
    }
}
#endif
