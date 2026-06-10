using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class LocalBodyHider : MonoBehaviour
{
    [Tooltip("Roots whose hierarchies should be hidden from the local camera (body, head, arm containers).")]
    public Transform[] hideRoots;

    [Tooltip("Roots that should stay visible even if nested under a hidden root (hands, held item anchor).")]
    public Transform[] keepVisibleRoots;

    [Tooltip("Layer name pattern; {0} = playerIndex+1. Layers must exist in Project Settings.")]
    public string layerNamePattern = "P{0}Body";

    [Tooltip("Layer to restore for keepVisibleRoots. Default (0) is usually right.")]
    public int visibleLayer = 0;

    void Start()
    {
        var pi = GetComponent<PlayerInput>();
        string layerName = string.Format(layerNamePattern, pi.playerIndex + 1);
        int hiddenLayer = LayerMask.NameToLayer(layerName);
        if (hiddenLayer < 0)
        {
            Debug.LogWarning($"[LocalBodyHider] Layer '{layerName}' missing — add it in Project Settings > Tags and Layers.", this);
            return;
        }

        foreach (var root in hideRoots)
            if (root != null) SetLayerRecursive(root.gameObject, hiddenLayer);

        foreach (var root in keepVisibleRoots)
            if (root != null) SetLayerRecursive(root.gameObject, visibleLayer);

        if (pi.camera != null)
            pi.camera.cullingMask &= ~(1 << hiddenLayer);
        else
            Debug.LogWarning("[LocalBodyHider] PlayerInput has no Camera assigned; nothing will be culled.", this);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
