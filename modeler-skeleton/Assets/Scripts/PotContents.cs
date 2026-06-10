using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Pickupable))]
public class PotContents : MonoBehaviour
{
    public const int MaxVegetables = 3;
    public const float SecondsPerVegetable = 10f;
    public const float OvercookSecondsUntilFire = 10f;

    [Serializable]
    public struct VegetableVisual
    {
        public VegetableType type;
        [Tooltip("Child GameObject shown while at least one vegetable of this type is in the pot.")]
        public GameObject visual;
    }

    [Header("Per-type visuals (toggled while that vegetable is present)")]
    public List<VegetableVisual> visuals = new List<VegetableVisual>();

    [Header("Vegetable sprites (assigned to slots as added)")]
    public Sprite carrotSprite;
    public Sprite onionSprite;
    public Sprite tomatoSprite;

    [Tooltip("Three sprite slots over the pot, ordered left, middle, right. Fill left to right as vegetables are added.")]
    public SpriteRenderer[] vegetableSlots = new SpriteRenderer[MaxVegetables];

    [Tooltip("Child shown instead of the per-type visuals when the pot is burned.")]
    public GameObject burnedVisual;

    [Tooltip("Optional 3D progress meter on the pot. Driven by cookSeconds / TotalCookTime.")]
    public ProgressIndicator progressIndicator;

    [Header("State (read-only-ish)")]
    public int vegCount;
    public float cookSeconds;
    public float overcookSeconds;
    public bool burned;

    // Per-type counts, indexed by (int)VegetableType.
    private readonly Dictionary<VegetableType, int> perType = new Dictionary<VegetableType, int>();

    // Vegetables in the order they were added, used to fill slots left to right.
    private readonly List<VegetableType> order = new List<VegetableType>();

    public float TotalCookTime => vegCount > 0 ? SecondsPerVegetable : 0f;
    public bool IsFull => vegCount >= MaxVegetables;
    public bool IsFullyCooked => vegCount == MaxVegetables && cookSeconds >= TotalCookTime;

    // True only when fully cooked, not on fire, and all 3 vegetables are the same type.
    public bool TryGetSoupType(out VegetableType type)
    {
        type = default;
        if (!IsFullyCooked || burned) return false;
        foreach (var kv in perType)
        {
            if (kv.Value == MaxVegetables) { type = kv.Key; return true; }
        }
        return false;
    }

    private void Awake()
    {
        RefreshVisuals();
    }

    public bool TryAddVegetable(VegetableType type)
    {
        if (IsFull || burned) return false;
        vegCount++;
        perType.TryGetValue(type, out int n);
        perType[type] = n + 1;
        order.Add(type);
        if (vegCount > 1) cookSeconds *= 0.5f;
        overcookSeconds = 0f;
        RefreshVisuals();
        UpdateIndicator();
        return true;
    }

    public void Empty()
    {
        vegCount = 0;
        cookSeconds = 0f;
        overcookSeconds = 0f;
        burned = false;
        perType.Clear();
        order.Clear();
        RefreshVisuals();
        UpdateIndicator();
    }

    public int CountOf(VegetableType type)
    {
        perType.TryGetValue(type, out int n);
        return n;
    }

    public void Tick(float dt)
    {
        if (vegCount == 0 || burned) { UpdateIndicator(); return; }

        float total = TotalCookTime;
        if (cookSeconds < total)
        {
            cookSeconds = Mathf.Min(cookSeconds + dt, total);
            UpdateIndicator();
            return;
        }

        overcookSeconds += dt;
        if (overcookSeconds >= OvercookSecondsUntilFire && !burned)
        {
            burned = true;
            RefreshVisuals();
        }
        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        if (progressIndicator == null) return;
        if (vegCount == 0 || burned) { progressIndicator.SetProgress(0f); return; }
        float total = TotalCookTime;
        float t = total > 0f ? cookSeconds / total : 0f;
        progressIndicator.SetProgress(t);
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            var v = visuals[i];
            if (v.visual == null) continue;
            v.visual.SetActive(!burned && CountOf(v.type) > 0);
        }
        if (burnedVisual != null) burnedVisual.SetActive(burned);
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (vegetableSlots == null) return;
        for (int i = 0; i < vegetableSlots.Length; i++)
        {
            var slot = vegetableSlots[i];
            if (slot == null) continue;
            if (burned || i >= order.Count)
            {
                slot.enabled = false;
                continue;
            }
            Sprite sprite = SpriteFor(order[i]);
            slot.sprite = sprite;
            slot.enabled = sprite != null;
        }
    }

    private Sprite SpriteFor(VegetableType type)
    {
        switch (type)
        {
            case VegetableType.Carrot: return carrotSprite;
            case VegetableType.Onion:  return onionSprite;
            case VegetableType.Tomato: return tomatoSprite;
            default: return null;
        }
    }
}
