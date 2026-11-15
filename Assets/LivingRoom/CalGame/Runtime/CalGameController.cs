using UnityEngine;
using TeamRoulette.Room3;

public sealed class CalGameController : MonoBehaviour
{
    public Renderer calendarRenderer;
    public Texture2D decemberCal, mayCal, mayCalCircle;
    public AudioSource pageAudio;
    public AudioClip pageTurnSound;
    public bool Turned { get; private set; }
    public bool Marked { get; private set; }
    public Texture CurrentPage => Marked ? mayCalCircle : Turned ? mayCal : decemberCal;
    MaterialPropertyBlock properties;
    int lastClickFrame = -1;
    void ShowPage()
    {
        if (!calendarRenderer) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        calendarRenderer.GetPropertyBlock(properties);
        properties.SetTexture("_BaseMap", CurrentPage);
        properties.SetTexture("_MainTex", CurrentPage);
        calendarRenderer.SetPropertyBlock(properties);
    }
    public void RestoreCompleted() { Turned = Marked = true; ShowPage(); }
    void Awake()
    {
        if (!calendarRenderer || !decemberCal || !mayCal || !mayCalCircle || !pageAudio)
        { Debug.LogError("CalGame: assign calendar Renderer, three PNG textures and AudioSource.", this); enabled = false; return; }
        ShowPage();
    }
    // Compatibility for existing Room3 verification and event callers.
    public void Click(bool unusedSpringPage) { Click(); }
    public void Click()
    {
        if (!isActiveAndEnabled || Marked || Time.timeScale <= 0 || lastClickFrame == Time.frameCount) return;
        if (!GameManager.TryBegin(GameStage.Calendar)) return;
        lastClickFrame = Time.frameCount;
        if (!Turned)
        {
            Turned = true; ShowPage();
            ObjectiveManager.Request(GameStage.Calendar, ObjectiveStep.MarkCalendar);
            if (pageTurnSound) pageAudio.PlayOneShot(pageTurnSound);
        }
        else
        {
            Marked = true; ShowPage();
            GameManager.ReportComplete(GameStage.Calendar);
        }
    }
}


