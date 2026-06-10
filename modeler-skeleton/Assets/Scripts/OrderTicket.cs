using UnityEngine;

/// <summary>
/// Component placed on each order ticket prefab. Lets the prefab own its
/// visual layout (background cube, fill cube, ticket art, etc.) while
/// telling OrderQueue which pieces to drive at runtime.
/// </summary>
public class OrderTicket : MonoBehaviour
{
    [Tooltip("Empty Transform pinned at the LEFT edge of the bar. Its localScale.x " +
             "is driven from 1 → 0 as time runs out. Put the visible fill cube as a " +
             "child of this anchor (offset +0.5 on X, unit scale) so it shrinks from " +
             "the right edge instead of from the center.")]
    public Transform fillAnchor;

    [Tooltip("Renderer on the fill cube. Its material color is set to OrderQueue's " +
             "fill color (or warning color near expiry). Leave null to skip recoloring.")]
    public Renderer fillRenderer;

    [Tooltip("Optional: renderer on the background cube. Its material color is set " +
             "once at spawn from OrderQueue's background color. Leave null to keep " +
             "the prefab's material color untouched.")]
    public Renderer backgroundRenderer;
}
