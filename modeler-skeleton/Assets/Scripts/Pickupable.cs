using UnityEngine;

public enum PickupableKind { Food, Plate, Pot, FireExtinguisher }
public enum FoodState { Raw, Cut }
public enum VegetableType { Carrot, Onion, Tomato }
public enum PlateState { Clean, Dirty }
public enum PlateContents { Empty, Soup }

public class Pickupable : MonoBehaviour
{
    [Header("Kind")]
    public PickupableKind kind = PickupableKind.Food;

    [Header("General")]
    [Tooltip("Disable colliders while held so the character capsule doesn't fight the item.")]
    public bool disableColliderWhileHeld = true;

    [Header("Food (only relevant when Kind == Food)")]
    public FoodState foodState = FoodState.Raw;
    public VegetableType vegetableType = VegetableType.Carrot;
    [Tooltip("Ordered visual stages: index 0 = whole, last index = fully cut. " +
             "Each chop advances to the next entry. If empty, falls back to cutsRequired with no visual swap.")]
    public GameObject[] cutStageVisuals;
    [Tooltip("Used only when cutStageVisuals is empty. Otherwise derived from the array length.")]
    public int cutsRequired = 5;
    [Tooltip("Read-only-ish — incremented by OvercookedCharacter when a chop lands.")]
    public int cutProgress = 0;

    public int CutsRequired => (cutStageVisuals != null && cutStageVisuals.Length >= 2)
        ? cutStageVisuals.Length - 1
        : cutsRequired;

    [Header("Plate (only relevant when Kind == Plate)")]
    public PlateState plateState = PlateState.Clean;
    public PlateContents plateContents = PlateContents.Empty;
    [Tooltip("Only meaningful when plateContents == Soup.")]
    public VegetableType soupType = VegetableType.Carrot;

    [Header("Pot (only relevant when Kind == Pot)")]
    [Tooltip("Cooking state lives on the PotContents component attached to this object.")]
    [TextArea] public string potNote = "See PotContents component for vegetable count, cook timer, and visuals.";

    private Collider[] cachedColliders;

    void Awake()
    {
        if (kind == PickupableKind.Food) ApplyStageVisual();
    }

    public bool RegisterCut()
    {
        if (kind != PickupableKind.Food || foodState != FoodState.Raw) return false;
        cutProgress++;
        int needed = CutsRequired;
        ApplyStageVisual();
        if (cutProgress >= needed)
        {
            foodState = FoodState.Cut;
            if (GameProgressTracker.Instance != null) GameProgressTracker.Instance.CompleteWork(1f);
            return true; // finished
        }
        return false;
    }

    private void ApplyStageVisual()
    {
        if (cutStageVisuals == null || cutStageVisuals.Length == 0) return;
        int active = Mathf.Clamp(cutProgress, 0, cutStageVisuals.Length - 1);
        for (int i = 0; i < cutStageVisuals.Length; i++)
        {
            var go = cutStageVisuals[i];
            if (go != null && go.activeSelf != (i == active)) go.SetActive(i == active);
        }
    }

    public void OnPickedUp(Transform anchor)
    {
        if (disableColliderWhileHeld)
        {
            if (cachedColliders == null) cachedColliders = GetComponentsInChildren<Collider>();
            foreach (var c in cachedColliders) c.enabled = false;
        }

        SnapTo(anchor);
    }

    public void OnPlaced(Transform slot)
    {
        SnapTo(slot);

        if (cachedColliders != null)
            foreach (var c in cachedColliders) c.enabled = true;
    }

    private void SnapTo(Transform target)
    {
        // SetParent(target, true) preserves world scale by adjusting localScale.
        // Then zero localPosition/Rotation so we snap to the anchor.
        transform.SetParent(target, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
