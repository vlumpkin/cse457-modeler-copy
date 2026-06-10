using UnityEngine;

// Attach to any holdable item to override the character's default carry hand positions.
// Right hand is automatically mirrored from the left (X-pos flipped, Y/Z-euler flipped).
public class CarryPose : MonoBehaviour
{
    [Header("Left hand (right hand auto-mirrors)")]
    public bool overrideLeftPos = true;
    public Vector3 leftHandPos = new Vector3(-1f, 3f, 0.8f);
    public bool overrideLeftEuler = false;
    public Vector3 leftHandEuler = new Vector3(0f, 0f, 23f);

    [Header("Placement anchor offset (where the held item sits)")]
    public bool overridePlacementAnchor = false;
    public Vector3 placementAnchorLocalPos = new Vector3(0f, 3.4f, 1.1f);

    public Vector3 GetMirroredRightPos()
    {
        return new Vector3(-leftHandPos.x, leftHandPos.y, leftHandPos.z);
    }

    public Vector3 GetMirroredRightEuler()
    {
        return new Vector3(leftHandEuler.x, -leftHandEuler.y, -leftHandEuler.z);
    }
}
