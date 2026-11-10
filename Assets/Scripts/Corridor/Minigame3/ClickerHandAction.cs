using System.Collections;
using UnityEngine;

// Attach to the same GameObject as ClickerPocketController.
// Hook ClickerPocketController.onClickerPressed to Press().
public class ClickerHandAction : MonoBehaviour
{
    [Header("Required references")]
    public GrabbableItem clicker;
    public HandPose handPose;
    public Transform buttonPart;

    [Header("Clicker-only holding pose (0~1)")]
    [Range(0f, 1f)] public float thumbReadyCurl = 0.48f;
    [Range(0f, 1f)] public float thumbPressedCurl = 0.83f;
    [Range(0f, 1f)] public float indexCurl = 0.65f;
    [Range(0f, 1f)] public float middleCurl = 0.78f;
    [Range(0f, 1f)] public float ringCurl = 0.78f;
    [Range(0f, 1f)] public float pinkyCurl = 0.72f;

    [Header("Button movement (local space)")]
    public Vector3 buttonPressOffset = new Vector3(0f, -0.003f, 0f);
    [Min(0.01f)] public float pressDownDuration = 0.12f;
    [Min(0.01f)] public float releaseDuration = 0.16f;

    [Header("Click sound")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    private bool wasHolding;
    private Vector3 buttonRestLocalPosition;
    private Coroutine pressRoutine;

    private void Awake()
    {
        if (buttonPart != null)
            buttonRestLocalPosition = buttonPart.localPosition;
    }

    private void Update()
    {
        PlayerHand hand = PlayerHand.Instance;
        bool isHolding = clicker != null && hand != null && hand.Held == clicker;

        if (isHolding && !wasHolding)
            SetClickerHoldingPose();

        if (!isHolding && wasHolding)
        {
            StopPressAnimation();
            RestoreButtonPosition();
            // PlayerHand.LetGo() restores the previous finger pose.
        }

        wasHolding = isHolding;
    }

    private void SetClickerHoldingPose()
    {
        if (handPose == null)
            return;

        handPose.SetFinger(HandPose.Finger.Thumb, thumbReadyCurl);
        handPose.SetFinger(HandPose.Finger.Index, indexCurl);
        handPose.SetFinger(HandPose.Finger.Middle, middleCurl);
        handPose.SetFinger(HandPose.Finger.Ring, ringCurl);
        handPose.SetFinger(HandPose.Finger.Pinky, pinkyCurl);
    }

    // Select this method in ClickerPocketController -> On Clicker Pressed.
    public void Press()
    {
        PlayerHand hand = PlayerHand.Instance;
        if (clicker == null || hand == null || hand.Held != clicker)
            return;

        if (pressRoutine != null)
            return;

        pressRoutine = StartCoroutine(PressAnimation());
    }

    private IEnumerator PressAnimation()
    {
        if (handPose != null)
            handPose.SetFinger(HandPose.Finger.Thumb, thumbPressedCurl);

        if (buttonPart != null)
        {
            yield return MoveButton(
                buttonRestLocalPosition,
                buttonRestLocalPosition + buttonPressOffset,
                pressDownDuration
            );
        }
        else
        {
            yield return new WaitForSeconds(pressDownDuration);
        }

        // Play at the end of the downward press for a synced click.
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        if (buttonPart != null)
        {
            yield return MoveButton(
                buttonRestLocalPosition + buttonPressOffset,
                buttonRestLocalPosition,
                releaseDuration
            );
        }
        else
        {
            yield return new WaitForSeconds(releaseDuration);
        }

        if (clicker != null && PlayerHand.Instance != null &&
            PlayerHand.Instance.Held == clicker && handPose != null)
        {
            handPose.SetFinger(HandPose.Finger.Thumb, thumbReadyCurl);
        }

        RestoreButtonPosition();
        pressRoutine = null;
    }

    private IEnumerator MoveButton(Vector3 start, Vector3 end, float duration)
    {
        if (buttonPart == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            buttonPart.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }

        buttonPart.localPosition = end;
    }

    private void StopPressAnimation()
    {
        if (pressRoutine == null)
            return;

        StopCoroutine(pressRoutine);
        pressRoutine = null;
    }

    private void RestoreButtonPosition()
    {
        if (buttonPart != null)
            buttonPart.localPosition = buttonRestLocalPosition;
    }

    private void OnDisable()
    {
        StopPressAnimation();
        RestoreButtonPosition();
    }
}
