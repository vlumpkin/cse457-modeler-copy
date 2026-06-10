using System;
using TMPro;
using UnityEngine;

// Tracks the team's score for a single session. Modeled loosely on Overcooked:
// each delivery awards a base value plus a speed-based tip, consecutive
// on-time deliveries grow a combo bonus, and missed (expired) orders apply
// a flat penalty that also breaks the combo.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Scoring")]
    [Tooltip("Base points awarded for any successful delivery.")]
    public int basePoints = 20;
    [Tooltip("Maximum tip awarded for an instant delivery (decays to 0 as the order timer runs out).")]
    public int maxTipPoints = 20;
    [Tooltip("Flat points added on top of the tip for each consecutive on-time delivery. Resets on a miss.")]
    public int comboBonusPerStep = 5;
    [Tooltip("Points removed when an order expires un-delivered.")]
    public int missedPenalty = 5;
    [Tooltip("If true, team score is clamped at 0 and cannot go negative.")]
    public bool clampAtZero = false;

    [Header("Display")]
    [Tooltip("3D TMP text that shows the team score (TextMeshPro, not UGUI).")]
    public TMP_Text scoreLabel;
    [Tooltip("Optional prefix, e.g. \"$ \" or \"SCORE: \". Leave blank for just digits.")]
    public string prefix = "";
    [Tooltip("Material applied when score is exactly zero.")]
    public Material zeroMaterial;
    [Tooltip("Material applied when score is positive.")]
    public Material positiveMaterial;
    [Tooltip("Material applied when score is negative.")]
    public Material negativeMaterial;

    public int TeamScore { get; private set; }
    public int Combo { get; private set; }
    public int DeliveriesCompleted { get; private set; }
    public int OrdersMissed { get; private set; }

    // Fires whenever TeamScore changes. UI can subscribe.
    public event Action<int> OnScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Render(TeamScore);
    }

    // Call when a plated soup is delivered to a DeliveryCounter and an order
    // was actually fulfilled. timeRemainingFraction in [0,1]: 1 = just spawned,
    // 0 = about to expire.
    public int RegisterDelivery(float timeRemainingFraction)
    {
        timeRemainingFraction = Mathf.Clamp01(timeRemainingFraction);
        int tip = Mathf.RoundToInt(maxTipPoints * timeRemainingFraction);
        int comboBonus = Combo * comboBonusPerStep;
        int total = basePoints + tip + comboBonus;

        Combo++;
        DeliveriesCompleted++;
        ApplyDelta(total);
        return total;
    }

    // Call when an order's timer hits zero before it was fulfilled.
    public void RegisterMiss()
    {
        Combo = 0;
        OrdersMissed++;
        ApplyDelta(-missedPenalty);
    }

    // Clear all session state. Call from whatever begins a new game.
    public void ResetSession()
    {
        TeamScore = 0;
        Combo = 0;
        DeliveriesCompleted = 0;
        OrdersMissed = 0;
        OnScoreChanged?.Invoke(TeamScore);
        Render(TeamScore);
    }

    private void ApplyDelta(int delta)
    {
        TeamScore += delta;
        if (clampAtZero && TeamScore < 0) TeamScore = 0;
        OnScoreChanged?.Invoke(TeamScore);
        Render(TeamScore);
    }

    private void Render(int score)
    {
        if (scoreLabel == null) return;
        scoreLabel.text = $"{prefix}{score}";

        Material mat = score == 0 ? zeroMaterial
                     : score > 0 ? positiveMaterial
                     : negativeMaterial;
        if (mat != null) scoreLabel.fontSharedMaterial = mat;
    }
}
