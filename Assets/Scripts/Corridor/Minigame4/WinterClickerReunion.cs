
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WinterClickerReunion : MonoBehaviour
{
    [Header("플레이어")]
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;
    public ClickerPocketController clickerController;

    [Header("재회 위치")]
    public Transform reunionPoint;
    public float activationDistance = 4f;

    [Header("겨울이")]
    public GameObject winter;
    public Transform winterSitPoint;
    public WinterFollowPlayer winterFollower;

    [Header("겨울이 짖는 소리")]
    public AudioSource barkAudioSource;
    public AudioClip barkSound;

    [Header("컷신 시간")]
    public float turnDelay = 0.5f;
    public float turnDuration = 1.7f;
    public float lookDuration = 1.8f;

    [Header("재회 이후 클릭커")]
    public float barkActionDelay = 0.35f;
    public UnityEvent onDogCommand;

    private int clickCount;
    private bool joined;
    private bool playingCutscene;
    private bool playingBark;

    public bool HasJoined => joined;

    private void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<FirstPersonPlayer>();

        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();

        if (clickerController == null)
            clickerController =
                FindFirstObjectByType<ClickerPocketController>();
    }

    private void Start()
    {
        if (winterFollower != null)
            winterFollower.enabled = false;

        if (winter != null)
            winter.SetActive(false);
    }

    public void OnClickerPressed()
    {
        if (playingCutscene)
            return;

        if (clickerController != null &&
            !clickerController.IsHoldingClicker)
            return;

        if (joined)
        {
            if (!playingBark)
                StartCoroutine(BarkAndActivate());

            return;
        }

        if (player == null ||
            reunionPoint == null ||
            winter == null ||
            winterSitPoint == null)
        {
            Debug.LogWarning(
                "겨울이 재회 연출의 참조를 확인해 주세요.",
                this
            );
            return;
        }

        Vector3 offset =
            player.transform.position - reunionPoint.position;

        offset.y = 0f;

        if (offset.sqrMagnitude >
            activationDistance * activationDistance)
            return;

        clickCount++;

        if (clickCount == 1)
        {
            PlayerInteractor.ShowHint(
                "이번에는 아무 반응이 없다."
            );
            return;
        }

        StartCoroutine(PlayReunionCutscene());
    }

    private IEnumerator PlayReunionCutscene()
    {
        playingCutscene = true;

        bool previousPlayerEnabled = player.enabled;

        bool previousInteractorEnabled =
            interactor != null && interactor.enabled;

        bool previousClickerEnabled =
            clickerController != null && clickerController.enabled;

        player.enabled = false;

        if (interactor != null)
            interactor.enabled = false;

        if (clickerController != null)
            clickerController.enabled = false;

        yield return new WaitForSeconds(0.15f);

        winter.transform.SetPositionAndRotation(
            winterSitPoint.position,
            winterSitPoint.rotation
        );

        winter.SetActive(true);

        if (winterFollower != null)
            winterFollower.enabled = false;

        Vector3 dogDirection =
            player.transform.position - winter.transform.position;

        dogDirection.y = 0f;

        if (dogDirection.sqrMagnitude > 0.001f)
        {
            winter.transform.rotation =
                Quaternion.LookRotation(dogDirection);
        }

        PlayBark();

        yield return new WaitForSeconds(
            Mathf.Max(0f, turnDelay)
        );

        Vector3 lookDirection =
            winter.transform.position - player.transform.position;

        lookDirection.y = 0f;

        Quaternion startRotation = player.transform.rotation;

        Quaternion endRotation = startRotation;

        if (lookDirection.sqrMagnitude > 0.001f)
            endRotation = Quaternion.LookRotation(lookDirection);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, turnDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            player.transform.rotation = Quaternion.Slerp(
                startRotation,
                endRotation,
                t
            );

            yield return null;
        }

        player.transform.rotation = endRotation;

        yield return new WaitForSeconds(
            Mathf.Max(0f, lookDuration)
        );

        joined = true;

        player.enabled = previousPlayerEnabled;

        if (interactor != null)
            interactor.enabled = previousInteractorEnabled;

        if (clickerController != null)
            clickerController.enabled = previousClickerEnabled;

        if (winterFollower != null)
            winterFollower.StartFollowing(player.transform);

        playingCutscene = false;

        PlayerInteractor.ShowHint(
            "겨울이가 함께한다."
        );

        Debug.Log("겨울이 재회 컷신 완료!");
    }

    private IEnumerator BarkAndActivate()
    {
        playingBark = true;

        PlayBark();

        yield return new WaitForSeconds(
            Mathf.Max(0f, barkActionDelay)
        );

        onDogCommand?.Invoke();

        playingBark = false;
    }

    private void PlayBark()
    {
        if (barkAudioSource != null && barkSound != null)
            barkAudioSource.PlayOneShot(barkSound);
    }
}
