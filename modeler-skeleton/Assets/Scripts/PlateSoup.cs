using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Pickupable))]
public class PlateSoup : MonoBehaviour
{
    [Serializable]
    public struct SoupVisual
    {
        public VegetableType type;
        [Tooltip("Child GameObject shown when the plate holds soup of this vegetable type.")]
        public GameObject visual;
    }

    [Header("Per-type soup visuals (toggled when plate holds that soup)")]
    public List<SoupVisual> visuals = new List<SoupVisual>();

    [Header("Dirty state")]
    [Tooltip("Child GameObject shown when the plate is dirty. While dirty the plate cannot hold soup.")]
    public GameObject dirtyVisual;

    [Tooltip("Whether the plate starts dirty.")]
    public bool isDirty;

    private Pickupable plate;

    private void Awake()
    {
        plate = GetComponent<Pickupable>();
        RefreshVisuals();
    }

    public bool TrySetSoup(VegetableType type)
    {
        if (isDirty) return false;
        plate.plateContents = PlateContents.Soup;
        plate.soupType = type;
        RefreshVisuals();
        return true;
    }

    // Kept for compatibility with existing callers; no-op when dirty.
    public void SetSoup(VegetableType type) => TrySetSoup(type);

    public void Clear()
    {
        plate.plateContents = PlateContents.Empty;
        RefreshVisuals();
    }

    public void SetDirty(bool dirty)
    {
        isDirty = dirty;
        if (dirty) plate.plateContents = PlateContents.Empty;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        bool hasSoup = !isDirty && plate.plateContents == PlateContents.Soup;
        for (int i = 0; i < visuals.Count; i++)
        {
            var v = visuals[i];
            if (v.visual == null) continue;
            v.visual.SetActive(hasSoup && v.type == plate.soupType);
        }
        if (dirtyVisual != null) dirtyVisual.SetActive(isDirty);
    }
}
