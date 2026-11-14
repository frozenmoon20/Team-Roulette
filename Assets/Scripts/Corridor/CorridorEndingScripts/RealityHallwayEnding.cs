using System.Collections;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.CorridorEnding
{
    // 현실 복도 전용 연출. 공용 Core/UI 스크립트는 수정하지 않습니다.
    public class RealityHallwayEnding : MonoBehaviour, IInteractable, IHasName
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

        [Header("친구 통화 대사 속도 (초/줄)")]
        [Min(1f)]
        public float friendLineDuration = 4.5f;

        [Header("이마를 짚는 손 연출")]
        public GameObject introHandVisual;
        [Tooltip("카메라 기준 손 위치. 씬 Inspector의 조정값을 유지하세요.")]
        public Vector3 foreheadHandLocalPosition = new Vector3(0.08f, -0.18f, 0.38f);
        public Vector3 foreheadHandLocalEuler = new Vector3(-35f, -15f, 45f);
        public Vector3 loweredHandLocalPosition = new Vector3(0.35f, -0.65f, 0.4f);
        public float handLowerDuration = 0.8f;

        [Header("평소 손 숨기기")]
        public Transform normalHandRoot;
        public bool hideNormalHandDuringIntro = true;

        [Header("전화기를 바라보는 연출")]
        public Transform phoneLookTarget;
        public float lookAtPhoneDuration = 1.2f;
        public float pauseBeforeControlReturn = 0.25f;

        [Header("전화 효과음")]
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
        private CorridorCutsceneWalkSilencer walkSilencer;

        private Renderer[] normalHandRenderers;
        private bool[] originalRendererStates;
        private Transform handOriginalParent;
        private Vector3 handOriginalLocalPosition;
        private Quaternion handOriginalLocalRotation;
        private Vector3 handOriginalLocalScale;
        private bool introHandPrepared;

        // 공용 GameUI의 2초 자동 넘김을 건드리지 않고, 통화 중에만 같은 패널을 수동 제어합니다.
        private bool slowDialogueVisible;
        private bool dialoguePanelWasActive;
        private string dialogueTextBefore;
        private Color dialogueColorBefore;
        private Vector2 dialogueSizeBefore;

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

            if (normalHandRoot == null && PlayerHand.Instance != null)
                normalHandRoot = PlayerHand.Instance.transform;

            // 복도 엔딩에서 전환한 경우 기존 전화벨을 유지합니다.
            if (EndingPhoneRing.Instance == null && testRingWhenStartingHere != null)
                EndingPhoneRing.Begin(testRingWhenStartingHere, 0.55f, 0.55f, 0.1f, 22000f);

            LockControls();
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            PrepareIntroHand();

            while (SceneFader.IsBusy)
                yield return null;

            yield return new WaitForSeconds(Mathf.Max(0f, waitAfterWhiteFade));
            yield return new WaitForSeconds(Mathf.Max(0f, foreheadHoldBeforeDialogue));

            // 전화기 클릭 전에는 이 독백만 나옵니다.
            yield return EndingDialogueBridge.Play(gameUI, wakeupDialogue);

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

            HideNormalHandRenderers();

            Transform hand = introHandVisual.transform;
            handOriginalParent = hand.parent;
            handOriginalLocalPosition = hand.localPosition;
            handOriginalLocalRotation = hand.localRotation;
            handOriginalLocalScale = hand.localScale;
            introHandPrepared = true;

            hand.SetParent(cam.transform, false);
            hand.localPosition = foreheadHandLocalPosition;
            hand.localRotation = Quaternion.Euler(foreheadHandLocalEuler);
            hand.localScale = handOriginalLocalScale;
            introHandVisual.SetActive(true);
        }

        private void HideNormalHandRenderers()
        {
            if (!hideNormalHandDuringIntro || normalHandRoot == null)
                return;

            if (introHandVisual != null &&
                introHandVisual.transform.IsChildOf(normalHandRoot))
            {
                Debug.LogWarning("IntroHandVisual은 HandRoot의 자식이 아닌 별도 오브젝트로 배치하세요.", this);
                return;
            }

            normalHandRenderers = normalHandRoot.GetComponentsInChildren<Renderer>(true);
            originalRendererStates = new bool[normalHandRenderers.Length];

            for (int i = 0; i < normalHandRenderers.Length; i++)
            {
                if (normalHandRenderers[i] == null)
                    continue;

                originalRendererStates[i] = normalHandRenderers[i].enabled;
                normalHandRenderers[i].enabled = false;
            }
        }

        private void RestoreNormalHandRenderers()
        {
            if (normalHandRenderers == null || originalRendererStates == null)
                return;

            int count = Mathf.Min(normalHandRenderers.Length, originalRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (normalHandRenderers[i] != null)
                    normalHandRenderers[i].enabled = originalRendererStates[i];
            }

            normalHandRenderers = null;
            originalRendererStates = null;
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
            if (introHandVisual != null && introHandPrepared)
            {
                Transform hand = introHandVisual.transform;
                introHandVisual.SetActive(false);
                hand.SetParent(handOriginalParent, false);
                hand.localPosition = handOriginalLocalPosition;
                hand.localRotation = handOriginalLocalRotation;
                hand.localScale = handOriginalLocalScale;
                introHandPrepared = false;
            }

            RestoreNormalHandRenderers();
        }

        private IEnumerator LookAtPhone()
        {
            if (phoneLookTarget == null)
            {
                Debug.LogWarning("RealityHallwayEnding: Phone Look Target을 연결하세요.", this);
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
            // 이 단계에서 전화기를 실제로 클릭해야만 통화합니다.
            if (phase != RealityPhase.WaitingForPhone)
                return;

            phase = RealityPhase.Calling;
            StartCoroutine(PlayCall());
        }

        private IEnumerator PlayCall()
        {
            // 플레이어 입력 + 걷기 카메라 흔들림 + 발소리를 동시에 중지합니다.
            LockControls();
            EndingPhoneRing.StopRinging(ringFadeOutTime);
            PlayPhoneHandleSound();

            // 공용 GameUI의 Update는 변경하지 않고 이 통화에서만 표시 시간을 늘립니다.
            yield return PlayFriendDialogueSlowly();

            PlayPhoneHandleSound();
            phase = RealityPhase.Finished;
            onCallFinished?.Invoke();

            if (endingPanel != null)
                endingPanel.SetActive(true);
            else
                RestoreControls();
        }

        private IEnumerator PlayFriendDialogueSlowly()
        {
            if (gameUI == null || friendPhoneDialogue == null ||
                friendPhoneDialogue.lines == null ||
                friendPhoneDialogue.lines.Length == 0 ||
                gameUI.dialoguePanel == null || gameUI.dialogueText == null)
            {
                Debug.LogError("RealityHallwayEnding: 통화 대사 또는 GameUI 패널 연결을 확인하세요.", this);
                yield break;
            }

            // 다른 대사나 설정 화면이 떠 있으면 종료될 때까지 기다립니다.
            while (gameUI != null && (gameUI.IsDialogueOpen || gameUI.IsPaused))
                yield return null;

            if (gameUI == null)
                yield break;

            dialoguePanelWasActive = gameUI.dialoguePanel.activeSelf;
            dialogueTextBefore = gameUI.dialogueText.text;
            dialogueColorBefore = gameUI.dialogueText.color;

            RectTransform panelRect = gameUI.dialoguePanel.transform as RectTransform;
            if (panelRect != null)
                dialogueSizeBefore = panelRect.sizeDelta;

            slowDialogueVisible = true;
            gameUI.dialoguePanel.SetActive(true);

            for (int i = 0; i < friendPhoneDialogue.lines.Length; i++)
            {
                if (gameUI == null)
                    break;

                string line = friendPhoneDialogue.lines[i] ?? "";
                gameUI.dialogueText.text = line;
                gameUI.dialogueText.color = friendPhoneDialogue.GetLineColor(i);

                if (panelRect != null)
                {
                    Vector2 size = gameUI.dialogueText.GetPreferredValues(
                        line, 880f, Mathf.Infinity);
                    panelRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        Mathf.Clamp(size.x + 40f, 80f, 920f));
                    panelRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        Mathf.Max(56f, size.y + 20f));
                }

                float remaining = Mathf.Max(1f, friendLineDuration);
                while (remaining > 0f)
                {
                    if (gameUI == null)
                        break;
                    if (!gameUI.IsPaused)
                        remaining -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            RestoreSlowDialogueUI();
        }

        private void RestoreSlowDialogueUI()
        {
            if (!slowDialogueVisible)
                return;

            slowDialogueVisible = false;
            if (gameUI == null)
                return;

            if (gameUI.dialogueText != null)
            {
                gameUI.dialogueText.text = dialogueTextBefore;
                gameUI.dialogueText.color = dialogueColorBefore;
            }

            if (gameUI.dialoguePanel != null)
            {
                RectTransform rect = gameUI.dialoguePanel.transform as RectTransform;
                if (rect != null)
                    rect.sizeDelta = dialogueSizeBefore;
                gameUI.dialoguePanel.SetActive(dialoguePanelWasActive);
            }
        }

        private void PlayPhoneHandleSound()
        {
            if (answerSound == null)
                return;

            if (phoneAudioSource != null)
                phoneAudioSource.PlayOneShot(answerSound, answerVolume);
            else
                AudioManager.Play(answerSound, answerVolume);
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

            // 기존 복도 전용 보조 스크립트 재사용: 카메라 걷기 흔들림 및 발소리 정지.
            walkSilencer = CorridorCutsceneWalkSilencer.GetOrCreate(player);
            if (walkSilencer != null)
                walkSilencer.Begin();

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

            if (walkSilencer != null)
            {
                walkSilencer.End();
                walkSilencer = null;
            }

            player.enabled = savedPlayerEnabled;
            interactor.enabled = savedInteractorEnabled;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            RestoreSlowDialogueUI();
            HideIntroHand();
            if (controlsLocked && player != null && interactor != null)
                RestoreControls();
        }
    }
}
