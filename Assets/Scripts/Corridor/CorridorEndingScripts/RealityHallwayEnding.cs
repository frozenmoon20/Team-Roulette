
using System.Collections;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.Events;

namespace TeamRoulette.CorridorEnding
{
    // 현실 복도 전용 연출
    // 공용 Core/UI 스크립트는 수정하지 않는다.
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
        private bool savedNameTextActive;

        [Header("친구 통화 대사 속도 (초/줄)")]
        [Min(1f)]
        public float friendLineDuration = 4.5f;

        [Header("이마를 짚는 손 연출")]
        public GameObject introHandVisual;

        public Vector3 foreheadHandLocalPosition =
            new Vector3(0.08f, -0.18f, 0.38f);

        public Vector3 foreheadHandLocalEuler =
            new Vector3(-35f, -15f, 45f);

        public Vector3 loweredHandLocalPosition =
            new Vector3(0.35f, -0.65f, 0.4f);

        public float handLowerDuration = 0.8f;

        [Header("평소 손 숨기기")]
        public Transform normalHandRoot;
        public bool hideNormalHandDuringIntro = true;
        public bool hideNormalHandDuringPhone = true;

        [Header("전화기를 바라보는 연출")]
        public Transform phoneLookTarget;
        public float lookAtPhoneDuration = 1.2f;
        public float pauseBeforeControlReturn = 0.25f;

        // ========================================
        // 새 기능: 전화 수화기 연출
        // ========================================

        [Header("수화기 모델")]
        [Tooltip("전화기 전체가 아닌 분리된 수화기 오브젝트")]
        public Transform handset;

        [Header("전화 전용 손")]
        [Tooltip("이마를 짚는 손과 별도로 준비한 손 모델 권장")]
        public GameObject phoneHandVisual;

        [Header("수화기를 잡는 손 위치")]
public Transform phoneHandGripPoint;

        [Header("수화기 귀 옆 위치 - 카메라 기준")]
        public Vector3 handsetEarLocalPosition =
            new Vector3(0.30f, -0.12f, 0.38f);

        public Vector3 handsetEarLocalEuler =
            new Vector3(0f, -20f, 75f);

        [Header("전화 손 시작 위치 - 카메라 기준")]
        public Vector3 phoneHandStartLocalPosition =
            new Vector3(0.35f, -0.65f, 0.45f);

        public Vector3 phoneHandStartLocalEuler =
            new Vector3(0f, 0f, 0f);

        [Header("수화기를 잡는 손 위치 - 수화기 기준")]
        public Vector3 phoneHandGripLocalPosition =
            new Vector3(0f, -0.06f, 0f);

        public Vector3 phoneHandGripLocalEuler =
            new Vector3(0f, 0f, 0f);

        [Header("전화 손 애니메이션 시간")]
        [Min(0.01f)]
        public float handReachDuration = 0.4f;

        [Min(0.01f)]
        public float handsetLiftDuration = 0.65f;

        [Min(0.01f)]
        public float handsetLowerDuration = 0.65f;

        [Min(0.01f)]
        public float handReturnDuration = 0.35f;

        // ========================================
        // 기존 전화 효과음
        // ========================================

        [Header("전화 효과음")]
        public AudioClip testRingWhenStartingHere;
        public AudioSource phoneAudioSource;

        [Tooltip("수화기를 들 때 재생")]
        public AudioClip answerSound;

        [Tooltip("수화기를 내려놓을 때 재생. 비우면 Answer Sound 사용")]
        public AudioClip hangupSound;

        [Header("손 모델의 실제 잡는 위치")]
public Transform phoneHandContactPoint;

        [Range(0f, 1f)]
        public float answerVolume = 1f;

        public float ringFadeOutTime = 0.6f;

        [Header("통화 완료")]
        public GameObject endingPanel;
        public UnityEvent onCallFinished;

        // ========================================
        // 기존 상태 관리
        // ========================================

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

        // ========================================
        // 새 기능: 수화기 원래 상태 보관
        // ========================================

        private Vector3 handsetOriginalPosition;
        private Quaternion handsetOriginalRotation;

        private Rigidbody handsetRigidbody;
        private bool handsetWasKinematic;
        private bool handsetUsedGravity;

        private bool handsetPrepared;

        private Transform phoneHandOriginalParent;
        private Vector3 phoneHandOriginalLocalPosition;
        private Quaternion phoneHandOriginalLocalRotation;
        private Vector3 phoneHandOriginalLocalScale;
        private bool phoneHandPrepared;

        // ========================================
        // 기존 통화 대사 UI 상태
        // ========================================

        private bool slowDialogueVisible;
        private bool dialoguePanelWasActive;
        private string dialogueTextBefore;
        private Color dialogueColorBefore;
        private Vector2 dialogueSizeBefore;

        public string DisplayName =>
            phase == RealityPhase.WaitingForPhone
                ? phoneInteractionName
                : "";

        // ========================================
        // 시작
        // ========================================

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

            if (normalHandRoot == null &&
                PlayerHand.Instance != null)
            {
                normalHandRoot = PlayerHand.Instance.transform;
            }

            // 전화 연출용 손은 처음에 숨긴다.
            if (phoneHandVisual != null)
                phoneHandVisual.SetActive(false);

            // 기존 전화벨 유지
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

            LockControls();

            StartCoroutine(PlayIntro());
        }

        // ========================================
        // 현실 복도 도착 연출
        // ========================================

        private IEnumerator PlayIntro()
        {
            PrepareIntroHand();

            while (SceneFader.IsBusy)
                yield return null;

            yield return new WaitForSeconds(
                Mathf.Max(0f, waitAfterWhiteFade)
            );

            yield return new WaitForSeconds(
                Mathf.Max(0f, foreheadHoldBeforeDialogue)
            );

            // 깨어난 뒤 독백
            yield return EndingDialogueBridge.Play(
                gameUI,
                wakeupDialogue
            );

            yield return LowerIntroHand();

            HideIntroHand();

            // 전화기를 바라봄
            yield return LookAtPhone();

            yield return new WaitForSeconds(
                Mathf.Max(0f, pauseBeforeControlReturn)
            );

            phase = RealityPhase.WaitingForPhone;

            RestoreControls();

            PlayerInteractor.ShowHint(
                "전화벨이 울리고 있다."
            );
        }

        // ========================================
        // 이마를 짚는 손
        // ========================================

        private void PrepareIntroHand()
        {
            if (introHandVisual == null)
                return;

            Transform cam = GetCameraTransform();

            if (cam == null)
                return;

            HideNormalHandRenderers(
                hideNormalHandDuringIntro,
                introHandVisual.transform
            );

            Transform hand = introHandVisual.transform;

            handOriginalParent = hand.parent;
            handOriginalLocalPosition = hand.localPosition;
            handOriginalLocalRotation = hand.localRotation;
            handOriginalLocalScale = hand.localScale;

            introHandPrepared = true;

            hand.SetParent(cam, false);

            hand.localPosition = foreheadHandLocalPosition;

            hand.localRotation =
                Quaternion.Euler(foreheadHandLocalEuler);

            hand.localScale = handOriginalLocalScale;

            introHandVisual.SetActive(true);
        }

        private void HideNormalHandRenderers(
            bool shouldHide,
            Transform cutsceneHand)
        {
            if (!shouldHide || normalHandRoot == null)
                return;

            if (cutsceneHand != null &&
                cutsceneHand.IsChildOf(normalHandRoot))
            {
                Debug.LogWarning(
                    "전화 연출용 손은 HandRoot 밖에 배치해 주세요.",
                    this
                );

                return;
            }

            if (normalHandRenderers != null)
                return;

            normalHandRenderers =
                normalHandRoot.GetComponentsInChildren<Renderer>(true);

            originalRendererStates =
                new bool[normalHandRenderers.Length];

            for (int i = 0; i < normalHandRenderers.Length; i++)
            {
                Renderer renderer = normalHandRenderers[i];

                if (renderer == null)
                    continue;

                originalRendererStates[i] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        private void RestoreNormalHandRenderers()
        {
            if (normalHandRenderers == null ||
                originalRendererStates == null)
            {
                return;
            }

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

        private IEnumerator LowerIntroHand()
        {
            if (introHandVisual == null ||
                !introHandVisual.activeSelf)
            {
                yield break;
            }

            Transform hand = introHandVisual.transform;

            Vector3 start = hand.localPosition;

            float duration =
                Mathf.Max(0.01f, handLowerDuration);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration)
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

        private void HideIntroHand()
        {
            if (introHandVisual != null &&
                introHandPrepared)
            {
                Transform hand = introHandVisual.transform;

                introHandVisual.SetActive(false);

                hand.SetParent(handOriginalParent, false);

                hand.localPosition =
                    handOriginalLocalPosition;

                hand.localRotation =
                    handOriginalLocalRotation;

                hand.localScale =
                    handOriginalLocalScale;

                introHandPrepared = false;
            }

            RestoreNormalHandRenderers();
        }

        // ========================================
        // 전화기를 바라보는 카메라 연출
        // ========================================

        private IEnumerator LookAtPhone()
        {
            if (phoneLookTarget == null)
            {
                Debug.LogWarning(
                    "RealityHallwayEnding: Phone Look Target을 연결하세요.",
                    this
                );

                yield break;
            }

            Transform cameraTransform = GetCameraTransform();

            if (cameraTransform == null)
                yield break;

            Vector3 direction =
                phoneLookTarget.position -
                cameraTransform.position;

            Vector3 horizontal = new Vector3(
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
                Mathf.Max(0.01f, horizontal.magnitude);

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

            float duration =
                Mathf.Max(0.01f, lookAtPhoneDuration);

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
                        startYaw,
                        targetYaw,
                        t
                    );

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

        // ========================================
        // 전화 클릭
        // ========================================

        public void Interact()
        {
            if (phase != RealityPhase.WaitingForPhone)
                return;

            phase = RealityPhase.Calling;

            StartCoroutine(PlayCall());
        }

        // ========================================
        // 전체 통화 연출
        // ========================================

        


private IEnumerator PlayCall()
{
    LockControls();
    EndingPhoneRing.StopRinging(ringFadeOutTime);

    // 전화 전용 손의 손가락 애니메이션
    HandPose phonePose = phoneHandVisual != null
        ? phoneHandVisual.GetComponent<HandPose>()
        : null;

    if (handset != null)
    {
        PrepareHandset();
        PreparePhoneHand();

        // 1. 손가락을 편 상태로 수화기에 접근
        if (phonePose != null)
            phonePose.SetAll(0.05f);

        yield return ReachForHandset();

        // 2. 수화기를 잡으면서 손가락 굽히기
        if (phonePose != null)
        {
            // 수화기를 부드럽게 감싸 쥐는 자세
phonePose.SetFinger(HandPose.Finger.Thumb, 0.25f);
phonePose.SetFinger(HandPose.Finger.Index, 0.3f);
phonePose.SetFinger(HandPose.Finger.Middle, 0.35f);
phonePose.SetFinger(HandPose.Finger.Ring, 0.6f);
phonePose.SetFinger(HandPose.Finger.Pinky, 0.5f);

            yield return new WaitForSeconds(0.3f);
        }

        PlayPhoneHandleSound(true);

        // 3. 손이 수화기를 잡은 채 귀 옆으로 이동
        yield return MoveHandset(true);
    }
    else
    {
        Debug.LogWarning(
            "RealityHallwayEnding: Handset이 연결되지 않았습니다.",
            this
        );

        PlayPhoneHandleSound(true);
    }

    // 4. 손과 수화기를 유지한 채 친구와 통화
    yield return PlayFriendDialogueSlowly();

    if (handsetPrepared)
    {
        // 5. 손이 수화기를 잡은 채 제자리로 내려놓기
        yield return MoveHandset(false);

        PlayPhoneHandleSound(false);

        // 6. 수화기를 놓으면서 손가락 펴기
        if (phonePose != null)
        {
            phonePose.SetAll(0.05f);
            yield return new WaitForSeconds(0.3f);
        }

        // 7. 손을 아래로 내리고 연출 종료
        yield return ReturnPhoneHand();
        RestorePhoneVisuals();
    }
    else
    {
        PlayPhoneHandleSound(false);
    }

    phase = RealityPhase.Finished;

    // 기존 통화 완료 이벤트 유지
    onCallFinished?.Invoke();

    if (endingPanel != null)
        endingPanel.SetActive(true);
    else
        RestoreControls();
}




        // ========================================
        // 수화기 원래 상태 저장
        // ========================================

        private void PrepareHandset()
        {
            if (handset == null || handsetPrepared)
                return;

            handsetOriginalPosition = handset.position;
            handsetOriginalRotation = handset.rotation;

            handsetRigidbody =
                handset.GetComponent<Rigidbody>();

            if (handsetRigidbody != null)
            {
                handsetWasKinematic =
                    handsetRigidbody.isKinematic;

                handsetUsedGravity =
                    handsetRigidbody.useGravity;

                handsetRigidbody.isKinematic = true;
                handsetRigidbody.useGravity = false;
            }

            handsetPrepared = true;

            HideNormalHandRenderers(
                hideNormalHandDuringPhone,
                phoneHandVisual != null
                    ? phoneHandVisual.transform
                    : null
            );
        }

        // ========================================
        // 전화용 손 준비
        // ========================================

        private void PreparePhoneHand()
        {
            if (phoneHandVisual == null)
                return;

            Transform cam = GetCameraTransform();

            if (cam == null)
                return;

            Transform hand = phoneHandVisual.transform;

            phoneHandOriginalParent = hand.parent;
            phoneHandOriginalLocalPosition =
                hand.localPosition;

            phoneHandOriginalLocalRotation =
                hand.localRotation;

            phoneHandOriginalLocalScale =
                hand.localScale;

            phoneHandPrepared = true;

            // 화면 아래쪽에서 손이 등장한다.
            hand.SetPositionAndRotation(
                cam.TransformPoint(
                    phoneHandStartLocalPosition
                ),
                cam.rotation *
                Quaternion.Euler(
                    phoneHandStartLocalEuler
                )
            );

            phoneHandVisual.SetActive(true);
        }

        // ========================================
        // 손이 수화기를 잡으러 이동
        // ========================================

        

        // ========================================
        // 수화기 들어 올리기 / 내려놓기
        // ========================================

        
private IEnumerator MoveHandset(bool toEar)
{
    if (handset == null)
        yield break;

    Transform cam = GetCameraTransform();

    if (cam == null)
        yield break;

    // 수화기 이동 시작 위치와 회전
    Vector3 startPos = handset.position;
    Quaternion startRot = handset.rotation;

    // 현재 손이 수화기를 잡고 있는 상대 위치와 회전 저장
    Transform hand = phoneHandPrepared && phoneHandVisual != null
        ? phoneHandVisual.transform
        : null;

    Vector3 handLocalPos = Vector3.zero;
    Quaternion handLocalRot = Quaternion.identity;

    if (hand != null)
    {
        // 수화기 기준으로 손의 위치와 회전을 기억
        handLocalPos = handset.InverseTransformPoint(hand.position);

        handLocalRot =
            Quaternion.Inverse(handset.rotation) * hand.rotation;
    }

    float duration = Mathf.Max(
        0.01f,
        toEar ? handsetLiftDuration : handsetLowerDuration
    );

    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;

        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(elapsed / duration)
        );

        // 기존에 맞춰 놓은 귀 옆 위치 유지
        Vector3 targetPos = toEar
            ? cam.TransformPoint(handsetEarLocalPosition)
            : handsetOriginalPosition;

        Quaternion targetRot = toEar
            ? cam.rotation * Quaternion.Euler(handsetEarLocalEuler)
            : handsetOriginalRotation;

        // 수화기 이동 및 회전
        handset.SetPositionAndRotation(
            Vector3.Lerp(startPos, targetPos, t),
            Quaternion.Slerp(startRot, targetRot, t)
        );

        // 손도 수화기와 동일한 회전량으로 움직이기
        if (hand != null)
        {
            hand.SetPositionAndRotation(
                handset.TransformPoint(handLocalPos),
                handset.rotation * handLocalRot
            );
        }

        yield return null;
    }

    // 최종 위치 보정
    Vector3 finalPos = toEar
        ? cam.TransformPoint(handsetEarLocalPosition)
        : handsetOriginalPosition;

    Quaternion finalRot = toEar
        ? cam.rotation * Quaternion.Euler(handsetEarLocalEuler)
        : handsetOriginalRotation;

    handset.SetPositionAndRotation(finalPos, finalRot);

    // 손도 마지막 위치와 회전 정확하게 맞추기
    if (hand != null)
    {
        hand.SetPositionAndRotation(
            handset.TransformPoint(handLocalPos),
            handset.rotation * handLocalRot
        );
    }
}


        // ========================================
        // 수화기를 잡은 손 위치 계산
        // ========================================

        private Vector3 GetGripPosition()
{
    if (phoneHandGripPoint != null)
        return phoneHandGripPoint.position;

    return handset.TransformPoint(
        phoneHandGripLocalPosition
    );
}

        private Quaternion GetGripRotation()
{
    if (phoneHandGripPoint != null)
        return phoneHandGripPoint.rotation;

    return handset.rotation *
        Quaternion.Euler(phoneHandGripLocalEuler);
}

        

        // ========================================
        // 전화기를 내려놓은 후 손 내리기
        // ========================================


private void GetDesiredHandPose(
    out Vector3 targetPosition,
    out Quaternion targetRotation)
{
    Transform hand = phoneHandVisual.transform;

    // 손에 기준 포인트가 없으면 기존 방식
    if (phoneHandContactPoint == null ||
        !phoneHandContactPoint.IsChildOf(hand))
    {
        targetPosition = GetGripPosition();
        targetRotation = GetGripRotation();
        return;
    }

    // 손바닥 포인트의 회전을
    // 수화기 잡는 포인트와 정확히 일치시킴
    Quaternion rotationDifference =
        GetGripRotation() *
        Quaternion.Inverse(
            phoneHandContactPoint.rotation
        );

    targetRotation =
        rotationDifference * hand.rotation;

    // 손 모델의 피벗이 아닌
    // 실제 손바닥 위치를 기준으로 이동
    Vector3 handOffset =
        phoneHandContactPoint.position -
        hand.position;

    targetPosition =
        GetGripPosition() -
        rotationDifference * handOffset;
}

// 손이 수화기를 잡으러 이동
private IEnumerator ReachForHandset()
{
    if (!phoneHandPrepared ||
        phoneHandVisual == null ||
        handset == null)
    {
        yield break;
    }

    Transform hand = phoneHandVisual.transform;

    Vector3 startPos = hand.position;
    Quaternion startRot = hand.rotation;

    GetDesiredHandPose(
        out Vector3 targetPos,
        out Quaternion targetRot
    );

    float duration =
        Mathf.Max(0.01f, handReachDuration);

    float elapsed = 0f;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;

        float t = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(elapsed / duration)
        );

        hand.SetPositionAndRotation(
            Vector3.Lerp(startPos, targetPos, t),
            Quaternion.Slerp(startRot, targetRot, t)
        );

        yield return null;
    }

    SetHandToGrip();
}

// 손바닥 포인트와 수화기 포인트를 일치
private void SetHandToGrip()
{
    if (!phoneHandPrepared ||
        phoneHandVisual == null ||
        handset == null)
    {
        return;
    }

    GetDesiredHandPose(
        out Vector3 targetPos,
        out Quaternion targetRot
    );

    phoneHandVisual.transform.SetPositionAndRotation(
        targetPos,
        targetRot
    );
}


        private IEnumerator ReturnPhoneHand()
        {
            if (!phoneHandPrepared ||
                phoneHandVisual == null)
            {
                yield break;
            }

            Transform cam = GetCameraTransform();

            if (cam == null)
                yield break;

            Transform hand = phoneHandVisual.transform;

            Vector3 startPos = hand.position;
            Quaternion startRot = hand.rotation;

            float duration =
                Mathf.Max(0.01f, handReturnDuration);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration)
                );

                Vector3 targetPos =
                    cam.TransformPoint(
                        phoneHandStartLocalPosition
                    );

                Quaternion targetRot =
                    cam.rotation *
                    Quaternion.Euler(
                        phoneHandStartLocalEuler
                    );

                hand.SetPositionAndRotation(
                    Vector3.Lerp(
                        startPos,
                        targetPos,
                        t
                    ),
                    Quaternion.Slerp(
                        startRot,
                        targetRot,
                        t
                    )
                );

                yield return null;
            }
        }

        // ========================================
        // 수화기와 손 원상복구
        // ========================================

        private void RestorePhoneVisuals()
        {
            if (handsetPrepared && handset != null)
            {
                handset.SetPositionAndRotation(
                    handsetOriginalPosition,
                    handsetOriginalRotation
                );

                if (handsetRigidbody != null)
                {
                    handsetRigidbody.isKinematic =
                        handsetWasKinematic;

                    handsetRigidbody.useGravity =
                        handsetUsedGravity;
                }
            }

            handsetPrepared = false;
            handsetRigidbody = null;

            if (phoneHandPrepared &&
                phoneHandVisual != null)
            {
                Transform hand = phoneHandVisual.transform;

                phoneHandVisual.SetActive(false);

                hand.SetParent(
                    phoneHandOriginalParent,
                    false
                );

                hand.localPosition =
                    phoneHandOriginalLocalPosition;

                hand.localRotation =
                    phoneHandOriginalLocalRotation;

                hand.localScale =
                    phoneHandOriginalLocalScale;
            }

            phoneHandPrepared = false;

            RestoreNormalHandRenderers();
        }

        // ========================================
        // 카메라 Transform
        // ========================================

        private Transform GetCameraTransform()
        {
            if (interactor != null &&
                interactor.playerCamera != null)
            {
                return interactor.playerCamera.transform;
            }

            Camera main = Camera.main;

            if (main != null)
                return main.transform;

            if (player != null)
                return player.cameraRoot;

            return null;
        }

        // ========================================
        // 기존 친구 통화 대사
        // ========================================

        private IEnumerator PlayFriendDialogueSlowly()
        {
            if (gameUI == null ||
                friendPhoneDialogue == null ||
                friendPhoneDialogue.lines == null ||
                friendPhoneDialogue.lines.Length == 0 ||
                gameUI.dialoguePanel == null ||
                gameUI.dialogueText == null)
            {
                Debug.LogError(
                    "RealityHallwayEnding: 통화 대사 또는 GameUI 패널 연결을 확인하세요.",
                    this
                );

                yield break;
            }

            while (gameUI != null &&
                (gameUI.IsDialogueOpen || gameUI.IsPaused))
            {
                yield return null;
            }

            if (gameUI == null)
                yield break;

            dialoguePanelWasActive =
                gameUI.dialoguePanel.activeSelf;

            dialogueTextBefore =
                gameUI.dialogueText.text;

            dialogueColorBefore =
                gameUI.dialogueText.color;

            RectTransform panelRect =
                gameUI.dialoguePanel.transform as RectTransform;

            if (panelRect != null)
                dialogueSizeBefore = panelRect.sizeDelta;

            slowDialogueVisible = true;

            gameUI.dialoguePanel.SetActive(true);

            for (int i = 0;
                 i < friendPhoneDialogue.lines.Length;
                 i++)
            {
                if (gameUI == null)
                    break;

                string line =
                    friendPhoneDialogue.lines[i] ?? "";

                gameUI.dialogueText.text = line;

                // 기존 대사별 색상 적용 유지
                gameUI.dialogueText.color =
                    friendPhoneDialogue.GetLineColor(i);

                if (panelRect != null)
                {
                    Vector2 size =
                        gameUI.dialogueText.GetPreferredValues(
                            line,
                            880f,
                            Mathf.Infinity
                        );

                    panelRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        Mathf.Clamp(
                            size.x + 40f,
                            80f,
                            920f
                        )
                    );

                    panelRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        Mathf.Max(
                            56f,
                            size.y + 20f
                        )
                    );
                }

                float remaining =
                    Mathf.Max(1f, friendLineDuration);

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
                gameUI.dialogueText.text =
                    dialogueTextBefore;

                gameUI.dialogueText.color =
                    dialogueColorBefore;
            }

            if (gameUI.dialoguePanel != null)
            {
                RectTransform rect =
                    gameUI.dialoguePanel.transform as RectTransform;

                if (rect != null)
                    rect.sizeDelta = dialogueSizeBefore;

                gameUI.dialoguePanel.SetActive(
                    dialoguePanelWasActive
                );
            }
        }

        // ========================================
        // 전화 효과음
        // ========================================

        private void PlayPhoneHandleSound(bool pickingUp)
        {
            AudioClip clip = pickingUp
                ? answerSound
                : (hangupSound != null
                    ? hangupSound
                    : answerSound);

            if (clip == null)
                return;

            if (phoneAudioSource != null)
            {
                phoneAudioSource.PlayOneShot(
                    clip,
                    answerVolume
                );
            }
            else
            {
                AudioManager.Play(
                    clip,
                    answerVolume
                );
            }
        }

        // ========================================
        // 기존 입력 제한
        // ========================================

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

    walkSilencer =
        CorridorCutsceneWalkSilencer.GetOrCreate(player);

    if (walkSilencer != null)
        walkSilencer.Begin();

    // 통화 중 기존 안내 문구가 남지 않도록 처리
    if (interactor.nameText != null)
    {
        savedNameTextActive =
            interactor.nameText.gameObject.activeSelf;

        interactor.nameText.text = "";

        // 글자가 다시 갱신되더라도 화면에 보이지 않게 숨김
        interactor.nameText.gameObject.SetActive(false);
    }

    // 조준 UI 숨기기
    if (interactor.crosshairDot != null)
        interactor.crosshairDot.SetActive(false);

    if (interactor.handIcon != null)
        interactor.handIcon.SetActive(false);

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

    // 안내 문구 초기화 및 원래 표시 상태 복원
    if (interactor.nameText != null)
    {
        interactor.nameText.text = "";

        interactor.nameText.gameObject.SetActive(
            savedNameTextActive
        );
    }

    player.enabled = savedPlayerEnabled;
    interactor.enabled = savedInteractorEnabled;

    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
}

        // ========================================
        // 오브젝트 비활성화 시 정리
        // ========================================

        private void OnDisable()
        {
            RestoreSlowDialogueUI();
            RestorePhoneVisuals();
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
