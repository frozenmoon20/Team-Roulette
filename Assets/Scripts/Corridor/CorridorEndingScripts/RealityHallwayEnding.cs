
using System.Collections;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.CorridorEnding
{
    // 현실 복도:
    // 흰색 페이드 해제
    // -> 이마 짚는 손 + 독백
    // -> 손 내리기
    // -> 전화기 바라보기
    // -> 조작권 회복
    // -> 전화기 클릭
    // -> 전화벨 중단 + 친구와 대화
    public class RealityHallwayEnding :
        MonoBehaviour, IInteractable, IHasName
    {
        private enum RealityPhase
        {
            Intro,
            WaitingForPhone,
            Calling,
            Finished
        }

        [Header("플레이어와 UI")]
        public FirstPersonPlayer player;
        public PlayerInteractor interactor;
        public GameUI gameUI;

        [Header("현실 복도 대사")]
        public DialogueSequence wakeupDialogue;
        public DialogueSequence friendPhoneDialogue;

        public string phoneInteractionName = "전화 받기";

        public float waitAfterWhiteFade = 0.6f;
        public float foreheadHoldBeforeDialogue = 0.4f;

        [Header("이마를 짚는 손 연출")]
        public GameObject introHandVisual;

        [Tooltip("카메라 기준 손 위치")]
        public Vector3 foreheadHandLocalPosition =
            new Vector3(0.08f, -0.18f, 0.38f);

        [Tooltip("카메라 기준 손 회전")]
        public Vector3 foreheadHandLocalEuler =
            new Vector3(-35f, -15f, 45f);

        public Vector3 loweredHandLocalPosition =
            new Vector3(0.35f, -0.65f, 0.4f);

        public float handLowerDuration = 0.8f;

        [Header("평소 손 숨기기")]
        [Tooltip("PlayerCameraRoot/Camera/HandRoot 연결")]
        public Transform normalHandRoot;

        public bool hideNormalHandDuringIntro = true;

        [Header("전화기를 바라보는 연출")]
        public Transform phoneLookTarget;

        public float lookAtPhoneDuration = 1.2f;
        public float pauseBeforeControlReturn = 0.25f;

        [Header("전화 효과음")]
        [Tooltip("현실 복도 씬 단독 테스트용 전화벨")]
        public AudioClip testRingWhenStartingHere;

        public AudioSource phoneAudioSource;
        public AudioClip answerSound;

        [Range(0f, 1f)]
        public float answerVolume = 1f;

        public float ringFadeOutTime = 0.6f;

        [Header("통화 완료")]
        public GameObject endingPanel;
        public UnityEvent onCallFinished;

        private RealityPhase phase = RealityPhase.Intro;

        private bool savedPlayerEnabled;
        private bool savedInteractorEnabled;

        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible;
        private bool controlsLocked;

        // 평소 손 렌더러 복구용
        private Renderer[] normalHandRenderers;
        private bool[] originalRendererStates;

        // 이마 손의 원래 위치 복구용
        private Transform handOriginalParent;
        private Vector3 handOriginalLocalPosition;
        private Quaternion handOriginalLocalRotation;
        private Vector3 handOriginalLocalScale;
        private bool introHandPrepared;

        public string DisplayName =>
            phase == RealityPhase.WaitingForPhone
                ? phoneInteractionName
                : "";

        private void Start()
        {
            if (endingPanel != null)
                endingPanel.SetActive(false);

            if (player == null)
                player =
                    FindFirstObjectByType<FirstPersonPlayer>();

            if (interactor == null)
                interactor =
                    FindFirstObjectByType<PlayerInteractor>();

            if (gameUI == null)
                gameUI =
                    FindFirstObjectByType<GameUI>();

            if (player == null ||
                player.cameraRoot == null ||
                interactor == null ||
                gameUI == null ||
                wakeupDialogue == null ||
                friendPhoneDialogue == null)
            {
                Debug.LogError(
                    "RealityHallwayEnding: 필수 참조를 확인하세요.",
                    this
                );

                enabled = false;
                return;
            }

            // Normal Hand Root가 비어 있다면
            // 기존 PlayerHand에서 자동으로 찾기
            if (normalHandRoot == null &&
                PlayerHand.Instance != null)
            {
                normalHandRoot =
                    PlayerHand.Instance.transform;
            }

            // 복도 엔딩에서 넘어온 경우
            // 기존 전화벨을 그대로 이어서 사용
            if (EndingPhoneRing.Instance == null &&
                testRingWhenStartingHere != null)
            {
                EndingPhoneRing.Begin(
                    testRingWhenStartingHere,
                    0.55f,
                    0.55f,
                    0.1f,
                    22000f
                );
            }

            // 시작 즉시 조작권 잠금
            LockControls();

            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            // 이마 짚는 손 표시
            PrepareIntroHand();

            // 흰색 화면이 완전히 걷힐 때까지 대기
            while (SceneFader.IsBusy)
                yield return null;

            yield return new WaitForSeconds(
                Mathf.Max(0f, waitAfterWhiteFade)
            );

            yield return new WaitForSeconds(
                Mathf.Max(0f, foreheadHoldBeforeDialogue)
            );

            // 독백:
            // "……내가 뭐 하고 있었지?"
           yield return EndingDialogueBridge.Play(
    gameUI, friendPhoneDialogue
);

// 친구와의 통화가 끝나면 수화기 내려놓는 소리
if (phoneAudioSource != null && answerSound != null)
{
    phoneAudioSource.PlayOneShot(
        answerSound,
        answerVolume
    );
}
else if (answerSound != null)
{
    AudioManager.Play(
        answerSound,
        answerVolume
    );
}

phase = RealityPhase.Finished;

            // 손을 천천히 내리기
            yield return LowerIntroHand();

            // 이마 손을 숨기고 평소 손 복구
            HideIntroHand();

            // 전화기를 바라보기
            yield return LookAtPhone();

            yield return new WaitForSeconds(
                Mathf.Max(
                    0f,
                    pauseBeforeControlReturn
                )
            );

            // 전화기 상호작용 허용
            phase = RealityPhase.WaitingForPhone;

            // 조작권 회복
            RestoreControls();

            PlayerInteractor.ShowHint(
                "전화벨이 울리고 있다."
            );
        }

        private void PrepareIntroHand()
        {
            if (introHandVisual == null)
                return;

            Camera cam =
                interactor.playerCamera != null
                    ? interactor.playerCamera
                    : Camera.main;

            if (cam == null)
                return;

            // 평소 손 모델을 렌더러 단위로 숨김
            HideNormalHandRenderers();

            Transform hand = introHandVisual.transform;

            // 기존 부모와 위치 저장
            handOriginalParent = hand.parent;
            handOriginalLocalPosition =
                hand.localPosition;

            handOriginalLocalRotation =
                hand.localRotation;

            handOriginalLocalScale =
                hand.localScale;

            introHandPrepared = true;

            // 카메라에 부착
            hand.SetParent(cam.transform, false);

            // 이마 짚는 손 위치
            hand.localPosition =
                foreheadHandLocalPosition;

            // 이마 짚는 손 회전
            hand.localRotation =
                Quaternion.Euler(
                    foreheadHandLocalEuler
                );

            // 기존 크기 유지
            hand.localScale = handOriginalLocalScale;

            introHandVisual.SetActive(true);
        }

        // 평소 손 렌더러 숨기기
        private void HideNormalHandRenderers()
        {
            if (!hideNormalHandDuringIntro)
                return;

            if (normalHandRoot == null)
                return;

            // 복제 손이 평소 손의 자식이라면
            // 부모를 잘못 설정한 것이므로 경고
            if (introHandVisual != null &&
                introHandVisual.transform.IsChildOf(
                    normalHandRoot
                ))
            {
                Debug.LogWarning(
                    "IntroHandVisual은 HandRoot의 " +
                    "자식이 아닌 별도 오브젝트로 배치하세요.",
                    this
                );

                return;
            }

            normalHandRenderers =
                normalHandRoot
                    .GetComponentsInChildren<Renderer>(
                        true
                    );

            originalRendererStates =
                new bool[normalHandRenderers.Length];

            for (int i = 0;
                i < normalHandRenderers.Length;
                i++)
            {
                Renderer renderer =
                    normalHandRenderers[i];

                if (renderer == null)
                    continue;

                // 원래 상태 기억
                originalRendererStates[i] =
                    renderer.enabled;

                // 컷신 동안 숨기기
                renderer.enabled = false;
            }
        }

        // 평소 손 렌더러 복구
        private void RestoreNormalHandRenderers()
        {
            if (normalHandRenderers == null ||
                originalRendererStates == null)
                return;

            int count = Mathf.Min(
                normalHandRenderers.Length,
                originalRendererStates.Length
            );

            for (int i = 0; i < count; i++)
            {
                if (normalHandRenderers[i] != null)
                {
                    normalHandRenderers[i].enabled =
                        originalRendererStates[i];
                }
            }

            normalHandRenderers = null;
            originalRendererStates = null;
        }

        // 이마 손을 아래로 내리는 애니메이션
        private IEnumerator LowerIntroHand()
        {
            if (introHandVisual == null ||
                !introHandVisual.activeSelf)
                yield break;

            Transform hand =
                introHandVisual.transform;

            Vector3 start = hand.localPosition;

            float duration = Mathf.Max(
                0.01f,
                handLowerDuration
            );

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(
                        elapsed / duration
                    )
                );

                hand.localPosition = Vector3.Lerp(
                    start,
                    loweredHandLocalPosition,
                    t
                );

                yield return null;
            }

            hand.localPosition =
                loweredHandLocalPosition;
        }

        // 이마 손 숨기기
        private void HideIntroHand()
        {
            if (introHandVisual != null &&
                introHandPrepared)
            {
                Transform hand =
                    introHandVisual.transform;

                introHandVisual.SetActive(false);

                // 원래 부모로 복구
                hand.SetParent(
                    handOriginalParent,
                    false
                );

                hand.localPosition =
                    handOriginalLocalPosition;

                hand.localRotation =
                    handOriginalLocalRotation;

                hand.localScale =
                    handOriginalLocalScale;

                introHandPrepared = false;
            }

            // 평소 손 다시 표시
            RestoreNormalHandRenderers();
        }

        // 카메라가 전화기를 바라보는 연출
        private IEnumerator LookAtPhone()
        {
            if (phoneLookTarget == null)
            {
                Debug.LogWarning(
                    "RealityHallwayEnding: " +
                    "Phone Look Target을 연결하세요.",
                    this
                );

                yield break;
            }

            Transform cameraTransform =
                interactor.playerCamera != null
                    ? interactor.playerCamera.transform
                    : player.cameraRoot;

            Vector3 direction =
                phoneLookTarget.position -
                cameraTransform.position;

            Vector3 horizontal =
                new Vector3(
                    direction.x,
                    0f,
                    direction.z
                );

            Quaternion startYaw =
                player.transform.rotation;

            Quaternion targetYaw =
                horizontal.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(horizontal)
                    : startYaw;

            float horizontalDistance =
                Mathf.Max(
                    0.01f,
                    horizontal.magnitude
                );

            float targetPitch = Mathf.Clamp(
                -Mathf.Atan2(
                    direction.y,
                    horizontalDistance
                ) * Mathf.Rad2Deg,
                -player.maxPitch,
                player.maxPitch
            );

            float startPitch = player.Pitch;
            float elapsed = 0f;

            float duration = Mathf.Max(
                0.01f,
                lookAtPhoneDuration
            );

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(
                        elapsed / duration
                    )
                );

                // 플레이어 몸 회전
                player.transform.rotation =
                    Quaternion.Slerp(
                        startYaw,
                        targetYaw,
                        t
                    );

                // 카메라 상하 회전
                player.SetLookPitch(
                    Mathf.LerpAngle(
                        startPitch,
                        targetPitch,
                        t
                    )
                );

                yield return null;
            }

            player.transform.rotation = targetYaw;
            player.SetLookPitch(targetPitch);
        }

        // 전화기 클릭
        public void Interact()
        {
            if (phase != RealityPhase.WaitingForPhone)
                return;

            phase = RealityPhase.Calling;

            StartCoroutine(PlayCall());
        }

        // 친구와 통화
        private IEnumerator PlayCall()
        {
            LockControls();

            // 전화벨 종료
            EndingPhoneRing.StopRinging(
                ringFadeOutTime
            );

            // 수화기 드는 효과음 (선택)
            if (phoneAudioSource != null &&
                answerSound != null)
            {
                phoneAudioSource.PlayOneShot(
                    answerSound,
                    answerVolume
                );
            }
            else if (answerSound != null)
            {
                AudioManager.Play(
                    answerSound,
                    answerVolume
                );
            }

            // 친구와 대화
            yield return EndingDialogueBridge.Play(
                gameUI,
                friendPhoneDialogue
            );

            phase = RealityPhase.Finished;

            onCallFinished?.Invoke();

            if (endingPanel != null)
            {
                endingPanel.SetActive(true);
            }
            else
            {
                RestoreControls();
            }
        }

        // 조작권 잠금
        private void LockControls()
        {
            if (controlsLocked)
                return;

            savedPlayerEnabled = player.enabled;
            savedInteractorEnabled = interactor.enabled;

            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;

            controlsLocked = true;

            player.StopMovement();

            player.enabled = false;
            interactor.enabled = false;

            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }

        // 조작권 복구
        private void RestoreControls()
        {
            if (!controlsLocked)
                return;

            controlsLocked = false;

            player.enabled = savedPlayerEnabled;
            interactor.enabled = savedInteractorEnabled;

            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }

        private void OnDisable()
        {
            // 연출 도중 비활성화되어도
            // 손 모델은 정상 복구
            HideIntroHand();

            if (controlsLocked &&
                player != null &&
                interactor != null)
            {
                RestoreControls();
            }
        }
    }
}
