using System.Collections;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.CorridorEnding
{
    // 현실 복도: 흰색 페이드 해제 -> 이마 짚는 손 + 독백 -> 전화기를 바라봄
    // -> 조작권 회복 -> 전화기 클릭 -> 전화벨 중단 + 친구 대화.
    // 전화기 Collider가 붙은 오브젝트(또는 그 부모)에 부착하세요.
    public class RealityHallwayEnding : MonoBehaviour, IInteractable, IHasName
    {
        private enum RealityPhase { Intro, WaitingForPhone, Calling, Finished }

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
        [Tooltip("컷신에서만 표시할 별도 손 모델. 기존 플레이어 손 모델과 분리하는 것을 권장합니다.")]
        public GameObject introHandVisual;
        [Tooltip("손 연출 위치는 카메라 기준 로컬 좌표입니다.")]
        public Vector3 foreheadHandLocalPosition = new Vector3(0.12f, 0.18f, 0.32f);
        public Vector3 foreheadHandLocalEuler = new Vector3(-35f, -15f, 45f);
        public Vector3 loweredHandLocalPosition = new Vector3(0.35f, -0.65f, 0.4f);
        public float handLowerDuration = 0.8f;
        [Tooltip("연출 중 평소 플레이어 손 모델을 잠시 숨깁니다.")]
        public bool hideNormalHandDuringIntro = true;

        [Header("전화기를 바라보는 연출")]
        [Tooltip("전화기 수화기 근처에 배치한 빈 오브젝트를 연결하세요.")]
        public Transform phoneLookTarget;
        public float lookAtPhoneDuration = 1.2f;
        public float pauseBeforeControlReturn = 0.25f;

        [Header("전화 효과음")]
        [Tooltip("씬을 직접 실행해 테스트할 때만 사용합니다. 복도 엔딩에서 전환되었다면 기존 전화벨이 유지됩니다.")]
        public AudioClip testRingWhenStartingHere;
        public AudioSource phoneAudioSource;
        public AudioClip answerSound;
        [Range(0f, 1f)] public float answerVolume = 1f;
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
        private PlayerHand normalHand;
        private bool normalHandWasVisible;
        private bool normalHandHidden;
        private Transform handOriginalParent;
        private Vector3 handOriginalLocalPosition;
        private Quaternion handOriginalLocalRotation;

        public string DisplayName =>
            phase == RealityPhase.WaitingForPhone ? phoneInteractionName : "";

        private void Start()
        {
            if (endingPanel != null)
                endingPanel.SetActive(false);

            if (player == null)
                player = FindFirstObjectByType<FirstPersonPlayer>();
            if (interactor == null)
                interactor = FindFirstObjectByType<PlayerInteractor>();
            if (gameUI == null)
                gameUI = FindFirstObjectByType<GameUI>();

            if (player == null || player.cameraRoot == null ||
                interactor == null || gameUI == null ||
                wakeupDialogue == null || friendPhoneDialogue == null)
            {
                Debug.LogError("RealityHallwayEnding: 필수 참조를 확인하세요.", this);
                enabled = false;
                return;
            }

            // 별도 씬 테스트 시에만 새 벨소리를 만들고,
            // 복도 엔딩에서 넘어왔다면 DontDestroyOnLoad 전화벨을 그대로 사용합니다.
            if (EndingPhoneRing.Instance == null && testRingWhenStartingHere != null)
                EndingPhoneRing.Begin(testRingWhenStartingHere, 0.55f, 0.55f, 0.1f, 22000f);

            // 시작 즉시 조작을 잠가 흰 화면이 걷히는 동안 움직이지 않게 합니다.
            LockControls();
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            // 흰색 페이드가 걷힐 때부터 이마 짚은 손이 보이게 미리 배치합니다.
            PrepareIntroHand();

            while (SceneFader.IsBusy)
                yield return null;

            yield return new WaitForSeconds(Mathf.Max(0f, waitAfterWhiteFade));
            yield return new WaitForSeconds(Mathf.Max(0f, foreheadHoldBeforeDialogue));

            // 이마를 짚은 상태에서 독백 진행
            yield return EndingDialogueBridge.Play(gameUI, wakeupDialogue);

            // 손을 내린 뒤 전화기 쪽으로 시선 이동
            yield return LowerIntroHand();
            HideIntroHand();
            yield return LookAtPhone();
            yield return new WaitForSeconds(Mathf.Max(0f, pauseBeforeControlReturn));

            phase = RealityPhase.WaitingForPhone;
            RestoreControls();
            PlayerInteractor.ShowHint("전화벨이 울리고 있다.");
        }

        private void PrepareIntroHand()
        {
            if (introHandVisual == null)
                return;

            Camera cam = interactor.playerCamera != null
                ? interactor.playerCamera : Camera.main;
            if (cam == null)
                return;

            if (hideNormalHandDuringIntro)
            {
                normalHand = PlayerHand.Instance;
                if (normalHand != null && normalHand.handModel != null)
                {
                    normalHandWasVisible = normalHand.handModel.activeSelf;
                    normalHand.handModel.SetActive(false);
                    normalHandHidden = true;
                }
            }

            Transform hand = introHandVisual.transform;
            handOriginalParent = hand.parent;
            handOriginalLocalPosition = hand.localPosition;
            handOriginalLocalRotation = hand.localRotation;

            hand.SetParent(cam.transform, false);
            hand.localPosition = foreheadHandLocalPosition;
            hand.localRotation = Quaternion.Euler(foreheadHandLocalEuler);
            introHandVisual.SetActive(true);
        }

        private IEnumerator LowerIntroHand()
        {
            if (introHandVisual == null || !introHandVisual.activeSelf)
                yield break;

            Transform hand = introHandVisual.transform;
            Vector3 start = hand.localPosition;
            float duration = Mathf.Max(0.01f, handLowerDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                hand.localPosition = Vector3.Lerp(start, loweredHandLocalPosition, t);
                yield return null;
            }
            hand.localPosition = loweredHandLocalPosition;
        }

        private void HideIntroHand()
        {
            if (introHandVisual != null)
            {
                introHandVisual.SetActive(false);
                if (handOriginalParent != null)
                {
                    introHandVisual.transform.SetParent(handOriginalParent, false);
                    introHandVisual.transform.localPosition = handOriginalLocalPosition;
                    introHandVisual.transform.localRotation = handOriginalLocalRotation;
                }
            }

            if (normalHandHidden && normalHand != null && normalHand.handModel != null)
                normalHand.handModel.SetActive(normalHandWasVisible);
            normalHandHidden = false;
        }

        private IEnumerator LookAtPhone()
        {
            if (phoneLookTarget == null)
            {
                Debug.LogWarning("RealityHallwayEnding: Phone Look Target을 연결하면 전화기를 바라보는 연출이 재생됩니다.", this);
                yield break;
            }

            Transform cameraTransform = interactor.playerCamera != null
                ? interactor.playerCamera.transform : player.cameraRoot;

            Vector3 direction = phoneLookTarget.position - cameraTransform.position;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            Quaternion startYaw = player.transform.rotation;
            Quaternion targetYaw = horizontal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(horizontal) : startYaw;

            float horizontalDistance = Mathf.Max(0.01f, horizontal.magnitude);
            float targetPitch = Mathf.Clamp(
                -Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg,
                -player.maxPitch, player.maxPitch);
            float startPitch = player.Pitch;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, lookAtPhoneDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                player.transform.rotation = Quaternion.Slerp(startYaw, targetYaw, t);
                player.SetLookPitch(Mathf.LerpAngle(startPitch, targetPitch, t));
                yield return null;
            }
            player.transform.rotation = targetYaw;
            player.SetLookPitch(targetPitch);
        }

        public void Interact()
        {
            if (phase != RealityPhase.WaitingForPhone)
                return;

            phase = RealityPhase.Calling;
            StartCoroutine(PlayCall());
        }

        private IEnumerator PlayCall()
        {
            LockControls();

            EndingPhoneRing.StopRinging(ringFadeOutTime);
            if (phoneAudioSource != null && answerSound != null)
                phoneAudioSource.PlayOneShot(answerSound, answerVolume);
            else if (answerSound != null)
                AudioManager.Play(answerSound, answerVolume);

            yield return EndingDialogueBridge.Play(gameUI, friendPhoneDialogue);

            phase = RealityPhase.Finished;
            onCallFinished?.Invoke();

            if (endingPanel != null)
                endingPanel.SetActive(true);
            else
                RestoreControls();
        }

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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void RestoreControls()
        {
            if (!controlsLocked)
                return;

            controlsLocked = false;
            player.enabled = savedPlayerEnabled;
            interactor.enabled = savedInteractorEnabled;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            HideIntroHand();
            if (controlsLocked && player != null && interactor != null)
                RestoreControls();
        }
    }
}
