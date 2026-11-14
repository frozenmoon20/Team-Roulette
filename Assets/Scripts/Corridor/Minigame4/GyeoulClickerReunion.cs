
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class GyeoulClickerReunion : MonoBehaviour
{
    [Header("플레이어")]
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;
    public ClickerPocketController clickerController;

    [Header("재회 위치")]
    public Transform reunionPoint;
    public float activationDistance = 4f;

    [Header("겨울이")]
    public GameObject gyeoul;
    public Transform gyeoulSitPoint;
    public GyeoulFollowPlayer gyeoulFollower;

    [Header("겨울이 애니메이션")]
    public DogAnimator gyeoulAnimator;

    [Header("겨울이 바라보기")]
    public Transform gyeoulLookTarget;
    public float dogLookHeight = 0.55f;

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
    private CorridorCutsceneWalkSilencer walkSilencer;

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

        if (gyeoulAnimator == null && gyeoul != null)
            gyeoulAnimator =
                gyeoul.GetComponentInChildren<DogAnimator>(true);
    }

    private void Start()
    {
        if (gyeoulFollower != null)
            gyeoulFollower.enabled = false;

        if (gyeoul != null)
            gyeoul.SetActive(false);
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
            PlayBark();

            if (!playingBark)
                StartCoroutine(BarkAndActivate());

            return;
        }

        if (player == null ||
            player.cameraRoot == null ||
            reunionPoint == null ||
            gyeoul == null ||
            gyeoulSitPoint == null)
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

walkSilencer =
    CorridorCutsceneWalkSilencer.GetOrCreate(player);

if (walkSilencer != null)
    walkSilencer.Begin();


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

        gyeoul.transform.SetPositionAndRotation(
            gyeoulSitPoint.position,
            gyeoulSitPoint.rotation
        );

        gyeoul.SetActive(true);

        if (gyeoulFollower != null)
            gyeoulFollower.enabled = false;

        if (gyeoulAnimator == null)
            gyeoulAnimator =
                gyeoul.GetComponentInChildren<DogAnimator>(true);

        Vector3 dogDirection =
            player.transform.position - gyeoul.transform.position;

        dogDirection.y = 0f;

        if (dogDirection.sqrMagnitude > 0.001f)
        {
            gyeoul.transform.rotation =
                Quaternion.LookRotation(dogDirection);
        }

        yield return null;

        PlayBark();

        yield return new WaitForSeconds(
            Mathf.Max(0f, turnDelay)
        );

        Vector3 targetPoint = gyeoulLookTarget != null
            ? gyeoulLookTarget.position
            : gyeoul.transform.position +
              Vector3.up * dogLookHeight;

        Vector3 horizontalDirection =
            targetPoint - player.transform.position;

        horizontalDirection.y = 0f;

        Quaternion startRotation =
            player.transform.rotation;

        Quaternion endRotation = startRotation;

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            endRotation =
                Quaternion.LookRotation(horizontalDirection);
        }

        Camera viewCamera = null;

        if (interactor != null)
            viewCamera = interactor.playerCamera;

        if (viewCamera == null)
            viewCamera = Camera.main;

        Vector3 cameraPosition = viewCamera != null
            ? viewCamera.transform.position
            : player.cameraRoot.position;

        Vector3 lookDirection =
            targetPoint - cameraPosition;

        float horizontalDistance = Mathf.Sqrt(
            lookDirection.x * lookDirection.x +
            lookDirection.z * lookDirection.z
        );

        float targetPitch = -Mathf.Atan2(
            lookDirection.y,
            Mathf.Max(0.01f, horizontalDistance)
        ) * Mathf.Rad2Deg;

        targetPitch = Mathf.Clamp(
            targetPitch,
            -player.maxPitch,
            player.maxPitch
        );

        float startPitch = Mathf.DeltaAngle(
            0f,
            player.cameraRoot.localEulerAngles.x
        );

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

            player.transform.rotation =
                Quaternion.Slerp(
                    startRotation,
                    endRotation,
                    t
                );

            float currentPitch = Mathf.LerpAngle(
                startPitch,
                targetPitch,
                t
            );

            player.SetLookPitch(currentPitch);

            yield return null;
        }

        player.transform.rotation = endRotation;
        player.SetLookPitch(targetPitch);

        if (gyeoulAnimator != null)
            gyeoulAnimator.Sit();

        yield return new WaitForSeconds(
            Mathf.Max(0f, lookDuration)
        );

        
joined = true;

// 겨울이 재회 컷신 종료: 걷기 효과 복구
if (walkSilencer != null)
{
    walkSilencer.End();
    walkSilencer = null;
}

player.enabled = previousPlayerEnabled;


        if (interactor != null)
            interactor.enabled = previousInteractorEnabled;

        if (clickerController != null)
            clickerController.enabled = previousClickerEnabled;

        if (gyeoulFollower != null)
            gyeoulFollower.StartFollowing(player.transform);

        playingCutscene = false;

        PlayerInteractor.ShowHint(
            "겨울이가 함께한다."
        );

        Debug.Log("겨울이 재회 컷신 완료!");
    }

    private IEnumerator BarkAndActivate()
    {
        playingBark = true;

        yield return new WaitForSeconds(
            Mathf.Max(0f, barkActionDelay)
        );

        onDogCommand?.Invoke();

        playingBark = false;
    }

    private void PlayBark()
    {
        if (gyeoulAnimator != null)
            gyeoulAnimator.Bark();

        if (barkAudioSource != null && barkSound != null)
            barkAudioSource.PlayOneShot(barkSound);
    }
}
