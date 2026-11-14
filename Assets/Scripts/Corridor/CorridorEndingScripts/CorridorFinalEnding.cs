using System.Collections;
using TeamRoulette.UI;
using UnityEngine;

namespace TeamRoulette.CorridorEnding
{
    public class CorridorFinalEnding : MonoBehaviour
    {
        private enum EndingPhase
        {
            WaitingForHurdle,
            PlayingFirstCutscene,
            WaitingForPet,
            PlayingPet,
            WaitingForDoor,
            PlayingDoor,
            Transitioning
        }

        [Header("플레이어와 기존 시스템")]
        public FirstPersonPlayer player;
        public PlayerInteractor interactor;
        public ClickerPocketController clickerController;
        public GameUI gameUI;

        [Header("시작 조건")]
        public GyeoulHurdleJump finalHurdle;
        public Transform endingStartPoint;
        public float startDistance = 2.5f;
        public float dogStartDistance = 4f;

        [Header("겨울이")]
        public Transform dog;
        public GyeoulFollowPlayer dogFollower;
        public DogAnimator dogAnimator;
        public Transform dogSitPoint;
        public Transform dogLookTarget;
        public float dogMoveSpeed = 2.2f;

        [Header("상호작용")]
        public EndingPetTarget petTarget;
        public EndingDoorTarget doorTarget;

        [Header("기존 GameUI 대사 데이터")]
        public DialogueSequence callDogDialogue;
        public DialogueSequence acceptRealityDialogue;
        public DialogueSequence petDogDialogue;
        public DialogueSequence goodbyeDialogue;

        [Header("클릭커 반복 연출")]
        public AudioSource clickAudioSource;
        public AudioClip clickSound;
        [Range(0f, 1f)] public float clickVolume = 0.9f;
        public Transform clickerButton;
        public Vector3 buttonPressedOffset = new Vector3(0f, -0.003f, 0f);
        public float buttonPressTime = 0.08f;
        public float[] repeatClickIntervals = { 0.85f, 0.55f, 0.3f, 0.15f };
        public float silenceAfterClicks = 1.5f;

        [Header("전용 손 연출 모델과 위치")]
        public GameObject cutsceneHandVisual;
        public Transform handStartPoint;
        public Transform handPetPoint;
        public Transform handPocketPoint;
        public Transform handLeavePoint;
        public Transform handDoorPoint;
        public float handMoveDuration = 0.7f;
        public float petStrokeHeight = 0.06f;
        public int petStrokeCount = 3;
        public float petStrokeDuration = 0.35f;

        [Header("추억의 클릭커 내려놓기")]
        public GameObject memorialClickerVisual;
        public Transform memorialClickerPlacePoint;
        public Vector3 memorialClickerHeldOffset = new Vector3(0f, 0f, 0.05f);
        public float memorialPlacementDuration = 0.55f;

        [Header("마지막 문")]
        public Transform doorPivot;
        public Transform doorLookTarget;
        public Vector3 doorLocalAxis = Vector3.up;
        public float doorOpenAngle = 90f;
        public float doorOpenDuration = 2.2f;
        public Light doorLight;
        public float doorLightOpenIntensity = 6f;
        public GameObject whiteLightBehindDoor;
        public float waitAfterGoodbye = 0.65f;

        [Header("현실 복도 전환")]
        public string realitySceneName = "";
        public float whiteFadeOutTime = 2.4f;
        public float whiteFadeInTime = 2.2f;
        public AudioClip telephoneRing;
        [Range(0f, 1f)] public float telephoneStartVolume = 0.02f;
        [Range(0f, 1f)] public float telephoneFinalVolume = 0.65f;
        public float telephoneFadeInDuration = 3.5f;
        public float telephoneInitialLowPass = 700f;

        private EndingPhase phase = EndingPhase.WaitingForHurdle;
        private bool savedPlayerEnabled;
        private bool savedInteractorEnabled;
        private bool savedClickerEnabled;
        private bool hasSavedControls;
        private bool controlsLocked;
        private CorridorCutsceneWalkSilencer walkSilencer;
        private bool transitionRequested;
        private bool savedOriginalHandVisible;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible;
        private Vector3 clickButtonRestPosition;
        private bool hasClickButton;
        private float doorLightInitialIntensity;
        private Quaternion doorInitialRotation;

        public bool CanPet => phase == EndingPhase.WaitingForPet;
        public bool CanOpenDoor => phase == EndingPhase.WaitingForDoor;

        private void Awake()
        {
            if (clickerButton != null)
            {
                clickButtonRestPosition = clickerButton.localPosition;
                hasClickButton = true;
            }

            if (doorPivot != null)
                doorInitialRotation = doorPivot.localRotation;

            if (doorLight != null)
                doorLightInitialIntensity = doorLight.intensity;

            if (cutsceneHandVisual != null)
                cutsceneHandVisual.SetActive(false);

            if (memorialClickerVisual != null)
                memorialClickerVisual.SetActive(false);

            if (whiteLightBehindDoor != null)
                whiteLightBehindDoor.SetActive(false);
        }

        
private void Update()
{
    if (phase != EndingPhase.WaitingForHurdle)
        return;

    if (player == null || finalHurdle == null ||
        endingStartPoint == null || dog == null ||
        dogFollower == null)
        return;

    // 마지막 허들은 겨울이가 넘어서는 안 된다.
    if (finalHurdle.IsCompleted)
        return;

    // 플레이어는 허들을 넘고, 겨울이는 반대편에 남아 있어야 한다.
    if (!finalHurdle.ShouldWait(player.transform, dog))
        return;

    // 플레이어가 엔딩 시작 지점에 도착했는지 확인
    if (HorizontalDistance(
        player.transform.position,
        endingStartPoint.position) > startDistance)
        return;

    // 겨울이가 마지막 허들 앞에서 기다리고 있는지 확인
    if (finalHurdle.approachPoint == null ||
        HorizontalDistance(
            dog.position,
            finalHurdle.approachPoint.position) > 1.5f)
        return;

    if (!ReferencesReady())
        return;

    phase = EndingPhase.PlayingFirstCutscene;
    StartCoroutine(FirstScene());
}


        private bool ReferencesReady()
        {
            bool good = player != null && player.cameraRoot != null &&
                interactor != null && clickerController != null &&
                gameUI != null && dogFollower != null && dogAnimator != null &&
                dogSitPoint != null && dogLookTarget != null &&
                petTarget != null && doorTarget != null &&
                doorPivot != null && doorLookTarget != null &&
                callDogDialogue != null && acceptRealityDialogue != null &&
                petDogDialogue != null && goodbyeDialogue != null;

            if (!good)
            {
                Debug.LogError("CorridorFinalEnding: 필수 Inspector 참조를 모두 연결해 주세요.", this);
                enabled = false;
            }

            return good;
        }

        private IEnumerator FirstScene()
        {

LockControls();

dogFollower.enabled = false;

dogAnimator.Interrupt();

// 겨울이의 최종 위치를 지정된 SitPoint로 고정
dog.SetPositionAndRotation(
    dogSitPoint.position,
    dogSitPoint.rotation
);

// 겨울이가 고정된 위치에서 앉기
dogAnimator.Sit();


// 카메라가 실제 겨울이의 위치를 바라보게 한다.
dogLookTarget.position = dog.position + Vector3.up * 0.5f;
yield return LookAt(dogLookTarget, 1f);


            yield return PlayClick();
            yield return EndingDialogueBridge.Play(gameUI, callDogDialogue);

            foreach (float interval in repeatClickIntervals)
            {
                yield return new WaitForSeconds(Mathf.Max(0.05f, interval));
                yield return PlayClick();
            }

            yield return new WaitForSeconds(Mathf.Max(0f, silenceAfterClicks));
            yield return EndingDialogueBridge.Play(gameUI, acceptRealityDialogue);

            PutClickerAway();
            phase = EndingPhase.WaitingForPet;
            RestoreControls();
        }

        private IEnumerator MoveDogToSitPoint()
        {
            while (dog != null && dogSitPoint != null &&
                   HorizontalDistance(dog.position, dogSitPoint.position) > 0.05f)
            {
                dog.position = Vector3.MoveTowards(
                    dog.position,
                    dogSitPoint.position,
                    Mathf.Max(0.2f, dogMoveSpeed) * Time.deltaTime
                );
                yield return null;
            }

            if (dog != null && dogSitPoint != null)
                dog.SetPositionAndRotation(dogSitPoint.position, dogSitPoint.rotation);
        }

        private IEnumerator PlayClick()
        {
            if (clickAudioSource != null && clickSound != null)
                clickAudioSource.PlayOneShot(clickSound, clickVolume);

            if (!hasClickButton || clickerButton == null)
            {
                yield return new WaitForSeconds(0.1f);
                yield break;
            }

            Vector3 down = clickButtonRestPosition + buttonPressedOffset;
            yield return AnimateButton(clickButtonRestPosition, down, buttonPressTime);
            yield return AnimateButton(down, clickButtonRestPosition, buttonPressTime);
        }

        private IEnumerator AnimateButton(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.02f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                clickerButton.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }

            clickerButton.localPosition = to;
        }

        private void PutClickerAway()
        {
            PlayerHand hand = PlayerHand.Instance;

            if (hand != null && clickerController != null &&
                hand.Held != null && hand.Held == clickerController.clicker)
            {
                hand.PlaceHeld(null, true);
            }

            clickerController.enabled = false;
        }

        public void StartPetting()
        {
            if (!CanPet)
                return;

            phase = EndingPhase.PlayingPet;
            StartCoroutine(PettingScene());
        }

        private IEnumerator PettingScene()
        {
            LockControls();
            yield return LookAt(dogLookTarget, 0.65f);
            ShowHandAt(handStartPoint);
            yield return MoveHandTo(handPetPoint, handMoveDuration);

            if (cutsceneHandVisual != null && handPetPoint != null)
            {
                for (int i = 0; i < Mathf.Max(1, petStrokeCount); i++)
                {
                    Vector3 normal = handPetPoint.position;
                    Vector3 stroke = normal + Vector3.up * petStrokeHeight;
                    yield return MoveHandPosition(stroke, petStrokeDuration);
                    yield return MoveHandPosition(normal, petStrokeDuration);
                }
            }

            yield return EndingDialogueBridge.Play(gameUI, petDogDialogue);
            yield return MoveHandTo(handPocketPoint, handMoveDuration);
            yield return LeaveMemorialClicker();
            HideHand();
            phase = EndingPhase.WaitingForDoor;
            RestoreControls();
        }

        private IEnumerator LeaveMemorialClicker()
        {
            if (memorialClickerVisual == null || memorialClickerPlacePoint == null ||
                cutsceneHandVisual == null || handPocketPoint == null || handLeavePoint == null)
            {
                Debug.LogWarning("클릭커를 내려놓는 시각 연출 참조가 비어 있습니다.", this);
                yield break;
            }

            Transform model = memorialClickerVisual.transform;
            model.SetParent(cutsceneHandVisual.transform, false);
            model.localPosition = memorialClickerHeldOffset;
            model.localRotation = Quaternion.identity;
            memorialClickerVisual.SetActive(true);

            yield return MoveHandTo(handLeavePoint, handMoveDuration);

            model.SetParent(null, true);
            Vector3 from = model.position;
            Quaternion initialRotation = model.rotation;
            float elapsed = 0f;
            float duration = Mathf.Max(0.02f, memorialPlacementDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                model.position = Vector3.Lerp(from, memorialClickerPlacePoint.position, t);
                model.rotation = Quaternion.Slerp(initialRotation, memorialClickerPlacePoint.rotation, t);
                yield return null;
            }

            model.SetPositionAndRotation(memorialClickerPlacePoint.position, memorialClickerPlacePoint.rotation);
        }

       
public void OpenEndingDoor()
{
    if (!CanOpenDoor)
    {
        Debug.LogWarning(
            "아직 문을 열 수 있는 엔딩 단계가 아닙니다.",
            this
        );
        return;
    }

    if (SceneFader.IsBusy)
        return;

    Debug.Log(
        "[엔딩] 마지막 문 상호작용 성공! 문 열기 연출 시작.",
        this
    );

    phase = EndingPhase.PlayingDoor;
    StartCoroutine(DoorScene());
}


        private IEnumerator DoorScene()
        {
            LockControls();
            yield return LookAt(doorLookTarget, 0.65f);
            ShowHandAt(handStartPoint);
            yield return MoveHandTo(handDoorPoint, handMoveDuration);

            Transform handOriginalParent = cutsceneHandVisual != null
                ? cutsceneHandVisual.transform.parent
                : null;

            if (cutsceneHandVisual != null && handDoorPoint != null)
                cutsceneHandVisual.transform.SetParent(handDoorPoint, true);

            if (whiteLightBehindDoor != null)
                whiteLightBehindDoor.SetActive(true);

            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, doorOpenDuration);
            Quaternion opened = doorInitialRotation * Quaternion.AngleAxis(
                doorOpenAngle,
                doorLocalAxis.sqrMagnitude > 0.0001f ? doorLocalAxis.normalized : Vector3.up
            );

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                doorPivot.localRotation = Quaternion.Slerp(doorInitialRotation, opened, t);

                if (doorLight != null)
                    doorLight.intensity = Mathf.Lerp(doorLightInitialIntensity, doorLightOpenIntensity, t);

                yield return null;
            }

            doorPivot.localRotation = opened;

            if (cutsceneHandVisual != null && handDoorPoint != null)
                cutsceneHandVisual.transform.SetParent(handOriginalParent, true);

            HideHand();
            yield return LookAt(dogLookTarget, 1.7f);
            yield return EndingDialogueBridge.Play(gameUI, goodbyeDialogue);
            yield return new WaitForSeconds(Mathf.Max(0f, waitAfterGoodbye));

            
            // 문 열기와 겨울이의 마지막 대사가 끝난 상태
            phase = EndingPhase.Transitioning;

            // 현실 복도 씬이 준비되었는지 확인
            bool hasRealityScene =
                !string.IsNullOrWhiteSpace(realitySceneName) &&
                Application.CanStreamedLevelBeLoaded(realitySceneName);

            if (!hasRealityScene)
            {
                Debug.LogWarning(
                    "[엔딩 테스트] 문 열기와 마지막 대사 완료! " +
                    "현실 복도 씬이 아직 연결되지 않아 " +
                    "여기서 테스트를 종료합니다.",
                    this
                );

                PlayerInteractor.ShowHint(
                    "엔딩 연출 테스트 완료."
                );

                RestoreControls();
                yield break;
            }

            // 전화벨은 연결된 경우에만 재생
            if (telephoneRing != null)
            {
                EndingPhoneRing.Begin(
                    telephoneRing,
                    telephoneStartVolume,
                    telephoneFinalVolume,
                    telephoneFadeInDuration,
                    telephoneInitialLowPass
                );
            }

            transitionRequested = true;

            // 현실 복도 씬으로 전환
            SceneFader.Go(
                realitySceneName,
                whiteFadeOutTime,
                whiteFadeInTime,
                0.35f,
                false,
                Color.white
            );
        }

        private void LockControls()
        {
            if (controlsLocked)
                return;

            if (!hasSavedControls)
            {
                savedPlayerEnabled = player.enabled;
                savedInteractorEnabled = interactor.enabled;
                savedClickerEnabled = clickerController.enabled;
                savedCursorLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                hasSavedControls = true;
            }

            controlsLocked = true;
            player.StopMovement();

walkSilencer =
    CorridorCutsceneWalkSilencer.GetOrCreate(player);

if (walkSilencer != null)
    walkSilencer.Begin();

            player.enabled = false;
            interactor.enabled = false;
            clickerController.enabled = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void RestoreControls()
        {
            if (!controlsLocked)
                return;

            controlsLocked = false;

// 마지막 엔딩 컷신이 끝나면 걷기 효과 복구
if (walkSilencer != null)
{
    walkSilencer.End();
    walkSilencer = null;
}

            player.enabled = savedPlayerEnabled;
            interactor.enabled = savedInteractorEnabled;
            clickerController.enabled = false;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        private IEnumerator LookAt(Transform target, float duration)
        {
            if (target == null)
                yield break;

            Vector3 direction = target.position - player.cameraRoot.position;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);

            Quaternion fromYaw = player.transform.rotation;
            Quaternion toYaw = horizontal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(horizontal)
                : fromYaw;

            float distance = Mathf.Max(0.01f, horizontal.magnitude);
            float toPitch = Mathf.Clamp(
                -Mathf.Atan2(direction.y, distance) * Mathf.Rad2Deg,
                -player.maxPitch,
                player.maxPitch
            );

            float fromPitch = player.Pitch;
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                player.transform.rotation = Quaternion.Slerp(fromYaw, toYaw, t);
                player.SetLookPitch(Mathf.LerpAngle(fromPitch, toPitch, t));
                yield return null;
            }

            player.transform.rotation = toYaw;
            player.SetLookPitch(toPitch);
        }

        private void ShowHandAt(Transform anchor)
        {
            if (cutsceneHandVisual == null || anchor == null)
                return;

            PlayerHand hand = PlayerHand.Instance;
            if (hand != null && hand.handModel != null)
            {
                savedOriginalHandVisible = hand.handModel.activeSelf;
                hand.handModel.SetActive(false);
            }

            cutsceneHandVisual.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            cutsceneHandVisual.SetActive(true);
        }

        private IEnumerator MoveHandTo(Transform anchor, float duration)
        {
            if (anchor == null || cutsceneHandVisual == null)
                yield break;

            Transform model = cutsceneHandVisual.transform;
            Vector3 from = model.position;
            Quaternion fromRotation = model.rotation;
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                model.position = Vector3.Lerp(from, anchor.position, t);
                model.rotation = Quaternion.Slerp(fromRotation, anchor.rotation, t);
                yield return null;
            }

            model.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private IEnumerator MoveHandPosition(Vector3 target, float duration)
        {
            if (cutsceneHandVisual == null)
                yield break;

            Transform model = cutsceneHandVisual.transform;
            Vector3 from = model.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                model.position = Vector3.Lerp(from, target, t);
                yield return null;
            }

            model.position = target;
        }

        private void HideHand()
        {
            if (cutsceneHandVisual != null)
                cutsceneHandVisual.SetActive(false);

            PlayerHand hand = PlayerHand.Instance;
            if (hand != null && hand.handModel != null)
                hand.handModel.SetActive(savedOriginalHandVisible);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDisable()
        {
            if (transitionRequested)
                return;

            if (cutsceneHandVisual != null && cutsceneHandVisual.activeSelf)
                HideHand();

            if (clickerButton != null && hasClickButton)
                clickerButton.localPosition = clickButtonRestPosition;

            if (controlsLocked && player != null && interactor != null)
                RestoreControls();
        }
    }
}
