using UnityEngine;

// 3D progress indicator: a transparent capsule/bar shell with an inner fill driven
// by a world-axis clip in the fill's shader. Drop the prefab in the scene, drag it
// into the owning script's slot, and call SetProgress(0..1).
//
// Vertical mode  (isVertical = true):  bottomMarker = empty (bottom), topMarker = full (top).   Drives _FillHeight on world Y.
// Horizontal mode (isVertical = false): bottomMarker = empty (left),  topMarker = full (right). Drives _FillWidth  on world X.
// The unused axis is held at a sentinel value so its Step always passes.
public class ProgressIndicator : MonoBehaviour
{
    [Tooltip("Renderer using a material with _FillHeight (world-Y) and _FillWidth (world-X) clip thresholds.")]
    public Renderer fillRenderer;

    [Tooltip("Optional shell renderer to hide alongside the fill when idle.")]
    public Renderer shellRenderer;

    [Tooltip("Marker for progress = 0. Vertical: bottom edge. Horizontal: left edge.")]
    public Transform bottomMarker;

    [Tooltip("Marker for progress = 1. Vertical: top edge. Horizontal: right edge.")]
    public Transform topMarker;

    [Tooltip("Vertical bar (fills bottom→top via world Y) when true; horizontal (left→right via world X) when false.")]
    public bool isVertical = true;

    [Tooltip("Shader property name for the object-Y clip threshold (vertical fill).")]
    public string fillHeightProperty = "_Fill_Height";

    [Tooltip("Shader property name for the object-X clip threshold (horizontal fill).")]
    public string fillWidthProperty = "_Fill_Width";

    [Tooltip("Hide the indicator when progress is 0 or 1 (no task running / just finished).")]
    public bool hideWhenIdle = true;

    // Any value larger than the world bounds the bar will ever occupy. Step on the unused axis
    // sees this as "infinite" and passes all pixels through.
    private const float DisabledAxisSentinel = 9999f;

    private Material _fillMat;
    private int _fillHeightId;
    private int _fillWidthId;
    private float _progress;
    private bool _visible;

    void Awake()
    {
        if (fillRenderer != null) _fillMat = fillRenderer.material;
        _fillHeightId = Shader.PropertyToID(fillHeightProperty);
        _fillWidthId = Shader.PropertyToID(fillWidthProperty);
        if (hideWhenIdle) SetVisible(false);
    }

    public void SetProgress(float t)
    {
        _progress = Mathf.Clamp01(t);
        SetVisible(!hideWhenIdle || (_progress > 0f && _progress < 1f));
        PushFill();
    }

    void LateUpdate()
    {
        // Markers move with carry sway / parent animation. Re-push every frame after
        // transforms settle so the clip plane tracks the mesh.
        if (_visible) PushFill();
    }

    private void PushFill()
    {
        if (_fillMat == null || bottomMarker == null || topMarker == null || fillRenderer == null) return;

        // Convert markers into the fill mesh's object space so the clip plane is invariant
        // to the bar's world rotation (pot rotating in a player's hand, etc.).
        Transform meshT = fillRenderer.transform;
        Vector3 localBottom = meshT.InverseTransformPoint(bottomMarker.position);
        Vector3 localTop = meshT.InverseTransformPoint(topMarker.position);

        if (isVertical)
        {
            float localY = Mathf.Lerp(localBottom.y, localTop.y, _progress);
            _fillMat.SetFloat(_fillHeightId, localY);
            _fillMat.SetFloat(_fillWidthId, DisabledAxisSentinel);
        }
        else
        {
            float localX = Mathf.Lerp(localBottom.x, localTop.x, _progress);
            _fillMat.SetFloat(_fillWidthId, localX);
            _fillMat.SetFloat(_fillHeightId, DisabledAxisSentinel);
        }
    }

    public void Hide() => SetVisible(false);

    private void SetVisible(bool v)
    {
        _visible = v;
        if (fillRenderer != null && fillRenderer.enabled != v) fillRenderer.enabled = v;
        if (shellRenderer != null && shellRenderer.enabled != v) shellRenderer.enabled = v;
    }
}
