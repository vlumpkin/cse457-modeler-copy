using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class OvercookedCharacter : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7.5f;
    public float turnSpeed = 720f;
    public float gravity = 20f;
    public float worldFloorY = 0f;
    [Tooltip("Tank-style controls: forward/back moves along facing, left/right turns in place.")]
    public bool tankControls = true;

    [Header("CharacterController (only used if one isn't already attached)")]
    public float controllerHeight = 2f;
    public float controllerRadius = 0.75f;
    public Vector3 controllerCenter = new Vector3(0f, 1f, 0f);

    [Header("Sway (Z-axis roll, degrees)")]
    public float swayAmplitude = 0.75f;
    public float swayFrequency = 14f;

    [Header("Arm swing (empty hands, X-axis degrees)")]
    public float leftArmMin = -20f;
    public float leftArmMax = 13f;
    public float rightArmMin = -13f;
    public float rightArmMax = 20f;
    public float armSwingFrequency = 14f;

    [Header("Carry pose (arm container local position + Z roll)")]
    public Vector3 leftCarryPos = new Vector3(-1f, 3f, 0.8f);
    public Vector3 rightCarryPos = new Vector3(1f, 3f, 0.8f);
    public Vector3 leftCarryEuler = new Vector3(0f, 0f, 23f);
    public Vector3 rightCarryEuler = new Vector3(0f, 0f, -23f);

    [Header("Hierarchy paths (relative to this Frame)")]
    [Tooltip("Optional sub-container holding all the character's visual children (arms, body mesh, etc). " +
             "When set, sway and wash lean are applied to this transform instead of the root, so the camera " +
             "(which follows the root) doesn't see those rotations. Leave null to apply rotations to the root.")]
    public Transform bodyVisualRoot;
    public string leftArmContainerName = "left_arm_container";
    public string rightArmContainerName = "right_arm_container";
    [Tooltip("Replacement hand transform used while carrying. Right arm is hidden; held item parents here. Auto-resolved by name if left unset.")]
    public Transform rightHoldingHand;
    public string rightHoldingHandName = "right_holding_hand";

    [Header("Right holding hand gyration (while moving + holding)")]
    [Tooltip("Seconds for a full left-right-left cycle.")]
    public float holdingHandPeriod = 0.6f;
    [Tooltip("Max horizontal (local X) offset from rest, in either direction.")]
    public float holdingHandXOffset = 0.25f;
    [Tooltip("Vertical (local Y) lift at the sides; the middle dips back to rest (valley).")]
    public float holdingHandYOffset = 0.1f;

    [Header("Input")]
    [Tooltip("Extra radial deadzone applied on top of the Input System's built-in stick deadzone. 0 = trust the asset's deadzone only.")]
    [Range(0f, 0.5f)] public float stickDeadzone = 0.1f;
    [Tooltip("Log pickup/action events and device pair/unpair changes for this character.")]
    public bool debugInput = false;

    [Header("Interaction")]
    [Tooltip("Where held items parent to. Auto-created in front of torso if null.")]
    public Transform holdAnchor;
    public Vector3 holdAnchorLocalPos = new Vector3(0f, 3.4f, 1.1f);
    [Tooltip("Extra anchor for the 2nd dirty plate when carrying a reclaim stack. Stacks on top of the first plate.")]
    public Transform dirtyPlateAnchor2;
    [Tooltip("Extra anchor for the 3rd dirty plate when carrying a reclaim stack. Stacks on top of the second plate.")]
    public Transform dirtyPlateAnchor3;
    [Tooltip("Optional separate anchor (e.g. PlacementAnchor child) whose local position CarryPose can override per item.")]
    public Transform placementAnchor;
    public float interactRange = 2.5f;
    public float interactHeight = 1.5f;
    public LayerMask interactMask = ~0;
    public bool drawInteractGizmo = true;
    [Tooltip("Dot product threshold between forward and direction-to-station. 1=directly ahead, 0=90°, -1=behind. ~0.3 is roughly a 70° cone.")]
    [Range(-1f, 1f)] public float facingDotThreshold = 0.3f;

    [Header("Facing highlight")]
    public bool showFacingHighlight = true;
    public Color highlightColor = new Color(0.3f, 1f, 0.6f, 0.35f);
    [Tooltip("Vertical offset above the station's top surface (avoids z-fighting).")]
    public float highlightYOffset = 0.01f;
    [Tooltip("Optional material. If left null, an unlit transparent material is generated at runtime.")]
    public Material highlightMaterial;

    public enum WashHand { Left, Right }

    [Header("Washing (Sink)")]
    [Tooltip("Which arm performs the wash motion.")]
    public WashHand washHand = WashHand.Right;
    [Tooltip("Amplitude along the plane's side axis (meters).")]
    public float washCircleSide = 0.25f;
    [Tooltip("Amplitude along the plane's forward axis (meters). Equal to washCircleSide gives a circle; unequal gives an ellipse.")]
    public float washCircleForward = 0.25f;
    [Tooltip("Number of full circles the hand traces during one wash cycle.")]
    public float washCircleRevolutions = 3f;
    [Tooltip("Euler rotation (degrees) that orients the circle plane in CHARACTER-ROOT space. " +
             "(0,0,0) = horizontal scrub (character XZ). Rotate around X to tilt the plane forward/back, " +
             "around Z to tilt side-to-side. Lift is applied along the plane's normal. " +
             "Because this is character-relative, the scrub stays oriented to the character's facing — " +
             "turning the character turns the plane with them.")]
    public Vector3 washPlaneEuler = Vector3.zero;
    [Tooltip("Constant offset along the plane's normal direction — lifts the hand above the plane (meters).")]
    public float washHandLift = 0f;
    [Tooltip("Absolute body-local Euler rotation of the washing arm container while washing. " +
             "Replaces the arm's rest rotation entirely — (30, 0, 0) means '30° pitch in body space,' " +
             "regardless of what the arm's rest rotation was.")]
    public Vector3 washArmEuler = Vector3.zero;
    [Tooltip("Body-local position the washing arm container sits at while washing. " +
             "REPLACES the arm's rest position as the base — the circular motion is added on top. " +
             "Leave at (0,0,0) to fall back to the arm's captured rest position.")]
    public Vector3 washArmLocalPos = Vector3.zero;
    [Tooltip("Additional Euler rotation applied to the character frame while washing — a forward lean over the sink. " +
             "Composes on top of the existing sway. Try (10, 0, 0) for a subtle hunch.")]
    public Vector3 washBodyLeanEuler = Vector3.zero;

    [Header("Cutting")]
    [Tooltip("Seconds between chops. Each X press triggers one chop animation lasting this long.")]
    public float cutCooldown = 0.25f;
    [Tooltip("Right-arm X-axis euler at the top of the chop (knife raised).")]
    public float chopMinX = -25f;
    [Tooltip("Right-arm X-axis euler at the bottom of the chop (knife down).")]
    public float chopMaxX = 60f;
    [Tooltip("Optional anchor inside the right arm where the knife is parented. Falls back to right_arm_container if null.")]
    public Transform rightHandAnchor;
    [Tooltip("Local position of the knife relative to the hand anchor.")]
    public Vector3 knifeGripLocalPos = Vector3.zero;
    [Tooltip("Local euler rotation of the knife relative to the hand anchor.")]
    public Vector3 knifeGripLocalEuler = Vector3.zero;

    [Header("Arm Offsets (additive, body-relative)")]
    [Tooltip("Additive offset applied to the right_arm_container's localPosition every frame, AFTER all other arm pose logic (walk swing, carry, chop, wash). Tweak live to nudge the arm without modifying rest poses.")]
    public Vector3 rightArmPosOffset = Vector3.zero;

    [Header("Debug ray")]
    public bool drawDebugRay = true;
    public Color rayHitColor = Color.green;
    public Color rayMissColor = Color.red;

    [Header("State (read-only)")]
    public Pickupable heldItem;
    [Tooltip("Stack of dirty plates being carried back from the reclaim station. " +
             "When non-empty, the player can only deposit them at a Sink.")]
    public Pickupable[] dirtyPlateStack = new Pickupable[3];

    private CharacterController controller;
    private Transform tiltTarget;          // bodyVisualRoot if set, else transform — gets sway + lean
    private Quaternion tiltTargetRestRot;  // rest rotation of tiltTarget at Start
    private Transform leftArm;
    private Transform rightArm;

    private Quaternion frameRestRot;
    private Quaternion leftArmRestRot;
    private Quaternion rightArmRestRot;
    private Vector3 leftArmRestPos;
    private Vector3 rightArmRestPos;
    private Vector3 rightHoldingHandRestPos;
    private bool hasRightHoldingHandRest;
    private float holdingHandPhase;
    private Vector3 placementAnchorRestPos;
    private bool hasPlacementAnchorRest;

    private Station facingStation;
    private GameObject highlightQuad;
    private Transform highlightTf;
    private MeshRenderer highlightRenderer;

    private bool hasKnife;
    private bool isChopping;
    private float chopTimer;
    private Station knifeStation;
    private Transform borrowedKnife;
    private Transform knifeOriginalParent;
    private Vector3 knifeOriginalLocalPos;
    private Quaternion knifeOriginalLocalRot;
    private KnifeGrip currentGrip;

    private bool isWashing;
    private Sink washSink;

    public bool HasKnife => hasKnife;
    public bool IsChopping => isChopping;
    public bool IsWashing => isWashing;

    private float swayPhase;
    private float swingPhase;
    private float verticalVelocity;

    private Vector2 moveInput;
    private Vector2 turnInput;
    private PlayerInput playerInput;

    public bool IsHolding => heldItem != null;
    public Pickupable Held => heldItem;

    // True while the player has at least one dirty plate in the reclaim carry
    // stack. While true, the only legal interaction (besides sink-deposit) is
    // walking — picking up / placing other items is blocked.
    public bool IsCarryingDirtyPlates
    {
        get
        {
            if (dirtyPlateStack == null) return false;
            for (int i = 0; i < dirtyPlateStack.Length; i++)
                if (dirtyPlateStack[i] != null) return true;
            return false;
        }
    }

    private int DirtyPlateCount
    {
        get
        {
            int n = 0;
            if (dirtyPlateStack == null) return 0;
            for (int i = 0; i < dirtyPlateStack.Length; i++)
                if (dirtyPlateStack[i] != null) n++;
            return n;
        }
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        playerInput.onDeviceLost += HandleDeviceLost;
        playerInput.onDeviceRegained += HandleDeviceRegained;
    }

    private void OnDisable()
    {
        if (playerInput == null) return;
        playerInput.onDeviceLost -= HandleDeviceLost;
        playerInput.onDeviceRegained -= HandleDeviceRegained;
    }

    private void HandleDeviceLost(PlayerInput pi)
    {
        moveInput = Vector2.zero;
        if (debugInput) Debug.Log($"[Overcooked][{name}] device lost (player {pi.playerIndex})");
    }

    private void HandleDeviceRegained(PlayerInput pi)
    {
        if (debugInput) Debug.Log($"[Overcooked][{name}] device regained (player {pi.playerIndex})");
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnTurn(InputValue value)
    {
        turnInput = value.Get<Vector2>();
    }

    public void OnPickup(InputValue value)
    {
        if (!value.isPressed) return;
        if (debugInput) Debug.Log($"[Overcooked][{name}] PICKUP fired");
        OnPickupPressed();
    }

    public void OnAction(InputValue value)
    {
        if (!value.isPressed) return;
        if (debugInput) Debug.Log($"[Overcooked][{name}] ACTION fired");
        OnActionPressed();
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = controllerHeight;
            controller.radius = controllerRadius;
            controller.center = controllerCenter;
            controller.skinWidth = 0.02f;
        }

        frameRestRot = transform.localRotation;

        // Tilt target = bodyVisualRoot when assigned, otherwise the root transform.
        // Sway and wash lean both write here so the camera (following root) doesn't see them.
        tiltTarget = bodyVisualRoot != null ? bodyVisualRoot : transform;
        tiltTargetRestRot = tiltTarget.localRotation;

        // Arm containers live under the visual root (or directly under the frame if not split out).
        Transform visualSearchRoot = bodyVisualRoot != null ? bodyVisualRoot : transform;
        // NOTE: Using Transform.Find (direct-child only) rather than FindDescendant
        // to preserve the pre-Sink-merge knife/arm visual behavior. The arm containers
        // are nested one level deeper in the Character prefab, so this resolves to
        // null and the arm-pose code paths stay dormant — which is the state the
        // prefab was visually tuned against. Swap back to FindDescendant once
        // leftCarryEuler / rightCarryEuler / rest values are re-tuned.
        leftArm = visualSearchRoot.Find(leftArmContainerName);
        rightArm = visualSearchRoot.Find(rightArmContainerName);

        if (leftArm == null) Debug.LogWarning($"OvercookedCharacter: '{leftArmContainerName}' not found under {name}");
        if (rightArm == null) Debug.LogWarning($"OvercookedCharacter: '{rightArmContainerName}' not found under {name}");

        if (leftArm != null)
        {
            leftArmRestRot = leftArm.localRotation;
            leftArmRestPos = leftArm.localPosition;
        }
        if (rightArm != null)
        {
            rightArmRestRot = rightArm.localRotation;
            rightArmRestPos = rightArm.localPosition;
        }

        if (placementAnchor != null)
        {
            placementAnchorRestPos = placementAnchor.localPosition;
            hasPlacementAnchorRest = true;
        }

        // Same revert rationale as the arm containers above — keep this as a
        // direct-child Find so the holding-hand anchor falls through to the
        // synthesized HoldAnchor branch and the knife sits where it used to.
        if (rightHoldingHand == null) rightHoldingHand = transform.Find(rightHoldingHandName);
        if (rightHoldingHand != null)
        {
            rightHoldingHandRestPos = rightHoldingHand.localPosition;
            hasRightHoldingHandRest = true;
        }

        if (holdAnchor == null)
        {
            if (rightHoldingHand != null)
            {
                holdAnchor = rightHoldingHand;
            }
            else
            {
                GameObject anchor = new GameObject("HoldAnchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = holdAnchorLocalPos;
                anchor.transform.localRotation = Quaternion.identity;
                holdAnchor = anchor.transform;
            }
        }

        if (debugInput)
        {
            string dev = playerInput != null && playerInput.devices.Count > 0
                ? playerInput.devices[0].displayName + " (" + playerInput.devices[0].deviceId + ")"
                : "<none>";
            Debug.Log($"[Overcooked][{name}] player {playerInput?.playerIndex} paired to {dev}");
        }
    }

    private void Update()
    {
        Vector3 move = ReadMoveInput();
        float turn = ReadTurnInput();
        bool usingGamepad = IsGamepadScheme();

        bool moving;
        if (usingGamepad)
            moving = move.sqrMagnitude > 0.0001f || Mathf.Abs(turn) > 0.0001f;
        else if (tankControls)
            moving = Mathf.Abs(move.x) > 0.0001f || Mathf.Abs(move.z) > 0.0001f;
        else
            moving = move.sqrMagnitude > 0.0001f;

        ApplyMovement(move, turn, usingGamepad);
        ApplyBodyTilt(moving);
        ApplyArmPose(moving);
        ApplyArmOffsets();

        UpdateFacing();
        UpdateKnifeEquip();
        TickChop();
        TickWash();
    }

    private void ApplyArmOffsets()
    {
        if (hasKnife && rightArm != null) rightArm.localPosition += rightArmPosOffset;
    }

    private Vector3 ReadMoveInput()
    {
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);

        // Extra radial deadzone on top of the InputAction's built-in stick deadzone.
        float mag = dir.magnitude;
        if (mag < stickDeadzone) return Vector3.zero;
        if (stickDeadzone > 0f)
        {
            float rescaled = (mag - stickDeadzone) / (1f - stickDeadzone);
            dir = dir / mag * Mathf.Clamp01(rescaled);
        }
        return dir;
    }

    private float ReadTurnInput()
    {
        float x = turnInput.x;
        float ax = Mathf.Abs(x);
        if (ax < stickDeadzone) return 0f;
        if (stickDeadzone > 0f)
        {
            float rescaled = (ax - stickDeadzone) / (1f - stickDeadzone);
            return Mathf.Sign(x) * Mathf.Clamp01(rescaled);
        }
        return x;
    }

    private bool IsGamepadScheme()
    {
        return playerInput != null && playerInput.currentControlScheme == "Gamepad";
    }

    private void ApplyMovement(Vector3 move, float turn, bool usingGamepad)
    {
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
        verticalVelocity -= gravity * Time.deltaTime;

        Vector3 horizontal;
        if (usingGamepad)
        {
            // Left stick = local-space motion (forward/strafe relative to facing), right stick X = turn.
            if (Mathf.Abs(turn) > 0.0001f)
            {
                transform.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f, Space.World);
            }
            horizontal = (transform.forward * move.z + transform.right * move.x) * moveSpeed;
        }
        else if (tankControls)
        {
            // move.x = turn (yaw), move.z = forward/back along current facing.
            float t = move.x;
            if (Mathf.Abs(t) > 0.0001f)
            {
                transform.Rotate(0f, t * turnSpeed * Time.deltaTime, 0f, Space.World);
            }
            horizontal = transform.forward * (move.z * moveSpeed);
        }
        else
        {
            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
            horizontal = move * moveSpeed;
        }

        Vector3 motion = (horizontal + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(motion);

        if (transform.position.y < worldFloorY)
        {
            Vector3 p = transform.position;
            p.y = worldFloorY;
            transform.position = p;
            verticalVelocity = 0f;
        }
    }

    private void ApplyBodyTilt(bool moving)
    {
        // Compute walk-sway roll.
        float roll;
        if (moving)
        {
            swayPhase += swayFrequency * Time.deltaTime;
            roll = Mathf.Sin(swayPhase) * swayAmplitude;
        }
        else
        {
            swayPhase = 0f;
            roll = 0f;
        }

        // Lean composition (full Euler, only while washing).
        Quaternion leanQ = isWashing ? Quaternion.Euler(washBodyLeanEuler) : Quaternion.identity;

        if (tiltTarget == transform)
        {
            // No sub-container: write back onto the root. Must preserve yaw set by ApplyMovement,
            // and re-base pitch each frame to prevent the lean from accumulating into eulerAngles.x.
            Vector3 e = transform.localEulerAngles;
            float basePitch = frameRestRot.eulerAngles.x;
            if (basePitch > 180f) basePitch -= 360f;
            Quaternion baseR = Quaternion.Euler(basePitch, e.y, roll);
            transform.localRotation = baseR * leanQ;
        }
        else
        {
            // Sub-container: it has no yaw responsibility, so start fresh from its rest pose each frame.
            // Camera (following root) never sees these rotations.
            Quaternion baseR = tiltTargetRestRot * Quaternion.Euler(0f, 0f, roll);
            tiltTarget.localRotation = baseR * leanQ;
        }
    }

    private void ApplyArmPose(bool moving)
    {
        if (hasKnife)
        {
            SetRightArmVisible(true);
            ApplyKnifePose();
            return;
        }

        // While holding: right arm hidden (held item rides on right_holding_hand), left arm keeps normal walk swing.
        // Carried dirty plates also ride on right_holding_hand (their anchors parent under it), so treat
        // carrying-a-reclaim-stack the same as holding — otherwise the hand is hidden and the plates vanish with it.
        bool holding = IsHolding || IsCarryingDirtyPlates;
        SetRightArmVisible(!holding);
        SetRightHoldingHandVisible(holding);
        ApplyHoldingHandGyration(holding, moving);

        if (moving)
        {
            swingPhase += armSwingFrequency * Time.deltaTime;
            float s = Mathf.Sin(swingPhase);

            float leftMid = (leftArmMin + leftArmMax) * 0.5f;
            float leftAmp = (leftArmMax - leftArmMin) * 0.5f;
            float leftAngle = leftMid + leftAmp * s;

            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRestPos;
                leftArm.localRotation = leftArmRestRot * Quaternion.Euler(leftAngle, 0f, 0f);
            }

            if (!holding && rightArm != null)
            {
                float rightMid = (rightArmMin + rightArmMax) * 0.5f;
                float rightAmp = (rightArmMax - rightArmMin) * 0.5f;
                float rightAngle = rightMid - rightAmp * s; // opposite phase
                rightArm.localPosition = rightArmRestPos;
                rightArm.localRotation = rightArmRestRot * Quaternion.Euler(rightAngle, 0f, 0f);
            }
        }
        else
        {
            swingPhase = 0f;
            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRestPos;
                leftArm.localRotation = leftArmRestRot;
            }
            if (!holding && rightArm != null)
            {
                rightArm.localPosition = rightArmRestPos;
                rightArm.localRotation = rightArmRestRot;
            }
        }

        // Washing override — circular motion on the chosen wash arm. Runs after the walk/rest
        // pass so it stomps whatever was set on that arm. Drives off the sink's persistent
        // progress so the circle resumes where it was when interrupted.
        if (isWashing && washSink != null)
        {
            Transform arm = (washHand == WashHand.Right) ? rightArm : leftArm;
            if (arm != null)
            {
                Vector3 restPos = (washHand == WashHand.Right) ? rightArmRestPos : leftArmRestPos;

                // washArmLocalPos overrides the rest position while washing (sentinel: Vector3.zero
                // = fall back to rest). The circle offset is added on top.
                Vector3 basePos = washArmLocalPos != Vector3.zero ? washArmLocalPos : restPos;

                // Build the circle's frame from washPlaneEuler interpreted in CHARACTER-ROOT space,
                // then convert to the arm parent's local space (which is bodyVisualRoot when split
                // out, else the root frame itself). Side/forward are the in-plane axes; up is the
                // plane normal that the constant lift rides along.
                //
                // Conversion path: root-local → world (via root transform) → arm.parent local
                // (via InverseTransformDirection). Works regardless of how arms are nested.
                Quaternion planeQ = Quaternion.Euler(washPlaneEuler);
                Transform armParent = arm.parent != null ? arm.parent : transform;
                Vector3 sideAxis = armParent.InverseTransformDirection(transform.TransformDirection(planeQ * Vector3.right));
                Vector3 fwdAxis = armParent.InverseTransformDirection(transform.TransformDirection(planeQ * Vector3.forward));
                Vector3 normalAxis = armParent.InverseTransformDirection(transform.TransformDirection(planeQ * Vector3.up));

                float t = washSink.WashProgress01;
                float angle = t * Mathf.PI * 2f * washCircleRevolutions;
                Vector3 offset = sideAxis * (Mathf.Cos(angle) * washCircleSide)
                                 + fwdAxis * (Mathf.Sin(angle) * washCircleForward)
                                 + normalAxis * washHandLift;

                arm.localPosition = basePos + offset;
                arm.localRotation = Quaternion.Euler(washArmEuler);
            }
        }
    }

    private void SetRightArmVisible(bool visible)
    {
        if (rightArm == null) return;
        if (rightArm.gameObject.activeSelf != visible) rightArm.gameObject.SetActive(visible);
    }

    private void ApplyHoldingHandGyration(bool holding, bool moving)
    {
        if (rightHoldingHand == null || !hasRightHoldingHandRest) return;

        if (!holding || !moving || holdingHandPeriod <= 0f)
        {
            holdingHandPhase = 0f;
            rightHoldingHand.localPosition = rightHoldingHandRestPos;
            return;
        }

        holdingHandPhase += (Mathf.PI * 2f / holdingHandPeriod) * Time.deltaTime;
        if (holdingHandPhase > Mathf.PI * 2f) holdingHandPhase -= Mathf.PI * 2f;

        // sin sweeps x from -offset → +offset; |sin| peaks at the sides and is 0 in the middle (valley).
        float s = Mathf.Sin(holdingHandPhase);
        float dx = holdingHandXOffset * s;
        float dy = holdingHandYOffset * Mathf.Abs(s);

        rightHoldingHand.localPosition = rightHoldingHandRestPos + new Vector3(dx, dy, 0f);
    }

    private void SetRightHoldingHandVisible(bool visible)
    {
        if (rightHoldingHand == null) return;
        if (rightHoldingHand.gameObject.activeSelf != visible) rightHoldingHand.gameObject.SetActive(visible);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDescendant(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    private void OnPickupPressed()
    {
        if (isChopping) return;
        if (isWashing) StopWashing();
        if (hasKnife) UnequipKnife();

        Station station = FindFacingStation();
        if (station == null)
        {
            Debug.Log("[Overcooked] Pickup pressed — no Station in front of character");
            return;
        }

        // Sink has its own deposit/take semantics and must short-circuit the default
        // Station.TryPlace/TryTake fallthrough (plates are counters, never stored in station.current).
        if (station.kind == StationKind.Sink)
        {
            Sink sink = station.GetComponent<Sink>();
            if (sink == null)
            {
                Debug.LogWarning($"[Overcooked] {station.name} is StationKind.Sink but has no Sink component");
                return;
            }
            // If the player is carrying a dirty plate stack from the reclaim station,
            // batch-deposit as many as the sink will accept.
            if (IsCarryingDirtyPlates)
            {
                int deposited = DepositCarriedDirtyPlatesIntoSink(sink);
                Debug.Log($"[Overcooked] Deposited {deposited} reclaimed dirty plate(s) into {sink.name} " +
                          $"(remaining carried={DirtyPlateCount}, sink dirty={sink.dirtyCount}, clean={sink.cleanCount})");
                return;
            }
            HandleSinkPickup(sink);
            return;
        }

        // DirtyPlateReclaim: empty-handed pickup grabs up to 3 dirty plates.
        // Any other interaction here is a no-op.
        if (station.kind == StationKind.DirtyPlateReclaim)
        {
            DirtyPlateReclaim reclaim = station.GetComponent<DirtyPlateReclaim>();
            if (reclaim == null)
            {
                Debug.LogWarning($"[Overcooked] {station.name} is StationKind.DirtyPlateReclaim but has no DirtyPlateReclaim component");
                return;
            }
            if (IsHolding || IsCarryingDirtyPlates)
            {
                Debug.Log($"[Overcooked] {station.name}: hands aren't empty — can't reclaim plates");
                return;
            }
            int picked = ReclaimDirtyPlatesFrom(reclaim);
            Debug.Log($"[Overcooked] Reclaimed {picked} dirty plate(s) from {station.name} (remaining on station={reclaim.dirtyCount})");
            return;
        }

        // While carrying dirty plates, the ONLY legal target is the sink (handled above).
        // Block everything else so we don't try to TryPlace a null heldItem or pick something up.
        if (IsCarryingDirtyPlates)
        {
            Debug.Log($"[Overcooked] Carrying {DirtyPlateCount} dirty plate(s) — go to a Sink to drop them");
            return;
        }

        if (IsHolding)
        {
            if (TryTrash(station)) return;
            if (TryDeliver(station)) return;
            if (TryDepositHeldFoodIntoStationPot(station)) return;
            if (TryDepositStationFoodIntoHeldPot(station)) return;
            if (TryPlateFromStationPot(station)) return;
            if (TryPlateFromHeldPot(station)) return;

            if (station.TryPlace(heldItem))
            {
                Debug.Log($"[Overcooked] Placed {heldItem.kind} on {station.name}");
                heldItem = null;
            }
            else
            {
                Debug.Log($"[Overcooked] {station.name} already has an item, can't place");
            }
        }
        else
        {
            Pickupable taken = station.TryTake();
            if (taken != null)
            {
                heldItem = taken;
                heldItem.OnPickedUp(holdAnchor);
                Debug.Log($"[Overcooked] Picked up {taken.kind} from {station.name}");
            }
            else
            {
                Debug.Log($"[Overcooked] {station.name} is empty");
            }
        }
    }

    private void HandleSinkPickup(Sink sink)
    {
        if (IsHolding)
        {
            // Only dirty plates can be deposited.
            if (sink.TryDepositDirtyPlate(heldItem))
            {
                Debug.Log($"[Overcooked] Deposited dirty plate at {sink.name} (dirty={sink.dirtyCount}, clean={sink.cleanCount})");
                heldItem = null; // sink destroyed the GameObject
            }
            else
            {
                Debug.Log($"[Overcooked] {sink.name} rejected the held item (must be a dirty plate, sink must have room)");
            }
        }
        else
        {
            // Empty-handed: take a clean plate if any.
            Pickupable plate = sink.TryTakeCleanPlate(holdAnchor);
            if (plate != null)
            {
                heldItem = plate;
                heldItem.OnPickedUp(holdAnchor);
                Debug.Log($"[Overcooked] Took clean plate from {sink.name} (dirty={sink.dirtyCount}, clean={sink.cleanCount})");
            }
            else
            {
                Debug.Log($"[Overcooked] {sink.name} has no clean plates");
            }
        }
    }

    private bool TryDeliver(Station station)
    {
        if (station.kind != StationKind.DeliveryCounter) return false;
        if (heldItem.kind != PickupableKind.Plate) return false;
        if (heldItem.plateContents != PlateContents.Soup) return false;

        VegetableType delivered = heldItem.soupType;

        if (OrderQueue.Instance != null)
        {
            OrderQueue.DeliveryResult result = OrderQueue.Instance.TryFulfillOrder(delivered);
            if (result.fulfilled)
            {
                int total = ScoreManager.Instance != null ? ScoreManager.Instance.TeamScore : 0;
                Debug.Log($"[Overcooked] Delivered {delivered} soup — +{result.pointsAwarded} pts (team: {total})");
            }
            else
            {
                Debug.Log($"[Overcooked] No matching order for {delivered} soup");
            }
        }

        // Notify the configured DirtyPlateReclaim that a plate has been sent out.
        // After the reclaim's delay it'll spawn a dirty plate at one of its
        // placement points for the players to fetch.
        DeliveryCounter dc = station.GetComponent<DeliveryCounter>();
        if (dc != null && dc.reclaim != null) dc.reclaim.PlateProcessed();

        // The plate is gone — destroy it, do not equip back onto the player
        // and do not park it on the counter (the counter is purely a drop slot).
        Destroy(heldItem.gameObject);
        heldItem = null;
        return true;
    }

    // Pull up to dirtyPlateStack.Length plates off the reclaim station into the
    // player's plate-carry anchors. Returns the number actually taken.
    private int ReclaimDirtyPlatesFrom(DirtyPlateReclaim reclaim)
    {
        Transform[] anchors = new Transform[3]
        {
            holdAnchor,
            dirtyPlateAnchor2,
            dirtyPlateAnchor3,
        };

        // Clear the stack first so partial-fill leaves no stale refs.
        for (int i = 0; i < dirtyPlateStack.Length; i++) dirtyPlateStack[i] = null;

        return reclaim.TakePlates(anchors, dirtyPlateStack.Length, dirtyPlateStack);
    }

    // Deposit every carried dirty plate into the sink, stopping early if the
    // sink fills up. Returns the count actually deposited.
    private int DepositCarriedDirtyPlatesIntoSink(Sink sink)
    {
        int deposited = 0;
        for (int i = 0; i < dirtyPlateStack.Length; i++)
        {
            Pickupable p = dirtyPlateStack[i];
            if (p == null) continue;
            if (sink.TryDepositDirtyPlate(p))
            {
                dirtyPlateStack[i] = null;
                deposited++;
            }
            else
            {
                // Sink rejected (likely full) — leave the rest of the stack
                // with the player so they can try another sink or wait.
                break;
            }
        }
        return deposited;
    }

    private bool TryTrash(Station station)
    {
        if (station.kind != StationKind.Trashcan) return false;

        switch (heldItem.kind)
        {
            case PickupableKind.Plate:
                PlateSoup plateSoup = heldItem.GetComponentInChildren<PlateSoup>();
                if (plateSoup != null) plateSoup.Clear();
                else heldItem.plateContents = PlateContents.Empty;
                Debug.Log($"[Overcooked] Trashed plate contents at {station.name}");
                return true;

            case PickupableKind.Pot:
                PotContents pot = heldItem.GetComponentInChildren<PotContents>();
                if (pot != null) pot.Empty(); // Empty() already sets burned=false
                Debug.Log($"[Overcooked] Emptied pot at {station.name}");
                return true;

            case PickupableKind.Food:
                Debug.Log($"[Overcooked] Trashed {heldItem.vegetableType} at {station.name}");
                Destroy(heldItem.gameObject);
                heldItem = null;
                return true;
        }

        return false;
    }

    private bool TryDepositHeldFoodIntoStationPot(Station station)
    {
        if (station.current == null || station.current.kind != PickupableKind.Pot)
        { Debug.Log($"[DepositToStationPot] reject: station.current is not a Pot"); return false; }
        if (heldItem.kind != PickupableKind.Food)
        { Debug.Log($"[DepositToStationPot] reject: held kind is {heldItem.kind}, need Food"); return false; }
        if (heldItem.foodState != FoodState.Cut)
        { Debug.Log($"[DepositToStationPot] reject: held foodState is {heldItem.foodState}, need Cut"); return false; }

        PotContents pot = station.current.GetComponentInChildren<PotContents>();
        if (pot == null)
        { Debug.Log($"[DepositToStationPot] reject: {station.current.name} has no PotContents component"); return false; }
        if (!pot.TryAddVegetable(heldItem.vegetableType))
        { Debug.Log($"[DepositToStationPot] reject: TryAddVegetable failed (vegCount={pot.vegCount}, burned={pot.burned})"); return false; }

        Debug.Log($"[Overcooked] Deposited {heldItem.vegetableType} into pot on {station.name} ({pot.vegCount}/{PotContents.MaxVegetables})");
        Destroy(heldItem.gameObject);
        heldItem = null;
        return true;
    }

    private bool TryDepositStationFoodIntoHeldPot(Station station)
    {
        if (heldItem.kind != PickupableKind.Pot)
        { Debug.Log($"[DepositToHeldPot] reject: held kind is {heldItem.kind}, need Pot"); return false; }
        if (station.current == null || station.current.kind != PickupableKind.Food)
        { Debug.Log($"[DepositToHeldPot] reject: station.current is not Food"); return false; }
        if (station.current.foodState != FoodState.Cut)
        { Debug.Log($"[DepositToHeldPot] reject: station food state is {station.current.foodState}, need Cut"); return false; }

        PotContents pot = heldItem.GetComponentInChildren<PotContents>();
        if (pot == null)
        { Debug.Log($"[DepositToHeldPot] reject: held pot has no PotContents"); return false; }
        if (!pot.TryAddVegetable(station.current.vegetableType))
        { Debug.Log($"[DepositToHeldPot] reject: TryAddVegetable failed (vegCount={pot.vegCount}, burned={pot.burned})"); return false; }

        Debug.Log($"[Overcooked] Deposited {station.current.vegetableType} from {station.name} into held pot ({pot.vegCount}/{PotContents.MaxVegetables})");
        Pickupable food = station.TryTake();
        if (food != null) Destroy(food.gameObject);
        return true;
    }

    private bool TryPlateFromStationPot(Station station)
    {
        if (station.current == null || station.current.kind != PickupableKind.Pot)
        { Debug.Log($"[PlateFromStationPot] reject: station has no Pot"); return false; }
        if (heldItem.kind != PickupableKind.Plate)
        { Debug.Log($"[PlateFromStationPot] reject: held kind is {heldItem.kind}, need Plate"); return false; }
        if (heldItem.plateState != PlateState.Clean || heldItem.plateContents != PlateContents.Empty)
        { Debug.Log($"[PlateFromStationPot] reject: plate is {heldItem.plateState}/{heldItem.plateContents}"); return false; }

        PotContents pot = station.current.GetComponentInChildren<PotContents>();
        if (pot == null)
        { Debug.Log($"[PlateFromStationPot] reject: pot has no PotContents"); return false; }
        if (!pot.TryGetSoupType(out VegetableType soupType))
        { Debug.Log($"[PlateFromStationPot] reject: pot not single-type fully cooked (veg={pot.vegCount}, cook={pot.cookSeconds:F1}/{pot.TotalCookTime:F1}, fire={pot.burned})"); return false; }

        PlateSoup plateSoup = heldItem.GetComponentInChildren<PlateSoup>();
        if (plateSoup == null)
        { Debug.Log($"[PlateFromStationPot] reject: plate has no PlateSoup component"); return false; }
        if (plateSoup.isDirty)
        { Debug.Log($"[PlateFromStationPot] reject: held plate is dirty"); return false; }

        if (!plateSoup.TrySetSoup(soupType))
        { Debug.Log($"[PlateFromStationPot] reject: plate refused soup"); return false; }
        pot.Empty();
        Debug.Log($"[Overcooked] Plated {soupType} soup from {station.name}");
        return true;
    }

    private bool TryPlateFromHeldPot(Station station)
    {
        if (heldItem.kind != PickupableKind.Pot)
        { Debug.Log($"[PlateFromHeldPot] reject: held kind is {heldItem.kind}, need Pot"); return false; }
        if (station.current == null || station.current.kind != PickupableKind.Plate)
        { Debug.Log($"[PlateFromHeldPot] reject: station has no Plate"); return false; }

        Pickupable plate = station.current;
        if (plate.plateState != PlateState.Clean || plate.plateContents != PlateContents.Empty)
        { Debug.Log($"[PlateFromHeldPot] reject: plate is {plate.plateState}/{plate.plateContents}"); return false; }

        PotContents pot = heldItem.GetComponentInChildren<PotContents>();
        if (pot == null)
        { Debug.Log($"[PlateFromHeldPot] reject: held pot has no PotContents"); return false; }
        if (!pot.TryGetSoupType(out VegetableType soupType))
        { Debug.Log($"[PlateFromHeldPot] reject: pot not single-type fully cooked (veg={pot.vegCount}, cook={pot.cookSeconds:F1}/{pot.TotalCookTime:F1}, fire={pot.burned})"); return false; }

        PlateSoup plateSoup = plate.GetComponentInChildren<PlateSoup>();
        if (plateSoup == null)
        { Debug.Log($"[PlateFromHeldPot] reject: plate has no PlateSoup component"); return false; }
        if (plateSoup.isDirty)
        { Debug.Log($"[PlateFromHeldPot] reject: station plate is dirty"); return false; }

        if (!plateSoup.TrySetSoup(soupType))
        { Debug.Log($"[PlateFromHeldPot] reject: plate refused soup"); return false; }
        pot.Empty();
        Debug.Log($"[Overcooked] Plated {soupType} soup onto {plate.name}");
        return true;
    }

    private void OnActionPressed()
    {
        if (isChopping || isWashing) return;

        if (hasKnife)
        {
            // Begin a chop cycle. Animation runs for cutCooldown; the cut lands at the bottom of the chop.
            isChopping = true;
            chopTimer = 0f;

            if (knifeStation != null && knifeStation.current != null)
            {
                bool finished = knifeStation.current.RegisterCut();
                if (finished)
                    Debug.Log($"[Overcooked] {knifeStation.current.name} is now Cut");
            }
            return;
        }

        // Sink: begin a wash cycle if facing a sink with dirty plates and hands empty.
        // Sink retains any partial progress from a previous interrupted wash, so this
        // can resume mid-cycle.
        if (!IsHolding && facingStation != null && facingStation.kind == StationKind.Sink)
        {
            Sink sink = facingStation.GetComponent<Sink>();
            if (sink != null && sink.CanStartWash())
            {
                isWashing = true;
                washSink = sink;
                Debug.Log($"[Overcooked] Started washing at {sink.name} (resuming at {sink.WashProgress01:P0}, dirty={sink.dirtyCount}, clean={sink.cleanCount})");
                return;
            }
        }
    }

    private void TickWash()
    {
        if (!isWashing) return;

        // Stop (but don't reset sink's persistent progress) if the player walked away,
        // picked something up, or the sink disappeared.
        if (washSink == null || IsHolding || facingStation == null || facingStation.GetComponent<Sink>() != washSink)
        {
            StopWashing();
            return;
        }

        Sink.WashTickResult result = washSink.AdvanceWash(Time.deltaTime);
        if (result == Sink.WashTickResult.NotReady)
        {
            // No more dirty plates or sink is now full of clean — nothing left to do.
            Debug.Log($"[Overcooked] Wash stopping (no work left) at {washSink.name}");
            StopWashing();
        }
        else if (result == Sink.WashTickResult.Completed)
        {
            Debug.Log($"[Overcooked] Wash cycle complete at {washSink.name} (dirty={washSink.dirtyCount}, clean={washSink.cleanCount}); continuing if more dirty remain");
            // Stay isWashing — next frame will either tick another plate (if dirty remain) or
            // return NotReady and stop us above.
        }
    }

    private void StopWashing()
    {
        // Note: does NOT touch washSink.washProgressSeconds. Sink owns its own progress
        // so it persists across player interruptions and across players.
        isWashing = false;
        washSink = null;
    }

    private void UpdateKnifeEquip()
    {
        bool wantKnife = !IsHolding
                         && !IsCarryingDirtyPlates
                         && facingStation != null
                         && facingStation.kind == StationKind.CuttingBoard
                         && facingStation.knife != null;

        if (wantKnife && !hasKnife)
        {
            EquipKnife(facingStation);
        }
        else if (hasKnife && !isChopping)
        {
            // Return the knife if we're no longer at the same cutting board, we picked something up,
            // or we picked up a dirty-plate stack.
            if (facingStation != knifeStation || IsHolding || IsCarryingDirtyPlates || !wantKnife)
                UnequipKnife();
        }
    }

    private void EquipKnife(Station station)
    {
        Transform handAnchor = rightHandAnchor != null ? rightHandAnchor : rightArm;
        if (handAnchor == null) return;

        knifeStation = station;
        borrowedKnife = station.knife;
        knifeOriginalParent = borrowedKnife.parent;
        knifeOriginalLocalPos = borrowedKnife.localPosition;
        knifeOriginalLocalRot = borrowedKnife.localRotation;

        currentGrip = borrowedKnife.GetComponent<KnifeGrip>();
        Vector3 gripPos = currentGrip != null ? currentGrip.localPos : knifeGripLocalPos;
        Vector3 gripEuler = currentGrip != null ? currentGrip.localEuler : knifeGripLocalEuler;

        borrowedKnife.SetParent(handAnchor, true);
        borrowedKnife.localPosition = gripPos;
        borrowedKnife.localRotation = Quaternion.Euler(gripEuler);

        hasKnife = true;
    }

    private void UnequipKnife()
    {
        if (borrowedKnife != null)
        {
            borrowedKnife.SetParent(knifeOriginalParent, true);
            borrowedKnife.localPosition = knifeOriginalLocalPos;
            borrowedKnife.localRotation = knifeOriginalLocalRot;
        }
        hasKnife = false;
        isChopping = false;
        knifeStation = null;
        borrowedKnife = null;
        knifeOriginalParent = null;
        currentGrip = null;
    }

    private float EffectiveChopDuration()
        => (currentGrip != null && currentGrip.overrideChop) ? Mathf.Max(0.01f, currentGrip.chopDuration) : cutCooldown;

    private float ChopWave(float t)
    {
        // Default symmetric sine when no grip override.
        if (currentGrip == null || !currentGrip.overrideChop)
            return Mathf.Sin(t * Mathf.PI);

        float down = Mathf.Clamp(currentGrip.downFraction, 0.01f, 0.99f);
        float hold = Mathf.Clamp(currentGrip.holdFraction, 0f, 1f - down);
        float up = Mathf.Max(0.01f, 1f - down - hold);

        if (t < down)
        {
            float u = t / down;
            return Mathf.Pow(u, Mathf.Max(0.01f, currentGrip.downEasePower));
        }
        if (t < down + hold)
        {
            return 1f;
        }
        float r = (t - down - hold) / up;
        return 1f - Mathf.Pow(r, Mathf.Max(0.01f, currentGrip.upEasePower));
    }

    private void TickChop()
    {
        if (!isChopping) return;
        chopTimer += Time.deltaTime;
        if (chopTimer >= EffectiveChopDuration()) isChopping = false;
    }

    private void ApplyKnifePose()
    {
        // Re-apply grip pos/rot each frame so Inspector tweaks show up live.
        // Skip when the grip is in tuneMode — lets the user drag the knife in the Scene view.
        if (borrowedKnife != null && (currentGrip == null || !currentGrip.tuneMode))
        {
            Vector3 gripPos = currentGrip != null ? currentGrip.localPos : knifeGripLocalPos;
            Vector3 gripEuler = currentGrip != null ? currentGrip.localEuler : knifeGripLocalEuler;
            borrowedKnife.localPosition = gripPos;
            borrowedKnife.localRotation = Quaternion.Euler(gripEuler);
        }

        // Left arm stays at rest.
        if (leftArm != null)
        {
            leftArm.localPosition = leftArmRestPos;
            leftArm.localRotation = leftArmRestRot;
        }

        // Choose rest/slice eulers from KnifeGrip if it overrides; else fall back to character chopMinX/chopMaxX.
        Vector3 restEuler;
        Vector3 sliceEuler;
        if (currentGrip != null && currentGrip.overrideChop)
        {
            restEuler = currentGrip.restEuler;
            sliceEuler = currentGrip.sliceEuler;
        }
        else
        {
            restEuler = new Vector3(chopMinX, 0f, 0f);
            sliceEuler = new Vector3(chopMaxX, 0f, 0f);
        }

        Vector3 angle = restEuler;
        float duration = EffectiveChopDuration();
        float wave = 0f;
        if (isChopping && duration > 0f)
        {
            float t = Mathf.Clamp01(chopTimer / duration);
            wave = ChopWave(t);
            angle = Vector3.Lerp(restEuler, sliceEuler, wave);
        }

        // Right arm position: KnifeGrip can override rest/slice positions; otherwise stay at scene-edit rest.
        Vector3 armPos = rightArmRestPos;
        if (currentGrip != null && currentGrip.overrideArmPosition)
        {
            armPos = Vector3.Lerp(currentGrip.restArmPos, currentGrip.sliceArmPos, wave);
        }

        if (rightArm != null)
        {
            rightArm.localPosition = armPos;
            rightArm.localRotation = rightArmRestRot * Quaternion.Euler(angle);
        }
    }

    private Station FindFacingStation() => facingStation;

    private static readonly Collider[] facingBuffer = new Collider[32];

    private void UpdateFacing()
    {
        Vector3 origin = transform.position + Vector3.up * interactHeight;
        Vector3 fwd = transform.forward;
        Vector3 fwdFlat = new Vector3(fwd.x, 0f, fwd.z);
        if (fwdFlat.sqrMagnitude > 0.0001f) fwdFlat.Normalize();

        Station bestStation = null;
        Collider bestCollider = null;
        float bestDist = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(origin, interactRange, facingBuffer, interactMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = facingBuffer[i];
            Station s = col.GetComponentInParent<Station>();
            if (s == null) continue;

            Vector3 closest = col.ClosestPoint(origin);
            Vector3 toClosest = closest - origin;
            float dist = toClosest.magnitude;
            if (dist > interactRange) continue;

            Vector3 toFlat = new Vector3(toClosest.x, 0f, toClosest.z);
            if (toFlat.sqrMagnitude < 0.0001f)
            {
                // Character is essentially on top of the collider — accept it.
            }
            else
            {
                toFlat.Normalize();
                if (Vector3.Dot(toFlat, fwdFlat) < facingDotThreshold) continue;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                bestStation = s;
                bestCollider = col;
            }
        }

        facingStation = bestStation;

        if (drawDebugRay)
        {
            Color c = bestStation != null ? rayHitColor : rayMissColor;
            Vector3 end = bestCollider != null ? bestCollider.ClosestPoint(origin) : origin + fwd * interactRange;
            Debug.DrawLine(origin, end, c);
        }

        UpdateFacingHighlight(bestCollider);
    }

    private void UpdateFacingHighlight(Collider hitCollider)
    {
        if (!showFacingHighlight)
        {
            if (highlightQuad != null) highlightQuad.SetActive(false);
            return;
        }

        if (facingStation == null || hitCollider == null)
        {
            if (highlightQuad != null) highlightQuad.SetActive(false);
            return;
        }

        EnsureHighlightQuad();

        Bounds b = hitCollider.bounds;
        highlightTf.position = new Vector3(b.center.x, 1.51f, b.center.z);
        highlightTf.rotation = Quaternion.Euler(90f, 0f, 0f); // flat on top
        highlightTf.localScale = new Vector3(b.size.x, b.size.z, 1f);
        highlightQuad.SetActive(true);
    }

    private void EnsureHighlightQuad()
    {
        if (highlightQuad != null) return;

        highlightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        highlightQuad.name = "FacingHighlight";
        Object.Destroy(highlightQuad.GetComponent<Collider>());
        highlightTf = highlightQuad.transform;
        highlightRenderer = highlightQuad.GetComponent<MeshRenderer>();
        highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        highlightRenderer.receiveShadows = false;

        Material mat = highlightMaterial;
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
            // Best-effort transparency setup for URP Unlit and built-in.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent (URP)
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", highlightColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", highlightColor);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        highlightRenderer.sharedMaterial = mat;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawInteractGizmo) return;
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * interactHeight;
        Gizmos.DrawLine(origin, origin + transform.forward * interactRange);
        Gizmos.DrawWireSphere(origin + transform.forward * interactRange, 0.05f);
    }
}
