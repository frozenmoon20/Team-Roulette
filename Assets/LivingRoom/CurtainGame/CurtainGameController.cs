using System.Collections;
using UnityEngine;

// Click interaction for the living room curtains.
[DefaultExecutionOrder(100)]
public sealed class CurtainGameController : MonoBehaviour
{
    public FirstPersonPlayer player;
    [HideInInspector] public BoxCollider startTrigger;
    [HideInInspector] public BoxCollider endTrigger;
    public Transform curtainL;
    public Transform curtainR;
    [HideInInspector] public GameObject dog;
    public GameObject animatedCurtainA;
    public GameObject animatedCurtainB;
    public CanvasGroup whiteScreen;

    [Header("Shrink (world Z)")]
    [HideInInspector] public float startZ = 10f;
    [Range(0.1f, 1f)] [HideInInspector] public float minimumScale = 0.2f;
    [HideInInspector] public float shrinkPerUnit = 0.2f;
    [HideInInspector] public float scaleChangePerSecond = 0.25f;
    [Header("Guide")]
    [HideInInspector] public Vector3 dogStart = new Vector3(16.5f, 0.2f, 10f);
    [HideInInspector] public float dogEndZ = 15f;
    [Min(0.01f)] [HideInInspector] public float dogStepSeconds = 1.5f;
    [HideInInspector] public RuntimeAnimatorController guideAnimationController;
    [Header("Finale")]
    [Min(0.1f)] public float finaleSeconds = 2f;
    [HideInInspector] public float curtainTravel = 5f;

    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public bool PlayingFinale { get; private set; }
    public float CurrentScale => 1f;
    bool initialized, playerWasEnabled, feelWasEnabled;
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

    public bool TryOpen()
    {
        if (!initialized || !isActiveAndEnabled || Started || Completed || PlayingFinale) return false;
        if (!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Curtain)) return false;
        Started = true;
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
            if (t >= .5f) SetCurtainsRevealed(true);
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
