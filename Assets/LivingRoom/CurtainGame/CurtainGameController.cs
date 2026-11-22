using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VolumetricLights;

// Click interaction for the living room curtains.
[DefaultExecutionOrder(100)]
public sealed class CurtainGameController : MonoBehaviour
{
    [Header("플레이어와 아직 닫힌 커튼")]
    public FirstPersonPlayer player;
    [HideInInspector] public BoxCollider startTrigger;
    [HideInInspector] public BoxCollider endTrigger;
    public Transform curtainL;
    public Transform curtainR;
    [HideInInspector] public GameObject dog;
    [Header("커튼을 연 뒤 표시할 모델")]
    public GameObject animatedCurtainA;
    public GameObject animatedCurtainB;
    [Header("화면 전환과 조명")]
    public CanvasGroup whiteScreen;
    [SerializeField] Light directionalLight;
    [SerializeField] Light boardCenterFillLight;
    [SerializeField] GameObject floatingDust;
    [SerializeField] VolumetricLight windowVolumetricLight;

    [HideInInspector] public float startZ = 10f;
    [Range(0.1f, 1f)] [HideInInspector] public float minimumScale = 0.2f;
    [HideInInspector] public float shrinkPerUnit = 0.2f;
    [HideInInspector] public float scaleChangePerSecond = 0.25f;
    [HideInInspector] public Vector3 dogStart = new Vector3(16.5f, 0.2f, 10f);
    [HideInInspector] public float dogEndZ = 15f;
    [Min(0.01f)] [HideInInspector] public float dogStepSeconds = 1.5f;
    [HideInInspector] public RuntimeAnimatorController guideAnimationController;
    [Tooltip("커튼을 여는 연출 시간입니다. 단위는 초입니다.")]
    [Min(0.1f)] public float finaleSeconds = 2f;
    [HideInInspector] public float curtainTravel = 5f;

    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public bool PlayingFinale { get; private set; }
    public float CurrentScale => 1f;
    bool initialized, playerWasEnabled, feelWasEnabled, curtainsRevealed;
    PlayerMotionFeel motionFeel;

    void Awake()
    {
        if (!player || !curtainL || !curtainR || !animatedCurtainA || !animatedCurtainB || !whiteScreen)
        {
            Debug.LogError("CurtainGame: assign player, closed/open curtains and white screen.", this);
            enabled = false;
            return;
        }
        motionFeel = player.GetComponent<PlayerMotionFeel>();
        SetCurtainsRevealed(false);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
        initialized = true;
    }

    void OnEnable()
    {
        if (initialized) ApplyLighting();
    }

    public bool TryOpen()
    {
        if (!initialized || !isActiveAndEnabled || Started || Completed || PlayingFinale) return false;
        if (!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Curtain)) return false;
        Started = true;
        TeamRoulette.Room3.RoomAudio.For(this)?.Play("Curtain");
        StartCoroutine(Finale());
        return true;
    }

    IEnumerator Finale()
    {
        PlayingFinale = true;
        TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Curtain, TeamRoulette.Room3.ObjectiveStep.Default);
        playerWasEnabled = player.enabled;
        player.enabled = false;
        if (motionFeel) { feelWasEnabled = motionFeel.enabled; motionFeel.enabled = false; }
        whiteScreen.blocksRaycasts = true;
        Vector3 position = player.transform.position;
        float elapsed = 0;
        while (elapsed < finaleSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, finaleSeconds));
            whiteScreen.alpha = t < .25f ? Mathf.SmoothStep(0, 1, t / .25f) :
                t < .7f ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - .7f) / .3f);
            if (t >= .5f && !curtainsRevealed) SetCurtainsRevealed(true);
            player.transform.position = position;
            yield return null;
        }
        CompleteCurtain();
    }

    void ReleasePlayer()
    {
        if (!PlayingFinale) return;
        if (player) player.enabled = playerWasEnabled;
        if (motionFeel) motionFeel.enabled = feelWasEnabled;
    }

    void CompleteCurtain()
    {
        if (Completed || !initialized) return;
        RestoreCompleted();
        TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Curtain);
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugComplete() { StopAllCoroutines(); CompleteCurtain(); }
#endif
    void SetCurtainsRevealed(bool revealed)
    {
        curtainL.gameObject.SetActive(!revealed);
        curtainR.gameObject.SetActive(!revealed);
        animatedCurtainA.SetActive(revealed);
        animatedCurtainB.SetActive(revealed);
        curtainsRevealed = revealed;
        if (boardCenterFillLight)
        {
            boardCenterFillLight.enabled = revealed;
            if (revealed) boardCenterFillLight.gameObject.SetActive(true);
        }
        ApplyLighting();
        if (floatingDust) floatingDust.SetActive(revealed);
        if (windowVolumetricLight && windowVolumetricLight.enableDustParticles != revealed)
        {
            windowVolumetricLight.enableDustParticles = revealed;
            windowVolumetricLight.Refresh();
        }
    }

    void ApplyLighting()
    {
        if (!directionalLight) return;
        directionalLight.shadowStrength = curtainsRevealed ? .8f : 1f;
        if (gameObject.scene != SceneManager.GetActiveScene()) return;
        float intensity = curtainsRevealed ? .3f : 0f;
        if (Mathf.Approximately(RenderSettings.ambientIntensity, intensity)) return;
        RenderSettings.ambientIntensity = intensity;
        DynamicGI.UpdateEnvironment();
    }
    public void RestoreCompleted()
    {
        if (!initialized) return;
        StopAllCoroutines();
        ReleasePlayer();
        Started = Completed = true;
        PlayingFinale = false;
        SetCurtainsRevealed(true);
        whiteScreen.alpha = 0;
        whiteScreen.blocksRaycasts = false;
    }
    void OnDisable()
    {
        if (!initialized) return;
        StopAllCoroutines();
        ReleasePlayer();
        if (PlayingFinale) { SetCurtainsRevealed(true); Completed = true; }
        PlayingFinale = false;
        if (whiteScreen) { whiteScreen.alpha = 0; whiteScreen.blocksRaycasts = false; }
    }
}
