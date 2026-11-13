using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TeamRoulette.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace TeamRoulette.Room3.BonePlay
{
    public enum CatchDirection { Left, Right, Up, Forward }
    public enum CatchPhase { Inactive, Aiming, Flying, Recovering, Returning, Celebrating, Completed, Cancelled }
    [Serializable] public sealed class CatchCountEvent : UnityEvent<int> { }

    // Earlier than the other toy games: a direct scene preview belongs to this game.
    [DefaultExecutionOrder(-100)]
    public sealed class BoneCatchGame : MonoBehaviour
    {
        public const string ToyId = "뼈다귀 장난감";
        [Header("기존 씬 연결")]
        public FirstPersonPlayer player;
        public Camera playerCamera;
        public PlayerInteractor interactor;
        public Transform winter, mouth;
        public Animator winterAnimator;
        public RuntimeAnimatorController existingController;
        public GameObject sceneBone;
        public GameUI sharedUI;
        public Transform playerStand;
        [Header("짧고 빠른 캐치")]
        public bool autoStart = true, previewWhenOpenedDirectly = true, returnToLivingRoom = true;
        [Min(1)] public int requiredSuccesses = 5;
        [Min(.1f)] public float flightSeconds = .36f;
        [Min(.08f)] public float targetRadius = .25f;
        [Range(1.5f, 3)] public float dogDistance = 2.1f;
        public float sideDistance = .65f, jumpHeight = .65f, forwardDistance = .55f;
        public float eyeHeight = 1.05f, celebrationSeconds = 1.6f;
        public Vector3 heldOffset = new Vector3(.26f, -.25f, .56f);
        public Vector3 mouthOffset = new Vector3(0, 0, .025f);
        public Vector3 heldEuler = new Vector3(0, 0, 75), carriedEuler = Vector3.zero;
        [Header("삑삑 소리")]
        public AudioSource squeakSource;
        public AudioClip squeakClip;
        [Range(0, 1)] public float squeakVolume = .7f;
        [Header("기존 Animator 상태")]
        public string idleState = "Arm_Spitz|Idle_1", biteState = "Arm_Spitz|Attack_Bite_IP";
        public string leftState = "Arm_Spitz|Trot_R_IP", rightState = "Arm_Spitz|Trot_L_IP";
        public string jumpState = "Arm_Spitz|Jump_Place_IP", forwardState = "Arm_Spitz|Jump_F_IP";
        public string pickupState = "Arm_Spitz|Pick_up", runState = "Arm_Spitz|Trot_F_IP";
        public string happyState = "Arm_Spitz|Bite_move";
        [Header("이벤트")]
        public UnityEvent onCompleted = new UnityEvent(), onCaught = new UnityEvent(), onMissed = new UnityEvent();
        public CatchCountEvent onSuccessCountChanged = new CatchCountEvent();
        public CatchPhase Phase { get; private set; }
        public CatchDirection Direction { get; private set; }
        public int SuccessCount { get; private set; }
        public int AttemptCount { get; private set; }
        public int SqueakCount { get; private set; }
        public bool OwnsInput => owned;
        public Vector3 TargetPosition { get; private set; }
        public bool LastThrowSuccessful { get; private set; }
        public GameObject ActiveBone => bone;
        public Vector3 BitePosition => MouthPoint;
        public bool IsPaused => Time.timeScale <= 0 || (sharedUI && sharedUI.IsPaused);

        bool owned, carried, held, completedRaised, oldPlayer, oldInteractor, oldMotion, oldRootMotion, oldAgent;
        bool oldCrosshair, oldHandIcon, oldBone, oldCursorVisible, oldHandEnabled, oldHandVisible;
        bool oldMove, oldLook, oldJump, oldSprint;
        Vector3 oldPlayerPos, oldCameraPos, oldWinterPos, home, right, forward, oldHandPos;
        Quaternion oldPlayerRot, oldCameraRot, oldWinterRot, oldHandRot;
        CursorLockMode oldCursor;
        AnimatorCullingMode oldCulling;
        RuntimeAnimatorController oldController;
        AnimatorStateInfo oldAnimatorState;
        Behaviour motion;
        NavMeshAgent agent;
        PlayerHand hand;
        Transform lipLeft, lipRight, tongue, nose, head;
        Quaternion boneAxis = Quaternion.identity;
        Vector3 boneCenter;
        GameObject bone, marker, help;
        LineRenderer ring;
        Material ringMaterial;
        SceneUIContent content;
        SceneUIContent.Objective objective;
        TMP_Text instructions;
        InputActionMap actions;
        InputAction click;
        Coroutine cycle;
        readonly List<Behaviour> blockedTargets = new List<Behaviour>();
        readonly Dictionary<Behaviour, bool> previewGames = new Dictionary<Behaviour, bool>();

        IEnumerable<T> InScene<T>() where T : Component => gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        void Awake()
        {
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (!previewWhenOpenedDirectly || (flow && !string.IsNullOrEmpty(flow.VisitingObject))) return;
            // Set their explicit preview flags without disabling components or modifying saved assets.
            foreach (var b in InScene<MonoBehaviour>().Where(b => b && (b.GetType().Name == "BallGame" || b.GetType().Name == "TugGame")))
            {
                var field = b.GetType().GetField("autoStart");
                if (field == null) continue;
                previewGames[b] = (bool)field.GetValue(b); field.SetValue(b, false);
            }
        }
        IEnumerator Start()
        {
            if (!autoStart) yield break;
            yield return null;
            while (GameManager.Instance && GameManager.Instance.Flow.IsTransitioning) yield return null;
            var flow = GameManager.Instance ? GameManager.Instance.Flow : null;
            if (flow && !string.IsNullOrEmpty(flow.VisitingObject) && flow.VisitingObject != ToyId) yield break;
            if ((!flow || string.IsNullOrEmpty(flow.VisitingObject)) && !previewWhenOpenedDirectly) yield break;
            BeginGame();
        }

        public bool BeginGame()
        {
            if (owned || Phase != CatchPhase.Inactive || gameObject.scene.name != Room3FlowController.ToyRoom) return false;
            if (!player || !player.cameraRoot || !playerCamera || !winter || !mouth || !winterAnimator || !sceneBone || !playerStand || !sharedUI)
            { Debug.LogError("[BoneCatch] Player / Camera / Winter / Bone / UI 연결을 확인해주세요.", this); return false; }
            if (!winterAnimator.runtimeAnimatorController && !existingController) return false;
            requiredSuccesses = Mathf.Max(1, requiredSuccesses);
            var bones = winter.GetComponentsInChildren<Transform>(true);
            lipLeft = bones.FirstOrDefault(b => b.name == "Mouth.L"); lipRight = bones.FirstOrDefault(b => b.name == "Mouth.R");
            tongue = bones.FirstOrDefault(b => b.name == "tongue_3"); nose = bones.FirstOrDefault(b => b.name == "nose"); head = bones.FirstOrDefault(b => b.name == "head");
            SaveAndLock();
            forward = Vector3.ProjectOnPlane(playerStand.forward, Vector3.up).normalized; right = Vector3.Cross(Vector3.up, forward);
            home = playerStand.position + forward * dogDistance;
            winter.SetPositionAndRotation(home, Quaternion.LookRotation(-forward));
            player.Teleport(playerStand.position, playerStand.rotation, Quaternion.Euler(14, 0, 0));
            player.cameraRoot.localPosition = new Vector3(oldCameraPos.x, eyeHeight, oldCameraPos.z);
            CreateBone(); CreateMarker(); CreateUI();
            actions = new InputActionMap("BoneCatch"); click = actions.AddAction("Throw", InputActionType.Button, "<Mouse>/leftButton"); actions.Enable();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Play(idleState); GiveToPlayer();
            SuccessCount = AttemptCount = SqueakCount = 0;
            NextTarget(); return true;
        }
        void SaveAndLock()
        {
            oldPlayerPos = player.transform.position; oldPlayerRot = player.transform.rotation;
            oldCameraPos = player.cameraRoot.localPosition; oldCameraRot = player.cameraRoot.localRotation;
            oldPlayer = player.enabled; oldMove = player.canMove; oldLook = player.canLook; oldJump = player.canJump; oldSprint = player.canSprint;
            oldWinterPos = winter.position; oldWinterRot = winter.rotation;
            oldCursor = Cursor.lockState; oldCursorVisible = Cursor.visible;
            oldInteractor = interactor && interactor.enabled;
            if (interactor)
            {
                oldCrosshair = interactor.crosshairDot && interactor.crosshairDot.activeSelf;
                oldHandIcon = interactor.handIcon && interactor.handIcon.activeSelf;
                interactor.enabled = false;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(false);
                if (interactor.handIcon) interactor.handIcon.SetActive(false);
                if (interactor.nameText) interactor.nameText.text = "";
            }
            motion = player.GetComponentsInChildren<Behaviour>(true).FirstOrDefault(b => b && b.GetType().Name == "PlayerMotionFeel");
            oldMotion = motion && motion.enabled; if (motion) motion.enabled = false;
            oldController = winterAnimator.runtimeAnimatorController; oldRootMotion = winterAnimator.applyRootMotion; oldCulling = winterAnimator.cullingMode;
            if (oldController) oldAnimatorState = winterAnimator.GetCurrentAnimatorStateInfo(0);
            else winterAnimator.runtimeAnimatorController = existingController;
            winterAnimator.applyRootMotion = false; winterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            agent = winter.GetComponent<NavMeshAgent>(); oldAgent = agent && agent.enabled; if (agent) agent.enabled = false;
            oldBone = sceneBone.activeSelf; sceneBone.SetActive(false);
            hand = player.GetComponentInChildren<PlayerHand>(true);
            if (hand && hand.handModel)
            {
                oldHandEnabled = hand.enabled; oldHandVisible = hand.handModel.activeSelf;
                oldHandPos = hand.handModel.transform.localPosition; oldHandRot = hand.handModel.transform.localRotation;
                hand.enabled = false; hand.handModel.SetActive(true);
            }
            player.enabled = false; player.canMove = player.canLook = player.canJump = player.canSprint = false;
            owned = true; BlockReturnClicks();
        }
        void BlockReturnClicks()
        {
            foreach (var target in InScene<Room3FlowTarget>())
                if (target && target.enabled && target.ObjectId == ToyId) { blockedTargets.Add(target); target.enabled = false; }
        }
        void CreateBone()
        {
            bone = Instantiate(sceneBone, transform); bone.name = "BoneCatch Toy"; bone.SetActive(false);
            foreach (var b in bone.GetComponentsInChildren<Behaviour>(true)) b.enabled = false;
            foreach (var c in bone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var body in bone.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            var renderers = bone.GetComponentsInChildren<Renderer>(true);
            var mesh = bone.GetComponentInChildren<MeshFilter>(true);
            if (mesh && mesh.sharedMesh)
            {
                Vector3 size = mesh.sharedMesh.bounds.size;
                Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
                axis = bone.transform.InverseTransformDirection(mesh.transform.TransformDirection(axis));
                boneAxis = Quaternion.FromToRotation(axis, Vector3.right);
            }
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                boneCenter = bone.transform.InverseTransformPoint(bounds.center);
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest > .0001f) bone.transform.localScale *= .24f / longest;
            }
            bone.SetActive(true);
        }
        void CreateMarker()
        {
            marker = new GameObject("BoneCatch World Target"); marker.transform.SetParent(transform, false);
            ring = marker.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64;
            ring.widthMultiplier = .024f; ring.numCornerVertices = 3; ring.numCapVertices = 3;
            ringMaterial = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = ringMaterial;
            ring.startColor = ring.endColor = new Color(.2f, 1, .78f, .72f);
            for (int i = 0; i < ring.positionCount; i++) { float a = i * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * targetRadius); }
        }
        void CreateUI()
        {
            content = sharedUI.GetComponent<SceneUIContent>();
            if (content) { objective = new SceneUIContent.Objective { id = "BoneCatch.Play" }; content.objectives.Insert(0, objective); }
            help = new GameObject("BoneCatch Instructions", typeof(RectTransform)); help.transform.SetParent(sharedUI.hud.transform, false);
            var helpRect = (RectTransform)help.transform; helpRect.anchorMin = Vector2.zero; helpRect.anchorMax = Vector2.one; helpRect.offsetMin = helpRect.offsetMax = Vector2.zero;
            instructions = Instantiate(sharedUI.objectiveText, help.transform, false);
            var rt = instructions.rectTransform; rt.anchorMin = new Vector2(.06f, 1); rt.anchorMax = new Vector2(.94f, 1); rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, -100); rt.sizeDelta = new Vector2(0, 90);
            instructions.fontSize = 24; instructions.enableAutoSizing = false; instructions.alignment = TextAlignmentOptions.Center; instructions.raycastTarget = false;
            instructions.text = "겨울이 주변의 초록 링을 마우스로 조준하고 클릭해 짧게 던져주세요.";
            RefreshObjective();
        }
        void RefreshObjective()
        {
            if (objective != null) { objective.text = "겨울이와 뼈다귀 캐치하기 " + SuccessCount + "/" + requiredSuccesses; objective.completed = Phase == CatchPhase.Completed; content.Refresh(); }
        }
        public bool SelectTarget(CatchDirection direction)
        {
            if (!owned || (Phase != CatchPhase.Aiming && Phase != CatchPhase.Inactive)) return false;
            Direction = direction;
            // Use the actual idle mouth height, including the authored dog scale.
            TargetPosition = MouthPoint + (home - winter.position);
            if (direction == CatchDirection.Left) TargetPosition -= right * sideDistance;
            if (direction == CatchDirection.Right) TargetPosition += right * sideDistance;
            if (direction == CatchDirection.Up) TargetPosition += Vector3.up * jumpHeight;
            if (direction == CatchDirection.Forward) TargetPosition -= forward * forwardDistance;
            marker.transform.position = TargetPosition; marker.SetActive(true); Phase = CatchPhase.Aiming; return true;
        }
        void NextTarget()
        {
            int next = UnityEngine.Random.Range(0, 4);
            if (AttemptCount > 0 && next == (int)Direction) next = (next + UnityEngine.Random.Range(1, 4)) % 4;
            Phase = CatchPhase.Aiming; SelectTarget((CatchDirection)next); RefreshObjective();
        }
        void Update()
        {
            if (!owned) return;
            BlockReturnClicks();
            if (IsPaused || Phase != CatchPhase.Aiming || Mouse.current == null || !click.WasPressedThisFrame()) return;
            Vector2 position = Mouse.current.position.ReadValue();
            if (!playerCamera.pixelRect.Contains(position)) return;
            var ray = playerCamera.ScreenPointToRay(position);
            var plane = new Plane(-playerCamera.transform.forward, TargetPosition);
            if (plane.Raycast(ray, out float distance)) TryThrow(ray.GetPoint(distance));
        }
        public bool TryThrow(Vector3 aim)
        {
            if (!owned || IsPaused || Phase != CatchPhase.Aiming || !Finite(aim)) return false;
            Vector3 delta = aim - TargetPosition;
            // Keep misses in this close-range play area, including clicks far outside the ring.
            Vector3 landingAim = TargetPosition + Vector3.ClampMagnitude(delta, 1.4f);
            LastThrowSuccessful = delta.magnitude <= targetRadius;
            AttemptCount++; Phase = CatchPhase.Flying; held = carried = false; marker.SetActive(false);
            cycle = StartCoroutine(ThrowAndCatch(landingAim, LastThrowSuccessful)); return true;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        Vector3 MouthPoint
        {
            get
            {
                if (!lipLeft || !lipRight || !tongue || !nose || !head) return mouth.TransformPoint(mouthOffset);
                // The imported "mouth" bone is a jaw pivot. Locate the actual bite using the same rig landmarks as BallGame.
                Vector3 bite = Vector3.Lerp((lipLeft.position + lipRight.position) * .5f, tongue.position, .7f);
                Vector3 outward = (nose.position - head.position).normalized;
                float clearance = Mathf.Max(0, Vector3.Dot(nose.position - bite, outward));
                return bite + outward * clearance + winter.rotation * mouthOffset;
            }
        }
        IEnumerator ThrowAndCatch(Vector3 aim, bool success)
        {
            Play(Direction == CatchDirection.Up ? jumpState : Direction == CatchDirection.Forward ? forwardState : Direction == CatchDirection.Left ? leftState : rightState);
            Vector3 start = bone.transform.position, dogStart = winter.position;
            Vector3 catchRoot = dogStart + (TargetPosition - MouthPoint);
            float seconds = Mathf.Max(.18f, flightSeconds);
            yield return Animate(seconds, t => {
                bone.transform.position = Vector3.Lerp(start, aim, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .055f);
                bone.transform.Rotate(right, 720 * Time.deltaTime, Space.World);
                winter.position = Vector3.Lerp(dogStart, catchRoot, Mathf.SmoothStep(0, 1, t));
            });
            if (success)
            {
                // The final mouth alignment uses the current animated rig, avoiding an airborne near-miss.
                winter.position += TargetPosition - MouthPoint;
                carried = true; bone.transform.position = MouthPoint; Play(biteState); Squeak();
                SuccessCount++; onCaught.Invoke(); onSuccessCountChanged.Invoke(SuccessCount); RefreshObjective();
                float landingStart = winter.position.y;
                yield return Animate(.24f, t => winter.position = new Vector3(winter.position.x, Mathf.Lerp(landingStart, home.y, t), winter.position.z));
                if (SuccessCount >= requiredSuccesses) { yield return Celebrate(); yield break; }
                yield return Wait(.18f);
            }
            else
            {
                onMissed.Invoke(); Phase = CatchPhase.Recovering;
                instructions.text = "괜찮아요. 겨울이가 주워서 다시 가져와요.";
                Vector3 drop = Ground(aim), from = bone.transform.position;
                yield return Animate(.28f, t => bone.transform.position = Vector3.Lerp(from, drop + Vector3.up * .06f, t * t));
                Play(runState);
                Vector3 pickup = drop + forward * .22f;
                yield return WalkTo(pickup);
                Play(pickupState); yield return Wait(.4f); carried = true; Squeak(); yield return Wait(.22f);
            }
            Phase = CatchPhase.Returning; Play(runState);
            yield return WalkTo(home);
            winter.rotation = Quaternion.LookRotation(-forward); Play(idleState); yield return Wait(.18f);
            // A short handoff instead of throwing/fetching over a long distance.
            carried = false; var giveStart = bone.transform.position;
            yield return Animate(.22f, t => bone.transform.position = Vector3.Lerp(giveStart, HeldPoint, t));
            GiveToPlayer(); instructions.text = "초록 링을 마우스로 조준하고 클릭해 짧게 던져주세요."; NextTarget(); cycle = null;
        }
        IEnumerator Celebrate()
        {
            Phase = CatchPhase.Celebrating; instructions.text = "잘했어, 겨울아!";
            Vector3 center = winter.position; center.y = home.y;
            Play(happyState, runState);
            yield return Animate(Mathf.Max(.5f, celebrationSeconds), t => {
                float wave = Mathf.Sin(t * Mathf.PI * 4);
                winter.position = center + right * (wave * .16f) + Vector3.up * (Mathf.Abs(wave) * .045f);
                winter.rotation = Quaternion.LookRotation(-forward) * Quaternion.Euler(0, wave * 18, 0);
            });
            winter.SetPositionAndRotation(center, Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - center, Vector3.up)));
            Play(idleState); yield return Wait(.5f);
            Phase = CatchPhase.Completed; RefreshObjective();
            if (!completedRaised) { completedRaised = true; onCompleted.Invoke(); }
            if (returnToLivingRoom && GameManager.Instance && GameManager.Instance.Flow.VisitingObject == ToyId)
                GameManager.Instance.Flow.CompleteVisit(ToyId);
            cycle = null;
        }
        Vector3 Ground(Vector3 p)
        {
            p.y = home.y;
            if (NavMesh.SamplePosition(p, out var nav, .45f, NavMesh.AllAreas)) return nav.position;
            // The yard is flat; raycast only ground-like surfaces, never the dog/toys.
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (Vector3.Dot(hit.normal, Vector3.up) > .8f && !hit.transform.IsChildOf(winter) && hit.transform != winter && !hit.transform.IsChildOf(player.transform) && Mathf.Abs(hit.point.y - home.y) < .25f)
                    return hit.point;
            return p;
        }
        IEnumerator WalkTo(Vector3 destination)
        {
            destination = Ground(destination);
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(winter.position, out var a, .5f, NavMesh.AllAreas) && NavMesh.SamplePosition(destination, out var b, .5f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            { foreach (var corner in path.corners.Skip(1)) yield return Segment(corner); }
            else yield return Segment(destination);
        }
        IEnumerator Segment(Vector3 destination)
        {
            Vector3 start = winter.position, flat = Vector3.ProjectOnPlane(destination - start, Vector3.up);
            if (flat.sqrMagnitude > .001f) winter.rotation = Quaternion.LookRotation(flat);
            yield return Animate(Mathf.Max(.18f, Vector3.Distance(start, destination) / 2.4f), t => winter.position = Vector3.Lerp(start, destination, t));
        }
        IEnumerator Animate(float seconds, Action<float> step)
        {
            float elapsed = 0;
            while (elapsed < seconds) { if (!IsPaused) { elapsed += Time.deltaTime; step(Mathf.Clamp01(elapsed / seconds)); } yield return null; }
        }
        IEnumerator Wait(float seconds) { yield return Animate(seconds, _ => { }); }
        void Squeak() { SqueakCount++; if (squeakSource && squeakClip) squeakSource.PlayOneShot(squeakClip, squeakVolume); }
        void GiveToPlayer() { held = true; carried = false; }
        Vector3 HeldPoint
        {
            get
            {
                Vector3 view = playerCamera.WorldToViewportPoint(playerCamera.transform.TransformPoint(heldOffset));
                // Keep the toy visible even in a narrow, docked Game view.
                view.x = Mathf.Clamp(view.x, .25f, .74f); view.y = Mathf.Clamp(view.y, .24f, .76f);
                return playerCamera.ViewportToWorldPoint(view);
            }
        }
        void Play(string name, string fallback = null)
        {
            int hash = Animator.StringToHash("Base Layer." + name);
            if (!winterAnimator.HasState(0, hash)) hash = Animator.StringToHash("Base Layer." + (fallback ?? idleState));
            if (winterAnimator.HasState(0, hash)) winterAnimator.CrossFadeInFixedTime(hash, .075f, 0, 0);
        }
        void LateUpdate()
        {
            if (!owned || !bone) return;
            if (held)
            {
                var point = HeldPoint;
                PositionBone(point, playerCamera.transform.rotation * Quaternion.Euler(heldEuler) * boneAxis);
                if (hand && hand.handModel && hand.palmPoint) hand.handModel.transform.position += point - hand.palmPoint.position;
            }
            if (carried) PositionBone(MouthPoint, winter.rotation * Quaternion.Euler(carriedEuler) * boneAxis);
            if (marker && marker.activeSelf) marker.transform.rotation = playerCamera.transform.rotation;
        }
        void PositionBone(Vector3 center, Quaternion rotation)
        {
            bone.transform.rotation = rotation;
            bone.transform.position += center - bone.transform.TransformPoint(boneCenter);
        }
        public void CancelGame()
        {
            if (!owned) return;
            if (cycle != null) StopCoroutine(cycle); cycle = null;
            if (Phase != CatchPhase.Completed) Phase = CatchPhase.Cancelled;
            Restore();
        }
        void OnDisable() { CancelGame(); }
        void OnDestroy()
        {
            Restore();
            foreach (var pair in previewGames) if (pair.Key) pair.Key.GetType().GetField("autoStart")?.SetValue(pair.Key, pair.Value);
        }
        void Restore()
        {
            if (!owned) return; owned = false;
            actions?.Disable(); actions?.Dispose(); actions = null;
            foreach (var item in new[] { bone, marker, help }) if (item) { item.SetActive(false); Destroy(item); }
            if (ringMaterial) Destroy(ringMaterial);
            if (content && objective != null) { content.objectives.Remove(objective); content.Refresh(); }
            if (sceneBone) sceneBone.SetActive(oldBone);
            if (winter) winter.SetPositionAndRotation(oldWinterPos, oldWinterRot);
            if (winterAnimator)
            {
                winterAnimator.runtimeAnimatorController = oldController; winterAnimator.applyRootMotion = oldRootMotion; winterAnimator.cullingMode = oldCulling;
                if (oldController && oldAnimatorState.fullPathHash != 0) winterAnimator.Play(oldAnimatorState.fullPathHash, 0, oldAnimatorState.normalizedTime);
            }
            if (agent) agent.enabled = oldAgent;
            if (player)
            {
                player.cameraRoot.localPosition = oldCameraPos; player.Teleport(oldPlayerPos, oldPlayerRot, oldCameraRot);
                player.canMove = oldMove; player.canLook = oldLook; player.canJump = oldJump; player.canSprint = oldSprint; player.enabled = oldPlayer;
            }
            if (motion) motion.enabled = oldMotion;
            if (interactor)
            {
                interactor.enabled = oldInteractor;
                if (interactor.crosshairDot) interactor.crosshairDot.SetActive(oldCrosshair);
                if (interactor.handIcon) interactor.handIcon.SetActive(oldHandIcon);
            }
            if (hand && hand.handModel) { hand.handModel.transform.localPosition = oldHandPos; hand.handModel.transform.localRotation = oldHandRot; hand.handModel.SetActive(oldHandVisible); hand.enabled = oldHandEnabled; }
            foreach (var b in blockedTargets) if (b) b.enabled = true; blockedTargets.Clear();
            Cursor.lockState = oldCursor; Cursor.visible = oldCursorVisible;
        }
        void OnValidate() { requiredSuccesses = Mathf.Max(1, requiredSuccesses); flightSeconds = Mathf.Max(.18f, flightSeconds); targetRadius = Mathf.Max(.08f, targetRadius); }
    }
}
