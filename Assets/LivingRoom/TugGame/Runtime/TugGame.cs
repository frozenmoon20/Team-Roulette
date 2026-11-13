using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TeamRoulette.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace TeamRoulette.Room3.TugPlay
{
    public enum TugDirection { W, A, S, D }
    public enum TugPhase { Inactive, Preparing, Waiting, Reacting, Exchanging, Celebrating, Completed, Cancelled }
    [Serializable] public sealed class TugProgressEvent : UnityEvent<int, int> { }

    [DefaultExecutionOrder(50)]
    public sealed class TugGame : MonoBehaviour
    {
        public const string ToyId = "로프";
        [Header("기존 씬 연결")]
        public FirstPersonPlayer player;
        public Camera playerCamera;
        public PlayerInteractor interactor;
        public Transform winter, mouth, head, playerStand;
        public Animator winterAnimator;
        public RuntimeAnimatorController existingController;
        public GameObject sceneRope;
        public GameObject[] ropePrefabs = Array.Empty<GameObject>();
        public InputActionAsset inputActions;
        public GameUI sharedUI;
        public AudioSource barkSource;
        public AudioClip barkClip;

        [Header("진행")]
        public bool autoStart = true;
        public bool playWhenSceneOpenedDirectly = true;
        public bool returnToLivingRoom = true;
        [Min(1)] public int roundCount = 2;
        [Min(1)] public int successesPerRound = 5;
        [Min(.1f)] public float responseSeconds = .6f;
        [Min(.1f)] public float exchangeWalkSeconds = 1.6f;
        [Min(.1f)] public float skyLookSeconds = 1.2f;
        [Min(.1f)] public float celebrationSeconds = 2.2f;

        [Header("3D 움직임 / 플레이어 기준")]
        [Min(.7f)] public float dogDistance = 1.45f;
        [Range(.2f, 1f)] public float sittingEyeHeight = .88f;
        [Min(.01f)] public float pullDistance = .16f;
        [Min(.01f)] public float sideDistance = .22f;
        [Min(.01f)] public float wrongPullDistance = .28f;
        [Range(0, 25)] public float headSwingDegrees = 12;
        [Range(0, 1)] public float cameraReactionDegrees = .32f;
        public Vector3 handOffset = new Vector3(.16f, -.27f, .52f);
        public Vector3 mouthOffset = new Vector3(0, 0, 0);
        [Min(0)] public float muzzleDrop = .055f;
        [Min(0)] public float carryingMouthLift = .02f;
        [Min(.1f)] public float hangingLength = .65f;
        [Min(.02f)] public float gripTransitionSeconds = .22f;
        public Vector3 carryHandOffset = new Vector3(0, -.08f, .65f);

        [Header("기존 Animator 상태")]
        public string idleState = "Arm_Spitz|Idle_1";
        public string biteState = "Arm_Spitz|Attack_Bite_IP";
        public string braceState = "Arm_Spitz|Crouch_Idle_loop_1";
        public string leftState = "Arm_Spitz|Crouch_R_IP";
        public string rightState = "Arm_Spitz|Crouch_L_IP";
        public string forwardState = "Arm_Spitz|Crouch_F_IP";
        public string backState = "Arm_Spitz|Crouch_B_IP";
        public string walkState = "Arm_Spitz|Trot_F_IP";
        public string barkState = "Arm_Spitz|Bark";

        [Header("이벤트")]
        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onWrongInput = new UnityEvent();
        public TugProgressEvent onProgress = new TugProgressEvent();
        public TugPhase Phase { get; private set; }
        public TugDirection ExpectedDirection { get; private set; }
        public int RoundIndex { get; private set; }
        public int RoundSuccesses { get; private set; }
        public int TotalSuccesses { get; private set; }
        public int WrongInputs { get; private set; }
        public bool OwnsInput => owned;
        public Transform MouthGrip => mouthGrip;
        public Transform HandGrip => handGrip;
        public GameObject CurrentRope => ropeInstance;
        public bool IsPaused => Time.timeScale <= 0 || (sharedUI && sharedUI.IsPaused);

        bool owned, completedRaised, originalPlayerEnabled, originalMove, originalLook, originalJump, originalSprint;
        bool originalInteractor, originalHandActive, originalRootMotion, originalRopeActive;
        bool originalCursorVisible, originalCrosshair, originalHandIcon;
        bool originalHandEnabled, originalAgentEnabled, originalAgentStopped, headApplied;
        CursorLockMode originalCursor;
        Vector3 playerPosition, winterPosition, cameraPosition, handPosition, homeDog;
        Quaternion playerRotation, winterRotation, cameraRotation, handRotation, animatedHead;
        RuntimeAnimatorController originalController;
        AnimatorStateInfo originalAnimatorState;
        AnimatorCullingMode originalCulling;
        PlayerHand hand;
        HandPose handPose;
        float[] fingerPose;
        NavMeshAgent agent;
        readonly List<Behaviour> blocked = new List<Behaviour>();
        GameObject ropeInstance, hud;
        SimpleTugRope solver;
        Transform mouthGrip, handGrip, bendGrip;
        TMP_Text prompt, progress, status;
        SceneUIContent content;
        SceneUIContent.Objective objective;
        InputActionMap tugActions;
        InputAction[] directions;
        Coroutine routine;
        Vector3 dogOffset, handPull;
        float headYaw, cameraKick, viewPitch = 20, eyeHeight;
        bool carrying;
        float carryingBlend;
        TugCarryPose carryPose;
        TeamRoulette.Room3.BallPlay.BallGame ballGame;
        bool ballAutoStart;

        void Awake()
        {
            // Standalone scene preview defaults to this game. Visits keep the existing toy routing.
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (playWhenSceneOpenedDirectly && (!flow || string.IsNullOrEmpty(flow.VisitingObject)))
            {
                ballGame = GetSceneComponents<TeamRoulette.Room3.BallPlay.BallGame>().FirstOrDefault();
                if (ballGame) { ballAutoStart = ballGame.autoStart; ballGame.autoStart = false; }
            }
        }

        IEnumerator Start()
        {
            if (!autoStart) yield break;
            yield return null;
            while (GameManager.Instance && GameManager.Instance.Flow.IsTransitioning) yield return null;
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (flow && !string.IsNullOrEmpty(flow.VisitingObject) && flow.VisitingObject != ToyId) yield break;
            if ((!flow || string.IsNullOrEmpty(flow.VisitingObject)) && !playWhenSceneOpenedDirectly) yield break;
            BeginGame();
        }

        IEnumerable<T> GetSceneComponents<T>() where T : Component => gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));

        public bool BeginGame()
        {
            if (owned || Phase != TugPhase.Inactive || gameObject.scene.name != Room3FlowController.ToyRoom) return false;
            if (!player || !player.cameraRoot || !playerCamera || !winter || !winterAnimator || !mouth || !head || !playerStand
                || !sharedUI || !sharedUI.hud || !sharedUI.objectiveText || !sceneRope)
            { Debug.LogError("[TugGame] 필수 씬 연결이 누락되었습니다.", this); return false; }
            if (!winterAnimator.runtimeAnimatorController && !existingController) return false;
            var initialSource = ropePrefabs != null && ropePrefabs.Length > 0 && ropePrefabs[0] ? ropePrefabs[0] : sceneRope;
            if (!initialSource.GetComponentInChildren<SimpleTugRope>(true))
            { Debug.LogError("[TugGame] 리깅된 로프의 SimpleTugRope를 연결해주세요.", this); return false; }
            if (ropePrefabs != null && ropePrefabs.Any(p => p && !p.GetComponentInChildren<SimpleTugRope>(true)))
            { Debug.LogError("[TugGame] 로프 목록의 모든 Prefab에 SimpleTugRope가 필요합니다.", this); return false; }
            roundCount = Mathf.Max(1, roundCount); successesPerRound = Mathf.Max(1, successesPerRound);
            CaptureAndLock();
            Phase = TugPhase.Preparing;
            MakeTargets(); MakeUI(); MakeActions();
            homeDog = playerStand.position + playerStand.forward * dogDistance;
            winter.SetPositionAndRotation(homeDog, Quaternion.LookRotation(-playerStand.forward));
            eyeHeight = sittingEyeHeight;
            SpawnRope(0);
            RoundIndex = 1; RoundSuccesses = TotalSuccesses = WrongInputs = 0;
            routine = StartCoroutine(Prepare());
            Debug.Log("[TugGame] 시작: 기존 Player / Animator / mouth / SimpleTugRope 재사용", this);
            return true;
        }

        void CaptureAndLock()
        {
            playerPosition = player.transform.position; playerRotation = player.transform.rotation;
            winterPosition = winter.position; winterRotation = winter.rotation;
            cameraPosition = player.cameraRoot.localPosition; cameraRotation = player.cameraRoot.localRotation;
            originalPlayerEnabled = player.enabled;
            originalMove = player.canMove; originalLook = player.canLook; originalJump = player.canJump; originalSprint = player.canSprint;
            originalCursor = Cursor.lockState; originalCursorVisible = Cursor.visible;
            originalInteractor = interactor && interactor.enabled;
            if (interactor)
            {
                originalCrosshair = interactor.crosshairDot && interactor.crosshairDot.activeSelf;
                originalHandIcon = interactor.handIcon && interactor.handIcon.activeSelf;
                interactor.enabled = false;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(false);
                if (interactor.handIcon) interactor.handIcon.SetActive(false);
                if (interactor.nameText) interactor.nameText.text = "";
            }
            foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
                if (b && b.enabled && b.GetType().Name == "PlayerMotionFeel") { blocked.Add(b); b.enabled = false; }
            foreach (var target in GetSceneComponents<Room3FlowTarget>())
                if (target && target.enabled && target.ObjectId == ToyId)
                { blocked.Add(target); target.enabled = false; }
            hand = player.GetComponentInChildren<PlayerHand>(true);
            if (hand && hand.handModel)
            {
                originalHandActive = hand.handModel.activeSelf; originalHandEnabled = hand.enabled;
                handPosition = hand.handModel.transform.localPosition; handRotation = hand.handModel.transform.localRotation;
                handPose = hand.GetComponent<HandPose>();
                if (handPose) fingerPose = Enumerable.Range(0, 5).Select(i => handPose.GetFinger((HandPose.Finger)i)).ToArray();
                hand.handModel.SetActive(true); hand.SetGrip(.8f); hand.enabled = false;
            }
            agent = winter.GetComponent<NavMeshAgent>(); originalAgentEnabled = agent && agent.enabled;
            if (agent && agent.enabled) { if (agent.isOnNavMesh) { originalAgentStopped = agent.isStopped; agent.isStopped = true; } agent.enabled = false; }
            originalController = winterAnimator.runtimeAnimatorController; originalRootMotion = winterAnimator.applyRootMotion;
            originalCulling = winterAnimator.cullingMode;
            if (originalController) originalAnimatorState = winterAnimator.GetCurrentAnimatorStateInfo(0);
            if (!originalController) winterAnimator.runtimeAnimatorController = existingController;
            winterAnimator.applyRootMotion = false; winterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            originalRopeActive = sceneRope.activeSelf;
            owned = true;
            player.canMove = player.canLook = player.canJump = player.canSprint = false;
            player.enabled = false;
            player.Teleport(playerStand.position, playerStand.rotation, Quaternion.identity);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            sceneRope.SetActive(false);
        }

        void MakeTargets()
        {
            mouthGrip = NewTarget("Tug Mouth Attachment"); handGrip = NewTarget("Tug Hand Attachment"); bendGrip = NewTarget("Tug Bend");
        }
        Transform NewTarget(string label) { var go = new GameObject(label); go.transform.SetParent(transform, false); return go.transform; }

        void MakeActions()
        {
            // Own four button actions; never disable or edit the shared Player action asset.
            tugActions = new InputActionMap("TugGame");
            directions = new InputAction[4];
            for (int i = 0; i < 4; i++) directions[i] = tugActions.AddAction(((TugDirection)i).ToString(), InputActionType.Button,
                "<Keyboard>/" + ((TugDirection)i).ToString().ToLowerInvariant());
            tugActions.Enable();
        }

        IEnumerator Prepare()
        {
            SetStatus("겨울이가 로프를 물었어요."); Play(biteState);
            yield return Wait(.65f);
            Play(braceState); NextPrompt();
        }
        void NextPrompt()
        {
            int next = UnityEngine.Random.Range(0, 4);
            if (TotalSuccesses > 0 && next == (int)ExpectedDirection) next = (next + UnityEngine.Random.Range(1, 4)) % 4;
            ExpectedDirection = (TugDirection)next;
            Phase = TugPhase.Waiting;
            SetStatus(""); RefreshUI();
        }

        void Update()
        {
            if (headApplied && head) { head.localRotation = animatedHead; headApplied = false; }
            if (!owned || Phase != TugPhase.Waiting || IsPaused || !Application.isFocused) return;
            int pressed = -1, count = 0;
            for (int i = 0; i < directions.Length; i++) if (directions[i].WasPressedThisFrame()) { pressed = i; count++; }
            if (count == 0) return;
            // Chords never earn a success. Held keys cannot repeat into the next prompt.
            int held = directions.Count(action => action.IsPressed());
            SubmitDirection((TugDirection)pressed, count == 1 && held <= 1);
        }
        public bool SubmitDirection(TugDirection direction) => SubmitDirection(direction, true);
        bool SubmitDirection(TugDirection direction, bool singleKey)
        {
            if (!owned || Phase != TugPhase.Waiting || IsPaused) return false;
            bool correct = singleKey && direction == ExpectedDirection;
            Phase = TugPhase.Reacting;
            if (correct) { RoundSuccesses++; TotalSuccesses++; onProgress.Invoke(RoundIndex, TotalSuccesses); }
            else { WrongInputs++; onWrongInput.Invoke(); }
            RefreshUI();
            routine = StartCoroutine(React(direction, correct));
            return correct;
        }

        IEnumerator React(TugDirection direction, bool correct)
        {
            Vector3 side = playerStand.right, forward = playerStand.forward;
            Vector3 offset = direction == TugDirection.W ? -forward * pullDistance
                : direction == TugDirection.S ? forward * pullDistance * .7f
                : side * (direction == TugDirection.A ? -sideDistance : sideDistance);
            if (!correct) offset = -offset.normalized * wrongPullDistance;
            string state = direction == TugDirection.A ? leftState : direction == TugDirection.D ? rightState
                : direction == TugDirection.W ? forwardState : backState;
            Play(correct ? state : biteState);
            SetStatus(correct ? "잘했어요!" : "겨울이가 반대로 당겨요. 다시 해보세요.");
            float duration = Mathf.Max(.1f, responseSeconds);
            for (float t = 0; t < duration;)
            {
                if (IsPaused) { yield return null; continue; }
                t += Time.deltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                dogOffset = offset * wave;
                handPull = offset * (correct ? .6f : .85f) * wave;
                headYaw = (direction == TugDirection.A ? -1 : direction == TugDirection.D ? 1 : .4f) * headSwingDegrees * wave * (correct ? 1 : -1.3f);
                cameraKick = correct ? 0 : Mathf.Sin(t / duration * Mathf.PI * 2) * cameraReactionDegrees * wave;
                yield return null;
            }
            dogOffset = handPull = Vector3.zero; headYaw = cameraKick = 0;
            Play(braceState);
            if (correct && RoundSuccesses >= successesPerRound)
            {
                if (RoundIndex >= roundCount) yield return Celebrate();
                else yield return Exchange();
            }
            else if (correct) NextPrompt();
            else { yield return Wait(.25f); Phase = TugPhase.Waiting; RefreshUI(); }
        }

        IEnumerator Exchange()
        {
            Phase = TugPhase.Exchanging; RefreshUI(); carrying = true;
            if (hand && hand.handModel) hand.handModel.SetActive(false);
            SetStatus("겨울이가 로프를 가져갑니다.");
            Vector3 start = homeDog;
            Vector3 away = start + playerStand.forward * 1.5f + playerStand.right * .55f;
            Play(walkState);
            yield return MoveDog(start, away, Quaternion.LookRotation(away - start), exchangeWalkSeconds);
            Play(idleState);
            SetStatus("잠깐 일어나 하늘을 바라봅니다.");
            yield return MoveView(cameraPosition.y, -48, .85f);
            yield return Wait(skyLookSeconds);
            // Keep one prepared toy when the list contains only one entry; repeat cyclically.
            SpawnRope(RoundIndex);
            Play(barkState); if (barkSource && barkClip) barkSource.PlayOneShot(barkClip);
            SetStatus("멍! 겨울이가 다시 놀자고 부릅니다.");
            yield return MoveView(cameraPosition.y, 18, .55f);
            Play(walkState);
            yield return MoveDog(away, start, Quaternion.LookRotation(start - away), exchangeWalkSeconds);
            yield return TurnDog(Quaternion.LookRotation(-playerStand.forward), .35f);
            Play(biteState);
            yield return MoveView(sittingEyeHeight, 20, .75f);
            carrying = false;
            if (hand && hand.handModel) hand.handModel.SetActive(true);
            RoundIndex++; RoundSuccesses = 0;
            SetStatus("겨울이가 물고 있는 로프를 다시 잡았어요.");
            yield return Wait(.65f);
            Play(braceState); NextPrompt();
        }

        IEnumerator MoveDog(Vector3 from, Vector3 to, Quaternion facing, float seconds)
        {
            Quaternion initial = winter.rotation;
            for (float t = 0; t < Mathf.Max(.1f, seconds);)
            {
                if (IsPaused) { yield return null; continue; }
                t += Time.deltaTime; float p = Mathf.Clamp01(t / Mathf.Max(.1f, seconds));
                winter.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, p));
                winter.rotation = Quaternion.Slerp(initial, facing, Mathf.Clamp01(p * 4));
                yield return null;
            }
            winter.position = to;
        }
        IEnumerator TurnDog(Quaternion rotation, float seconds)
        {
            var from = winter.rotation;
            for (float t = 0; t < seconds;)
            {
                if (IsPaused) { yield return null; continue; }
                t += Time.deltaTime; winter.rotation = Quaternion.Slerp(from, rotation, Mathf.SmoothStep(0, 1, t / seconds)); yield return null;
            }
        }
        IEnumerator MoveView(float height, float pitch, float seconds)
        {
            float oldHeight = eyeHeight, oldPitch = viewPitch;
            for (float t = 0; t < seconds;)
            {
                if (IsPaused) { yield return null; continue; }
                t += Time.deltaTime; float p = Mathf.SmoothStep(0, 1, t / seconds);
                eyeHeight = Mathf.Lerp(oldHeight, height, p); viewPitch = Mathf.Lerp(oldPitch, pitch, p); yield return null;
            }
        }

        IEnumerator Celebrate()
        {
            Phase = TugPhase.Celebrating; carrying = true; RefreshUI();
            if (hand && hand.handModel) hand.handModel.SetActive(false);
            SetStatus("겨울이가 신나게 로프를 흔듭니다!"); Play(biteState);
            float duration = Mathf.Max(.1f, celebrationSeconds);
            for (float t = 0; t < duration;)
            {
                if (IsPaused) { yield return null; continue; }
                t += Time.deltaTime;
                headYaw = Mathf.Sin(t * 15) * headSwingDegrees * 1.5f;
                winter.rotation = Quaternion.LookRotation(-playerStand.forward);
                yield return null;
            }
            headYaw = 0; Play(barkState);
            if (barkSource && barkClip) barkSource.PlayOneShot(barkClip);
            yield return Wait(.85f); Play(idleState);
            Phase = TugPhase.Completed; RefreshUI(); SetStatus("겨울이와 터그놀이를 마쳤어요.");
            if (!completedRaised)
            {
                completedRaised = true; onCompleted.Invoke();
                Debug.Log("[TugGame] 완료 이벤트 1회 호출 / 성공 " + TotalSuccesses + "회", this);
            }
            yield return Wait(.8f);
            Restore();
            if (returnToLivingRoom && GameManager.Instance && GameManager.Instance.Flow.VisitingObject == ToyId)
                GameManager.Instance.Flow.CompleteVisit(ToyId);
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds;) { if (!IsPaused) t += Time.deltaTime; yield return null; }
        }

        void LateUpdate()
        {
            if (!owned) return;
            if (Phase == TugPhase.Waiting || Phase == TugPhase.Reacting || Phase == TugPhase.Preparing)
                winter.position = homeDog + dogOffset;
            player.canMove = player.canLook = player.canJump = player.canSprint = false;
            player.cameraRoot.localPosition = new Vector3(cameraPosition.x, eyeHeight, cameraPosition.z);
            player.cameraRoot.localRotation = Quaternion.Euler(viewPitch + cameraKick, 0, 0);
            if (head)
            {
                animatedHead = head.localRotation;
                head.localRotation = animatedHead * Quaternion.Euler(0, headYaw, -headYaw * .4f); headApplied = true;
            }
            if (!IsPaused)
            {
                carryingBlend = Mathf.MoveTowards(carryingBlend, carrying ? 1 : 0,
                    Time.deltaTime / Mathf.Max(.02f, gripTransitionSeconds));
            }
            float blend = Mathf.SmoothStep(0, 1, carryingBlend);
            mouthGrip.SetPositionAndRotation(mouth.TransformPoint(mouthOffset)
                + Vector3.up * (carryingMouthLift * blend - muzzleDrop), mouth.rotation);
            Vector3 handPoint = playerCamera.transform.TransformPoint(handOffset) + handPull;
            if (blend > 0)
            {
                // The mouth bone's axes change during a bark. Use its actual position relative to the body.
                Vector3 outward = Vector3.ProjectOnPlane(mouthGrip.position - winter.position, Vector3.up);
                if (outward.sqrMagnitude < .0001f) outward = winter.forward;
                Vector3 freeEnd = carryPose.Build(mouthGrip.position, outward.normalized,
                    playerStand.position.y + .055f, hangingLength);
                handPoint = Vector3.Lerp(handPoint, freeEnd, blend);
            }
            carryPose.Weight = blend;
            handGrip.position = handPoint;
            bendGrip.position = (mouthGrip.position + handGrip.position) * .5f
                + Vector3.down * Mathf.Lerp(.06f, .13f, carryingBlend) + winter.right * (.04f * carryingBlend);
            if (hand && hand.handModel && hand.handModel.activeSelf && hand.palmPoint)
            {
                // Translate the existing hand so its grip stays on the rope, retaining its authored rotation.
                hand.handModel.transform.position += handPoint - hand.palmPoint.position;
            }
        }

        void SpawnRope(int index)
        {
            if (ropeInstance) { ropeInstance.SetActive(false); Destroy(ropeInstance); }
            GameObject source = ropePrefabs != null && ropePrefabs.Length > 0 ? ropePrefabs[index % ropePrefabs.Length] : null;
            if (!source) source = sceneRope;
            ropeInstance = Instantiate(source, transform); ropeInstance.name = "Tug Rope Round " + (index + 1);
            ropeInstance.SetActive(false);
            // Independent clone: do not alter the original rope, imported rig, shared mesh, or material.
            foreach (var collider in ropeInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in ropeInstance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var b in ropeInstance.GetComponentsInChildren<Behaviour>(true))
                if (b is IInteractable || b is Animator) b.enabled = false;
            solver = ropeInstance.GetComponentInChildren<SimpleTugRope>(true);
            if (!solver) throw new InvalidOperationException("로프 Prefab에 SimpleTugRope가 필요합니다.");
            solver.mouthTarget = mouthGrip; solver.handTarget = handGrip; solver.bendTarget = bendGrip; solver.allowStretch = true;
            carryPose = solver.gameObject.AddComponent<TugCarryPose>();
            carryPose.Bind(solver.transform);
            foreach (var r in ropeInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
            ropeInstance.SetActive(true); solver.enabled = true;
        }

        void Play(string state)
        {
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!winterAnimator.HasState(0, hash)) hash = Animator.StringToHash("Base Layer." + idleState);
            if (winterAnimator.HasState(0, hash)) winterAnimator.CrossFadeInFixedTime(hash, .12f, 0, 0);
        }

        void MakeUI()
        {
            hud = new GameObject("Tug HUD", typeof(RectTransform)); hud.transform.SetParent(sharedUI.hud.transform, false);
            var rect = (RectTransform)hud.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            prompt = Label("Direction", new Vector2(.5f, .27f), new Vector2(500, 75), 38);
            progress = Label("Rounds", new Vector2(.5f, .19f), new Vector2(700, 42), 23);
            status = Label("Status", new Vector2(.5f, .12f), new Vector2(900, 44), 23);
            content = sharedUI.GetComponent<SceneUIContent>();
            if (content) { objective = new SceneUIContent.Objective { id = "TugGame.Play", text = "겨울이와 로프 터그놀이를 해보자" }; content.objectives.Insert(0, objective); content.Refresh(); }
        }
        TMP_Text Label(string label, Vector2 anchor, Vector2 size, float fontSize)
        {
            var text = Instantiate(sharedUI.objectiveText, hud.transform, false); text.name = label;
            var rt = text.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
            text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = false; text.fontSize = fontSize;
            text.color = Color.white; text.outlineWidth = .18f; text.outlineColor = new Color32(15, 25, 20, 255);
            text.raycastTarget = false; text.text = ""; return text;
        }
        void RefreshUI()
        {
            if (!prompt) return;
            string[] arrows = { "위", "왼쪽", "아래", "오른쪽" };
            prompt.text = Phase == TugPhase.Waiting ? ExpectedDirection + "  " + arrows[(int)ExpectedDirection] : "";
            progress.text = Phase == TugPhase.Completed ? "터그놀이 완료 · " + TotalSuccesses + "회 성공"
                : RoundIndex + " / " + roundCount + " 라운드    " + RoundSuccesses + " / " + successesPerRound + "회 성공";
        }
        void SetStatus(string message) { if (status) status.text = message; }

        public void CancelGame()
        {
            if (!owned) return;
            if (routine != null) StopCoroutine(routine);
            Phase = TugPhase.Cancelled; Restore();
        }
        void OnDisable() { CancelGame(); }
        void OnDestroy()
        {
            Restore();
            if (ballGame) ballGame.autoStart = ballAutoStart;
        }
        void Restore()
        {
            if (!owned) return;
            owned = false;
            if (headApplied && head) { head.localRotation = animatedHead; headApplied = false; }
            tugActions?.Disable(); tugActions?.Dispose(); tugActions = null;
            if (ropeInstance) { ropeInstance.SetActive(false); Destroy(ropeInstance); }
            if (sceneRope) sceneRope.SetActive(originalRopeActive);
            foreach (var target in new[] { mouthGrip, handGrip, bendGrip }) if (target) Destroy(target.gameObject);
            if (hud) { hud.SetActive(false); Destroy(hud); }
            if (content && objective != null) { content.objectives.Remove(objective); content.Refresh(); }
            if (player)
            {
                player.cameraRoot.localPosition = cameraPosition;
                player.Teleport(playerPosition, playerRotation, cameraRotation);
                player.canMove = originalMove; player.canLook = originalLook; player.canJump = originalJump; player.canSprint = originalSprint;
                player.enabled = originalPlayerEnabled;
            }
            foreach (var b in blocked) if (b) b.enabled = true; blocked.Clear();
            if (interactor)
            {
                interactor.enabled = originalInteractor;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(originalCrosshair);
                if (interactor.handIcon) interactor.handIcon.SetActive(originalHandIcon);
            }
            if (hand && hand.handModel)
            {
                hand.handModel.transform.localPosition = handPosition; hand.handModel.transform.localRotation = handRotation;
                if (handPose && fingerPose != null) for (int i = 0; i < 5; i++) handPose.SetFinger((HandPose.Finger)i, fingerPose[i]);
                hand.enabled = originalHandEnabled; hand.handModel.SetActive(originalHandActive);
            }
            if (winter) winter.SetPositionAndRotation(winterPosition, winterRotation);
            if (winterAnimator)
            {
                winterAnimator.runtimeAnimatorController = originalController; winterAnimator.applyRootMotion = originalRootMotion;
                winterAnimator.cullingMode = originalCulling;
                if (originalController && originalAnimatorState.fullPathHash != 0) winterAnimator.Play(originalAnimatorState.fullPathHash, 0, originalAnimatorState.normalizedTime);
            }
            if (agent && originalAgentEnabled) { agent.enabled = true; if (agent.isOnNavMesh) agent.isStopped = originalAgentStopped; }
            Cursor.lockState = originalCursor; Cursor.visible = originalCursorVisible;
        }

        void OnValidate()
        {
            roundCount = Mathf.Max(1, roundCount); successesPerRound = Mathf.Max(1, successesPerRound);
            responseSeconds = Mathf.Max(.1f, responseSeconds); exchangeWalkSeconds = Mathf.Max(.1f, exchangeWalkSeconds);
        }
    }
}
