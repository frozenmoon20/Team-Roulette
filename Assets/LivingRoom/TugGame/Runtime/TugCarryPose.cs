using UnityEngine;

namespace TeamRoulette.Room3.TugPlay
{
    // The shared two-grip IK runs at 100. A free rope has only one grip and a ground contact.
    [DefaultExecutionOrder(110)]
    public sealed class TugCarryPose : MonoBehaviour
    {
        readonly Transform[] joints = new Transform[7];
        readonly Vector3[] hanging = new Vector3[7], posed = new Vector3[7];
        readonly float[] lengths = new float[6], fractions = new float[7];
        readonly int[] axes = new int[6];
        float restLength;
        public float Weight { private get; set; }

        public void Bind(Transform root)
        {
            var bones = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < joints.Length; i++)
            {
                string name = i == 6 ? "RopeTip" : $"DEF_Rope_{i + 1:00}";
                joints[i] = System.Array.Find(bones, bone => bone.name == name);
                if (i == 0) continue;
                Vector3 segment = joints[i].position - joints[i - 1].position;
                lengths[i - 1] = segment.magnitude;
                restLength += segment.magnitude;
                fractions[i] = restLength;
                Vector3 local = joints[i - 1].InverseTransformVector(segment);
                int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? 0 : 1;
                axes[i - 1] = Mathf.Abs(local.z) > Mathf.Abs(local[axis]) ? 2 : axis;
            }
            for (int i = 1; i < fractions.Length; i++) fractions[i] /= Mathf.Max(.00001f, restLength);
        }

        public Vector3 Build(Vector3 bite, Vector3 outward, float ground, float hangingLength)
        {
            hanging[0] = bite;
            float bottom = Mathf.Min(bite.y, Mathf.Max(ground, bite.y - hangingLength));
            float scale = Mathf.Max(1, hangingLength / Mathf.Max(.00001f, restLength));
            for (int i = 0; i < lengths.Length; i++)
            {
                float length = lengths[i] * scale;
                // Leave the muzzle pointing out first, then drape. Lay surplus length along the ground.
                float downRatio = Mathf.Lerp(.48f, .96f, Mathf.Clamp01(fractions[i] * 3));
                float drop = Mathf.Min(length * downRatio, Mathf.Max(0, hanging[i].y - bottom));
                float run = Mathf.Sqrt(Mathf.Max(0, length * length - drop * drop));
                hanging[i + 1] = hanging[i] + outward * run + Vector3.down * drop;
            }
            return hanging[6];
        }

        void LateUpdate()
        {
            if (Weight <= 0) return;
            // Correct the interior of the chain too. Both grip positions stay pinned during the handoff.
            Vector3 tipOffset = joints[6].position - hanging[6];
            for (int i = 0; i < joints.Length; i++)
                posed[i] = Vector3.Lerp(joints[i].position, hanging[i] + tipOffset * fractions[i], Weight);
            for (int i = 0; i < lengths.Length; i++)
            {
                Transform joint = joints[i], next = joints[i + 1];
                joint.position = posed[i];
                Vector3 from = next.position - joint.position, to = posed[i + 1] - posed[i];
                if (from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) continue;
                joint.rotation = Quaternion.FromToRotation(from, to) * joint.rotation;
                float current = Vector3.Distance(joint.position, next.position);
                if (current < .00001f) continue;
                Vector3 scale = joint.localScale;
                scale[axes[i]] *= to.magnitude / current;
                joint.localScale = scale;
            }
            joints[6].position = posed[6];
        }
    }
}
