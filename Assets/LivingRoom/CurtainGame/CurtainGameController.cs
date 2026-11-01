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
    public CanvasGroup whiteScreen;

    [Header("Shrink (world Z)")]
    public float startZ = 10f;
    [Range(0.1f, 1f)] public float minimumScale = 0.2f;
    public float shrinkPerUnit = 0.2f;
    [Header("Guide")]
    public Vector3 dogStart = new Vector3(16.5f, 0.2f, 10f);
    public float dogEndZ = 15f;
    [Min(0.01f)] public float dogStepSeconds = 0.5f;
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
    Vector3 originalScale, leftClosed, rightClosed, guideFrom, guideTo;
    float walk, sprint, jump, acceleration, deceleration, originalStep, guideTime;
    bool initialized, guideMoving, playerWasEnabled;
    PlayerMotionFeel motionFeel;
    bool feelWasEnabled;

    void Awake()
    {
        if (!player || !startTrigger || !endTrigger || !curtainL || !curtainR || !dog || !whiteScreen)
        {
            Debug.LogError("CurtainGame: assign all scene references.", this);
            enabled = false;
            return;
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
        leftClosed = curtainL.position; rightClosed = curtainR.position;
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
            guideFrom = guideTo = dogStart;
            dog.SetActive(true);
        }
        if (!Started) return;

        float z = player.transform.position.z;
        float retreatEdge = startTrigger.bounds.min.z - body.radius * Mathf.Abs(originalScale.z);
        float scale = z < retreatEdge - 0.02f ? 1f :
            Mathf.Clamp(1f - Mathf.Max(0, z - startZ) * shrinkPerUnit, minimumScale, 1f);
        ApplyScale(scale);
        UpdateGuide(z, Time.deltaTime);
        if (endTrigger.bounds.Intersects(body.bounds)) StartCoroutine(Finale());
    }

    void UpdateGuide(float playerZ, float dt)
    {
        if (!guideMoving && playerZ >= guideTo.z && guideTo.z < dogEndZ)
        {
            guideFrom = dog.transform.position;
            guideTo = new Vector3(dogStart.x, dogStart.y, Mathf.Min(dogEndZ, guideTo.z + 1f));
            guideTime = 0;
            guideMoving = true;
        }
        if (!guideMoving) return;
        guideTime += dt;
        float t = Mathf.Clamp01(guideTime / Mathf.Max(0.01f, dogStepSeconds));
        dog.transform.position = Vector3.Lerp(guideFrom, guideTo, Mathf.SmoothStep(0, 1, t));
        if (t >= 1) guideMoving = false;
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
            float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.15f, 0.75f, t));
            curtainL.position = leftClosed + Vector3.left * curtainTravel * open;
            curtainR.position = rightClosed + Vector3.right * curtainTravel * open;
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

    public void RestoreCompleted()
    {
        if (!initialized) return;
        Started = Completed = true;
        PlayingFinale = false;
        ApplyScale(1f);
        curtainL.position = leftClosed + Vector3.left * curtainTravel;
        curtainR.position = rightClosed + Vector3.right * curtainTravel;
        dog.SetActive(false);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
    }

    void OnDisable()
    {
        if (!initialized) return;
        StopAllCoroutines();
        if (player) ApplyScale(1f);
        if (whiteScreen) { whiteScreen.alpha = 0; whiteScreen.blocksRaycasts = false; }
        if (PlayingFinale)
        {
            if (player) player.enabled = playerWasEnabled;
            if (motionFeel) motionFeel.enabled = feelWasEnabled;
            curtainL.position = leftClosed + Vector3.left * curtainTravel;
            curtainR.position = rightClosed + Vector3.right * curtainTravel;
            Completed = true;
            PlayingFinale = false;
        }
    }
}
