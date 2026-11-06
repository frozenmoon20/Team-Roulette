
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class SofaPeekInteraction : MonoBehaviour, IInteractable, IHasName
{
    public FirstPersonPlayer player;
    public PlayerMotionFeel motionFeel;
    public PlayerHand playerHand;
    public GrabbableItem capsule;

    public Transform cameraPoint;
    public Transform handBracePoint;

    public float lowerDuration = 0.85f;
    public float riseDuration = 0.85f;
    public float pickupPause = 0.3f;

    public string DisplayName => "소파 아래 보기";

    bool playing;
    bool bracing;
    float braceAmount;

    Transform handBone;
    Vector3 handStartPosition;
    Quaternion handStartRotation;

    public void Interact()
    {
        if (playing)
            return;

        if (player == null || playerHand == null ||
            capsule == null || cameraPoint == null ||
            handBracePoint == null)
            return;

        if (playerHand.Held != null || !playerHand.IsVisible)
        {
            PlayerInteractor.ShowHint("먼저 손에 든 물건을 내려놓자.");
            return;
        }

        StartCoroutine(PeekRoutine());
    }

    void LateUpdate()
    {
        if (!bracing || handBone == null || handBracePoint == null)
            return;

        Vector3 targetPosition = handBracePoint.position;
        Quaternion targetRotation = handBracePoint.rotation;

        handBone.position = Vector3.Lerp(
            handBone.parent.TransformPoint(handStartPosition),
            targetPosition,
            braceAmount
        );

        handBone.rotation = Quaternion.Slerp(
            handBone.parent.rotation * handStartRotation,
            targetRotation,
            braceAmount
        );
    }

    IEnumerator PeekRoutine()
    {
        playing = true;

        Transform cameraRoot = player.cameraRoot;
        handBone = playerHand.handBone;

        if (cameraRoot == null || handBone == null)
        {
            playing = false;
            yield break;
        }

        bool oldPlayer = player.enabled;
        bool oldMotion = motionFeel != null && motionFeel.enabled;

        Vector3 cameraStartPosition = cameraRoot.position;
        Quaternion cameraStartRotation = cameraRoot.rotation;

        handStartPosition = handBone.localPosition;
        handStartRotation = handBone.localRotation;

        Collider targetCollider = GetComponent<Collider>();

        if (targetCollider != null)
            targetCollider.enabled = false;

        player.enabled = false;

        if (motionFeel != null)
            motionFeel.enabled = false;

        bracing = true;

        yield return MoveCamera(
            cameraRoot,
            cameraStartPosition,
            cameraStartRotation,
            cameraPoint.position,
            cameraPoint.rotation,
            lowerDuration,
            true
        );

        PlayerInteractor.ShowHint("소파 아래에 무언가 있다.");

        bool pickedUp = false;

        while (!pickedUp)
        {
            if (playerHand.Held == capsule)
            {
                pickedUp = true;
                break;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                break;

            yield return null;
        }

        if (pickedUp)
        {
            bracing = false;
            handBone.localPosition = handStartPosition;
            handBone.localRotation = handStartRotation;

            yield return new WaitForSeconds(pickupPause);
        }

        yield return MoveCamera(
            cameraRoot,
            cameraRoot.position,
            cameraRoot.rotation,
            cameraStartPosition,
            cameraStartRotation,
            riseDuration,
            !pickedUp
        );

        bracing = false;
        braceAmount = 0f;

        cameraRoot.SetPositionAndRotation(
            cameraStartPosition,
            cameraStartRotation
        );

        if (!pickedUp)
        {
            handBone.localPosition = handStartPosition;
            handBone.localRotation = handStartRotation;
        }

        if (motionFeel != null)
            motionFeel.enabled = oldMotion;

        player.enabled = oldPlayer;

        if (targetCollider != null)
            targetCollider.enabled = true;

        playing = false;
    }

    IEnumerator MoveCamera(
        Transform cameraRoot,
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        float duration,
        bool animateBrace
    )
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(timer / Mathf.Max(0.01f, duration))
            );

            cameraRoot.position = Vector3.Lerp(
                fromPosition,
                toPosition,
                t
            );

            cameraRoot.rotation = Quaternion.Slerp(
                fromRotation,
                toRotation,
                t
            );

            if (animateBrace)
            {
                if (toPosition == cameraPoint.position)
                    braceAmount = t;
                else
                    braceAmount = 1f - t;
            }

            yield return null;
        }

        cameraRoot.SetPositionAndRotation(toPosition, toRotation);
    }
}
