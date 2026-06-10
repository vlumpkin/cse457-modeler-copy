using UnityEngine;

public enum StationKind { Counter, CuttingBoard, Sink, FireExtinguisher, Burner, SupplyBox, Trashcan, DeliveryCounter, DirtyPlateReclaim }

public class Station : MonoBehaviour
{
    public StationKind kind = StationKind.Counter;

    [Tooltip("Optional anchor where placed items sit. Falls back to this transform.")]
    public Transform placementAnchor;

    [Tooltip("Item currently sitting on the station (auto-detected on Start if left null and a Pickupable is a child).")]
    public Pickupable current;

    [Tooltip("Knife transform (for CuttingBoard kind). Borrowed by the character while cutting.")]
    public Transform knife;

    [Tooltip("Prefab dispensed when a player picks up from an empty SupplyBox (e.g. an onion Pickupable).")]
    public Pickupable supplyPrefab;

    [Tooltip("Optional 3D progress meter above this station. Used by CuttingBoard (cuts/total). Burner cooking progress lives on the pot itself.")]
    public ProgressIndicator progressIndicator;

    private void Start()
    {
        if (placementAnchor == null) placementAnchor = transform;
        if (current == null) current = GetComponentInChildren<Pickupable>();
        if (current != null) current.OnPlaced(placementAnchor);
    }

    public bool HasItem => current != null;

    public bool TryPlace(Pickupable item)
    {
        if (current != null) return false;
        current = item;
        item.OnPlaced(placementAnchor);
        return true;
    }

    public Pickupable TryTake()
    {
        if (current == null)
        {
            if (kind == StationKind.SupplyBox && supplyPrefab != null)
            {
                // Spawn at the placement anchor so it has a sensible world pose before OnPickedUp re-parents it.
                Pickupable spawned = Instantiate(supplyPrefab, placementAnchor.position, placementAnchor.rotation);
                if (GameProgressTracker.Instance != null) GameProgressTracker.Instance.RegisterWork(1f);
                return spawned;
            }
            return null;
        }
        Pickupable p = current;
        current = null;
        return p;
    }

    private void Update()
    {
        switch (kind)
        {
            case StationKind.Burner:
                TickBurner();
                break;
            case StationKind.CuttingBoard:
                TickCuttingBoard();
                break;
        }
    }

    private void TickBurner()
    {
        if (current == null) return;
        PotContents pot = current.GetComponentInChildren<PotContents>();
        if (pot != null) pot.Tick(Time.deltaTime);
    }

    private void TickCuttingBoard()
    {
        if (progressIndicator == null) return;
        if (current == null || current.kind != PickupableKind.Food || current.foodState != FoodState.Raw)
        {
            progressIndicator.SetProgress(0f);
            return;
        }
        int needed = current.CutsRequired;
        float t = needed > 0 ? (float)current.cutProgress / needed : 0f;
        progressIndicator.SetProgress(t);
    }
}
