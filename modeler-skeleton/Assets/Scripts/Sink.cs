using System.Collections.Generic;
using UnityEngine;

// Sink station: holds internal counters of dirty/clean plates (plates are not stored as
// Pickupable objects in slots — they are pure counters with one visual stand-in per slot).
//
// Wiring:
//   - Put this component on the same GameObject as a Station whose kind == StationKind.Sink.
//   - Fill dirtyPlacementPoints / cleanPlacementPoints with empty child Transforms (one per
//     visual slot, in the order they should fill).
//   - Assign dirtyPlateVisualPrefab / cleanPlateVisualPrefab — plain visual-only prefabs
//     (no Pickupable needed) that get instantiated at each occupied placement point.
//   - Assign cleanPlatePrefab — the real Pickupable plate prefab spawned when a player
//     takes a clean plate from the sink.
//   - Optionally assign a ProgressIndicator for the wash progress bar.
//
// The OvercookedCharacter dispatches sink-specific pickup/action behavior by checking
// for this component on the facing Station.
[RequireComponent(typeof(Station))]
public class Sink : MonoBehaviour
{
    [Header("State (read-only-ish)")]
    public int dirtyCount;
    public int cleanCount;

    [Header("Capacity")]
    [Tooltip("Hard cap on dirty plates the sink can hold. Should match dirtyPlacementPoints length but enforced separately so you can cap below visual count.")]
    public int maxDirty = 4;
    [Tooltip("Hard cap on clean plates the sink can hold.")]
    public int maxClean = 4;

    [Header("Placement points (one Transform per slot, fill order)")]
    public Transform[] dirtyPlacementPoints;
    public Transform[] cleanPlacementPoints;

    [Header("Plate prefab (single source — clean/dirty toggled via PlateSoup.isDirty)")]
    [Tooltip("The plate Pickupable prefab. Used both as the visual stand-in at each occupied " +
             "placement point (with isDirty set to match the slot) and as the real plate handed " +
             "to the player when they take a clean plate.")]
    public Pickupable platePrefab;

    [Header("Visual scale")]
    [Tooltip("Local scale applied to a plate while it sits in a dirty slot. Reset to 1 when it becomes clean.")]
    public float dirtyPlateScale = 0.8f;

    [Header("Washing")]
    [Tooltip("Seconds to wash one dirty plate into a clean plate.")]
    public float washSeconds = 3f;
    [Tooltip("Optional 3D progress indicator. Driven by current wash progress (0 when idle).")]
    public ProgressIndicator progressIndicator;

    // Spawned visual stand-ins, tracked so we can release/recreate as counts change.
    // Invariant (after init): dirtyVisuals.Count == dirtyCount, cleanVisuals.Count == cleanCount.
    private readonly List<GameObject> dirtyVisuals = new List<GameObject>();
    private readonly List<GameObject> cleanVisuals = new List<GameObject>();

    private void Start()
    {
        RefreshVisuals();
        UpdateIndicator();
    }

    public bool IsFullDirty => dirtyCount >= maxDirty;
    public bool IsFullClean => cleanCount >= maxClean;
    public bool HasDirty => dirtyCount > 0;
    public bool HasClean => cleanCount > 0;

    // Called by OvercookedCharacter when the player presses pickup while holding a plate.
    // Only dirty plates can be deposited. Returns true if the plate was consumed.
    //
    // Whenever possible, the held plate itself is reparented into the next free dirty slot
    // and reused as the visual stand-in — no Destroy + Instantiate, so the deposit happens
    // in a single frame with no hitch (matches how Station.TryPlace works for counters).
    public bool TryDepositDirtyPlate(Pickupable plate)
    {
        if (plate == null || plate.kind != PickupableKind.Plate) return false;
        if (IsFullDirty) return false;
        if (!IsPlateDirty(plate)) return false;

        // Require a visual slot — keeps dirtyCount and dirtyVisuals in lockstep so the
        // wash-completion handler can just promote the same plate to a clean slot.
        int slot = dirtyVisuals.Count;
        if (dirtyPlacementPoints == null
            || slot >= dirtyPlacementPoints.Length
            || dirtyPlacementPoints[slot] == null) return false;

        dirtyCount++;
        plate.OnPlaced(dirtyPlacementPoints[slot]); // reparents + snaps + re-enables colliders
        SetPlateDirty(plate, true);
        dirtyVisuals.Add(plate.gameObject);
        return true;
    }

    // Called by OvercookedCharacter when the player presses pickup with empty hands.
    // Hands the player the top-of-stack clean visual (no Instantiate). Falls back to a fresh
    // Instantiate only if the visual list got out of sync.
    public Pickupable TryTakeCleanPlate(Transform spawnAnchor)
    {
        if (!HasClean) return null;

        // Pop the most recent live clean visual (skip nulls in case one was externally destroyed).
        Pickupable plate = null;
        while (cleanVisuals.Count > 0)
        {
            int last = cleanVisuals.Count - 1;
            GameObject go = cleanVisuals[last];
            cleanVisuals.RemoveAt(last);
            if (go == null) continue;
            plate = go.GetComponent<Pickupable>();
            if (plate != null) break;
            Destroy(go);
        }

        if (plate == null)
        {
            // List was empty / corrupt — fall back to an Instantiate so we never deny a player
            // a plate when the counter says there should be one.
            if (platePrefab == null) return null;
            Transform a = spawnAnchor != null ? spawnAnchor : transform;
            plate = Instantiate(platePrefab, a.position, a.rotation);
        }

        cleanCount--;
        SetPlateDirty(plate, false);
        return plate;
    }

    // Called by OvercookedCharacter when a wash cycle completes (dirty → clean).
    // Just promotes the most recently deposited dirty plate into the next clean slot —
    // single reparent, no Instantiate, no Destroy.
    public bool ConsumeOneDirtyToClean()
    {
        if (!HasDirty) return false;
        if (IsFullClean) return false;

        // Pop the top dirty visual (skipping nulls in case one was externally destroyed).
        GameObject plateGo = null;
        Pickupable plate = null;
        while (dirtyVisuals.Count > 0)
        {
            int last = dirtyVisuals.Count - 1;
            plateGo = dirtyVisuals[last];
            dirtyVisuals.RemoveAt(last);
            if (plateGo == null) continue;
            plate = plateGo.GetComponent<Pickupable>();
            break;
        }

        if (plate == null)
        {
            // Visuals out of sync with counter — just decrement and abort the move.
            if (plateGo != null) Destroy(plateGo);
            dirtyCount--;
            return false;
        }

        dirtyCount--;
        cleanCount++;

        // Move the same plate into the next clean slot.
        int cleanSlot = cleanVisuals.Count;
        bool hasCleanSlot = cleanPlacementPoints != null
                            && cleanSlot < cleanPlacementPoints.Length
                            && cleanPlacementPoints[cleanSlot] != null;
        if (hasCleanSlot)
        {
            plate.OnPlaced(cleanPlacementPoints[cleanSlot]);
            cleanVisuals.Add(plateGo);
        }
        else
        {
            // No clean slot available — should not happen because CanStartWash gates on IsFullClean,
            // but guard anyway so we don't leave a hidden plate dangling under the dirty slot.
            Destroy(plateGo);
        }

        SetPlateDirty(plate, false); // resets scale and dirty visuals
        return true;
    }

    // Whether a wash cycle is currently possible (used to gate the player's action button).
    public bool CanStartWash() => HasDirty && !IsFullClean;

    // Persistent wash progress in seconds. Survives the player walking away and resumes
    // when any player starts washing again. Reset to 0 only on wash completion (or when
    // there's nothing left to wash).
    private float washProgressSeconds;
    public float WashProgress01 => washSeconds > 0f ? Mathf.Clamp01(washProgressSeconds / washSeconds) : 0f;

    public enum WashTickResult { NotReady, InProgress, Completed }

    // Called by the active OvercookedCharacter once per frame while washing.
    // Returns NotReady if no work to do (caller should stop washing), InProgress if mid-cycle,
    // Completed when one plate finished (sink already consumed dirty/added clean; caller may keep
    // washing to start the next plate).
    public WashTickResult AdvanceWash(float dt)
    {
        if (!CanStartWash())
        {
            // Nothing to wash — clear any stale partial progress so the indicator hides.
            if (washProgressSeconds != 0f) { washProgressSeconds = 0f; UpdateIndicator(); }
            return WashTickResult.NotReady;
        }

        washProgressSeconds += dt;
        if (washProgressSeconds >= washSeconds)
        {
            ConsumeOneDirtyToClean();
            washProgressSeconds = 0f; // start fresh for the next plate (or stay 0 if no more dirty)
            UpdateIndicator();
            return WashTickResult.Completed;
        }
        UpdateIndicator();
        return WashTickResult.InProgress;
    }

    private void UpdateIndicator()
    {
        if (progressIndicator == null) return;
        progressIndicator.SetProgress(WashProgress01);
    }

    private static bool IsPlateDirty(Pickupable plate)
    {
        // Plate dirtiness is tracked on the PlateSoup component (authoritative) and mirrored on Pickupable.plateState.
        PlateSoup ps = plate.GetComponentInChildren<PlateSoup>();
        if (ps != null) return ps.isDirty;
        return plate.plateState == PlateState.Dirty;
    }

    private void RefreshVisuals()
    {
        SyncVisualSlots(dirtyVisuals, dirtyPlacementPoints, dirtyCount, dirty: true);
        SyncVisualSlots(cleanVisuals, cleanPlacementPoints, cleanCount, dirty: false);
    }

    private void SyncVisualSlots(List<GameObject> spawned, Transform[] points, int target, bool dirty)
    {
        int slots = points != null ? points.Length : 0;
        // Clamp displayed count to available placement points (counters can technically exceed).
        int show = Mathf.Clamp(target, 0, slots);

        // Grow.
        while (spawned.Count < show)
        {
            int i = spawned.Count;
            if (platePrefab == null || points[i] == null) { spawned.Add(null); continue; }
            Pickupable v = Instantiate(platePrefab, points[i].position, points[i].rotation, points[i]);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            SetPlateDirty(v, dirty);
            spawned.Add(v.gameObject);
        }

        // Shrink (always destroy from the top so lower-indexed slots stay populated).
        while (spawned.Count > show)
        {
            int i = spawned.Count - 1;
            if (spawned[i] != null) Destroy(spawned[i]);
            spawned.RemoveAt(i);
        }
    }

    private void SetPlateDirty(Pickupable plate, bool dirty)
    {
        if (plate == null) return;
        plate.plateState = dirty ? PlateState.Dirty : PlateState.Clean;
        PlateSoup ps = plate.GetComponentInChildren<PlateSoup>();
        if (ps != null) ps.SetDirty(dirty);
        // Scale down while dirty, back to full when clean. Assumes the plate prefab's
        // natural scale is 1; tweak dirtyPlateScale if you want a different size.
        plate.transform.localScale = dirty ? Vector3.one * dirtyPlateScale : Vector3.one;
    }
}
