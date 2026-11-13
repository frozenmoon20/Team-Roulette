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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamRoulette.Room3.BallPlay
{
    [Serializable] public sealed class BallCountEvent : UnityEvent<int> { }
    public enum FetchPhase { Inactive, Aiming, Flying, Turning, RunningToBall, PickingUp, Returning, Dropping, Recovering, Celebrating, Completed, Unavailable }

    [DefaultExecutionOrder(450)]
    public sealed class BallGame : MonoBehaviour
    {
        public const string ToyId = "테니스공";
        [Header("기존 씬 참조")]
        public FirstPersonPlayer player;
        public Camera playerCamera;
        public PlayerInteractor interactor;
        public Transform winter;
        public Animator winterAnimator;
        public RuntimeAnimatorController existingWinterController;
        public Transform mouth;
        [UnityEngine.Serialization.FormerlySerializedAs("tennisBall")] public GameObject ball;
        public InputActionAsset inputActions;
        public Transform playerStand;
        public BoxCollider playArea;

        [Header("성공 조건")]
        [Min(1)] public int requiredSuccesses = 5;
        [Min(.3f)] public float targetRadius = 1.05f;
        [Min(.1f)] public float targetBorderClearance = .35f;
        public Vector2 nearRange = new Vector2(4, 7);
        public Vector2 middleRange = new Vector2(7, 10);
        public Vector2 farRange = new Vector2(10, 14);
        [Min(.5f)] public float minimumTargetSeparation = 2.5f;
        public LayerMask environmentLayers = ~0;

        [Header("투척")]
        [Min(1)] public float clickThrowSpeed = 12.5f;
        public float upwardBoost = 2.3f;
        [Min(.02f)] public float ballRadius = .045f;
        public Vector3 heldBallOffset = new Vector3(.23f, -.24f, .55f);
        [Range(24, 160)] public int trajectorySegments = 100;
        public float trajectoryStep = .045f;

        [Header("겨울이 / In Place + NavMesh")]
        [Min(.5f)] public float runSpeed = 3.4f;
        public float turnSpeed = 270f;
        [Range(0f,.2f)] public float shrinkPerSuccess = .08f;
        public float CurrentRadius { get; private set; }
        Transform lipL, lipR, tongue, nose, head, earL, earR;
        PlayerHand hand;
        bool petting, handWasVisible, couldLook;
        Vector3 handRest;
        Quaternion cameraRest, handRotation, animatedHead;
        bool appliedHeadPose;
        float petTime;
        GameObject visitPanel;
        SceneUIContent objectives;
        SceneUIContent.Objective fetchObjective;
        float curiousTilt;
        [Range(.4f, 1.8f)] public float muzzleClearanceInRadii = 1.05f;
        public string idleState = "Arm_Spitz|Idle_1";
        public string runState = "Arm_Spitz|Run_F_IP";
        public string pickupState = "Arm_Spitz|Pick_up";
        public string putDownState = "Arm_Spitz|Put_down";
        public string turnLeftState = "Arm_Spitz|Turn_L_IP";
        public string turnRightState = "Arm_Spitz|Turn_R_IP";
        public string barkState = "Arm_Spitz|Bark";
        public string sitStartState = "Arm_Spitz|Sitting_start";
        public string sitState = "Arm_Spitz|Sitting_loop_1";
        public string curiousState = "Arm_Spitz|Idle_3";
        public bool buildLocalNavMeshIfNeeded = true;

        [Header("완료 연결")]
        public bool autoStart = true;
        public bool returnToLivingRoomOnComplete = true;
        [Min(0)] public float completionDisplaySeconds = 1.2f;
        public UnityEvent onCompleted = new UnityEvent();
        public BallCountEvent onSuccessCountChanged = new BallCountEvent();
        public event Action Completed;
        public int SuccessCount { get; private set; }
        public int AttemptCount { get; private set; }
        public FetchPhase Phase { get; private set; } = FetchPhase.Inactive;
        public bool IsComplete => Phase == FetchPhase.Completed;
        public Vector3 TargetPosition => targetPosition;
        public Vector3 LastLanding { get; private set; }
        public bool LastThrowSuccessful { get; private set; }
        public bool HasUsableNavMesh => agent && agent.enabled && agent.isOnNavMesh;
        public bool OwnsInput => ownsInput;
        public bool IsCarryingBall => carrying;

        Rigidbody ballBody;
        Collider[] ballColliders;
        NavMeshAgent agent;
        NavMeshData localNavData;
        NavMeshDataInstance localNavInstance;
        InputActionAsset ownedActions;
        InputAction throwAction;
        bool enabledThrowAction, ownsInput, oldCanMove, oldCanJump, oldCanSprint, oldInteractorEnabled;
        bool oldCrosshairActive, oldHandActive, oldApplyRootMotion, agentAdded, oldAgentEnabled;
        bool sawContact, scoreResolved, returnStarted, finishedEventRaised, initialized;
        bool oldBallKinematic, oldBallGravity;
        bool ballBodyAdded, sphereAdded;
        bool[] originalColliderEnabled;
        Collider[] originalBallColliders;
        float oldBallMass, oldLinearDamping, oldAngularDamping;
        bool oldDetectCollisions;
        RigidbodyInterpolation oldInterpolation;
        CollisionDetectionMode oldCollisionMode;
        SphereCollider ballSphere;
        Vector3 oldSphereCenter;
        float oldSphereRadius;
        bool oldSphereTrigger;
        float launchedAt, floorY;
        Vector3 targetPosition, previousTarget, ballOriginalPosition, ballOriginalScale, winterOriginalPosition;
        Quaternion ballOriginalRotation, winterOriginalRotation;
        Transform ballOriginalParent;
        RuntimeAnimatorController oldController;
        string playingState;
        Coroutine cycle;
        GameUI sharedUI;
        
        TMP_Text hint;
        readonly List<BallProgressCircle> circles = new List<BallProgressCircle>();
        readonly List<Behaviour> disabledBallBehaviours = new List<Behaviour>();
        readonly List<GameObject> generated = new List<GameObject>();
        LineRenderer trajectory;
        GameObject targetVisual;
        Material guideMaterial, ringMaterial, diskMaterial;
        Mesh ringMesh, diskMesh;
        bool carrying;

        IEnumerator Start()
        {
            if (!autoStart) yield break;
            // VisitScene is assigned after the additive load callback. Never take input during travel.
            yield return null;
            while (GameManager.Instance && GameManager.Instance.Flow.IsTransitioning) yield return null;
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (flow && !string.IsNullOrEmpty(flow.VisitingObject) && flow.VisitingObject != ToyId) yield break;
            BeginGame();
        }

        public bool BeginGame()
        {
            if (Phase != FetchPhase.Inactive || gameObject.scene.name != Room3FlowController.ToyRoom) return false;
            if (!player || !playerCamera || !winter || !winterAnimator || !ball || !playArea || !playerStand)
                return FailSetup("미니게임의 씬 참조를 확인해주세요.");
            if (!playArea.isTrigger) return FailSetup("Play Area는 Trigger여야 합니다.");
            initialized = true;
            requiredSuccesses = Mathf.Max(1, requiredSuccesses);
            sharedUI = FindObjectsByType<GameUI>(FindObjectsSortMode.None).FirstOrDefault(ui =>
                ui.gameObject.scene == gameObject.scene && ui.objectiveText && ui.hud) ?? FindFirstObjectByType<GameUI>();
            if (!sharedUI || !sharedUI.objectiveText || !sharedUI.hud) return FailSetup("마당의 GameUI 연결을 확인해주세요.");
            hand = player.GetComponentInChildren<PlayerHand>(true);
            var bones = winter.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => Array.Find(bones, bone => bone.name == name);
            lipL = Bone("Mouth.L"); lipR = Bone("Mouth.R"); tongue = Bone("tongue_3");
            nose = Bone("nose"); head = Bone("head"); earL = Bone("Ear_01.L"); earR = Bone("Ear_01.R");
            if (!lipL || !lipR || !tongue || !nose || !head || !earL || !earR || !hand || !hand.handModel)
                return FailSetup("겨울이의 입·머리 본과 기존 PlayerHand 연결을 확인해주세요.");
            floorY = playerStand.position.y;
            SaveAndOwnInput();
            player.Teleport(playerStand.position, playerStand.rotation, Quaternion.identity);
            oldController = winterAnimator.runtimeAnimatorController;
            oldApplyRootMotion = winterAnimator.applyRootMotion;
            if (!oldController && existingWinterController) winterAnimator.runtimeAnimatorController = existingWinterController;
            winterAnimator.applyRootMotion = false;
            winterOriginalPosition = winter.position; winterOriginalRotation = winter.rotation;
            InitializeBall();
            CreateUI(); CreateWorldVisuals();
            Physics.SyncTransforms();
            if (!InitializeNavigation()) return FailSetup("겨울이가 이동할 수 있는 마당 NavMesh가 없습니다.");
            var wait = playerStand.position + playerStand.forward * 3.3f + playerStand.right * 1.7f;
            if (!TryWalkable(wait, .8f, out var start)) start = ClampInside(playArea.bounds.center, .7f);
            if (!agent.Warp(start)) return FailSetup("겨울이의 시작 위치를 NavMesh에서 찾지 못했습니다.");
            winter.rotation = Quaternion.LookRotation(Flat(playerStand.position - winter.position));
            agent.isStopped = true;
            Play(idleState);
            previousTarget = new Vector3(10000, 0, 10000);
            GiveBallToPlayer();
            if (!CreateNextTarget()) return FailSetup("장애물을 피해 목표를 만들 수 없습니다. Play Area 범위를 확인해주세요.");
            SetHint("");
            Phase = FetchPhase.Aiming;
            Debug.Log("[BallGame] 시작: 기존 Player/Camera, Input Actions, In Place, NavMesh 사용", this);
            return true;
        }

        bool FailSetup(string message)
        {
            Phase = FetchPhase.Unavailable;
            Debug.LogError("[BallGame] " + message, this);
            if (hint) hint.text = message;
            RestoreInput();
            return false;
        }

        void SaveAndOwnInput()
        {
            ownsInput = true;
            oldCanMove = player.canMove; oldCanJump = player.canJump; oldCanSprint = player.canSprint;
            player.canMove = false; player.canJump = false; player.canSprint = false;
            if (interactor)
            {
                oldInteractorEnabled = interactor.enabled; interactor.enabled = false;
                oldCrosshairActive = interactor.crosshairDot && interactor.crosshairDot.activeSelf;
                oldHandActive = interactor.handIcon && interactor.handIcon.activeSelf;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(true);
                if (interactor.handIcon) interactor.handIcon.SetActive(false);
                if (interactor.nameText) interactor.nameText.text = "";
            }
            var input = player.GetComponent<PlayerInput>();
            if (input && input.actions) throwAction = input.actions.FindAction("Player/Attack", false);
            if (throwAction == null && inputActions)
            {
                ownedActions = Instantiate(inputActions);
                throwAction = ownedActions.FindAction("Player/Attack", false);
            }
            if (throwAction != null && !throwAction.enabled) { throwAction.Enable(); enabledThrowAction = true; }
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        void InitializeBall()
        {
            ballOriginalParent = ball.transform.parent;
            ballOriginalPosition = ball.transform.position; ballOriginalRotation = ball.transform.rotation;
            ballOriginalScale = ball.transform.localScale;
            ballBody = ball.GetComponent<Rigidbody>();
            ballBodyAdded = !ballBody;
            if (ballBodyAdded) ballBody = ball.AddComponent<Rigidbody>();
            oldBallKinematic = ballBody.isKinematic; oldBallGravity = ballBody.useGravity;
            oldBallMass = ballBody.mass; oldLinearDamping = ballBody.linearDamping; oldAngularDamping = ballBody.angularDamping;
            oldDetectCollisions = ballBody.detectCollisions; oldInterpolation = ballBody.interpolation; oldCollisionMode = ballBody.collisionDetectionMode;
            ballBody.isKinematic = true; ballBody.useGravity = true;
            ballBody.mass = .058f; ballBody.linearDamping = 0; ballBody.angularDamping = .1f;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            foreach (var behaviour in ball.GetComponentsInChildren<Behaviour>(true))
                if (behaviour.enabled && behaviour is IInteractable) { disabledBallBehaviours.Add(behaviour); behaviour.enabled = false; }
            ballColliders = ball.GetComponentsInChildren<Collider>(true);
            originalBallColliders = ballColliders;
            originalColliderEnabled = ballColliders.Select(x => x.enabled).ToArray();
            foreach (var col in ballColliders) col.enabled = false;
            var sphere = ball.GetComponent<SphereCollider>();
            sphereAdded = !sphere;
            if (sphereAdded) sphere = ball.AddComponent<SphereCollider>();
            ballSphere = sphere; oldSphereCenter = sphere.center; oldSphereRadius = sphere.radius; oldSphereTrigger = sphere.isTrigger;
            // Keep the existing art. Its pivot is accounted for by the mesh bounds center.
            var renderers = ball.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                sphere.center = ball.transform.InverseTransformPoint(bounds.center);
                if (diameter > .001f) ball.transform.localScale *= (ballRadius * 2 / diameter);
            }
            sphere.radius = ballRadius / Mathf.Max(.0001f, Mathf.Abs(ball.transform.lossyScale.x));
            sphere.isTrigger = false;
            ballColliders = new Collider[] { sphere };
            sphere.enabled = false;
            var reporter = ball.GetComponent<BallContact>();
            if (!reporter) reporter = ball.AddComponent<BallContact>();
            reporter.owner = this;
        }

        bool InitializeNavigation()
        {
            agent = winter.GetComponent<NavMeshAgent>();
            agentAdded = !agent;
            oldAgentEnabled = agent && agent.enabled;
            if (agent) agent.enabled = false;
            var settings = NavMesh.GetSettingsByIndex(0);
            if (settings.agentTypeID < 0) return false;
            // Prefer a complete existing path across this yard. A local bake is isolated to this visit.
            var existingPath = new NavMeshPath();
            bool existing = NavMesh.SamplePosition(playerStand.position, out var standHit, 1, NavMesh.AllAreas)
                && NavMesh.SamplePosition(playArea.bounds.center, out var yardHit, 1, NavMesh.AllAreas)
                && NavMesh.CalculatePath(standHit.position, yardHit.position, NavMesh.AllAreas, existingPath)
                && existingPath.status == NavMeshPathStatus.PathComplete;
            if (!existing && buildLocalNavMeshIfNeeded)
            {
                settings.agentRadius = .22f; settings.agentHeight = .65f;
                settings.agentClimb = .25f; settings.agentSlope = 35;
                settings.overrideVoxelSize = true; settings.voxelSize = .075f;
                var sources = new List<NavMeshBuildSource>();
                var markups = new List<NavMeshBuildMarkup> {
                    new NavMeshBuildMarkup { root = winter, ignoreFromBuild = true },
                    new NavMeshBuildMarkup { root = player.transform, ignoreFromBuild = true },
                    new NavMeshBuildMarkup { root = ball.transform, ignoreFromBuild = true },
                    new NavMeshBuildMarkup { root = transform, ignoreFromBuild = true }
                };
                var bounds = playArea.bounds; bounds.Encapsulate(playerStand.position);
                bounds.Expand(new Vector3(2, 4, 2));
                NavMeshBuilder.CollectSources(bounds, environmentLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
                // Dedicated invisible floor source: needed only for navigation, never renders over the yard.
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(bounds.center.x, floorY - .08f, bounds.center.z), Quaternion.identity, Vector3.one),
                    size = new Vector3(bounds.size.x, .1f, bounds.size.z), area = 0 });
                localNavData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (!localNavData) return false;
                localNavInstance = NavMesh.AddNavMeshData(localNavData);
                localNavData.name = "BallGame_LocalYardNavMesh";
            }
            if (!NavMesh.SamplePosition(playerStand.position + playerStand.forward * 3, out var hit, 4, NavMesh.AllAreas)) return false;
            winter.position = hit.position;
            if (!agent) agent = winter.gameObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            agent.radius = .22f; agent.height = .65f; agent.baseOffset = 0;
            agent.speed = runSpeed; agent.acceleration = 12; agent.angularSpeed = turnSpeed;
            agent.stoppingDistance = .28f; agent.autoBraking = true;
            agent.enabled = true;
            return agent.isOnNavMesh;
        }

        void Update()
        {
            RestoreAnimatedHead();
            if (!ownsInput) return;
            bool paused = Time.timeScale <= 0 || (sharedUI && sharedUI.IsPaused);
            
            if (paused || Cursor.lockState != CursorLockMode.Locked)
            {
                if (trajectory) trajectory.enabled = false;
                return;
            }
            if (Phase != FetchPhase.Aiming) return;
            UpdateHeldBall();
            bool pressed = throwAction != null ? throwAction.WasPressedThisFrame() : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            Vector3 velocity = AimVelocity();
            DrawTrajectory(BallOrigin(), velocity);
            if (pressed) LaunchBall(velocity);
        }

        void LateUpdate()
        {
            if (!initialized || !ball) return;
            if (Phase == FetchPhase.Aiming) UpdateHeldBall();
            else if (carrying)
            {
                // FBX mouth origin is the jaw joint. Locate the bite between the lips and the tongue tip.
                Vector3 lips = (lipL.position + lipR.position) * .5f;
                Vector3 bite = Vector3.Lerp(lips, tongue.position, .7f);
                Vector3 outward = (nose.position - head.position).normalized;
                float toNose = Mathf.Max(0, Vector3.Dot(nose.position - bite, outward));
                ball.transform.rotation = head.rotation;
                ball.transform.position += bite + outward * (toNose + ballRadius * muzzleClearanceInRadii)
                    - ball.transform.TransformPoint(ballSphere.center);
            }
            if (petting) PosePetting();
            else if (curiousTilt > 0)
            {
                animatedHead = head.localRotation;
                appliedHeadPose = true;
                head.rotation = Quaternion.AngleAxis(18f * curiousTilt, winter.forward) * head.rotation;
            }
        }

        Vector3 BallOrigin() => playerCamera.transform.TransformPoint(heldBallOffset);
        Vector3 AimVelocity()
        {
            return playerCamera.transform.forward * clickThrowSpeed + Vector3.up * upwardBoost;
        }
        void UpdateHeldBall() { ball.transform.position = BallOrigin(); }
        void GiveBallToPlayer()
        {
            carrying = false;
            ballBody.isKinematic = true; ballBody.detectCollisions = false;
            ball.transform.SetParent(null, true);
            foreach (var col in ballColliders) col.enabled = false;
            UpdateHeldBall();
        }

        // A public physics entry point also permits deterministic editor verification without fake scoring.
        public bool LaunchBall(Vector3 velocity)
        {
            if (Phase != FetchPhase.Aiming || Time.timeScale <= 0 || !Finite(velocity) || velocity.sqrMagnitude < .01f) return false;
            AttemptCount++;
            scoreResolved = sawContact = false; LastThrowSuccessful = false;
            launchedAt = Time.time;
            ballBody.isKinematic = false; ballBody.detectCollisions = true;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var col in ballColliders) col.enabled = true;
            ballBody.position = BallOrigin(); ballBody.linearVelocity = velocity;
            ballBody.linearDamping = 0;
            ballBody.angularVelocity = new Vector3(2, 0, 5);
            if (trajectory) trajectory.enabled = false;
            Phase = FetchPhase.Flying;
            cycle = StartCoroutine(FetchCycle());
            return true;
        }

        public void ReportBallContact(Collision collision)
        {
            if (scoreResolved || Phase == FetchPhase.Aiming || Phase == FetchPhase.Completed || collision == null) return;
            if (collision.transform.IsChildOf(player.transform) || collision.transform.IsChildOf(winter)) return;
            sawContact = true;
            var contact = collision.GetContact(0);
            if (contact.normal.y > .55f) ballBody.linearDamping = 4f;
            ResolveLanding(contact.point, contact.normal.y > .55f && Mathf.Abs(contact.point.y - targetPosition.y) < .4f);
        }
        void ResolveLanding(Vector3 point, bool isFloor)
        {
            if (scoreResolved) return;
            scoreResolved = true; LastLanding = point;
            LastThrowSuccessful = isFloor && IsInside(point, ballRadius) && Flat(point - targetPosition).sqrMagnitude <= CurrentRadius * CurrentRadius;
            if (LastThrowSuccessful)
            {
                SuccessCount = Mathf.Min(requiredSuccesses, SuccessCount + 1);
                for (int i = 0; i < circles.Count; i++) circles[i].SetFilled(i < SuccessCount);
                onSuccessCountChanged.Invoke(SuccessCount);
                SetTargetColor(true);
                SetHint("겨울이가 공을 가져오고 있어요.");
            }
            else SetHint("괜찮아. 겨울이와 한 번 더 놀자.");
        }

        IEnumerator FetchCycle()
        {
            yield return FacePoint(ball.transform.position + Flat(ballBody.linearVelocity) * .25f);
            SetHint("겨울이가 공을 쫓아가요.");
            Phase = FetchPhase.RunningToBall;
            Play(runState);
            agent.isStopped = false;
            float nextPath = 0;
            bool outOfReach = false;
            var chasePath = new NavMeshPath();
            while (Time.time - launchedAt < 7f)
            {
                if (Time.time >= nextPath)
                {
                    nextPath = Time.time + .2f;
                    bool onLawn = TryWalkable(ball.transform.position, .8f, out var next);
                    if (onLawn && agent.CalculatePath(next, chasePath) && chasePath.status == NavMeshPathStatus.PathComplete)
                        agent.SetPath(chasePath);
                    else if (!IsInside(ball.transform.position, 0) && Time.time - launchedAt > .4f)
                    {
                        outOfReach = true;
                        break;
                    }
                }
                if (sawContact && ballBody.linearVelocity.sqrMagnitude < .12f && Time.time - launchedAt > .6f) break;
                if (ball.transform.position.y < floorY - 3) break;
                yield return null;
            }
            if (!scoreResolved) ResolveLanding(ball.transform.position, false);
            Vector3 ballPoint = ball.transform.position;
            // The red-rectangle bounds constrain targets, not Winter's retrieval.
            // A missed/rolling ball anywhere on the reachable lawn is still fetched.
            bool reachable = !outOfReach && Mathf.Abs(ballPoint.y - floorY) < .75f && TryWalkable(ballPoint, .65f, out _);
            bool arrived = false;
            if (reachable)
            {
                yield return MoveTo(ballPoint, result => arrived = result);
                reachable = arrived;
            }
            if (!reachable)
            {
                Phase = FetchPhase.Recovering;
                if (HasUsableNavMesh) agent.isStopped = true;
                yield return FacePoint(player.transform.position);
                Phase = FetchPhase.Recovering;
                Play(curiousState);
                SetHint("조금 멀리 갔네. 다시 던져 볼까?");
                for (float t = 0; t < 1.3f; t += Time.deltaTime)
                {
                    curiousTilt = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(t, 1.3f-t) / .3f));
                    yield return null;
                }
                curiousTilt = 0;
                // An unreachable ball is recalled only after Winter returns empty-handed.
                yield return MoveTo(ReturnPoint(), result => arrived = result);
                yield return FacePoint(playerStand.position);
                Play(idleState);
                GiveBallToPlayer();
            }
            else
            {
                if (HasUsableNavMesh) agent.isStopped = true;
                yield return FacePoint(ballPoint);
                Phase = FetchPhase.PickingUp;
                ballBody.linearVelocity = Vector3.zero; ballBody.angularVelocity = Vector3.zero;
                ballBody.isKinematic = true; ballBody.detectCollisions = false;
                foreach (var col in ballColliders) col.enabled = false;
                Play(pickupState);
                float pickDuration = ClipDuration(pickupState, .95f);
                yield return new WaitForSeconds(pickDuration * .55f);
                carrying = true;
                yield return new WaitForSeconds(pickDuration * .45f);
                yield return FacePoint(ReturnPoint());
                Phase = FetchPhase.Returning;
                yield return MoveTo(ReturnPoint(), result => arrived = result);
                yield return FacePoint(playerStand.position);
                Phase = FetchPhase.Dropping;
                Play(putDownState);
                float dropDuration = ClipDuration(putDownState, .8f);
                yield return new WaitForSeconds(dropDuration * .55f);
                carrying = false;
                Vector3 drop = winter.position + winter.forward * .36f;
                if (GroundPoint(drop, out var ground)) drop = ground;
                ball.transform.position = drop + Vector3.up * ballRadius;
                yield return new WaitForSeconds(dropDuration * .45f);
                Play(idleState);
                yield return new WaitForSeconds(.5f);
                if (LastThrowSuccessful) yield return PetWinter();
            }
            if (SuccessCount >= requiredSuccesses)
            {
                yield return CelebrateAndComplete();
                yield break;
            }
            GiveBallToPlayer();
            if (!CreateNextTarget()) { FailSetup("목표를 배치할 수 없습니다. Play Area를 확인해주세요."); yield break; }
            Play(idleState);
            Phase = FetchPhase.Aiming;
            SetHint("");
            cycle = null;
        }

        IEnumerator PetWinter()
        {
            var model = hand.handModel.transform;
            handRest = model.localPosition;
            handRotation = model.localRotation;
            handWasVisible = model.gameObject.activeSelf;
            cameraRest = playerCamera.transform.localRotation;
            couldLook = player.canLook;
            player.canLook = false;
            model.gameObject.SetActive(true);
            petTime = 0;
            petting = true;
            SetHint("겨울이를 쓰다듬어요.");
            Play(sitStartState);
            float sitTime = ClipDuration(sitStartState, .65f);
            while (petTime < 2.4f)
            {
                if (petTime >= sitTime) Play(sitState);
                yield return null;
                petTime += Time.deltaTime;
            }
            EndPetting();
        }

        void PosePetting()
        {
            float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(petTime, 2.4f-petTime)/.48f));
            float stroke = Mathf.Sin((petTime-.48f)*Mathf.PI*2);
            // Animator evaluates the seated pose before LateUpdate.
            animatedHead = head.localRotation;
            appliedHeadPose = true;
            head.rotation = Quaternion.AngleAxis(reach * (5f + 4f*stroke), winter.forward) * head.rotation;
            Vector3 crown = (earL.position + earR.position)*.5f + winter.up*.018f;
            var cam = playerCamera.transform;
            Quaternion rest = cam.parent ? cam.parent.rotation * cameraRest : cameraRest;
            cam.rotation = Quaternion.Slerp(rest, Quaternion.LookRotation(crown-cam.position, Vector3.up), reach);
            var model = hand.handModel.transform;
            model.localPosition = handRest;
            model.localRotation = handRotation;
            Transform palm = hand.palmPoint ? hand.palmPoint : hand.handBone;
            if (palm)
            {
                var palmDown = Quaternion.FromToRotation(palm.up, -winter.up);
                model.rotation = Quaternion.Slerp(Quaternion.identity, palmDown, reach) * model.rotation;
                model.position += reach * (crown + winter.forward*(stroke*.055f) - palm.position);
            }
        }

        void RestoreAnimatedHead()
        {
            if (appliedHeadPose && head) head.localRotation = animatedHead;
            appliedHeadPose = false;
        }

        void EndPetting()
        {
            if (!petting) return;
            petting = false;
            RestoreAnimatedHead();
            hand.handModel.transform.localPosition = handRest;
            hand.handModel.transform.localRotation = handRotation;
            hand.handModel.SetActive(handWasVisible);
            playerCamera.transform.localRotation = cameraRest;
            player.canLook = couldLook;
        }

        IEnumerator FacePoint(Vector3 point)
        {
            if (HasUsableNavMesh) agent.isStopped = true;
            Vector3 direction = Flat(point - winter.position);
            if (direction.sqrMagnitude < .01f) yield break;
            Phase = FetchPhase.Turning;
            float signed = Vector3.SignedAngle(winter.forward, direction, Vector3.up);
            if (Mathf.Abs(signed) > 18) Play(signed < 0 ? turnLeftState : turnRightState);
            Quaternion rotation = Quaternion.LookRotation(direction);
            float until = Time.time + 1.5f;
            while (Quaternion.Angle(winter.rotation, rotation) > 2 && Time.time < until)
            { winter.rotation = Quaternion.RotateTowards(winter.rotation, rotation, turnSpeed * Time.deltaTime); yield return null; }
        }

        IEnumerator MoveTo(Vector3 destination, Action<bool> result)
        {
            if (!HasUsableNavMesh || !TryWalkable(destination, .8f, out var point)) { result(false); yield break; }
            var path = new NavMeshPath();
            if (!agent.CalculatePath(point, path) || path.status != NavMeshPathStatus.PathComplete) { result(false); yield break; }
            agent.isStopped = false;
            agent.SetPath(path); Play(runState);
            float deadline = Time.time + Mathf.Max(5, Vector3.Distance(winter.position, point) / runSpeed * 3 + 2);
            while (Time.time < deadline)
            {
                if (!agent || !agent.isOnNavMesh) { result(false); yield break; }
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + .12f)
                { agent.isStopped = true; Play(idleState); result(true); yield break; }
                yield return null;
            }
            agent.isStopped = true; Play(idleState); result(false);
        }
        Vector3 ReturnPoint() => playerStand.position + playerStand.forward * 1.35f;

        IEnumerator CelebrateAndComplete()
        {
            Phase = FetchPhase.Celebrating;
            if (targetVisual) targetVisual.SetActive(false);
            if (trajectory) trajectory.enabled = false;
            SetHint("겨울이와 즐거운 시간을 보냈다.");
            Play(barkState); yield return new WaitForSeconds(Mathf.Min(2.5f, ClipDuration(barkState, 1.5f)));
            Play(sitStartState); yield return new WaitForSeconds(ClipDuration(sitStartState, .8f));
            Play(sitState); yield return new WaitForSeconds(.7f);
            Phase = FetchPhase.Completed;
            if (!finishedEventRaised)
            {
                finishedEventRaised = true;
                onCompleted.Invoke(); Completed?.Invoke();
                Debug.Log("[BallGame] 완료 이벤트: 마지막 공 내려놓기와 반응 종료", this);
            }
            yield return new WaitForSeconds(completionDisplaySeconds);
            if (returnToLivingRoomOnComplete) ReturnToLivingRoom();
        }

        public void ReturnToLivingRoom()
        {
            if (!IsComplete || returnStarted) return;
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (!flow || flow.VisitingObject != ToyId) return; // Direct scene tests never mark unrelated Room3 stages.
            // Keep this component independent of the home progression API.
            var method = flow.GetType().GetMethod("CompleteVisit", new[] { typeof(string) });
            if (method != null) returnStarted = (bool)method.Invoke(flow, new object[] { ToyId });
            else Debug.LogError("[BallGame] Room3FlowController.CompleteVisit 연결을 확인해주세요.", this);
        }

        public bool CreateNextTarget()
        {
            Vector2 range = AttemptCount % 3 == 0 ? nearRange : AttemptCount % 3 == 1 ? middleRange : farRange;
            var bounds = playArea.bounds;
            CurrentRadius = targetRadius * Mathf.Max(.55f, 1 - shrinkPerSuccess * SuccessCount);
            float margin = CurrentRadius + targetBorderClearance;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 180; i++)
                {
                    Vector3 candidate = new Vector3(UnityEngine.Random.Range(bounds.min.x + margin, bounds.max.x - margin), floorY,
                        UnityEngine.Random.Range(bounds.min.z + margin, bounds.max.z - margin));
                    float distance = Flat(candidate - playerStand.position).magnitude;
                    if (distance < (pass == 0 ? range.x : nearRange.x) || distance > (pass == 0 ? range.y : farRange.y)) continue;
                    if (Flat(candidate - previousTarget).sqrMagnitude < minimumTargetSeparation * minimumTargetSeparation) continue;
                    if (!ValidateTarget(candidate, out var grounded)) continue;
                    targetPosition = previousTarget = grounded;
                    if (targetVisual) { targetVisual.transform.position = grounded + Vector3.up * .035f; targetVisual.transform.localScale = Vector3.one * (CurrentRadius/targetRadius); SetTargetColor(false); targetVisual.SetActive(true); }
                    return true;
                }
            return false;
        }

        public bool ValidateTarget(Vector3 candidate, out Vector3 ground)
        {
            ground = candidate;
            if (!IsInside(candidate, CurrentRadius + targetBorderClearance) || !GroundPoint(candidate, out ground)) return false;
            if (!TryWalkable(ground, .25f, out var navPoint) || Flat(navPoint - ground).magnitude > .25f) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(navPoint, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            // Validate the whole disk and the air above it, not only its center.
            var overlaps = Physics.OverlapCapsule(ground + Vector3.up * (CurrentRadius + .12f),
                ground + Vector3.up * (CurrentRadius + .35f), CurrentRadius, environmentLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
                if (!IgnoreCollider(col) && col.bounds.max.y > ground.y + .16f) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                if (!GroundPoint(ground + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * CurrentRadius, out var edge)
                    || Mathf.Abs(edge.y - ground.y) > .18f) return false;
            }
            return true;
        }
        bool GroundPoint(Vector3 point, out Vector3 ground)
        {
            ground = point;
            var hits = Physics.RaycastAll(new Vector3(point.x, floorY + 4, point.z), Vector3.down, 8, environmentLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IgnoreCollider(hit.collider)) continue;
                if (hit.normal.y < .75f || Mathf.Abs(hit.point.y - floorY) > .45f) return false;
                ground = hit.point; return true;
            }
            return false;
        }
        bool TryWalkable(Vector3 point, float radius, out Vector3 result)
        {
            var flattened = new Vector3(point.x, floorY, point.z);
            if (NavMesh.SamplePosition(flattened, out var hit, radius, NavMesh.AllAreas)) { result = hit.position; return true; }
            result = flattened; return false;
        }
        public bool IsInside(Vector3 point, float margin)
        {
            Vector3 p = playArea.transform.InverseTransformPoint(point) - playArea.center;
            Vector3 scale = playArea.transform.lossyScale;
            return Mathf.Abs(p.x) <= playArea.size.x * .5f - margin / Mathf.Abs(scale.x)
                && Mathf.Abs(p.z) <= playArea.size.z * .5f - margin / Mathf.Abs(scale.z);
        }
        Vector3 ClampInside(Vector3 point, float margin)
        {
            var b = playArea.bounds;
            return new Vector3(Mathf.Clamp(point.x, b.min.x + margin, b.max.x - margin), floorY,
                Mathf.Clamp(point.z, b.min.z + margin, b.max.z - margin));
        }
        bool IgnoreCollider(Collider col) => !col || col.isTrigger || col.transform.IsChildOf(winter)
            || col.transform.IsChildOf(player.transform) || col.transform.IsChildOf(ball.transform);

        public Vector3 TrajectoryPoint(Vector3 origin, Vector3 velocity, float time) => origin + velocity * time + Physics.gravity * (.5f * time * time);
        void DrawTrajectory(Vector3 origin, Vector3 velocity)
        {
            trajectory.enabled = true;
            trajectory.positionCount = trajectorySegments + 1;
            trajectory.SetPosition(0, origin);
            Vector3 previous = origin;
            for (int i = 1; i <= trajectorySegments; i++)
            {
                Vector3 next = TrajectoryPoint(origin, velocity, i * trajectoryStep);
                Vector3 delta = next - previous;
                var hits = Physics.SphereCastAll(previous, ballRadius, delta.normalized, delta.magnitude, environmentLayers, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
                var hit = hits.FirstOrDefault(h => !IgnoreCollider(h.collider));
                if (hit.collider)
                {
                    trajectory.SetPosition(i, previous + delta.normalized * hit.distance);
                    trajectory.positionCount = i + 1; break;
                }
                trajectory.SetPosition(i, next); previous = next;
            }
        }

        void Play(string state)
        {
            if (!winterAnimator || string.IsNullOrEmpty(state) || state == playingState) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!winterAnimator.HasState(0, hash)) return;
            playingState = state;
            winterAnimator.CrossFadeInFixedTime(hash, .12f, 0);
        }
        float ClipDuration(string state, float fallback)
        {
            var controller = winterAnimator ? winterAnimator.runtimeAnimatorController : null;
            if (!controller) return fallback;
            var clip = controller.animationClips.FirstOrDefault(x => x.name == state);
            return clip ? Mathf.Max(.2f, clip.length) : fallback;
        }

        void CreateUI()
        {
            objectives = sharedUI.GetComponent<SceneUIContent>();
            fetchObjective = new SceneUIContent.Objective { id = "BallGame.Throw", text = "겨울이에게 공을 던져보자" };
            objectives.objectives.Insert(0, fetchObjective);
            objectives.Refresh();

            var instructions = MakeHudLabel("Ball Instructions", 1f, -24f);
            instructions.text = "노란 원을 조준하고 마우스 왼쪽 버튼을 눌러 공을 던지세요.";
            visitPanel = instructions.gameObject;
            hint = MakeHudLabel("Ball Status", 0f, 64f);
            hint.text = "";
            float spacing = 70, width = Mathf.Max(300, requiredSuccesses * spacing + 42);
            var row = new GameObject("Success Circles", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(visitPanel.transform, false);
            var rect = row.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -8); rect.sizeDelta = new Vector2(width, 78);
            row.GetComponent<Image>().color = new Color(1, 1, 1, .94f); row.GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < requiredSuccesses; i++)
            {
                var item = new GameObject("Success " + (i+1), typeof(RectTransform), typeof(BallProgressCircle));
                item.transform.SetParent(row.transform, false);
                var itemRect = item.GetComponent<RectTransform>(); itemRect.sizeDelta = new Vector2(42, 42);
                itemRect.anchorMin = itemRect.anchorMax = new Vector2(.5f,.5f); itemRect.pivot = new Vector2(.5f,.5f);
                itemRect.anchoredPosition = new Vector2((i - (requiredSuccesses - 1) * .5f) * spacing, 0);
                var circle = item.GetComponent<BallProgressCircle>(); circle.color = Color.black; circle.raycastTarget = false; circles.Add(circle);
            }
        }
        TMP_Text MakeHudLabel(string label, float anchorY, float offsetY)
        {
            var text = Instantiate(sharedUI.objectiveText, sharedUI.hud.transform, false);
            text.name = label;
            generated.Add(text.gameObject);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(.19f, anchorY);
            rect.anchorMax = new Vector2(.81f, anchorY);
            rect.pivot = new Vector2(.5f, anchorY);
            rect.anchoredPosition = new Vector2(0, offsetY);
            rect.sizeDelta = new Vector2(0, 54f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
        void SetHint(string message) { if (hint) hint.text = message; }

        GameObject NewObject(string name, params Type[] components)
        {
            var go = new GameObject(name, components); SceneManager.MoveGameObjectToScene(go, gameObject.scene); generated.Add(go); return go;
        }
        void CreateWorldVisuals()
        {
            guideMaterial = MakeMaterial(new Color(1,1,1,.8f));
            var guide = NewObject("Ball Trajectory", typeof(LineRenderer)); guide.transform.SetParent(transform, false);
            trajectory = guide.GetComponent<LineRenderer>(); trajectory.sharedMaterial = guideMaterial;
            trajectory.useWorldSpace = true; trajectory.startWidth = .009f; trajectory.endWidth = .005f;
            trajectory.numCapVertices = 4; trajectory.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectory.receiveShadows = false; trajectory.enabled = false;
            ringMaterial = MakeMaterial(Color.white); diskMaterial = MakeMaterial(Color.white);
            targetVisual = NewObject("Ball Target"); targetVisual.transform.SetParent(transform, false);
            ringMesh = BuildAnnulus(targetRadius, targetRadius - .075f);
            diskMesh = BuildAnnulus(targetRadius - .075f, 0);
            AddMesh("Target Ring", ringMesh, ringMaterial); AddMesh("Target Fill", diskMesh, diskMaterial);
        }
        void SetTargetColor(bool hit)
        {
            Color color = hit ? new Color(.12f,.52f,1f,.98f) : new Color(1f,.84f,.04f,.98f);
            ringMaterial.color = color;
            color.a = .26f;
            diskMaterial.color = color;
        }
        void AddMesh(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(targetVisual.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader); material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            return material;
        }
        static Mesh BuildAnnulus(float outer, float inner)
        {
            const int n = 96;
            var vertices = new Vector3[(n+1)*2]; var triangles = new int[n*6];
            for (int i=0; i<=n; i++)
            {
                float a = i * Mathf.PI * 2 / n; Vector3 direction = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                vertices[i*2] = direction * outer; vertices[i*2+1] = direction * inner;
                if (i==n) break;
                int t=i*6,v=i*2; triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
                triangles[t+3]=v+2; triangles[t+4]=v+1; triangles[t+5]=v+3;
            }
            var mesh = new Mesh { name = "BallTargetAnnulus", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Vector3 Flat(Vector3 v) { v.y=0; return v; }
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));

        void RestoreInput()
        {
            if (!ownsInput) return; ownsInput = false;
            if (player) { player.canMove = oldCanMove; player.canJump = oldCanJump; player.canSprint = oldCanSprint; }
            if (interactor)
            {
                interactor.enabled = oldInteractorEnabled;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(oldCrosshairActive);
                if (interactor.handIcon) interactor.handIcon.SetActive(oldHandActive);
            }
            if (enabledThrowAction && throwAction != null) throwAction.Disable();
        }
        void OnDisable()
        {
            if (cycle != null) { StopCoroutine(cycle); cycle = null; }
            RestoreInput();
            if (trajectory) trajectory.enabled = false;
            if (targetVisual) targetVisual.SetActive(false);
            EndPetting();
            if (visitPanel) visitPanel.SetActive(false);
            if (hint) hint.gameObject.SetActive(false);
            if (objectives && objectives.objectives.Remove(fetchObjective)) objectives.Refresh();
            curiousTilt = 0;
            RestoreAnimatedHead();
            if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (localNavInstance.valid) localNavInstance.Remove();
            if (!initialized) return;
            if (agent) agent.enabled = false;
            if (winter) winter.SetPositionAndRotation(winterOriginalPosition,winterOriginalRotation);
            if (winterAnimator) { winterAnimator.applyRootMotion = oldApplyRootMotion; winterAnimator.runtimeAnimatorController = oldController; }
            if (ball)
            {
                ball.transform.SetParent(ballOriginalParent,true); ball.transform.SetPositionAndRotation(ballOriginalPosition,ballOriginalRotation);
                ball.transform.localScale = ballOriginalScale;
            }
            if (ballBody)
            {
                ballBody.isKinematic = true; ballBody.collisionDetectionMode = oldCollisionMode;
                ballBody.mass = oldBallMass; ballBody.linearDamping = oldLinearDamping; ballBody.angularDamping = oldAngularDamping;
                ballBody.interpolation = oldInterpolation; ballBody.detectCollisions = oldDetectCollisions;
                ballBody.useGravity = oldBallGravity; ballBody.isKinematic = oldBallKinematic;
                if (ballBodyAdded) Destroy(ballBody);
            }
            if (originalBallColliders != null)
                for (int i=0;i<originalBallColliders.Length;i++) if(originalBallColliders[i]) originalBallColliders[i].enabled=originalColliderEnabled[i];
            if (ballSphere)
            {
                if (sphereAdded) Destroy(ballSphere);
                else { ballSphere.center=oldSphereCenter; ballSphere.radius=oldSphereRadius; ballSphere.isTrigger=oldSphereTrigger; }
            }
            if (agent)
            {
                if (agentAdded) Destroy(agent);
                else agent.enabled=oldAgentEnabled;
            }
            foreach (var behaviour in disabledBallBehaviours) if (behaviour) behaviour.enabled = true;
        }
        void OnDestroy()
        {
            if (ownedActions) Destroy(ownedActions);
            if (localNavData) Destroy(localNavData);
            if (guideMaterial) Destroy(guideMaterial); if (ringMaterial) Destroy(ringMaterial); if (diskMaterial) Destroy(diskMaterial);
            if (ringMesh) Destroy(ringMesh); if (diskMesh) Destroy(diskMesh);
            foreach (var go in generated) if (go) Destroy(go);
        }
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!playArea) return;
            Gizmos.color = new Color(1,.15f,.05f,.8f); Gizmos.matrix = playArea.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(playArea.center,playArea.size);
            Gizmos.matrix=Matrix4x4.identity;
            if (playerStand) { Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(playerStand.position+Vector3.up,.4f); Gizmos.DrawRay(playerStand.position+Vector3.up,playerStand.forward*2); }
        }
#endif
    }
}
