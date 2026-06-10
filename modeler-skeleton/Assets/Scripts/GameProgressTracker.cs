using UnityEngine;

public class GameProgressTracker : MonoBehaviour
{
    public static GameProgressTracker Instance { get; private set; }

    [Tooltip("The arena cylinder's ProgressIndicator component.")]
    public ProgressIndicator arenaCylinder;

    [Header("Auto Fill (for testing)")]
    [Tooltip("Fill the bar automatically over time instead of waiting for game events.")]
    public bool autoFill = false;
    [Tooltip("Seconds to go from 0% to 100%.")]
    public float autoFillDuration = 30f;

    private float totalWork;
    private float workDone;
    private float autoFillElapsed;

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
        if (arenaCylinder != null)
        {
            arenaCylinder.hideWhenIdle = false;
            arenaCylinder.SetProgress(0f);
        }
    }

    private void Update()
    {
        if (!autoFill || arenaCylinder == null) return;
        autoFillElapsed = Mathf.Min(autoFillElapsed + Time.deltaTime, autoFillDuration);
        arenaCylinder.SetProgress(autoFillElapsed / autoFillDuration);
    }

    public void RegisterWork(float amount)
    {
        totalWork += amount;
        Push();
    }

    public void CompleteWork(float amount)
    {
        workDone = Mathf.Min(workDone + amount, totalWork);
        Push();
    }

    private void Push()
    {
        if (arenaCylinder == null) return;
        float progress = totalWork > 0f ? workDone / totalWork : 0f;
        arenaCylinder.SetProgress(progress);
    }
}
