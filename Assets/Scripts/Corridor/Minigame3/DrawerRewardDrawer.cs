
using System.Collections;
using UnityEngine;

public class DrawerRewardDrawer : MonoBehaviour
{
    [Header("잠긴 보상 서랍")]
    public Transform lockedDrawer;

    [Header("열리는 방향")]
    public Vector3 openOffset =
        new Vector3(-1.5f, 0f, 0f);

    public float unlockDelay = 0.4f;
    public float openDuration = 1.5f;

    [Header("효과음")]
    public AudioSource audioSource;
    public AudioClip unlockSound;
    public AudioClip slideSound;

    [Header("보상 클릭커")]
    public GrabbableItem clicker;

    bool opening;
    bool opened;

    public void Prepare(DrawerPuzzleManager manager)
    {
        if (clicker != null)
        {
            clicker.enabled = false;
            clicker.gameObject.SetActive(false);
        }
    }

    public void OpenDrawer()
    {
        if (opening || opened)
            return;

        if (lockedDrawer == null)
        {
            Debug.LogError(
                "보상 서랍이 연결되지 않았습니다.", this
            );
            return;
        }

        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        opening = true;

        // 잠금 해제 소리
        if (audioSource != null && unlockSound != null)
            audioSource.PlayOneShot(unlockSound);

        yield return new WaitForSeconds(
            Mathf.Max(0f, unlockDelay)
        );

        // 클릭커는 보이지만 아직 집을 수 없음
        if (clicker != null)
        {
            clicker.gameObject.SetActive(true);
            clicker.enabled = false;
        }

        if (audioSource != null && slideSound != null)
            audioSource.PlayOneShot(slideSound);

        Vector3 start = lockedDrawer.localPosition;
        Vector3 target = start + openOffset;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            lockedDrawer.localPosition =
                Vector3.Lerp(start, target, t);

            yield return null;
        }

        lockedDrawer.localPosition = target;

        opened = true;
        opening = false;

        // 서랍이 열린 후부터 클릭커를 집을 수 있음
        if (clicker != null)
            clicker.enabled = true;

        PlayerInteractor.ShowHint(
            "잠겨 있던 서랍이 열렸다."
        );
    }
}
