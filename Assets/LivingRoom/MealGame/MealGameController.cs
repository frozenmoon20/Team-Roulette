using System.Collections;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(300)]
public sealed class MealGameController : MonoBehaviour
{
    [Header("Existing objects")]
    public GrabbableItem rawToast, jam, juice, cookedToast, plate, juiceCup;
    public GameObject jamToast, emptyCup, bakingToast;
    public PlayerInteractor interactor;
    public FirstPersonPlayer player;

    [Header("Placement markers (move these in the Scene view)")]
    public Transform jamPoint, cookedPopPoint, toastOnPlatePoint, plateTablePoint, cupTablePoint, eatingView;
    [Tooltip("Player feet position after the meal; remains here after controls resume.")]
    public Transform playerAfterMeal;
    [Header("Timer and ending")]
    public GameObject timerPanel;
    public TMP_Text timerText;
    public CanvasGroup blackScreen;
    [Min(.1f)] public float toastSeconds = 10f;
    [Min(0)] public float blackoutSeconds = .5f;
    [Min(.1f)] public float eatingSeconds = 3f;
    [Header("Replaceable sound effects")]
    public AudioSource toasterAudio, actionAudio;
    public AudioClip dingSound, pourSound, eatSound;

    public bool CookingStarted { get; private set; }
    public bool ToastReady { get; private set; }
    public bool JamOnCounter { get; private set; }
    public bool ToastOnPlate { get; private set; }
    public bool JamApplied { get; private set; }
    public bool PlateOnTable { get; private set; }
    public bool JuicePoured { get; private set; }
    public bool CupOnTable { get; private set; }
    public bool Ending { get; private set; }
    public bool Completed { get; private set; }
    public float SecondsRemaining { get; private set; }

    PlayerHand Hand => PlayerHand.Instance;
    public bool IsHoldingMealItem => Hand && Owns(Hand.Held);
    MealAction selected;
    GrabbableItem carried;
    Transform returnParent;
    Vector3 returnPosition, returnScale;
    Quaternion returnRotation;
    bool returnActive;
    string hint;
    float hintUntil;
    bool controlsSaved, playerEnabled, feelEnabled;
    PlayerMotionFeel feel;
    Vector3 cameraPosition;
    Quaternion cameraRotation;

    void LateUpdate()
    {
        // PalmPoint inherits the rotated wrist bone. Only MealGame objects are
        // leveled here; the shared hand, pose and pickup code remain untouched.
        if (!carried || !Hand || Hand.Held != carried) return;
        Transform palm = Hand.palmPoint ? Hand.palmPoint : Hand.transform;
        if (carried.transform.parent != palm) return; // Wait for the hand animation.
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
        carried.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        var mesh = carried.GetComponent<MeshFilter>();
        if (!mesh || !mesh.sharedMesh) return;
        Bounds bounds = mesh.sharedMesh.bounds;
        Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        carried.transform.position = palm.position + player.transform.TransformVector(carried.positionOffset)
            - carried.transform.TransformVector(bottom);
    }

    void Start()
    {
        // Initialize GrabbableItem's cached bounds/home before any pose change.
        foreach (var item in new[] { rawToast, jam, juice, cookedToast, plate, juiceCup })
        {
            item.gameObject.SetActive(true);
            item.enabled = false; // Only this sequence may authorize a pickup.
        }
        rawToast.gameObject.SetActive(false);
        jam.gameObject.SetActive(false);
        juice.gameObject.SetActive(false);
        cookedToast.gameObject.SetActive(false);
        juiceCup.gameObject.SetActive(false);
        jamToast.SetActive(false);
        bakingToast.SetActive(false);
        emptyCup.SetActive(true);
        timerPanel.SetActive(false);
        blackScreen.alpha = 0;
        blackScreen.blocksRaycasts = false;
    }

    public bool Owns(GrabbableItem item) => item &&
        (item == rawToast || item == jam || item == juice || item == cookedToast || item == plate || item == juiceCup);

    bool CanPickUp => Hand && Hand.IsVisible && !Hand.Held && OptionState.Instance &&
        OptionState.Instance.IsUnlocked(OptionType.Grab);

    public void ClearSelection() { selected = MealAction.None; }
    public string Label(MealTarget target) => Time.time < hintUntil ? hint : target ? target.displayName : "";
    void Hint(string message) { hint = message; hintUntil = Time.time + 1.5f; }

    public bool CanUse(MealAction action)
    {
        if (Ending || Completed || !TeamRoulette.Room3.GameManager.CanPlay(TeamRoulette.Room3.GameStage.Meal)) return false;
        var held = Hand ? Hand.Held : null;
        switch (action)
        {
            case MealAction.Fridge: return CanPickUp && (!CookingStarted || !JamOnCounter || (PlateOnTable && !JuicePoured));
            case MealAction.Toaster: return held == rawToast || (CanPickUp && ToastReady && !ToastOnPlate);
            case MealAction.Counter: return held == jam && !JamOnCounter;
            case MealAction.Plate: return held == cookedToast || (CanPickUp && JamApplied && !PlateOnTable);
            case MealAction.CookedToast: return CanPickUp && ToastReady && (!ToastOnPlate || (JamOnCounter && !JamApplied));
            case MealAction.Jam: return !held && JamOnCounter && ToastOnPlate && !JamApplied;
            case MealAction.JamToast: return CanPickUp && JamApplied && !PlateOnTable;
            case MealAction.EmptyCup: return held == juice && !JuicePoured;
            case MealAction.JuiceCup: return CanPickUp && JuicePoured && !CupOnTable;
            case MealAction.Table: return (held == plate && JamApplied) || (held == juiceCup && JuicePoured);
            default: return false;
        }
    }

    // Every left click (including scenery/empty space) reaches here, breaking the pair.
    public void Click(MealAction action)
    {
        try
        {
        if (Ending || Completed || Time.timeScale <= 0) return;
        if (!TeamRoulette.Room3.GameManager.CanPlay(TeamRoulette.Room3.GameStage.Meal)) return;
        if (action != MealAction.None && !TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Meal)) return;
        MealAction previous = selected;
        ClearSelection();
        var held = Hand ? Hand.Held : null;
        if (!held && JamOnCounter && ToastOnPlate && !JamApplied &&
            (action == MealAction.Jam || action == MealAction.CookedToast))
        {
            if ((previous == MealAction.Jam && action == MealAction.CookedToast) ||
                (previous == MealAction.CookedToast && action == MealAction.Jam))
            {
                cookedToast.gameObject.SetActive(false);
                jamToast.SetActive(true);
                JamApplied = true;
                Hint("잼 토스트가 완성됐다.");
            }
            else { selected = action; Hint(action == MealAction.Jam ? "이어서 토스트를 누르자." : "이어서 잼을 누르자."); }
            return;
        }

        if (action == MealAction.Fridge)
        {
            if (held) { Hint("먼저 내려놓자."); return; }
            if (!CookingStarted) PickUp(rawToast, false);
            else if (!JamOnCounter) PickUp(jam, false);
            else if (PlateOnTable && !JuicePoured) PickUp(juice, false);
            else Hint(PlateOnTable ? "컵을 테이블에 놓자." : "토스트를 준비해서 테이블에 놓자.");
        }
        else if (action == MealAction.Toaster)
        {
            if (held == rawToast && !CookingStarted)
            {
                ConsumeHeld();
                CookingStarted = true;
                StartCoroutine(Toast());
            }
            else if (!held && ToastReady && !ToastOnPlate) PickUp(cookedToast, true);
            else Hint(CookingStarted ? "토스트가 구워지고 있다." : "토스트를 먼저 가져오자.");
        }
        else if (action == MealAction.Counter && held == jam && !JamOnCounter)
        {
            PlaceHeld(jamPoint);
            JamOnCounter = true;
        }
        else if (action == MealAction.CookedToast && !ToastOnPlate && ToastReady && !held) PickUp(cookedToast, true);
        else if (action == MealAction.Plate && held == cookedToast && !ToastOnPlate)
        {
            PlaceHeld(toastOnPlatePoint);
            ToastOnPlate = true;
        }
        else if ((action == MealAction.Plate || action == MealAction.JamToast) && JamApplied && !PlateOnTable && !held)
            PickUp(plate, true);
        else if (action == MealAction.EmptyCup && held == juice && !JuicePoured)
        {
            ConsumeHeld();
            // Use the same bottom point even if the two placeholder meshes differ in height.
            Bounds emptyBounds = emptyCup.GetComponent<Renderer>().bounds;
            juiceCup.gameObject.SetActive(true);
            juiceCup.transform.rotation = emptyCup.transform.rotation;
            Bounds fullBounds = juiceCup.GetComponent<Renderer>().bounds;
            juiceCup.transform.position += new Vector3(emptyBounds.center.x - fullBounds.center.x,
                emptyBounds.min.y - fullBounds.min.y, emptyBounds.center.z - fullBounds.center.z);
            emptyCup.SetActive(false);
            JuicePoured = true;
            Play(actionAudio, pourSound);
        }
        else if (action == MealAction.JuiceCup && JuicePoured && !CupOnTable && !held) PickUp(juiceCup, true);
        else if (action == MealAction.Table)
        {
            if (held == plate && JamApplied && !PlateOnTable) { PlaceHeld(plateTablePoint); PlateOnTable = true; }
            else if (held == juiceCup && JuicePoured && !CupOnTable) { PlaceHeld(cupTablePoint); CupOnTable = true; }
            else { Hint("완성한 접시와 주스컵을 여기에 놓자."); return; }
            if (PlateOnTable && CupOnTable) StartCoroutine(FinishMeal());
        }
        else if (held && action != MealAction.None) Hint("여기에 둘 건 아니다.");
            }
        finally { RequestObjective(); }
    }

    void RequestObjective()
    {
        if (Completed || !TeamRoulette.Room3.GameManager.CanPlay(TeamRoulette.Room3.GameStage.Meal)) return;
        var held = Hand ? Hand.Held : null;
        TeamRoulette.Room3.ObjectiveStep goal;
        if (Ending || (PlateOnTable && CupOnTable)) goal = TeamRoulette.Room3.ObjectiveStep.EatMeal;
        else if (!CookingStarted) goal = held == rawToast ? TeamRoulette.Room3.ObjectiveStep.PutToastInToaster : TeamRoulette.Room3.ObjectiveStep.GetToast;
        else if (!JamOnCounter) goal = held == jam ? TeamRoulette.Room3.ObjectiveStep.PutJamOnCounter : TeamRoulette.Room3.ObjectiveStep.GetJam;
        else if (!ToastReady) goal = TeamRoulette.Room3.ObjectiveStep.WaitForToast;
        else if (!ToastOnPlate) goal = held == cookedToast ? TeamRoulette.Room3.ObjectiveStep.PutToastOnPlate : TeamRoulette.Room3.ObjectiveStep.TakeToast;
        else if (!JamApplied) goal = TeamRoulette.Room3.ObjectiveStep.ApplyJam;
        else if (!PlateOnTable) goal = TeamRoulette.Room3.ObjectiveStep.PlacePlate;
        else if (!JuicePoured) goal = held == juice ? TeamRoulette.Room3.ObjectiveStep.PourJuice : TeamRoulette.Room3.ObjectiveStep.GetJuice;
        else goal = TeamRoulette.Room3.ObjectiveStep.PlaceCup;
        TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Meal, goal);
    }

    bool PickUp(GrabbableItem item, bool visibleOnReturn)
    {
        if (!CanPickUp) { Hint("손과 잡기 기능이 필요하다."); return false; }
        returnParent = item.transform.parent;
        returnPosition = item.transform.position;
        returnRotation = item.transform.rotation;
        returnScale = item.transform.localScale;
        returnActive = visibleOnReturn;
        item.gameObject.SetActive(true);
        if (!Hand.Hold(item)) { item.gameObject.SetActive(visibleOnReturn); return false; }
        carried = item;
        return true;
    }

    public void ReturnHeld()
    {
        ClearSelection();
        if (!IsHoldingMealItem) return;
        var item = Hand.Held;
        Hand.ReturnHeld();
        if (item == carried)
        {
            item.transform.SetParent(returnParent, true);
            item.transform.SetPositionAndRotation(returnPosition, returnRotation);
            item.transform.localScale = returnScale;
            item.gameObject.SetActive(returnActive);
        }
        item.enabled = false;
        carried = null;
        RequestObjective();
    }

    void ConsumeHeld()
    {
        var item = Hand.Held;
        Hand.PlaceHeld(transform, true);
        item.transform.SetParent(transform, true);
        carried = null;
    }
    void PlaceHeld(Transform point) { Hand.PlaceHeld(point, false); carried = null; }

    IEnumerator Toast()
    {
        bakingToast.SetActive(true);
        timerPanel.SetActive(true);
        SecondsRemaining = toastSeconds;
        while (SecondsRemaining > 0)
        {
            timerText.text = Mathf.CeilToInt(SecondsRemaining).ToString("00");
            yield return null;
            SecondsRemaining = Mathf.Max(0, SecondsRemaining - Time.deltaTime);
        }
        timerText.text = "00";
        bakingToast.SetActive(false);
        cookedToast.gameObject.SetActive(true);
        cookedToast.transform.SetPositionAndRotation(cookedPopPoint.position, cookedPopPoint.rotation);
        ToastReady = true;
        RequestObjective();
        Play(toasterAudio, dingSound);
    }

    IEnumerator FinishMeal()
    {
        Ending = true;
        RequestObjective();
        ClearSelection();
        SaveControls();
        blackScreen.alpha = 1;
        blackScreen.blocksRaycasts = true;
        MovePlayerToTable();
        // All arrangement happens while the screen is black.
        plate.PlaceAt(plateTablePoint);
        juiceCup.PlaceAt(cupTablePoint);
        yield return new WaitForSeconds(blackoutSeconds);
        interactor.playerCamera.transform.SetPositionAndRotation(eatingView.position, eatingView.rotation);
        blackScreen.alpha = 0;
        if (actionAudio && eatSound)
        {
            actionAudio.clip = eatSound;
            actionAudio.loop = true;
            actionAudio.Play();
        }
        yield return new WaitForSeconds(eatingSeconds);
        if (actionAudio) { actionAudio.Stop(); actionAudio.loop = false; }
        jamToast.SetActive(false);
        juiceCup.gameObject.SetActive(false);
        emptyCup.SetActive(true);
        emptyCup.transform.rotation = cupTablePoint.rotation;
        Bounds b = emptyCup.GetComponent<Renderer>().bounds;
        emptyCup.transform.position += cupTablePoint.position - new Vector3(b.center.x, b.min.y, b.center.z);
        emptyCup.transform.SetParent(cupTablePoint, true);
        Completed = true;
        Ending = false;
        blackScreen.blocksRaycasts = false;
        timerPanel.SetActive(false);
        RestoreControls();
        TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Meal);
    }

    public void RestoreCompleted()
    {
        Completed = true;
        CookingStarted = ToastReady = JamOnCounter = ToastOnPlate = JamApplied = PlateOnTable = JuicePoured = CupOnTable = true;
        foreach (var item in new[] { rawToast, jam, juice, cookedToast, juiceCup }) item.gameObject.SetActive(false);
        plate.gameObject.SetActive(true);
        plate.transform.SetPositionAndRotation(plateTablePoint.position, plateTablePoint.rotation);
        jamToast.SetActive(false);
        bakingToast.SetActive(false);
        emptyCup.SetActive(true);
        emptyCup.transform.rotation = cupTablePoint.rotation;
        Bounds bounds = emptyCup.GetComponent<Renderer>().bounds;
        emptyCup.transform.position += cupTablePoint.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        timerPanel.SetActive(false);
        blackScreen.alpha = 0;
        blackScreen.blocksRaycasts = false;
    }

    void MovePlayerToTable()
    {
        if (!playerAfterMeal) return;
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body && body.enabled;
        if (body) body.enabled = false;
        player.transform.SetPositionAndRotation(playerAfterMeal.position, playerAfterMeal.rotation);
        if (body) body.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    void SaveControls()
    {
        controlsSaved = true;
        playerEnabled = player.enabled;
        player.enabled = false;
        feel = player.GetComponent<PlayerMotionFeel>();
        if (feel) { feelEnabled = feel.enabled; feel.enabled = false; }
        cameraPosition = interactor.playerCamera.transform.localPosition;
        cameraRotation = interactor.playerCamera.transform.localRotation;
    }
    void RestoreControls()
    {
        if (!controlsSaved) return;
        controlsSaved = false;
        if (player) player.enabled = playerEnabled;
        if (feel) feel.enabled = feelEnabled;
        if (interactor && interactor.playerCamera)
        {
            interactor.playerCamera.transform.localPosition = cameraPosition;
            interactor.playerCamera.transform.localRotation = cameraRotation;
        }
    }
    static void Play(AudioSource source, AudioClip clip) { if (source && clip) source.PlayOneShot(clip); }
    void OnDisable()
    {
        if (IsHoldingMealItem) ReturnHeld();
        RestoreControls();
        if (blackScreen) { blackScreen.alpha = 0; blackScreen.blocksRaycasts = false; }
    }
}
