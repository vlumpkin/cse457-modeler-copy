using System;
using TMPro;
using UnityEngine;

public class RoundTimer : MonoBehaviour
{
    public static RoundTimer Instance { get; private set; }

    [Header("Round")]
    [Tooltip("Total round length in seconds.")]
    public float roundDuration = 180f;
    [Tooltip("Start counting down on Start(). Disable if another system starts the round.")]
    public bool autoStart = true;

    [Header("Display")]
    [Tooltip("3D TMP text that shows the timer (TextMeshPro, not UGUI).")]
    public TMP_Text timerLabel;
    [Tooltip("Optional: tint when this many seconds or fewer remain.")]
    public float lowTimeThreshold = 10f;
    public Color normalColor = Color.white;
    public Color lowTimeColor = new Color(1f, 0.25f, 0.25f);

    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action OnRoundEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        TimeRemaining = roundDuration;
    }

    private void Start()
    {
        Render();
        if (autoStart) StartRound();
    }

    public void StartRound()
    {
        TimeRemaining = roundDuration;
        IsRunning = true;
        Render();
    }

    public void StopRound()
    {
        IsRunning = false;
    }

    private void Update()
    {
        if (!IsRunning) return;
        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            IsRunning = false;
            Render();
            OnRoundEnded?.Invoke();
            return;
        }
        Render();
    }

    private void Render()
    {
        if (timerLabel == null) return;
        int total = Mathf.CeilToInt(TimeRemaining);
        int m = total / 60;
        int s = total % 60;
        timerLabel.text = $"{m}:{s:00}";
        timerLabel.color = TimeRemaining <= lowTimeThreshold ? lowTimeColor : normalColor;
    }
}
