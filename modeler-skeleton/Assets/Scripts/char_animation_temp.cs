using System.Collections;
using UnityEngine;

public class char_animation_temp : MonoBehaviour
{
    public float rotationDuration = 2f;

    public float walkDuration = 3f;
    public float walkLimbAngle = 30f;
    public float walkCycleDuration = 0.9f;
    public float walkSpeed = 10.0f;
    public float walkTurnRate = 0f;
    public bool returnToStart = true;

    public float antennaSpinDuration = 3f;
    public float antennaSpinSpeed = 360f;

    public bool enableKeyboardControl = true;
    public float moveSpeed = 30f;
    public float mouseSensitivity = 3f;
    public float pitchSensitivity = 3f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool lockCursor = true;

    private float lookPitch;
    public float LookPitch => lookPitch;

    public float jumpHeight = 2.0f;
    public float gravity = 9.81f;
    public KeyCode jumpKey = KeyCode.Space;

    public KeyCode selectRampKey = KeyCode.E;
    public KeyCode selectFloorKey = KeyCode.C;
    public KeyCode selectWallKey = KeyCode.Q;
    public int placeMouseButton = 0;
    public float wallThickness = 0.1f;
    public float placeRepeatInterval = 0.15f;

    public enum BuildPiece { Ramp, Floor, Wall }
    public BuildPiece currentPiece = BuildPiece.Ramp;

    private float lastPlaceTime = -999f;
    private MeshFilter previewMeshFilter;
    private static Mesh cachedCubeMesh;
    public float blockDistance = 1.5f;
    public float blockSize = 1f;
    public float floorThickness = 0.1f;
    public float placementYOffset = -3f;
    public Vector3 aimOriginOffset = new Vector3(0f, 1.5f, 0f);
    public bool useLineOfSight = true;
    public float gridSize = 1f;
    public bool snapToGrid = true;
    public bool preventBlockOverlap = true;
    public Material blockMaterial;

    private static Mesh cachedRampMesh;

    public float worldFloorY = 0f;
    public float controllerHeight = 2f;
    public float controllerRadius = 0.4f;

    public bool showPlacementPreview = true;
    public Color previewColor = new Color(0.3f, 0.6f, 1f, 0.25f);

    public bool showAimBeam = true;
    public Color aimBeamRawColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    public Color aimBeamSnapColor = new Color(0.2f, 1f, 0.4f, 0.9f);
    public float aimBeamWidth = 0.04f;

    private bool isAnimating;
    private float walkPhase;
    private float verticalVelocity;
    private bool grounded;
    private CharacterController controller;
    private GameObject previewObj;
    private LineRenderer aimBeam;
    private Quaternion restRotation;

    private Transform rightArmContainer;
    private Transform leftArmContainer;
    private Transform rightLegContainer;
    private Transform leftLegContainer;
    private Transform antennaContainer;

    private Quaternion rightArmRest;
    private Quaternion leftArmRest;
    private Quaternion rightLegRest;
    private Quaternion leftLegRest;
    private Quaternion antennaRest;

    private void Start()
    {
        restRotation = transform.localRotation;

        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        controller.height = controllerHeight;
        controller.radius = controllerRadius;
        controller.center = new Vector3(0f, controllerHeight * 0.5f, 0f);
        controller.skinWidth = 0.02f;
        controller.minMoveDistance = 0f;
        controller.slopeLimit = 50f;

        if (showPlacementPreview)
            CreatePreview();

        if (showAimBeam)
            CreateAimBeam();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        rightArmContainer = FindLimb("Right Arm Container", out rightArmRest);
        leftArmContainer = FindLimb("Left Arm Container", out leftArmRest);
        rightLegContainer = FindLimb("Right Leg Container", out rightLegRest);
        leftLegContainer = FindLimb("Left Leg Container", out leftLegRest);
        antennaContainer = FindLimb("HeadContainer/Antenna Container", out antennaRest);
    }

    private void Update()
    {
        UpdatePreview();
        UpdateAimBeam();

        if (!enableKeyboardControl || isAnimating) return;

        if (Input.GetKeyDown(selectRampKey)) currentPiece = BuildPiece.Ramp;
        if (Input.GetKeyDown(selectFloorKey)) currentPiece = BuildPiece.Floor;
        if (Input.GetKeyDown(selectWallKey)) currentPiece = BuildPiece.Wall;

        if (ShouldFireMousePlacement())
        {
            switch (currentPiece)
            {
                case BuildPiece.Ramp: PlaceRamp(); break;
                case BuildPiece.Floor: PlaceFloor(); break;
                case BuildPiece.Wall: PlaceWall(); break;
            }
            lastPlaceTime = Time.time;
        }

        float mouseX = Input.GetAxis("Mouse X");
        if (mouseX != 0f)
            transform.Rotate(0f, mouseX * mouseSensitivity, 0f, Space.Self);

        float mouseY = Input.GetAxis("Mouse Y");
        lookPitch = Mathf.Clamp(lookPitch - mouseY * pitchSensitivity, minPitch, maxPitch);

        float strafe = Input.GetAxisRaw("Horizontal");
        float move = Input.GetAxisRaw("Vertical");

        grounded = controller.isGrounded || transform.position.y <= worldFloorY + 0.001f;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -1f;

        if (grounded && Input.GetKey(jumpKey))
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);

        verticalVelocity -= gravity * Time.deltaTime;

        Vector3 horizontal = (transform.forward * move + transform.right * strafe) * moveSpeed;
        Vector3 motion = (horizontal + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(motion);

        if (transform.position.y < worldFloorY)
        {
            Vector3 p = transform.position;
            p.y = worldFloorY;
            transform.position = p;
            verticalVelocity = 0f;
        }

        float walkInput = Mathf.Max(Mathf.Abs(move), Mathf.Abs(strafe));
        if (walkInput > 0.01f)
        {
            walkPhase += (walkInput * Time.deltaTime / walkCycleDuration) * 2f * Mathf.PI;
            float swing = Mathf.Sin(walkPhase) * walkLimbAngle * (move != 0f ? Mathf.Sign(move) : 1f);

            if (rightArmContainer != null) rightArmContainer.localRotation = rightArmRest * Quaternion.Euler(swing, 0f, 0f);
            if (leftArmContainer != null) leftArmContainer.localRotation = leftArmRest * Quaternion.Euler(-swing, 0f, 0f);
            if (rightLegContainer != null) rightLegContainer.localRotation = rightLegRest * Quaternion.Euler(-swing, 0f, 0f);
            if (leftLegContainer != null) leftLegContainer.localRotation = leftLegRest * Quaternion.Euler(swing, 0f, 0f);
        }
        else if (walkPhase != 0f)
        {
            walkPhase = 0f;
            if (rightArmContainer != null) rightArmContainer.localRotation = rightArmRest;
            if (leftArmContainer != null) leftArmContainer.localRotation = leftArmRest;
            if (rightLegContainer != null) rightLegContainer.localRotation = rightLegRest;
            if (leftLegContainer != null) leftLegContainer.localRotation = leftLegRest;
        }
    }

    private bool ShouldFireMousePlacement()
    {
        if (Input.GetMouseButtonDown(placeMouseButton)) return true;
        if (Input.GetMouseButton(placeMouseButton) && Time.time - lastPlaceTime >= placeRepeatInterval) return true;
        return false;
    }

    public Vector3 PlacementForward()
    {
        return Quaternion.AngleAxis(lookPitch, transform.right) * transform.forward;
    }

    public Vector3 AimOrigin()
    {
        return transform.position + transform.rotation * aimOriginOffset;
    }

    private Vector3 SnapForwardCell()
    {
        Vector3 origin = AimOrigin();
        Vector3 dir = PlacementForward();
        float reach = blockDistance;

        if (useLineOfSight)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, reach);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (hits[i].collider is CharacterController) continue;
                if (hits[i].transform.name == "PlacedRamp") continue;
                reach = Mathf.Max(0f, hits[i].distance - blockSize * 0.5f);
                break;
            }
        }

        Vector3 pos = origin + dir * reach;
        pos.y += placementYOffset;
        if (snapToGrid)
        {
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        }
        return pos;
    }

    private Quaternion SnapForwardYaw()
    {
        float yaw = transform.eulerAngles.y;
        yaw = Mathf.Round(yaw / 90f) * 90f;
        return Quaternion.Euler(0f, yaw, 0f);
    }

    private bool IsCellOccupied(Vector3 center, Vector3 halfExtents, Quaternion rot)
    {
        if (!preventBlockOverlap) return false;
        Collider[] hits = Physics.OverlapBox(center, halfExtents, rot);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.IsChildOf(transform)) continue;
            if (hits[i] is CharacterController) continue;
            return true;
        }
        return false;
    }

    private void PlaceRamp()
    {
        Vector3 cell = SnapForwardCell();
        Quaternion rot = SnapForwardYaw();

        Vector3 rampCenter = cell + rot * new Vector3(0f, blockSize * 0.5f, 0f);
        if (IsCellOccupied(rampCenter, Vector3.one * (blockSize * 0.45f), rot)) return;

        GameObject ramp = new GameObject("PlacedRamp");
        ramp.transform.position = cell;
        ramp.transform.rotation = rot;
        ramp.transform.localScale = Vector3.one * blockSize;

        MeshFilter mf = ramp.AddComponent<MeshFilter>();
        MeshRenderer mr = ramp.AddComponent<MeshRenderer>();
        mf.sharedMesh = GetRampMesh();
        mr.sharedMaterial = blockMaterial != null ? blockMaterial : DefaultDiffuseMaterial();

        MeshCollider mc = ramp.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
    }

    private void ComputeWallTransform(Vector3 cell, out Vector3 center, out Quaternion rot)
    {
        Vector3 toPlayer = transform.position - cell;
        toPlayer.y = 0f;
        Vector3 nearEdgeDir;
        float sameCellRadius = blockSize * 0.5f;
        if (toPlayer.sqrMagnitude < sameCellRadius * sameCellRadius)
        {
            Vector3 fwd = PlacementForward();
            fwd.y = 0f;
            nearEdgeDir = Mathf.Abs(fwd.x) > Mathf.Abs(fwd.z)
                ? new Vector3(Mathf.Sign(fwd.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(fwd.z));
        }
        else
        {
            nearEdgeDir = Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.z)
                ? new Vector3(Mathf.Sign(toPlayer.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(toPlayer.z));
        }
        if (nearEdgeDir == Vector3.zero) nearEdgeDir = Vector3.forward;

        rot = Quaternion.LookRotation(nearEdgeDir);
        center = cell + nearEdgeDir * (blockSize * 0.5f) + Vector3.up * (blockSize * 0.5f);
    }

    private void PlaceWall()
    {
        Vector3 cell = SnapForwardCell();
        ComputeWallTransform(cell, out Vector3 wallCenter, out Quaternion rot);
        Vector3 wallHalf = new Vector3(blockSize * 0.45f, blockSize * 0.45f, wallThickness * 0.45f);
        if (IsCellOccupied(wallCenter, wallHalf, rot)) return;

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "PlacedWall";
        wall.transform.position = wallCenter;
        wall.transform.rotation = rot;
        wall.transform.localScale = new Vector3(blockSize, blockSize, wallThickness);
        if (blockMaterial != null)
            wall.GetComponent<Renderer>().sharedMaterial = blockMaterial;
    }

    private void PlaceFloor()
    {
        Vector3 cell = SnapForwardCell();

        Vector3 floorCenter = new Vector3(cell.x, cell.y - floorThickness * 0.5f, cell.z);
        Vector3 floorHalf = new Vector3(blockSize * 0.45f, floorThickness * 0.45f, blockSize * 0.45f);
        if (IsCellOccupied(floorCenter, floorHalf, Quaternion.identity)) return;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PlacedFloor";
        floor.transform.position = floorCenter;
        floor.transform.rotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(blockSize, floorThickness, blockSize);
        if (blockMaterial != null)
            floor.GetComponent<Renderer>().sharedMaterial = blockMaterial;
    }

    private void CreatePreview()
    {
        previewObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObj.name = "PlacementPreview";
        Destroy(previewObj.GetComponent<Collider>());

        previewMeshFilter = previewObj.GetComponent<MeshFilter>();
        if (cachedCubeMesh == null) cachedCubeMesh = previewMeshFilter.sharedMesh;

        Renderer r = previewObj.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = MakeTransparentMaterial(previewColor);
    }

    private void UpdatePreview()
    {
        if (previewObj == null) return;
        bool active = showPlacementPreview && enableKeyboardControl && !isAnimating;
        if (previewObj.activeSelf != active) previewObj.SetActive(active);
        if (!active) return;

        Vector3 cell = SnapForwardCell();

        switch (currentPiece)
        {
            case BuildPiece.Ramp:
                previewMeshFilter.sharedMesh = GetRampMesh();
                previewObj.transform.position = cell;
                previewObj.transform.rotation = SnapForwardYaw();
                previewObj.transform.localScale = Vector3.one * blockSize;
                break;
            case BuildPiece.Floor:
                previewMeshFilter.sharedMesh = cachedCubeMesh;
                previewObj.transform.position = new Vector3(cell.x, cell.y - floorThickness * 0.5f, cell.z);
                previewObj.transform.rotation = Quaternion.identity;
                previewObj.transform.localScale = new Vector3(blockSize, floorThickness, blockSize);
                break;
            case BuildPiece.Wall:
                previewMeshFilter.sharedMesh = cachedCubeMesh;
                ComputeWallTransform(cell, out Vector3 wc, out Quaternion wr);
                previewObj.transform.position = wc;
                previewObj.transform.rotation = wr;
                previewObj.transform.localScale = new Vector3(blockSize, blockSize, wallThickness);
                break;
        }
    }

    private void CreateAimBeam()
    {
        GameObject obj = new GameObject("AimBeam");
        aimBeam = obj.AddComponent<LineRenderer>();
        aimBeam.positionCount = 3;
        aimBeam.startWidth = aimBeamWidth;
        aimBeam.endWidth = aimBeamWidth;
        aimBeam.useWorldSpace = true;
        aimBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimBeam.receiveShadows = false;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Unlit/Color");
        aimBeam.material = new Material(s);
    }

    private void UpdateAimBeam()
    {
        if (aimBeam == null) return;
        bool active = showAimBeam && enableKeyboardControl && !isAnimating;
        if (aimBeam.gameObject.activeSelf != active) aimBeam.gameObject.SetActive(active);
        if (!active) return;

        Vector3 origin = AimOrigin();
        Vector3 rawAim = origin + PlacementForward() * blockDistance;
        Vector3 snapped = SnapForwardCell();

        aimBeam.SetPosition(0, origin);
        aimBeam.SetPosition(1, rawAim);
        aimBeam.SetPosition(2, snapped);
        aimBeam.startColor = aimBeamRawColor;
        aimBeam.endColor = aimBeamSnapColor;
    }

    private static Material MakeTransparentMaterial(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s != null)
        {
            Material m = new Material(s);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
            return m;
        }
        s = Shader.Find("Standard");
        if (s != null)
        {
            Material m = new Material(s);
            m.color = c;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }
        return new Material(Shader.Find("Sprites/Default")) { color = c };
    }

    private static Mesh GetRampMesh()
    {
        if (cachedRampMesh != null) return cachedRampMesh;

        Mesh m = new Mesh { name = "RampPlane" };

        Vector3 v0 = new Vector3(-0.5f, 0f, -0.5f);
        Vector3 v1 = new Vector3( 0.5f, 0f, -0.5f);
        Vector3 v2 = new Vector3( 0.5f, 1f,  0.5f);
        Vector3 v3 = new Vector3(-0.5f, 1f,  0.5f);

        m.vertices = new[] { v0, v1, v2, v3, v0, v1, v2, v3 };

        m.triangles = new[]
        {
            0, 3, 2,  0, 2, 1,
            4, 6, 7,  4, 5, 6,
        };

        m.uv = new[]
        {
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
        };

        m.RecalculateNormals();
        m.RecalculateBounds();
        cachedRampMesh = m;
        return m;
    }

    private static Material DefaultDiffuseMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return new Material(s);
    }

    private Transform FindLimb(string childName, out Quaternion rest)
    {
        Transform t = transform.Find(childName);
        if (t != null)
        {
            rest = t.localRotation;
        }
        else
        {
            rest = Quaternion.identity;
            Debug.LogWarning("char_animation_temp: '" + childName + "' not found under " + name);
        }
        return t;
    }

    public void PlayFullBodyRotation()
    {
        if (isAnimating) return;
        StartCoroutine(FullBodyRotationRoutine());
    }

    public void PlayWalk()
    {
        if (isAnimating) return;
        StartCoroutine(WalkRoutine());
    }

    public void PlayAntennaSpin()
    {
        if (isAnimating) return;
        StartCoroutine(AntennaSpinRoutine());
    }

    private IEnumerator FullBodyRotationRoutine()
    {
        isAnimating = true;

        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            float angle = 360f * t;
            transform.localRotation = restRotation * Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        transform.localRotation = restRotation;
        isAnimating = false;
    }

    private IEnumerator WalkRoutine()
    {
        isAnimating = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float elapsed = 0f;
        while (elapsed < walkDuration)
        {
            elapsed += Time.deltaTime;
            float phase = (elapsed / walkCycleDuration) * 2f * Mathf.PI;
            float swing = Mathf.Sin(phase) * walkLimbAngle;

            if (rightArmContainer != null) rightArmContainer.localRotation = rightArmRest * Quaternion.Euler(swing, 0f, 0f);
            if (leftArmContainer != null) leftArmContainer.localRotation = leftArmRest * Quaternion.Euler(-swing, 0f, 0f);
            if (rightLegContainer != null) rightLegContainer.localRotation = rightLegRest * Quaternion.Euler(-swing, 0f, 0f);
            if (leftLegContainer != null) leftLegContainer.localRotation = leftLegRest * Quaternion.Euler(swing, 0f, 0f);

            transform.position += transform.forward * walkSpeed * Time.deltaTime;
            if (walkTurnRate != 0f)
                transform.Rotate(0f, walkTurnRate * Time.deltaTime, 0f, Space.Self);

            yield return null;
        }

        if (rightArmContainer != null) rightArmContainer.localRotation = rightArmRest;
        if (leftArmContainer != null) leftArmContainer.localRotation = leftArmRest;
        if (rightLegContainer != null) rightLegContainer.localRotation = rightLegRest;
        if (leftLegContainer != null) leftLegContainer.localRotation = leftLegRest;

        if (returnToStart)
        {
            transform.position = startPos;
            transform.rotation = startRot;
        }

        isAnimating = false;
    }

    private IEnumerator AntennaSpinRoutine()
    {
        isAnimating = true;

        float elapsed = 0f;
        while (elapsed < antennaSpinDuration)
        {
            elapsed += Time.deltaTime;
            if (antennaContainer != null) antennaContainer.localRotation = antennaRest * Quaternion.Euler(0f, elapsed * antennaSpinSpeed, 0f);
            yield return null;
        }

        if (antennaContainer != null) antennaContainer.localRotation = antennaRest;
        isAnimating = false;
    }
}
