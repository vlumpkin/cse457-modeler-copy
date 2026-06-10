using UnityEngine;
using UnityEngine.InputSystem;

// Drop one of these in the gameplay scene (e.g. temp3). On Start it reads
// PlayerSelections (populated by the lobby) and tells the local PlayerInputManager
// to instantiate one player per saved entry, re-pairing the original devices.
//
// If PlayerSelections is empty (scene was entered directly, not via the lobby),
// this does nothing and the PIM keeps its normal press-to-join behavior.
public class GameSceneSpawner : MonoBehaviour
{
    [Tooltip("Optional. If left null, FindAnyObjectByType<PlayerInputManager>() is used.")]
    public PlayerInputManager inputManager;

    [Tooltip("Optional. If set, spawned players are repositioned to these transforms in order. " +
             "Used by both the lobby auto-spawn flow AND direct press-to-join when entering the scene without a lobby.")]
    public Transform[] spawnPoints;

    [Tooltip("Fallback spawn position used when spawnPoints is empty AND no lobby selection was loaded " +
             "(direct scene entry + press-to-join). Set to e.g. (5, 0, 5) to keep players off the origin.")]
    public Vector3 fallbackSpawnPosition = new Vector3(5f, 0f, 5f);

    [Tooltip("Disable further joins on the PIM after auto-spawning, so a stray controller can't add a 5th player mid-game.")]
    public bool disableJoiningAfterSpawn = true;

    public bool verbose = true;

    // Index of the next spawnPoint to assign, advanced by both flows.
    private int nextSpawnIndex;
    private bool subscribedToJoin;

    void Start()
    {
        if (inputManager == null) inputManager = FindAnyObjectByType<PlayerInputManager>();
        if (inputManager == null)
        {
            Debug.LogError("[GameSceneSpawner] No PlayerInputManager in scene — cannot position players.");
            return;
        }

        // Hook manual press-to-join too. Used in both modes — if PlayerSelections
        // is empty we rely entirely on this; if it's not, the auto-spawn loop
        // below repositions directly and the callback is a no-op for those
        // because nextSpawnIndex stays in sync.
        if (!subscribedToJoin)
        {
            inputManager.onPlayerJoined += HandlePlayerJoined;
            subscribedToJoin = true;
        }

        if (PlayerSelections.Count == 0)
        {
            if (verbose) Debug.Log("[GameSceneSpawner] No PlayerSelections — leaving PIM in manual-join mode; will reposition joiners.");
            return;
        }

        if (inputManager.playerPrefab == null)
        {
            Debug.LogError("[GameSceneSpawner] PlayerInputManager.playerPrefab is null — cannot auto-spawn players.");
            return;
        }

        // Make sure joining is on for the duration of the auto-spawn — DisableJoining()
        // from the lobby persists on the singleton if it isn't reset here.
        inputManager.EnableJoining();

        int spawned = 0;
        foreach (var e in PlayerSelections.All)
        {
            var devices = ResolveDevices(e.deviceIds);
            PlayerInput pi = null;
            try
            {
                if (devices != null && devices.Length > 0)
                {
                    pi = PlayerInput.Instantiate(
                        inputManager.playerPrefab,
                        controlScheme: string.IsNullOrEmpty(e.controlScheme) ? null : e.controlScheme,
                        pairWithDevices: devices);
                }
                else
                {
                    // No saved device (shouldn't happen via lobby) — let PIM auto-pair from unpaired devices.
                    pi = PlayerInput.Instantiate(inputManager.playerPrefab);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameSceneSpawner] Failed to instantiate player {e.playerIndex}: {ex.Message}");
                continue;
            }

            if (pi == null) continue;

            // PIM's onPlayerJoined is not guaranteed to fire during PlayerInput.Instantiate
            // for explicit/auto-spawn (unlike manual press-to-join). Reposition inline so
            // auto-spawned players always land on their spawn point.
            RepositionPlayer(pi);
            ApplyVisual(pi, e.animalIndex, e.colorIndex);

            if (verbose)
                Debug.Log($"[GameSceneSpawner] Spawned player {e.playerIndex} (color {e.colorIndex}, animal {e.animalIndex}) with {(devices != null ? devices.Length : 0)} device(s).");
            spawned++;
        }

        if (disableJoiningAfterSpawn) inputManager.DisableJoining();
        if (verbose) Debug.Log($"[GameSceneSpawner] Auto-spawn complete — {spawned} player(s).");
    }

    private void OnDisable()
    {
        if (subscribedToJoin && inputManager != null)
        {
            inputManager.onPlayerJoined -= HandlePlayerJoined;
            subscribedToJoin = false;
        }
    }

    private void HandlePlayerJoined(PlayerInput pi)
    {
        // Manual press-to-join path. Auto-spawn calls RepositionPlayer directly.
        RepositionPlayer(pi);
    }

    private void RepositionPlayer(PlayerInput pi)
    {
        if (pi == null) return;
        Vector3 pos;
        Quaternion rot;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform t = spawnPoints[nextSpawnIndex % spawnPoints.Length];
            pos = t.position;
            rot = t.rotation;
        }
        else
        {
            pos = fallbackSpawnPosition;
            rot = Quaternion.identity;
        }
        nextSpawnIndex++;

        // Teleporting a root with a CharacterController or kinematic Rigidbody
        // requires temporarily disabling the controller so the move sticks. The
        // CharacterController on the player is sometimes added at runtime in
        // OvercookedCharacter.Start, so it can be absent here at PlayerInput.Instantiate
        // time — that's fine; SetPositionAndRotation alone is enough in that case.
        Transform root = pi.transform.root;
        CharacterController cc = root.GetComponent<CharacterController>();
        Rigidbody rb = root.GetComponent<Rigidbody>();
        bool ccWas = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        if (rb != null) rb.position = pos;
        root.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = ccWas;

        if (verbose) Debug.Log($"[GameSceneSpawner] Repositioned player {pi.playerIndex} to {pos}.");
    }

    static void ApplyVisual(PlayerInput pi, int animalIndex, int colorIndex)
    {
        if (pi == null) return;
        var visual = pi.GetComponentInChildren<PlayerCharacterVisual>(true);
        if (visual == null)
        {
            Debug.LogWarning($"[GameSceneSpawner] Player {pi.playerIndex} has no PlayerCharacterVisual — animal/color not applied.", pi);
            return;
        }
        visual.Apply(animalIndex, colorIndex);
    }

    static InputDevice[] ResolveDevices(int[] ids)
    {
        if (ids == null || ids.Length == 0) return null;
        var list = new System.Collections.Generic.List<InputDevice>(ids.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            var dev = InputSystem.GetDeviceById(ids[i]);
            if (dev != null) list.Add(dev);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }
}
