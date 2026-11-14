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
        public Transform memorialClickerGripPoint;
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


[Header("컷신 손 모델 구분")]
public GameObject petHandModel;
public GameObject doorHandModel;


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


private void SelectHandModel(bool useDoorHand)
{
    if (petHandModel != null)
        petHandModel.SetActive(!useDoorHand);

    if (doorHandModel != null)
        doorHandModel.SetActive(useDoorHand);
}


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

    // 플레이어가 공중에 있을 때는 엔딩을 시작하지 않는다.
    // 착지 후에만 이별 컷신을 진행한다.
    CharacterController playerController =
        player.GetComponent<CharacterController>();

    if (playerController == null ||
        !playerController.enabled ||
        !playerController.isGrounded)
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

    // 겨울이가 더 이상 움직이지 않도록 중지
    dogFollower.enabled = false;

    // 진행 중인 행동 초기화
    dogAnimator.Interrupt();

    // 이동이 안정될 때까지 잠시 대기
    yield return new WaitForSeconds(0.25f);

    // 겨울이가 플레이어 카메라를 바라보게 설정
    Transform cameraTarget =
        interactor != null && interactor.playerCamera != null
        ? interactor.playerCamera.transform
        : player.cameraRoot;

    Vector3 lookDirection =
        cameraTarget.position - dog.position;

    // 머리나 몸이 위아래로 기울지 않도록 Y축만 회전
    lookDirection.y = 0f;

    if (lookDirection.sqrMagnitude > 0.001f)
    {
        dog.rotation =
            Quaternion.LookRotation(lookDirection);
    }

    // 정면을 바라보는 상태에서 앉기
    dogAnimator.Sit();

    // 플레이어 카메라도 겨울이를 바라보도록 회전
    dogLookTarget.position =
        dog.position + Vector3.up * 0.5f;

    yield return LookAt(dogLookTarget, 1f);

    // 기존 클릭커와 대사 연출 유지
    yield return PlayClick();
    yield return EndingDialogueBridge.Play(
        gameUI, callDogDialogue
    );

    foreach (float interval in repeatClickIntervals)
    {
        yield return new WaitForSeconds(
            Mathf.Max(0.05f, interval)
        );

        yield return PlayClick();
    }

    yield return new WaitForSeconds(
        Mathf.Max(0f, silenceAfterClicks)
    );

    yield return EndingDialogueBridge.Play(
        gameUI, acceptRealityDialogue
    );

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

    // 쓰다듬기 전용 손
    SelectHandModel(false);
    ShowHandAt(handStartPoint);

    yield return MoveHandTo(
        handPetPoint,
        handMoveDuration
    );

    // 겨울이 머리 쓰다듬기
    if (cutsceneHandVisual != null && handPetPoint != null)
    {
        for (int i = 0; i < Mathf.Max(1, petStrokeCount); i++)
        {
            Vector3 normal = handPetPoint.position;
            Vector3 stroke =
                normal + Vector3.up * petStrokeHeight;

            yield return MoveHandPosition(
                stroke, petStrokeDuration
            );

            yield return MoveHandPosition(
                normal, petStrokeDuration
            );
        }
    }

    // 기존 쓰다듬기 대사 복구
    yield return EndingDialogueBridge.Play(
        gameUI, petDogDialogue
    );

    // 주머니에서 클릭커를 가져오는 동작
    yield return MoveHandTo(
        handPocketPoint,
        handMoveDuration
    );

    // 손에 클릭커를 쥔 상태로 내려놓기
    yield return LeaveMemorialClicker();

    // 빈손만 주머니 쪽으로 돌아오기
    yield return MoveHandTo(
        handPocketPoint,
        handMoveDuration
    );

    HideHand();

    phase = EndingPhase.WaitingForDoor;
    RestoreControls();
}

        

        


private IEnumerator LeaveMemorialClicker()
{
    if (cutsceneHandVisual == null ||
        petHandModel == null ||
        memorialClickerVisual == null ||
        memorialClickerGripPoint == null ||
        memorialClickerPlacePoint == null ||
        handLeavePoint == null)
    {
        Debug.LogWarning(
            "[엔딩] 클릭커 내려놓기 참조가 비어 있습니다.",
            this
        );
        yield break;
    }

    Transform hand = cutsceneHandVisual.transform;
    Transform clicker = memorialClickerVisual.transform;

    Transform originalParent = clicker.parent;

    // 1. 쓰다듬기용 손 활성화
    SelectHandModel(false);
    cutsceneHandVisual.SetActive(true);

    // 2. 클릭커를 손바닥에 완전히 고정
    clicker.SetParent(memorialClickerGripPoint, false);
    clicker.localPosition = Vector3.zero;
    clicker.localRotation = Quaternion.identity;

    memorialClickerVisual.SetActive(true);

    // 클릭커를 쥔 손을 잠시 보여줌
    yield return new WaitForSeconds(0.35f);

    // 3. 손과 클릭커를 함께 겨울이 옆으로 이동
    yield return MoveHandTo(
        handLeavePoint,
        handMoveDuration
    );

    // 4. 손이 바닥까지 내려가는 거리 계산
    // 클릭커는 GripPoint의 자식이므로 손을 따라 움직임
    Vector3 startHandPosition = hand.position;

    Vector3 offset =
        memorialClickerPlacePoint.position -
        clicker.position;

    Vector3 endHandPosition =
        startHandPosition + offset;

    float duration = Mathf.Max(
        0.3f,
        memorialPlacementDuration
    );

    float elapsed = 0f;

    // 5. 손만 움직임
    // 클릭커는 손에 붙어 있으므로 같은 프레임에 이동
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;

        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(elapsed / duration)
        );

        hand.position = Vector3.Lerp(
            startHandPosition,
            endHandPosition,
            t
        );

        yield return null;
    }

    hand.position = endHandPosition;

    // 클릭커와 손이 모두 도착한 상태 유지
    yield return new WaitForSeconds(0.3f);

    // 6. 손 이동이 완전히 끝난 다음 클릭커 분리
    // 여기서는 클릭커 위치를 다시 강제로 변경하지 않음
    clicker.SetParent(originalParent, true);

    Debug.Log(
        "[엔딩] 클릭커와 손 동시 이동 완료.",
        this
    );
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

SelectHandModel(true); // 문 열기용 손 선택
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
