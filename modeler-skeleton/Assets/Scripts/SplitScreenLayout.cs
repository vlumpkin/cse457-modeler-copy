using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SplitScreenLayout : MonoBehaviour
{
    [Tooltip("Horizontal field of view (degrees) each player camera should render. Vertical FOV is derived per-viewport so framing stays consistent.")]
    public float horizontalFovDegrees = 90f;

    [Tooltip("Minimum vertical FOV (degrees). Wide-but-short viewports (e.g. 2-player top/bottom split) would derive a tiny vertical FOV from horizontalFovDegrees alone; this floor prevents the 'mail slot' effect by letting horizontal FOV grow past the target instead.")]
    [Range(30f, 120f)] public float minVerticalFovDegrees = 55f;

    [Header("Border")]
    [Tooltip("Thickness in pixels of the line drawn between viewports. 0 = no border.")]
    public float borderPixels = 4f;
    [Tooltip("Color of the border line.")]
    public Color borderColor = Color.black;

    readonly List<PlayerInput> players = new List<PlayerInput>();
    Canvas borderCanvas;
    readonly List<RectTransform> borderRects = new List<RectTransform>();

    void LateUpdate()
    {
        if (PlayerInputCountChanged())
        {
            players.Clear();
            players.AddRange(PlayerInput.all);
            Relayout();
        }
    }

    bool PlayerInputCountChanged()
    {
        if (PlayerInput.all.Count != players.Count) return true;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != PlayerInput.all[i]) return true;
        return false;
    }

    void Relayout()
    {
        Debug.Log($"[SplitScreenLayout] Relayout for {players.Count} player(s)");
        for (int i = 0; i < players.Count; i++)
        {
            var cam = players[i].camera;
            if (cam == null)
            {
                Debug.LogWarning($"[SplitScreenLayout] Player {i} has no camera assigned on PlayerInput; skipping.", players[i]);
                continue;
            }
            Rect r = RectFor(i, players.Count);
            cam.rect = r;
            float pixelW = Screen.width * r.width;
            float pixelH = Screen.height * r.height;
            if (pixelH > 0f)
            {
                float aspect = pixelW / pixelH;
                cam.aspect = aspect;

                // Derive vertical FOV from the target horizontal FOV.
                float hFovRad = horizontalFovDegrees * Mathf.Deg2Rad;
                float vFovRad = 2f * Mathf.Atan(Mathf.Tan(hFovRad * 0.5f) / aspect);
                float vFovDeg = vFovRad * Mathf.Rad2Deg;

                // Wide viewports (e.g. 16:4.5 in a 2-player top/bottom split) would
                // give a tiny vertical FOV like 36°. Clamp up to the configured floor;
                // horizontal FOV grows past the target as a consequence — exactly
                // what players want for ultra-wide letterboxed views.
                if (vFovDeg < minVerticalFovDegrees) vFovDeg = minVerticalFovDegrees;

                cam.fieldOfView = vFovDeg;
            }
            Debug.Log($"[SplitScreenLayout]  P{i} ({cam.name}) rect={r} aspect={cam.aspect:F3} vFov={cam.fieldOfView:F1}");
        }
        UpdateBorders(players.Count);
    }

    void UpdateBorders(int count)
    {
        if (borderPixels <= 0f)
        {
            if (borderCanvas != null) borderCanvas.gameObject.SetActive(false);
            return;
        }

        EnsureBorderCanvas();
        borderCanvas.gameObject.SetActive(true);

        // (anchorMin, anchorMax) for each border line in normalized 0..1 screen coords.
        // Width/height in pixels is borderPixels for the thin axis.
        var lines = new List<(Vector2 aMin, Vector2 aMax, bool horizontal)>();
        switch (count)
        {
            case 2:
                lines.Add((new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), true)); // mid horizontal
                break;
            case 3:
                lines.Add((new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), true)); // mid horizontal
                lines.Add((new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), false)); // top vertical
                break;
            case 4:
                lines.Add((new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), true)); // mid horizontal
                lines.Add((new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), false)); // mid vertical
                break;
            // case 1: no borders
        }

        // Reuse / spawn / hide RectTransforms to match `lines`.
        for (int i = 0; i < lines.Count; i++)
        {
            RectTransform rt = i < borderRects.Count ? borderRects[i] : CreateBorderRect();
            rt.gameObject.SetActive(true);
            rt.anchorMin = lines[i].aMin;
            rt.anchorMax = lines[i].aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // Stretch axis = 0 thickness from anchors → set sizeDelta on that axis = thickness.
            rt.sizeDelta = lines[i].horizontal ? new Vector2(0f, borderPixels) : new Vector2(borderPixels, 0f);
            rt.GetComponent<Image>().color = borderColor;
        }
        for (int i = lines.Count; i < borderRects.Count; i++)
            borderRects[i].gameObject.SetActive(false);
    }

    void EnsureBorderCanvas()
    {
        if (borderCanvas != null) return;
        var go = new GameObject("SplitScreenBorders", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        borderCanvas = go.GetComponent<Canvas>();
        borderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        borderCanvas.sortingOrder = 1000; // on top of player UI
    }

    RectTransform CreateBorderRect()
    {
        var go = new GameObject("Border", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(borderCanvas.transform, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        borderRects.Add(rt);
        return rt;
    }

    static Rect RectFor(int index, int count)
    {
        switch (count)
        {
            case 1:
                return new Rect(0f, 0f, 1f, 1f);

            case 2:
                return index == 0
                    ? new Rect(0f, 0.5f, 1f, 0.5f)
                    : new Rect(0f, 0f, 1f, 0.5f);

            case 3:
                switch (index)
                {
                    case 0: return new Rect(0f,   0.5f, 0.5f, 0.5f);
                    case 1: return new Rect(0.5f, 0.5f, 0.5f, 0.5f);
                    default: return new Rect(0f, 0f, 1f, 0.5f);
                }

            case 4:
            default:
                switch (index)
                {
                    case 0: return new Rect(0f,   0.5f, 0.5f, 0.5f);
                    case 1: return new Rect(0.5f, 0.5f, 0.5f, 0.5f);
                    case 2: return new Rect(0f,   0f,   0.5f, 0.5f);
                    default: return new Rect(0.5f, 0f, 0.5f, 0.5f);
                }
        }
    }
}
