using UnityEngine;

// One per lobby slot. Polls LobbyManager each frame and swaps which of three
// child GameObjects is active based on the slot's state.
//
// Suggested hierarchy inside the lobby scene:
//   SpawnPoint0/
//     SlotUI/                        (World-Space Canvas, faces camera)
//       Empty       → "Press (A) to join"
//       Occupied    → "◀  (A) to confirm  ▶"
//       Locked      → "Ready!"
// Drag Empty/Occupied/Locked into the matching fields on this component.
public class LobbySlotUI : MonoBehaviour
{
    [Header("Slot")]
    public LobbyManager manager;
    [Tooltip("0 = first joiner (Blue), 1 = Red, 2 = Green, 3 = Yellow.")]
    public int slotIndex = 0;

    [Header("State roots (toggle SetActive)")]
    public GameObject emptyRoot;
    public GameObject occupiedRoot;
    public GameObject lockedRoot;

    [Header("Per-color backgrounds")]
    [Tooltip("One background GameObject per color, indexed the same as LobbyPlayer.colors " +
             "(0=Blue, 1=Red, 2=Green, 3=Yellow). Only backgrounds[slotIndex] is shown, " +
             "and only while the slot is Occupied or Locked.")]
    public GameObject[] backgrounds = new GameObject[4];

    void Awake()
    {
        if (manager == null) manager = FindAnyObjectByType<LobbyManager>();
    }

    void LateUpdate()
    {
        if (manager == null) return;

        LobbyPlayer lp = manager.GetPlayerInSlot(slotIndex);
        bool empty = lp == null;
        bool locked = lp != null && lp.IsLocked;
        bool occupied = lp != null && !locked;

        if (emptyRoot != null && emptyRoot.activeSelf != empty) emptyRoot.SetActive(empty);
        if (occupiedRoot != null && occupiedRoot.activeSelf != occupied) occupiedRoot.SetActive(occupied);
        if (lockedRoot != null && lockedRoot.activeSelf != locked) lockedRoot.SetActive(locked);

        // Backgrounds: hide all during Empty, show only the slot's own color during Occupied/Locked.
        bool showBg = !empty;
        if (backgrounds != null)
        {
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] == null) continue;
                bool on = showBg && i == slotIndex;
                if (backgrounds[i].activeSelf != on) backgrounds[i].SetActive(on);
            }
        }
    }
}
