using UnityEngine;

// Attach to a knife transform to specify how it sits in the character's hand
// AND how the right arm chops with it.
// Overrides the character's default knife grip + chop settings.
public class KnifeGrip : MonoBehaviour
{
    [Header("Hand grip (knife local pos/rot under the hand anchor)")]
    public Vector3 localPos = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
    [Tooltip("While ON, the character will NOT overwrite this knife's transform — drag it freely in the Scene view to find the right pose, then copy the values back and turn this off.")]
    public bool tuneMode = false;

    [Header("Chop animation (right_arm_container euler)")]
    [Tooltip("Master toggle — if off, the character's chop defaults are used instead.")]
    public bool overrideChop = true;
    [Tooltip("Right arm euler when the knife is equipped but not actively chopping.")]
    public Vector3 restEuler = new Vector3(-25f, 0f, 0f);
    [Tooltip("Right arm euler at the bottom of the chop. The animation lerps rest → slice → rest each chop.")]
    public Vector3 sliceEuler = new Vector3(60f, 0f, 0f);
    [Tooltip("Seconds per chop. Doubles as the input cooldown between chops.")]
    public float chopDuration = 0.25f;

    [Header("Chop timing curve (must sum to ≤ 1; remainder is the recovery up)")]
    [Tooltip("Fraction of chopDuration spent slashing down. Lower = snappier.")]
    [Range(0.05f, 0.9f)] public float downFraction = 0.22f;
    [Tooltip("Fraction of chopDuration paused at the bottom.")]
    [Range(0f, 0.5f)] public float holdFraction = 0.08f;
    [Tooltip("Down easing exponent. >1 accelerates (fast finish), <1 decelerates. 2 = quadratic ease-in.")]
    [Range(0.5f, 5f)] public float downEasePower = 2.4f;
    [Tooltip("Up easing exponent. >1 = slow start fast end, <1 = fast start slow end. 1 = linear.")]
    [Range(0.5f, 5f)] public float upEasePower = 1.4f;

    [Header("Right arm container position (optional)")]
    [Tooltip("If ON, the right_arm_container's localPosition is overridden by restArmPos / sliceArmPos. If OFF, the arm stays at its scene-edit rest position.")]
    public bool overrideArmPosition = false;
    [Tooltip("Right arm container localPosition while idle with the knife.")]
    public Vector3 restArmPos = Vector3.zero;
    [Tooltip("Right arm container localPosition at the bottom of the chop. Lerps rest → slice → rest each chop.")]
    public Vector3 sliceArmPos = Vector3.zero;
}
