using UnityEngine;

public sealed class CalGameController : MonoBehaviour
{
    public GameObject calenderWin;
    public GameObject calenderSpr;
    public GameObject circle;
    public AudioSource pageAudio;
    [Tooltip("Replace this clip to change the paper-turn sound.")]
    public AudioClip pageTurnSound;
    public bool Turned { get; private set; }
    public bool Marked { get; private set; }
    int lastClickFrame = -1;

    public void RestoreCompleted()
    {
        Turned = Marked = true;
        calenderWin.SetActive(false);
        calenderSpr.SetActive(true);
        circle.SetActive(true);
    }

    void Awake()
    {
        if (!calenderWin || !calenderSpr || !circle || !pageAudio)
        {
            Debug.LogError("CalGame: assign both calendars, Circle and AudioSource.", this);
            enabled = false;
            return;
        }
        calenderWin.SetActive(true);
        calenderSpr.SetActive(false);
        circle.SetActive(false);
    }

    public void Click(bool springPage)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0 || lastClickFrame == Time.frameCount) return;
        if (!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Calendar)) return;
        if (!Turned && !springPage)
        {
            lastClickFrame = Time.frameCount;
            Turned = true;
            TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Calendar, TeamRoulette.Room3.ObjectiveStep.MarkCalendar);
            calenderWin.SetActive(false);
            calenderSpr.SetActive(true);
            if (pageTurnSound) pageAudio.PlayOneShot(pageTurnSound);
        }
        else if (Turned && springPage && !Marked)
        {
            lastClickFrame = Time.frameCount;
            Marked = true;
            circle.SetActive(true);
            TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Calendar);
        }
    }
}
