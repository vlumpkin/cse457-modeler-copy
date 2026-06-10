using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Customer-side plate reclaim station. When a delivered plate is "processed"
/// (DeliveryCounter calls <see cref="PlateProcessed"/>), a dirty plate is
/// returned to this station after <see cref="reclaimDelay"/> seconds and
/// instantiated at the next available placement point.
///
/// Players approach with empty hands and pick up as many dirty plates as are
/// present, up to the carry cap (3). The plates aren't re-instantiated on
/// pickup — the existing GameObjects are reparented to the player's hold
/// anchors, then the carry stack can only be dropped at a Sink.
///
/// Wiring:
///   - Put this component on the same GameObject as a Station whose
///     kind == StationKind.DirtyPlateReclaim.
///   - Assign platePrefab (the same Pickupable plate prefab used elsewhere).
///   - Fill placementPoints with empty child Transforms (one per visual slot,
///     fill order).
/// </summary>
[RequireComponent(typeof(Station))]
public class DirtyPlateReclaim : MonoBehaviour
{
    [Header("Plate prefab + slots")]
    [Tooltip("Pickupable plate prefab instantiated at a placement point each time a queued reclaim timer fires.")]
    public Pickupable platePrefab;
    [Tooltip("One empty Transform per visual slot, in fill order. Capacity is placementPoints.Length.")]
    public Transform[] placementPoints;

    [Header("Timing")]
    [Tooltip("Seconds between PlateProcessed() and the resulting dirty plate appearing at this station.")]
    public float reclaimDelay = 12f;

    [Header("State (read-only-ish)")]
    [Tooltip("Live count of dirty plates currently sitting on the station.")]
    public int dirtyCount;
    [Tooltip("How many reclaim timers are running but haven't yet produced a plate.")]
    public int pendingCount;

    // Plates that are physically sitting at placementPoints[0..dirtyCount-1].
    private readonly List<Pickupable> visuals = new List<Pickupable>();

    // Time-remaining list for queued plate returns. We use a plain list since
    // the typical pending count is tiny (1–3) and we tick every frame anyway.
    private readonly List<float> pendingTimers = new List<float>();

    public int Capacity => placementPoints != null ? placementPoints.Length : 0;
    public bool IsFull => dirtyCount >= Capacity;
    public bool HasAny => dirtyCount > 0;

    /// <summary>
    /// Called by DeliveryCounter when a plated soup is delivered. After
    /// <see cref="reclaimDelay"/> seconds the customer "returns" a dirty plate
    /// that lands at the next free placement point.
    /// </summary>
    public void PlateProcessed()
    {
        pendingTimers.Add(reclaimDelay);
        pendingCount = pendingTimers.Count;
    }

    private void Update()
    {
        if (pendingTimers.Count == 0) return;

        float dt = Time.deltaTime;
        // Iterate backwards so we can remove fired timers cleanly.
        for (int i = pendingTimers.Count - 1; i >= 0; i--)
        {
            float t = pendingTimers[i] - dt;
            if (t <= 0f)
            {
                pendingTimers.RemoveAt(i);
                SpawnDirtyPlate();
            }
            else
            {
                pendingTimers[i] = t;
            }
        }
        pendingCount = pendingTimers.Count;
    }

    private void SpawnDirtyPlate()
    {
        if (platePrefab == null)
        {
            Debug.LogWarning($"[DirtyPlateReclaim] {name}: platePrefab is null — dropping queued plate.");
            return;
        }
        if (IsFull)
        {
            // No room. Keep things simple: drop the plate on the floor (log it).
            // If you'd rather queue indefinitely until a slot frees, swap this
            // for "pendingTimers.Add(0.5f);" to retry shortly.
            Debug.LogWarning($"[DirtyPlateReclaim] {name}: all {Capacity} slots full — dropping queued plate.");
            return;
        }

        int slot = dirtyCount;
        Transform anchor = placementPoints[slot];
        if (anchor == null)
        {
            Debug.LogWarning($"[DirtyPlateReclaim] {name}: placementPoints[{slot}] is null — dropping queued plate.");
            return;
        }

        Pickupable plate = Instantiate(platePrefab, anchor.position, anchor.rotation, anchor);
        plate.transform.localPosition = Vector3.zero;
        plate.transform.localRotation = Quaternion.identity;
        MarkDirty(plate);
        visuals.Add(plate);
        dirtyCount = visuals.Count;
    }

    /// <summary>
    /// Player walks up empty-handed and grabs up to <paramref name="max"/>
    /// plates. The plates are reparented to the supplied anchors (anchors[0]
    /// = bottom of stack); any remaining placementPoint slots reflow toward
    /// the front so the next pickup finds them contiguously.
    /// </summary>
    public int TakePlates(Transform[] anchors, int max, Pickupable[] outPlates = null)
    {
        if (anchors == null || max <= 0 || !HasAny) return 0;

        int taken = 0;
        for (int i = 0; i < max && i < anchors.Length && visuals.Count > 0; i++)
        {
            Pickupable plate = visuals[0];
            visuals.RemoveAt(0);
            if (plate == null) continue;

            if (anchors[i] == null)
            {
                // Caller gave us a slot but no Transform — leave the plate behind by re-inserting.
                visuals.Insert(0, plate);
                break;
            }

            plate.OnPickedUp(anchors[i]);
            if (outPlates != null && i < outPlates.Length) outPlates[i] = plate;
            taken++;
        }

        // Reflow remaining plates onto the front placementPoints so the next
        // pickup / next reclaim spawn sees a packed list.
        for (int i = 0; i < visuals.Count; i++)
        {
            Pickupable p = visuals[i];
            if (p == null) continue;
            Transform anchor = (placementPoints != null && i < placementPoints.Length) ? placementPoints[i] : null;
            if (anchor != null) p.OnPlaced(anchor);
        }

        dirtyCount = visuals.Count;
        return taken;
    }

    private static void MarkDirty(Pickupable plate)
    {
        plate.plateState = PlateState.Dirty;
        plate.plateContents = PlateContents.Empty;
        PlateSoup ps = plate.GetComponentInChildren<PlateSoup>();
        if (ps != null) ps.SetDirty(true);
    }
}
