
using System.Collections;
using UnityEngine;

public class TeddyBearStandUp : MonoBehaviour, IInteractable, IHasName
{
    public string displayName = "쓰러진 곰인형";

    public Transform uprightPoint;
    public Transform grabPoint;
    public FirstPersonPlayer player;
    public PlayerMotionFeel motionFeel;
    public PlayerHand playerHand;

    public float reachDuration = 0.75f;
    public float standDuration = 1.2f;
    public float returnDuration = 0.65f;
    public float gripPause = 0.15f;

    public float cameraLowerAmount = 0.12f;
    public float cameraLookWeight = 0.8f;
    public float maxLookAngle = 35f;

    public string DisplayName => completed
        ? "정리된 곰인형"
        : displayName;

    bool playing;
    bool completed;

    public void Interact()
    {
        if (playing || completed)
            return;

        if (uprightPoint == null || grabPoint == null)
            return;

        if (player == null || playerHand == null)
            return;

        if (!playerHand.IsVisible || playerHand.Held != null)
            return;

        if (playerHand.handBone == null)
        {
            Debug.LogWarning("곰인형: Hand Bone이 연결되지 않았습니다.");
            return;
        }

        StartCoroutine(StandUpRoutine());
    }

    Quaternion GetLookRotation(Transform cameraRoot)
    {
        Vector3 direction = grabPoint.position - cameraRoot.position;

        if (direction.sqrMagnitude < 0.001f)
            return cameraRoot.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    IEnumerator StandUpRoutine()
    {
        playing = true;

        Transform cameraRoot = player.cameraRoot;
        Transform handBone = playerHand.handBone;

        if (cameraRoot == null || handBone == null)
        {
            playing = false;
            yield break;
        }

        PlayerInteractor interactor =
            player.GetComponentInChildren<PlayerInteractor>(true);

        bool oldPlayer = player.enabled;
        bool oldHand = playerHand.enabled;
        bool oldMotion = motionFeel != null && motionFeel.enabled;
        bool oldInteractor = interactor != null && interactor.enabled;

        Vector3 cameraStartPos = cameraRoot.localPosition;
        Quaternion cameraStartRot = cameraRoot.rotation;
        Vector3 handStartPos = handBone.localPosition;
        Quaternion handStartRot = handBone.localRotation;

        HandPose handPose = playerHand.GetComponent<HandPose>();
        float[] oldFingers = new float[5];

        if (handPose != null)
        {
            for (int i = 0; i < 5; i++)
                oldFingers[i] = handPose.GetFinger((HandPose.Finger)i);
        }

        Vector3 bearStartPos = transform.position;
        Quaternion bearStartRot = transform.rotation;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (interactor != null)
            interactor.enabled = false;

        if (motionFeel != null)
            motionFeel.enabled = false;

        player.enabled = false;
        playerHand.enabled = false;

        float time = 0f;

        while (time < reachDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(time / Mathf.Max(0.01f, reachDuration))
            );

            cameraRoot.localPosition = Vector3.Lerp(
                cameraStartPos,
                cameraStartPos + Vector3.down * cameraLowerAmount,
                t
            );

            Quaternion look = Quaternion.RotateTowards(
                cameraStartRot,
                GetLookRotation(cameraRoot),
                maxLookAngle
            );

            cameraRoot.rotation = Quaternion.Slerp(
                cameraStartRot,
                look,
                t * cameraLookWeight
            );

            Vector3 targetLocalPos = handBone.parent != null
                ? handBone.parent.InverseTransformPoint(grabPoint.position)
                : grabPoint.position;

            Quaternion targetLocalRot = handBone.parent != null
                ? Quaternion.Inverse(handBone.parent.rotation) * grabPoint.rotation
                : grabPoint.rotation;

            handBone.localPosition = Vector3.Lerp(
                handStartPos,
                targetLocalPos,
                t
            );

            handBone.localRotation = Quaternion.Slerp(
                handStartRot,
                targetLocalRot,
                t
            );

            yield return null;
        }

        playerHand.SetGrip(0.75f);

        yield return new WaitForSeconds(gripPause);

        time = 0f;

        while (time < standDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(time / Mathf.Max(0.01f, standDuration))
            );

            transform.position = Vector3.Lerp(
                bearStartPos,
                uprightPoint.position,
                t
            );

            transform.rotation = Quaternion.Slerp(
                bearStartRot,
                uprightPoint.rotation,
                t
            );

            Quaternion look = Quaternion.RotateTowards(
                cameraStartRot,
                GetLookRotation(cameraRoot),
                maxLookAngle
            );

            cameraRoot.rotation = Quaternion.Slerp(
                cameraRoot.rotation,
                look,
                Time.deltaTime * 6f * cameraLookWeight
            );

            handBone.position = grabPoint.position;
            handBone.rotation = grabPoint.rotation;

            yield return null;
        }

        transform.SetPositionAndRotation(
            uprightPoint.position,
            uprightPoint.rotation
        );

        time = 0f;

        while (time < returnDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(time / Mathf.Max(0.01f, returnDuration))
            );

            cameraRoot.localPosition = Vector3.Lerp(
                cameraStartPos + Vector3.down * cameraLowerAmount,
                cameraStartPos,
                t
            );

            cameraRoot.rotation = Quaternion.Slerp(
                cameraRoot.rotation,
                cameraStartRot,
                t
            );

            handBone.localPosition = Vector3.Lerp(
                handBone.localPosition,
                handStartPos,
                t
            );

            handBone.localRotation = Quaternion.Slerp(
                handBone.localRotation,
                handStartRot,
                t
            );

            yield return null;
        }

        cameraRoot.localPosition = cameraStartPos;
        cameraRoot.rotation = cameraStartRot;

        handBone.localPosition = handStartPos;
        handBone.localRotation = handStartRot;

        if (handPose != null)
        {
            for (int i = 0; i < 5; i++)
                handPose.SetFinger((HandPose.Finger)i, oldFingers[i]);
        }

        playerHand.enabled = oldHand;

        if (motionFeel != null)
            motionFeel.enabled = oldMotion;

        player.enabled = oldPlayer;

        if (interactor != null)
            interactor.enabled = oldInteractor;

        completed = true;
        playing = false;

        PlayerInteractor.ShowHint("곰인형을 제자리에 세웠다.");
    }
}
