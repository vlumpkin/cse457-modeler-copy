using UnityEngine;
using UnityEngine.InputSystem;

// One spawned per joined player. LobbyManager calls AssignColor() at spawn to
// pin the player to a color (0=Blue, 1=Red, 2=Green, 3=Yellow). The player
// cycles through the animal previews (0=Cat, 1=Chicken, 2=Dog, 3=Frog) and
// locks in. The lobby itself shows ONLY animals — no hands, no color preview.
// The (color, animal) pair is written to PlayerSelections at lock; the gameplay
// scene's GameSceneSpawner reads it and applies it to the Character prefab.
[RequireComponent(typeof(PlayerInput))]
public class LobbyPlayer : MonoBehaviour
{
    [Header("Animal previews")]
    [Tooltip("One Transform per animal, in cycle order: 0=Cat, 1=Chicken, 2=Dog, 3=Frog. " +
             "Each is a child of this prefab; we SetActive the chosen one.")]
    public Transform[] animals = new Transform[4];

    [Header("Input")]
    [Range(0.1f, 0.9f)] public float cycleDeadzone = 0.5f;

    [Header("State (read-only)")]
    [SerializeField] int colorIndex = 0;
    [SerializeField] int animalIndex = 0;
    [SerializeField] bool locked = false;

    PlayerInput playerInput;
    float lastCycleX;

    public int PlayerIndex => playerInput != null ? playerInput.playerIndex : -1;
    public int ColorIndex => colorIndex;
    public int AnimalIndex => animalIndex;
    public bool IsLocked => locked;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        DeactivateAll();
    }

    public void AssignColor(int color)
    {
        colorIndex = Mathf.Max(0, color);
        animalIndex = 0;
        locked = false;
        ShowCurrent();
    }

    public void OnMove(InputValue value)
    {
        Vector2 v = value.Get<Vector2>();
        float x = v.x;
        if (!locked && Mathf.Abs(lastCycleX) < cycleDeadzone)
        {
            if (x >= cycleDeadzone) Cycle(+1);
            else if (x <= -cycleDeadzone) Cycle(-1);
        }
        lastCycleX = x;
    }

    public void OnTurn(InputValue _) { }

    public void OnPickup(InputValue value)
    {
        if (!value.isPressed) return;
        if (!locked) Lock();
    }

    public void OnAction(InputValue value)
    {
        if (!value.isPressed) return;
        if (locked) Unlock();
    }

    void Cycle(int dir)
    {
        int n = animals != null ? animals.Length : 0;
        if (n <= 0) return;
        animalIndex = ((animalIndex + dir) % n + n) % n;
        ShowCurrent();
    }

    void Lock()
    {
        locked = true;
        PlayerSelections.Set(PlayerIndex, colorIndex, animalIndex);
        Debug.Log($"[LobbyPlayer] P{PlayerIndex} LOCKED (color {colorIndex}, animal {animalIndex}) at t={Time.time:F3}s");
    }

    void Unlock()
    {
        locked = false;
        Debug.Log($"[LobbyPlayer] P{PlayerIndex} UNLOCKED at t={Time.time:F3}s");
    }

    void DeactivateAll()
    {
        if (animals == null) return;
        for (int i = 0; i < animals.Length; i++)
            if (animals[i] != null) animals[i].gameObject.SetActive(false);
    }

    void ShowCurrent()
    {
        DeactivateAll();
        if (animals == null || animalIndex < 0 || animalIndex >= animals.Length) return;
        var t = animals[animalIndex];
        if (t != null) t.gameObject.SetActive(true);
    }
}
