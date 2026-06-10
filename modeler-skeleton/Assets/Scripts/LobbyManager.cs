using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Drop one in the lobby scene. Hook up:
//   - A PlayerInputManager (JoinBehavior = JoinPlayersWhenButtonIsPressed) with
//     playerPrefab pointing at your lobby player prefab (has LobbyPlayer + PlayerInput).
//   - spawnPoints: 1..4 transforms where joined players appear.
//   - gameSceneName: the gameplay scene to load when everyone has locked in.
public class LobbyManager : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInputManager inputManager;
    public Transform[] spawnPoints;

    [Header("Start conditions")]
    [Min(1)] public int minPlayersToStart = 1;
    [Tooltip("Seconds the lobby waits with everyone locked before loading the game scene.")]
    public float startDelay = 1.0f;
    public string gameSceneName = "temp3";

    [Header("Debug")]
    public bool verbose = true;
    [Tooltip("Editor-only: press this key to simulate a player join.")]
    public Key debugJoinKey = Key.F2;

    readonly List<LobbyPlayer> lobbyPlayers = new List<LobbyPlayer>();
    float allLockedTimer = -1f;
    bool launching = false;
    int lastPlayerInputCount = -1;

    void Awake()
    {
        if (inputManager == null) inputManager = FindAnyObjectByType<PlayerInputManager>();
    }

    void OnEnable()
    {
        if (inputManager == null) inputManager = FindAnyObjectByType<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.onPlayerJoined += HandleJoined;
            inputManager.onPlayerLeft += HandleLeft;
        }
        else
        {
            Debug.LogError("[LobbyManager] No PlayerInputManager found! Assign 'inputManager' in the inspector or add a PlayerInputManager component to a scene object.");
        }
        // Wipe any stale selections from a previous round.
        PlayerSelections.Clear();
    }

    void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.onPlayerJoined -= HandleJoined;
            inputManager.onPlayerLeft -= HandleLeft;
        }
    }

    void HandleJoined(PlayerInput pi)
    {
        var lp = pi.GetComponent<LobbyPlayer>();
        if (lp == null)
        {
            Debug.LogWarning($"[LobbyManager] Joined player has no LobbyPlayer component on {pi.name}. " +
                             "Make sure PlayerInputManager.playerPrefab is the lobby prefab, not the gameplay one.", pi);
            return;
        }
        int slot = lobbyPlayers.Count;
        lobbyPlayers.Add(lp);

        // Color is fixed by join order: 0=Blue, 1=Red, 2=Green, 3=Yellow
        // (whatever order LobbyPlayer.colors[] declares).
        lp.AssignColor(slot);

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform t = spawnPoints[slot % spawnPoints.Length];
            // Move the prefab ROOT, not whatever child PlayerInput happens to live on.
            Transform root = pi.transform.root;
            // CharacterController / Rigidbody cache their own pose and will overwrite
            // a transform write on the next physics step. Toggle them around the move.
            var cc = root.GetComponent<CharacterController>();
            var rb = root.GetComponent<Rigidbody>();
            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            if (rb != null) rb.position = t.position;
            root.SetPositionAndRotation(t.position, t.rotation);
            if (cc != null) cc.enabled = ccWas;
        }
        else
        {
            Debug.LogWarning($"[LobbyManager] No spawnPoints assigned — player {pi.playerIndex} stays at origin.");
        }

        if (verbose) Debug.Log($"[LobbyManager] Player {pi.playerIndex} joined → slot {slot} ({lobbyPlayers.Count} total)");
    }

    void HandleLeft(PlayerInput pi)
    {
        var lp = pi.GetComponent<LobbyPlayer>();
        if (lp != null) lobbyPlayers.Remove(lp);
        if (verbose) Debug.Log($"[LobbyManager] Player {pi.playerIndex} left ({lobbyPlayers.Count} total)");
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current[debugJoinKey].wasPressedThisFrame)
            DebugJoinFakePlayer();
#endif

        // Polling fallback: PIM's onPlayerJoined C# event can silently drop, so we
        // watch PlayerInput.all directly and adopt any joiners we haven't seen yet.
        int n = PlayerInput.all.Count;
        if (n != lastPlayerInputCount)
        {
            for (int i = 0; i < n; i++)
            {
                var pi = PlayerInput.all[i];
                var lp = pi.GetComponent<LobbyPlayer>();
                if (lp != null && !lobbyPlayers.Contains(lp)) HandleJoined(pi);
            }
            lastPlayerInputCount = n;
        }

        if (launching) return;

        int lockedCount = CountLocked();
        bool ready = lobbyPlayers.Count >= minPlayersToStart && AllLocked();

        if (ready)
        {
            // First frame where everyone is locked → log the transition and start the timer.
            if (allLockedTimer < 0f)
            {
                allLockedTimer = 0f;
                Debug.Log($"[LobbyManager] All {lobbyPlayers.Count} player(s) locked at t={Time.time:F3}s — starting countdown ({startDelay:F2}s)");
            }

            float prev = allLockedTimer;
            allLockedTimer += Time.deltaTime;

            // Log each whole-second tick: "3...", "2...", "1...".
            int prevSecLeft = Mathf.CeilToInt(startDelay - prev);
            int nowSecLeft = Mathf.CeilToInt(startDelay - allLockedTimer);
            if (nowSecLeft != prevSecLeft && nowSecLeft >= 0)
                Debug.Log($"[LobbyManager] Countdown tick: {nowSecLeft} (elapsed {allLockedTimer:F3}s / {startDelay:F2}s) at t={Time.time:F3}s");

            if (allLockedTimer >= startDelay)
            {
                Debug.Log($"[LobbyManager] Countdown complete at t={Time.time:F3}s (elapsed {allLockedTimer:F3}s) — calling Launch()");
                Launch();
            }
        }
        else
        {
            // Was counting, now we're not — log why we cancelled.
            if (allLockedTimer >= 0f)
            {
                string reason = lobbyPlayers.Count < minPlayersToStart
                    ? $"player count dropped to {lobbyPlayers.Count} (< {minPlayersToStart})"
                    : $"only {lockedCount}/{lobbyPlayers.Count} locked";
                Debug.Log($"[LobbyManager] Countdown cancelled at t={Time.time:F3}s after {allLockedTimer:F3}s — {reason}");
            }
            allLockedTimer = -1f;
        }
    }


    int CountLocked()
    {
        int c = 0;
        for (int i = 0; i < lobbyPlayers.Count; i++)
            if (lobbyPlayers[i] != null && lobbyPlayers[i].IsLocked) c++;
        return c;
    }

#if UNITY_EDITOR
    // Spawns a player prefab with no paired device. Useful for previewing
    // multi-player layouts without extra controllers.
    void DebugJoinFakePlayer()
    {
        if (inputManager == null || inputManager.playerPrefab == null) return;
        var go = Instantiate(inputManager.playerPrefab);
        go.name = inputManager.playerPrefab.name + "(FakeClone)";
        // PlayerInput on the prefab will register itself in PlayerInput.all on enable,
        // which the polling fallback above picks up and routes through HandleJoined.
        if (verbose) Debug.Log($"[LobbyManager] Spawned fake player '{go.name}' via {debugJoinKey}");
    }
#endif

    public LobbyPlayer GetPlayerInSlot(int slot)
        => (slot >= 0 && slot < lobbyPlayers.Count) ? lobbyPlayers[slot] : null;

    // True once every joined player is locked in (and the countdown is running).
    public bool CountdownActive => allLockedTimer >= 0f && !launching;

    // Seconds remaining before the gameplay scene loads. -1 if countdown not running.
    public float CountdownRemaining =>
        allLockedTimer < 0f ? -1f : Mathf.Max(0f, startDelay - allLockedTimer);

    bool AllLocked()
    {
        if (lobbyPlayers.Count == 0) return false;
        for (int i = 0; i < lobbyPlayers.Count; i++)
            if (lobbyPlayers[i] == null || !lobbyPlayers[i].IsLocked) return false;
        return true;
    }

    void Launch()
    {
        launching = true;
        PlayerSelections.Clear();
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            var lp = lobbyPlayers[i];
            if (lp == null) continue;
            var pi = lp.GetComponent<PlayerInput>();
            int[] deviceIds = null;
            string scheme = null;
            if (pi != null)
            {
                var devs = pi.devices;
                deviceIds = new int[devs.Count];
                for (int d = 0; d < devs.Count; d++) deviceIds[d] = devs[d].deviceId;
                scheme = pi.currentControlScheme;
            }
            PlayerSelections.Set(lp.PlayerIndex, lp.ColorIndex, lp.AnimalIndex, deviceIds, scheme);
        }

        // Stop further joins; gameplay scene takes over input wiring.
        if (inputManager != null) inputManager.DisableJoining();

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[LobbyManager] gameSceneName not set — cannot transition.");
            return;
        }
        if (verbose) Debug.Log($"[LobbyManager] Launching '{gameSceneName}' with {PlayerSelections.Count} player(s) at t={Time.time:F3}s");
        SceneManager.LoadScene(gameSceneName);
    }
}
