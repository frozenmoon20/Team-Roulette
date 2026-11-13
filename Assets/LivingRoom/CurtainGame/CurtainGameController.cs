using System.Collections;
using UnityEngine;

// Scene-specific sequence; the shared player and Dog remain reusable.
[DefaultExecutionOrder(100)]
public sealed class CurtainGameController : MonoBehaviour
{
    public FirstPersonPlayer player;
    public BoxCollider startTrigger;
    public BoxCollider endTrigger;
    public Transform curtainL;
    public Transform curtainR;
    public GameObject dog;
    public GameObject animatedCurtainA;
    public GameObject animatedCurtainB;
    public CanvasGroup whiteScreen;

    [Header("Shrink (world Z)")]
    public float startZ = 10f;
    [Range(0.1f, 1f)] public float minimumScale = 0.2f;
    [HideInInspector] public float shrinkPerUnit = 0.2f;
    [HideInInspector] public float scaleChangePerSecond = 0.25f;
    [Header("Guide")]
    public Vector3 dogStart = new Vector3(16.5f, 0.2f, 10f);
    public float dogEndZ = 15f;
    [Min(0.01f)] public float dogStepSeconds = 1.5f;
    public RuntimeAnimatorController guideAnimationController;
    [Header("Finale")]
    [Min(0.1f)] public float finaleSeconds = 2f;
    public float curtainTravel = 5f;

    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public bool PlayingFinale { get; private set; }
    public float CurrentScale { get; private set; } = 1f;

    CharacterController body;
    Camera[] cameras;
    float[] originalNear;
    Vector3 originalScale, guideFrom, guideTo, guideDirection, guideEnd;
    float walk, sprint, jump, acceleration, deceleration, originalStep, guideTime;
    bool initialized, guideMoving, playerWasEnabled;
    PlayerMotionFeel motionFeel;
    bool feelWasEnabled;
    int guideDialogueIndex = 1;
    Animator guideAnimator;
    RuntimeAnimatorController previousGuideController;
    bool previousRootMotion;

    void Awake()
    {
        if (!player || !startTrigger || !endTrigger || !curtainL || !curtainR || !dog || !whiteScreen || !animatedCurtainA || !animatedCurtainB)
        {
            Debug.LogError("CurtainGame: assign all scene references.", this);
            enabled = false;
            return;
        }
        guideAnimator = dog.GetComponentInChildren<Animator>(true);
        if (guideAnimator)
        {
            previousGuideController = guideAnimator.runtimeAnimatorController;
            previousRootMotion = guideAnimator.applyRootMotion;
            if (guideAnimationController) guideAnimator.runtimeAnimatorController = guideAnimationController;
            guideAnimator.applyRootMotion = false;
        }
        body = player.GetComponent<CharacterController>();
        originalScale = player.transform.localScale;
        walk = player.moveSpeed; sprint = player.sprintSpeed; jump = player.jumpHeight;
        acceleration = player.acceleration; deceleration = player.deceleration;
        motionFeel = player.GetComponent<PlayerMotionFeel>();
        if (motionFeel) originalStep = motionFeel.stepDistance;
        cameras = player.GetComponentsInChildren<Camera>(true);
        originalNear = new float[cameras.Length];
        for (int i = 0; i < cameras.Length; i++) originalNear[i] = cameras[i].nearClipPlane;
        guideDirection = Vector3.ProjectOnPlane(endTrigger.bounds.center - startTrigger.bounds.center, Vector3.up).normalized;
        if (guideDirection.sqrMagnitude < 0.5f) guideDirection = Vector3.forward;
        float length = Vector3.ProjectOnPlane(endTrigger.bounds.center - startTrigger.bounds.center, Vector3.up).magnitude;
        float height = dog.transform.position.y;
        dogStart = startTrigger.bounds.center + guideDirection * Mathf.Min(1f, length);
        dogStart.y = height;
        guideEnd = endTrigger.bounds.center;
        guideEnd.y = height;
        SetCurtainsRevealed(false);
        dog.SetActive(false);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
        initialized = true;
    }

    void Update()
    {
        if (!initialized || Completed || PlayingFinale || !body.enabled) return;
        // Query the controller bounds so no Rigidbody is required on these scene markers.
        // CharacterController.Move can skip trigger messages when its scale changes.
        if (!Started && startTrigger.bounds.Intersects(body.bounds))
        {
            if (!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Curtain)) return;
            Started = true;
            TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Curtain, TeamRoulette.Room3.ObjectiveStep.FollowDog);
            dog.transform.position = dogStart;
            dog.transform.rotation = Quaternion.LookRotation(guideDirection, Vector3.up);
            guideFrom = guideTo = dogStart;
            dog.SetActive(true);
            SetGuideWalking(false);
        }
        if (!Started) return;

        float progress = Vector3.Dot(player.transform.position - startTrigger.bounds.center, guideDirection);
        // Spread the full shrink across the route, ending at the player's first End contact.
        float routeLength = Vector3.Dot(endTrigger.bounds.center - startTrigger.bounds.center, guideDirection);
        Vector3 extents = endTrigger.bounds.extents;
        float endHalfDepth = Mathf.Abs(guideDirection.x) * extents.x + Mathf.Abs(guideDirection.z) * extents.z;
        float finalRadius = body.radius * Mathf.Max(Mathf.Abs(originalScale.x), Mathf.Abs(originalScale.z)) * minimumScale;
        float shrinkDistance = Mathf.Max(0.01f, routeLength - endHalfDepth - finalRadius);
        float scale = Mathf.Lerp(1f, minimumScale, Mathf.Clamp01(progress / shrinkDistance));
        ApplyScale(scale);
        UpdateGuide(Time.deltaTime);
        if (endTrigger.bounds.Intersects(body.bounds)) StartCoroutine(Finale());
    }

    void UpdateGuide(float dt)
    {
        if (!guideMoving && Vector3.Dot(player.transform.position - guideTo, guideDirection) >= 0f && Vector3.Dot(guideEnd - guideTo, guideDirection) > 0.001f)
        {
            guideFrom = dog.transform.position;
            guideTo += guideDirection * Mathf.Min(1f, Vector3.Dot(guideEnd - guideTo, guideDirection));
            guideTime = 0;
            guideMoving = true;
            SetGuideWalking(true);
        }
        if (!guideMoving) return;
        guideTime += Mathf.Min(dt, 0.05f);
        float t = Mathf.Clamp01(guideTime / Mathf.Max(0.01f, dogStepSeconds));
        dog.transform.position = Vector3.Lerp(guideFrom, guideTo, Mathf.SmoothStep(0, 1, t));
        if (t >= 1)
        {
            guideMoving = false;
            SetGuideWalking(false);
            var lines = TeamRoulette.Room3.Room3DialogueScript.CurtainStart;
            if (guideDialogueIndex < lines.Length)
            {
                var dialogue = Object.FindFirstObjectByType<TeamRoulette.Room3.DialogueManager>();
                if (dialogue) dialogue.PlayLines(new[] { lines[guideDialogueIndex++] });
            }
        }
    }

    void SetGuideWalking(bool walking)
    {
        if (!guideAnimator || !guideAnimator.runtimeAnimatorController) return;
        int state = Animator.StringToHash("Base Layer.Arm_Spitz|" + (walking ? "Walk_F_IP" : "Idle_1"));
        if (guideAnimator.HasState(0, state)) guideAnimator.CrossFadeInFixedTime(state, 0.12f, 0);
    }

    void ApplyScale(float scale)
    {
        CurrentScale = scale;
        player.transform.localScale = originalScale * scale;
        player.moveSpeed = walk * scale;
        player.sprintSpeed = sprint * scale;
        player.jumpHeight = jump * scale;
        player.acceleration = acceleration * scale;
        player.deceleration = deceleration * scale;
        if (motionFeel) motionFeel.stepDistance = originalStep * scale;
        for (int i = 0; i < cameras.Length; i++) cameras[i].nearClipPlane = originalNear[i] * scale;
        Physics.SyncTransforms();
    }

    IEnumerator Finale()
    {
        PlayingFinale = true;
        TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Curtain, TeamRoulette.Room3.ObjectiveStep.OpeningCurtains);
        dog.SetActive(false);
        playerWasEnabled = player.enabled;
        player.enabled = false; // canMove alone still permits momentum and gravity.
        if (motionFeel) { feelWasEnabled = motionFeel.enabled; motionFeel.enabled = false; }
        whiteScreen.blocksRaycasts = true;
        Vector3 fixedPosition = player.transform.position;
        float elapsed = 0;
        while (elapsed < finaleSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, finaleSeconds));
            // Fade to white, open under the light, then reveal the window.
            whiteScreen.alpha = t < 0.25f ? Mathf.SmoothStep(0, 1, t / 0.25f) :
                t < 0.7f ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - 0.7f) / 0.3f);
            if (t >= 0.5f) SetCurtainsRevealed(true);
            if (t >= 0.5f && CurrentScale != 1f) ApplyScale(1f);
            player.transform.position = fixedPosition;
            yield return null;
        }
        ApplyScale(1f);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
        Completed = true;
        PlayingFinale = false;
        player.enabled = playerWasEnabled;
        if (motionFeel) motionFeel.enabled = feelWasEnabled;
        TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Curtain);
    }

    void SetCurtainsRevealed(bool revealed)
    {
        curtainL.gameObject.SetActive(!revealed);
        curtainR.gameObject.SetActive(!revealed);
        animatedCurtainA.SetActive(revealed);
        animatedCurtainB.SetActive(revealed);
    }

    public void RestoreCompleted()
    {
        if (!initialized) return;
        Started = Completed = true;
        PlayingFinale = false;
        ApplyScale(1f);
        SetCurtainsRevealed(true);
        dog.SetActive(false);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
    }

    void OnDisable()
    {
        if (!initialized) return;
        StopAllCoroutines();
        if (guideAnimator)
        {
            guideAnimator.runtimeAnimatorController = previousGuideController;
            guideAnimator.applyRootMotion = previousRootMotion;
        }
        if (player) ApplyScale(1f);
        if (whiteScreen) { whiteScreen.alpha = 0; whiteScreen.blocksRaycasts = false; }
        if (PlayingFinale)
        {
            if (player) player.enabled = playerWasEnabled;
            if (motionFeel) motionFeel.enabled = feelWasEnabled;
            SetCurtainsRevealed(true);
            Completed = true;
            PlayingFinale = false;
        }
    }
}





