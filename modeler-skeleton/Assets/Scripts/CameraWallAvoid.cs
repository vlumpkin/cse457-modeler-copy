using UnityEngine;

// Attach to the Camera that's parented under the character.
// Position the camera in the editor as usual — this script captures that
// local offset at Start and only pulls the camera IN toward the character
// when something (a wall) would block the view.
public class CameraWallAvoid : MonoBehaviour
{
    [Tooltip("Point the camera should be able to see (head/shoulder). " +
             "If null, uses the parent transform's position + pivotHeight.")]
    public Transform pivot;
    public float pivotHeight = 1.5f;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float cameraRadius = 0.25f;
    public float collisionBuffer = 0.1f;
    public float pullInSpeed = 30f;   // snap in quickly when blocked
    public float pushOutSpeed = 8f;   // ease back out when clear

    Vector3 desiredLocalPos;
    Quaternion desiredLocalRot;
    Transform anchor;   // what pivot is measured from (parent by default)

    void Start()
    {
        desiredLocalPos = transform.localPosition;
        desiredLocalRot = transform.localRotation;
        anchor = transform.parent;
    }

    void LateUpdate()
    {
        if (anchor == null) return;

        // Keep editor-set rotation, relative to parent.
        transform.localRotation = desiredLocalRot;

        Vector3 pivotWorld = pivot != null
            ? pivot.position
            : anchor.position + Vector3.up * pivotHeight;

        Vector3 desiredWorld = anchor.TransformPoint(desiredLocalPos);
        Vector3 toCam = desiredWorld - pivotWorld;
        float desiredDist = toCam.magnitude;
        if (desiredDist < 0.0001f) return;

        Vector3 dir = toCam / desiredDist;
        float allowedDist = desiredDist;

        if (Physics.SphereCast(pivotWorld, cameraRadius, dir, out RaycastHit hit,
                desiredDist, collisionMask, QueryTriggerInteraction.Ignore))
        {
            allowedDist = Mathf.Max(0f, hit.distance - collisionBuffer);
        }

        Vector3 targetWorld = pivotWorld + dir * allowedDist;
        float speed = allowedDist < (transform.position - pivotWorld).magnitude
            ? pullInSpeed : pushOutSpeed;
        transform.position = Vector3.Lerp(transform.position, targetWorld, speed * Time.deltaTime);
    }
}
