using System.Collections;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.CorridorEnding
{
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

        public string DisplayName => phase == RealityPhase.WaitingForPhone
            ? phoneInteractionName
            : "";

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

            if (player == null || interactor == null || gameUI == null ||
                wakeupDialogue == null || friendPhoneDialogue == null)
            {
                Debug.LogError("RealityHallwayEnding: 필수 참조를 확인하세요.", this);
                enabled = false;
                return;
            }

            if (EndingPhoneRing.Instance == null && testRingWhenStartingHere != null)
                EndingPhoneRing.Begin(testRingWhenStartingHere, 0.55f, 0.55f, 0.1f, 22000f);

            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            LockControls();

            while (SceneFader.IsBusy)
                yield return null;

            yield return new WaitForSeconds(Mathf.Max(0f, waitAfterWhiteFade));
            yield return EndingDialogueBridge.Play(gameUI, wakeupDialogue);

            phase = RealityPhase.WaitingForPhone;
            RestoreControls();
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
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        private void OnDisable()
        {
            if (controlsLocked && player != null && interactor != null)
                RestoreControls();
        }
    }
}
