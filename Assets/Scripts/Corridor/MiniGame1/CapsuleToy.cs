
using System.Collections;
using UnityEngine;

public class CapsuleToy : MonoBehaviour
{
    public ShakeableItem shakeableItem;
    public Transform inspectPoint;

    public Transform topPart;
    public Transform bottomPart;

    public GameObject keyObject;

    public Vector3 inspectRotation =
        new Vector3(90f, 0f, 0f);

    public float inspectScale = 1.5f;

    public Vector3 topOpenOffset =
        new Vector3(0f, 0.05f, 0f);

    public Vector3 bottomOpenOffset =
        new Vector3(0f, -0.05f, 0f);

    public float openDelay = 0.2f;
    public float openDuration = 0.4f;
    public float keyVisibleTime = 5f;

    [Header("캡슐 및 열쇠 효과음")]
    public AudioClip capsuleOpenSound;
    public AudioClip keyCollectSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    Vector3 topClosedPosition;
    Vector3 bottomClosedPosition;
    Vector3 originalScale;

    bool inspecting;
    bool opened;
    bool keySoundPlayed;

    public bool IsInspecting => inspecting;
    public bool IsOpened => opened;

    void Awake()
    {
        originalScale = transform.localScale;

        if (topPart != null)
            topClosedPosition = topPart.localPosition;

        if (bottomPart != null)
            bottomClosedPosition = bottomPart.localPosition;

        if (keyObject != null)
            keyObject.SetActive(false);
    }

    public bool TryOpen()
    {
        if (inspecting)
            return true;

        if (shakeableItem == null ||
            !shakeableItem.IsDiscovered)
            return false;

        if (inspectPoint == null)
            return false;

        inspecting = true;

        shakeableItem.enabled = false;

        transform.SetParent(inspectPoint);

        transform.localPosition = Vector3.zero;
        transform.localRotation =
            Quaternion.Euler(inspectRotation);

        transform.localScale =
            originalScale * inspectScale;

        StartCoroutine(OpenCapsule());

        return true;
    }

    IEnumerator OpenCapsule()
    {
        yield return new WaitForSeconds(openDelay);

        if (topPart == null || bottomPart == null)
            yield break;

        // 실제 개봉 애니메이션 시작 시점
        AudioManager.Play(
            capsuleOpenSound,
            soundVolume
        );

        if (keyObject != null)
            keyObject.SetActive(true);

        Vector3 topStart = topPart.localPosition;
        Vector3 bottomStart = bottomPart.localPosition;

        Vector3 topTarget =
            topClosedPosition + topOpenOffset;

        Vector3 bottomTarget =
            bottomClosedPosition + bottomOpenOffset;

        float time = 0f;

        while (time < openDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(
                time / Mathf.Max(0.01f, openDuration)
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            topPart.localPosition = Vector3.Lerp(
                topStart,
                topTarget,
                t
            );

            bottomPart.localPosition = Vector3.Lerp(
                bottomStart,
                bottomTarget,
                t
            );

            yield return null;
        }

        topPart.localPosition = topTarget;
        bottomPart.localPosition = bottomTarget;

        opened = true;

        PlayerInteractor.ShowHint(
            "아, 여기에 키가 있었네."
        );

        yield return new WaitForSeconds(keyVisibleTime);

        // 실제 열쇠 획득 처리
        MiniGame1State.HasKey = true;

        if (!keySoundPlayed)
        {
            keySoundPlayed = true;

            AudioManager.Play(
                keyCollectSound,
                soundVolume
            );
        }

        if (keyObject != null)
            keyObject.SetActive(false);

        PlayerHand hand = PlayerHand.Instance;

        if (hand != null && hand.Held != null)
        {
            hand.PlaceHeld(transform, true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
