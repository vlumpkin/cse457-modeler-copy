using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drop on a Canvas (World Space or Screen Space) holding a Text/TMP_Text.
// Shows "Starting in 3..." that counts down once every player has locked in,
// then hides itself. Works with either legacy UI Text or TextMeshPro — assign
// whichever one your project uses; leave the other null.
public class LobbyCountdownUI : MonoBehaviour
{
    [Header("Refs")]
    public LobbyManager manager;
    [Tooltip("Root GameObject to show/hide as the countdown starts/stops. Usually this same GameObject.")]
    public GameObject root;

    [Header("Label (assign one)")]
    public Text legacyLabel;
    public TMP_Text tmpLabel;

    [Header("Format")]
    [Tooltip("{0} is replaced with the seconds-remaining integer.")]
    public string format = "Starting in {0}...";
    [Tooltip("Shown during the final second before the game starts.")]
    public string loadingText = "Loading...";

    void Awake()
    {
        if (manager == null) manager = FindAnyObjectByType<LobbyManager>();
        // Note: do NOT default 'root' to gameObject. If this script disables its
        // own GameObject, LateUpdate stops running and the countdown never shows.
        // Leave it null to fall back to toggling label visibility only.
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (manager == null) { SetVisible(false); return; }

        bool active = manager.CountdownActive;
        SetVisible(active);
        if (!active) return;

        float remaining = manager.CountdownRemaining;
        // Display one less than the seconds remaining so a 4s delay reads
        // "Starting in 3, 2, 1" and then "Loading..." during the final second.
        int secs = Mathf.CeilToInt(remaining) - 1;
        string text = secs <= 0 ? loadingText : string.Format(format, secs);
        if (legacyLabel != null) legacyLabel.text = text;
        if (tmpLabel != null) tmpLabel.text = text;
    }

    void SetVisible(bool v)
    {
        if (root != null)
        {
            if (root != gameObject && root.activeSelf != v) root.SetActive(v);
        }
        // Always toggle the labels too, so even with no 'root' assigned the UI works.
        if (legacyLabel != null && legacyLabel.enabled != v) legacyLabel.enabled = v;
        if (tmpLabel != null && tmpLabel.enabled != v) tmpLabel.enabled = v;
    }
}
