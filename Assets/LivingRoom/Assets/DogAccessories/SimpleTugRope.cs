using UnityEngine;

// Put this on the imported Rope_Rig root. Assign the dog's mouth and the hand targets.
// If targets are omitted, the exported Mouth_Grip / Hand_Grip transforms are used.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class SimpleTugRope : MonoBehaviour
{
    public Transform mouthTarget;
    public Transform handTarget;
    public Transform bendTarget;
    [Range(2, 32)] public int iterations = 16;
    public bool allowStretch = true;

    private Transform[] joints;
    private Vector3[] restPosition, restScale, points;
    private Quaternion[] restRotation;
    private float[] lengths;
    private int[] lengthAxis;

    private Transform FindNamed(string targetName)
    {
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
            if (candidate.name == targetName) return candidate;
        return null;
    }

    private void Awake()
    {
        joints = new Transform[7];
        for (int i = 0; i < 6; i++) joints[i] = FindNamed("DEF_Rope_" + (i + 1).ToString("00"));
        joints[6] = FindNamed("RopeTip");
        foreach (Transform joint in joints)
            if (joint == null)
            {
                Debug.LogError("SimpleTugRope needs DEF_Rope_01..06 and RopeTip.", this);
                enabled = false;
                return;
            }
        if (mouthTarget == null) mouthTarget = FindNamed("Mouth_Grip");
        if (handTarget == null) handTarget = FindNamed("Hand_Grip");
        if (bendTarget == null) bendTarget = FindNamed("Bend_Direction");
        restPosition = new Vector3[7]; restScale = new Vector3[7];
        restRotation = new Quaternion[7]; points = new Vector3[7];
        lengths = new float[6]; lengthAxis = new int[6];
        for (int i = 0; i < 7; i++)
        {
            restPosition[i] = joints[i].localPosition;
            restRotation[i] = joints[i].localRotation;
            restScale[i] = joints[i].localScale;
            if (i == 6) continue;
            Vector3 axis = joints[i].InverseTransformVector(joints[i + 1].position - joints[i].position);
            lengthAxis[i] = Mathf.Abs(axis.x) > Mathf.Abs(axis.y) ? 0 : 1;
            if (Mathf.Abs(axis.z) > Mathf.Abs(axis[lengthAxis[i]])) lengthAxis[i] = 2;
        }
    }

    private void LateUpdate()
    {
        if (joints == null || mouthTarget == null || handTarget == null) return;
        Vector3 start = mouthTarget.position;
        Vector3 end = handTarget.position;
        for (int i = 0; i < 7; i++)
        {
            joints[i].localPosition = restPosition[i];
            joints[i].localRotation = restRotation[i];
            joints[i].localScale = restScale[i];
        }
        Vector3 offset = start - joints[0].position;
        float total = 0;
        for (int i = 0; i < 7; i++)
        {
            points[i] = joints[i].position + offset;
            if (i == 0) continue;
            lengths[i - 1] = Vector3.Distance(points[i - 1], points[i]);
            total += lengths[i - 1];
        }
        if (total < 0.000001f) return;
        float distance = Vector3.Distance(start, end);
        if (distance >= total)
        {
            Vector3 direction = (end - start).normalized;
            float stretch = allowStretch ? distance / total : 1;
            points[0] = start;
            for (int i = 1; i < 7; i++) points[i] = points[i - 1] + direction * lengths[i - 1] * stretch;
        }
        else
        {
            for (int pass = 0; pass < iterations; pass++)
            {
                points[6] = end;
                for (int i = 5; i >= 0; i--)
                    points[i] = points[i + 1] + Direction(points[i] - points[i + 1]) * lengths[i];
                points[0] = start;
                for (int i = 1; i < 7; i++)
                    points[i] = points[i - 1] + Direction(points[i] - points[i - 1]) * lengths[i - 1];
                if ((points[6] - end).sqrMagnitude < 0.00000001f) break;
            }
        }
        if (bendTarget != null && distance < total)
        {
            for (int i = 1; i < 6; i++)
            {
                Vector3 axis = points[i + 1] - points[i - 1];
                if (axis.sqrMagnitude < 0.0000000001f) continue;
                Plane plane = new Plane(axis.normalized, points[i - 1]);
                Vector3 jointDirection = plane.ClosestPointOnPlane(points[i]) - points[i - 1];
                Vector3 poleDirection = plane.ClosestPointOnPlane(bendTarget.position) - points[i - 1];
                if (jointDirection.sqrMagnitude < 0.0000000001f || poleDirection.sqrMagnitude < 0.0000000001f) continue;
                float angle = Vector3.SignedAngle(jointDirection, poleDirection, axis);
                points[i] = points[i - 1] + Quaternion.AngleAxis(angle, axis.normalized) * (points[i] - points[i - 1]);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            joints[i].position = points[i];
            Vector3 from = joints[i + 1].position - joints[i].position;
            Vector3 to = points[i + 1] - points[i];
            if (from.sqrMagnitude < 0.0000000001f || to.sqrMagnitude < 0.0000000001f) continue;
            joints[i].rotation = Quaternion.FromToRotation(from, to) * joints[i].rotation;
            float currentLength = Vector3.Distance(joints[i].position, joints[i + 1].position);
            if (currentLength > 0.000001f)
            {
                Vector3 scale = joints[i].localScale;
                scale[lengthAxis[i]] *= to.magnitude / currentLength;
                joints[i].localScale = scale;
            }
        }
        joints[6].position = points[6];
    }

    private static Vector3 Direction(Vector3 vector)
    {
        return vector.sqrMagnitude > 0.0000000001f ? vector.normalized : Vector3.up;
    }
}
