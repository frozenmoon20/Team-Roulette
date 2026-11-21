using System.Collections;
using UnityEngine;
using TeamRoulette.Room3;
using TeamRoulette.UI;
using VolumetricLights;

// Scene-local visuals; progression remains owned by the existing Room3 GameManager.
[DefaultExecutionOrder(200)]
public sealed class WindowGameController : MonoBehaviour
{
    [Header("플레이어와 상호작용")]
    public CurtainGameController curtainGame;
    public FirstPersonPlayer player;
    public GameUI sharedUI;
    public Transform slidingWindow;
    public WindowGameTarget interactionTarget;
    public Collider interactionCollider;
    [Header("커튼 바람 연출")]
    public GameObject[] staticCurtains;
    public GameObject[] animatedCurtains;
    [SerializeField] VolumetricLight windowVolumetricLight;
    [Header("창문의 로컬 위치와 회전")]
    public Vector3 closedPosition;
    public Vector3 closedRotation;
    public Vector3 openPosition;
    public Vector3 openRotation;
    [Tooltip("창문이 닫힌 위치에서 열린 위치까지 이동하는 시간입니다. 단위는 초입니다.")]
    [Min(.1f)] public float openDuration = 2f;
    [HideInInspector] public AudioSource windSource;
    [HideInInspector] public AudioClip windClip;
    [HideInInspector] public float windVolume = .18f;
    [HideInInspector] public float windFadeSeconds = 1.2f;
    public bool WindowOpened { get; private set; }
    public bool WindActive { get; private set; }
    public bool IsOpening { get; private set; }
    public bool CanInteract => isActiveAndEnabled && !WindowOpened && !IsOpening &&
        curtainGame && curtainGame.Completed && GameManager.Instance &&
        GameManager.Instance.IsComplete(GameStage.Curtain) && GameManager.CanPlay(GameStage.Window);
    bool moveBefore, lookBefore, jumpBefore, sprintBefore, inputCaptured;
    GameManager owner;
    bool initialized;
    Vector3[] windPositions, windScales;
    Quaternion[] windRotations;

    void Awake()
    {
        if (!curtainGame || !player || !slidingWindow || !interactionTarget || !windSource || !windClip ||
            staticCurtains == null || staticCurtains.Length != 2 || animatedCurtains == null || animatedCurtains.Length != 2)
        { Debug.LogError("WindowGame: scene references are incomplete.", this); enabled = false; return; }
        foreach (var curtain in staticCurtains) if (!curtain) { enabled = false; return; }
        foreach (var curtain in animatedCurtains)
        {
            if (!curtain) { enabled = false; return; }
            foreach (var animation in curtain.GetComponentsInChildren<Animation>(true))
            {
                animation.playAutomatically = false;
                var state = animation["idle"];
                if (state == null || !state.clip.legacy)
                { Debug.LogError("WindowGame: a Legacy idle clip is required.", animation); enabled = false; return; }
                animation.Stop();
            }
            curtain.SetActive(false);
        }
        windPositions = new Vector3[animatedCurtains.Length]; windScales = new Vector3[animatedCurtains.Length];
        windRotations = new Quaternion[animatedCurtains.Length];
        for (int i = 0; i < animatedCurtains.Length; i++)
        { windPositions[i] = animatedCurtains[i].transform.localPosition; windRotations[i] = animatedCurtains[i].transform.localRotation; windScales[i] = animatedCurtains[i].transform.localScale; }
        windSource.playOnAwake = false; windSource.Stop();
        slidingWindow.localPosition = closedPosition;
        slidingWindow.localRotation = Quaternion.Euler(closedRotation);
        interactionTarget.enabled = false;
        SetDustWind(1f);
        initialized = true;
    }
    void Start() { Bind(); }
    void OnEnable() { if (initialized) Bind(); }
    void Bind()
    {
        if (owner || !GameManager.Instance) return;
        owner = GameManager.Instance;
        owner.Changed += Refresh;
        Refresh();
    }
    void Update()
    {
        if (!initialized) return;
        Bind();
        // Curtain finale sets Completed before reporting to the shared manager.
        if (interactionTarget) interactionTarget.enabled = CanInteract;
    }
    void Refresh()
    {
        if (!initialized || !owner) return;
        if (owner.IsComplete(GameStage.Window))
        {
            RestoreCompleted();
            return;
        }
        if (!WindowOpened && owner.IsComplete(GameStage.Curtain)) SetDustWind(1f);
        if (interactionTarget) interactionTarget.enabled = CanInteract;
    }
    void LateUpdate()
    {
        if (!WindActive || !initialized) return;
        // Keep the placed model root anchored even if the imported clip contains root curves.
        for (int i = 0; i < animatedCurtains.Length; i++)
        { var t = animatedCurtains[i].transform; t.localPosition = windPositions[i]; t.localRotation = windRotations[i]; t.localScale = windScales[i]; }
    }
    public void Interact()
    {
        if (!CanInteract || Time.timeScale <= 0 || (sharedUI && sharedUI.IsPaused)) return;
        if (PlayerHand.Instance && PlayerHand.Instance.Held) { PlayerInteractor.ShowHint("손을 비워주세요."); return; }
        if (!GameManager.TryBegin(GameStage.Window)) return;
        ObjectiveManager.Request(GameStage.Window, ObjectiveStep.Default);
        IsOpening = true;
        interactionTarget.enabled = false;
        CaptureInput();
        StartCoroutine(Open());
    }
    void CaptureInput()
    {
        moveBefore = player.canMove; lookBefore = player.canLook;
        jumpBefore = player.canJump; sprintBefore = player.canSprint;
        player.Teleport(player.transform.position, player.transform.rotation,
            player.cameraRoot ? player.cameraRoot.localRotation : Quaternion.identity);
        player.canMove = player.canLook = player.canJump = player.canSprint = false;
        inputCaptured = true;
    }
    void ReleaseInput()
    {
        if (!inputCaptured || !player) return;
        player.canMove = moveBefore; player.canLook = lookBefore;
        player.canJump = jumpBefore; player.canSprint = sprintBefore;
        inputCaptured = false;
    }
    IEnumerator Open()
    {
        float elapsed = 0;
        var fromPosition = slidingWindow.localPosition;
        var fromRotation = slidingWindow.localRotation;
        var targetRotation = Quaternion.Euler(openRotation);
        while (elapsed < Mathf.Max(.1f, openDuration))
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.1f, openDuration)));
            slidingWindow.localPosition = Vector3.Lerp(fromPosition, openPosition, t);
            slidingWindow.localRotation = Quaternion.Slerp(fromRotation, targetRotation, t);
            yield return null;
        }
        CompleteWindow();
    }
    void CompleteWindow()
    {
        if (WindowOpened || !initialized) return;
        slidingWindow.localPosition = openPosition;
        slidingWindow.localRotation = Quaternion.Euler(openRotation);
        WindowOpened = true;
        SetDustWind(5f);
        ApplyWind();
        IsOpening = false;
        ReleaseInput();
        GameManager.ReportComplete(GameStage.Window);
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugComplete() { StopAllCoroutines(); CompleteWindow(); }
#endif
    void ApplyWind()
    {
        foreach (var curtain in staticCurtains) curtain.SetActive(false);
        foreach (var curtain in animatedCurtains)
        {
            curtain.SetActive(true);
            foreach (var animation in curtain.GetComponentsInChildren<Animation>(true))
            {
                animation.playAutomatically = false;
                animation.Stop();
                var state = animation["idle"];
                state.time = 0; state.speed = 1; state.wrapMode = WrapMode.Loop;
                animation.Play("idle", PlayMode.StopAll);
                animation.Sample();
            }
        }
        WindActive = true;
        PlayWind();
    }

    void PlayWind()
    {
        var roomAudio = RoomAudio.For(this);
        if (roomAudio)
            roomAudio.Play("Window Wind", windSource);
        else if (AudioManager.Instance)
            AudioManager.Instance.PlaySceneLoop(windSource, windClip, windVolume, windFadeSeconds);
    }

    public void RestoreCompleted()
    {
        if (!initialized || IsOpening) return;
        bool restart = !WindowOpened || !animatedCurtains[0].activeSelf || !animatedCurtains[1].activeSelf;
        foreach (var curtain in animatedCurtains)
            foreach (var animation in curtain.GetComponentsInChildren<Animation>(true)) restart |= !animation.IsPlaying("idle");
        WindowOpened = true;
        SetDustWind(5f);
        slidingWindow.localPosition = openPosition;
        slidingWindow.localRotation = Quaternion.Euler(openRotation);
        if (restart) ApplyWind();
        else
        {
            foreach (var curtain in staticCurtains) curtain.SetActive(false);
            if (!windSource || !windSource.isPlaying)
                PlayWind();
        }
        interactionTarget.enabled = false;
    }

    void SetDustWind(float speed)
    {
        if (!windowVolumetricLight || Mathf.Approximately(windowVolumetricLight.dustWindSpeed, speed)) return;
        windowVolumetricLight.dustWindSpeed = speed;
        windowVolumetricLight.Refresh();
    }

    void OnDisable()
    {
        if (owner) owner.Changed -= Refresh;
        owner = null;
        if (!initialized) return;
        StopAllCoroutines(); ReleaseInput();
        if (IsOpening)
        {
            // An interrupted opening is retryable and never reports completion.
            slidingWindow.localPosition = closedPosition;
            slidingWindow.localRotation = Quaternion.Euler(closedRotation);
            IsOpening = false;
        }
        var roomAudio = RoomAudio.For(this);
        if (roomAudio)
            roomAudio.Stop("Window Wind");
        else if (AudioManager.Instance)
            AudioManager.Instance.StopSceneLoop(windSource);
    }
}
