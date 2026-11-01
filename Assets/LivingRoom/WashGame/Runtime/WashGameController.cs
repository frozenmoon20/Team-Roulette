using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

[DefaultExecutionOrder(400)]
public sealed class WashGameController : MonoBehaviour
{
    public enum Stage { Idle, WaterRunning, Wet, Soaping, RinseReady, Rinsing, TowelReady, Wiping, Complete }
    [Header("Scene references")]
    public FirstPersonPlayer player;
    public PlayerInteractor interactor;
    public Camera viewCamera;
    public Transform washView;
    public ParticleSystem water;
    public Canvas overlay;
    public RawImage screen;
    public TMP_Text instruction;
    public Image progressFill;
    public RectTransform toolCursor;
    public Image toolImage;
    public AudioSource actionAudio, runningAudio;
    public AudioClip waterSound, dingSound;
    public Material screenMaterial, blurMaterial;
    public Behaviour[] otherInputs;
    [Header("Adjustable gameplay")]
    [Range(.5f, .95f)] public float requiredCoverage = .9f;
    [Range(.5f, .99f)] public float soapRequiredCoverage = .9f;
    [Range(.03f, .2f)] public float brushRadius = .10f;
    [Range(1, 15)] public float blurRadius = 7;
    [Min(.1f)] public float rinseSeconds = 1.1f;

    public Stage CurrentStage { get; private set; }
    public bool IsActive => ownsControls;
    public WashMask Mask { get; private set; }
    public float Coverage => Mask == null ? 0 : CurrentStage == Stage.Wiping ? Mask.TowelCoverage : Mask.SoapCoverage;
    public bool BlurVisible => ownsControls && OptionState.Instance && OptionState.Instance.IsUnlocked(OptionType.Blur);
    public int SplashCount { get; private set; }
    bool savedMove, savedLook, savedCursorControl, savedBlur;
    bool paintingFrozen, paintingPlayerEnabled, paintingMotionEnabled;
    PlayerMotionFeel paintingMotion;
    Material material, blurRuntime;
    Camera presentationCamera;
    RenderTexture target, previousTarget, blurHorizontal, blurVertical;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float cameraAspect, cameraFov;
    CursorLockMode cursorLock;
    bool cursorVisible, ownsControls, dragging, lostFocus, paused;
    int enteredFrame;
    float rinseTime;
    Vector2 lastUV;
    readonly Dictionary<Behaviour, bool> blocked = new Dictionary<Behaviour, bool>();
    readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();

    void Awake()
    {
        // Keep the completion gauge at the top even when using the shared GameUI overlay.
        if (progressFill && progressFill.transform.parent is RectTransform track)
        {
            track.anchorMin = new Vector2(.3f, .9f);
            track.anchorMax = new Vector2(.7f, .915f);
            track.offsetMin = track.offsetMax = Vector2.zero;
        }
        overlay.gameObject.SetActive(false);
        if (water) water.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void Start()
    {
        if (OptionState.Instance) { OptionState.Instance.Unlock(OptionType.Move); OptionState.Instance.Unlock(OptionType.RotateCamera); }
    }
    void OnEnable() { OptionState.OnOptionChanged += OnOptionChanged; }
    void OnOptionChanged(OptionType type, bool value)
    { if (type == OptionType.Blur && ownsControls) UpdateVisuals(); }

    public void Click(WashAction action)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0 || CurrentStage == Stage.Complete) return;
        if (!TeamRoulette.Room3.GameManager.CanPlay(TeamRoulette.Room3.GameStage.Wash)) return;
        if (CurrentStage == Stage.Idle)
        {
            if (action != WashAction.Water) { PlayerInteractor.ShowHint("세면대에서 먼저 물을 틀자."); return; }
            if (!OptionState.Instance) { PlayerInteractor.ShowHint("옵션 시스템 연결을 확인해주세요."); return; }
            if (!TeamRoulette.Room3.GameManager.TryBegin(TeamRoulette.Room3.GameStage.Wash)) return;
            Begin();
            return;
        }
        if (action == WashAction.Water && CurrentStage == Stage.WaterRunning)
        { SplashCount++; actionAudio.PlayOneShot(waterSound); SetStage(Stage.Wet); }
        else if (action == WashAction.Soap && CurrentStage == Stage.Wet) SetStage(Stage.Soaping);
        else if (action == WashAction.Water && CurrentStage == Stage.RinseReady)
        { SplashCount++; actionAudio.PlayOneShot(waterSound); rinseTime = 0; SetStage(Stage.Rinsing); }
        else if (action == WashAction.Towel && CurrentStage == Stage.TowelReady) SetStage(Stage.Wiping);
    }

    void Block(Behaviour component)
    {
        if (!component || component == this || blocked.ContainsKey(component)) return;
        blocked.Add(component, component.enabled);
        component.enabled = false;
    }
    void Hide(GameObject item)
    {
        if (!item || hidden.ContainsKey(item)) return;
        hidden.Add(item, item.activeSelf); item.SetActive(false);
    }

    void Begin()
    {
        if (!viewCamera || !screenMaterial || !blurMaterial || !washView) { Debug.LogError("WashGame references missing.", this); return; }
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
        cameraPosition = viewCamera.transform.localPosition; cameraRotation = viewCamera.transform.localRotation;
        cameraAspect = viewCamera.aspect; cameraFov = viewCamera.fieldOfView; previousTarget = viewCamera.targetTexture;
        savedBlur = OptionState.Instance.IsUnlocked(OptionType.Blur);
        OptionState.Instance.Unlock(OptionType.Blur);
        SplashCount = 0;
        savedMove = player.canMove; savedLook = player.canLook; savedCursorControl = player.externalCursorControl;
        player.canMove = true; player.canLook = true; player.externalCursorControl = true;
        OptionState.Instance.Unlock(OptionType.RotateCamera);
        if (otherInputs != null) foreach (var input in otherInputs) Block(input);
        Block(interactor);
        if (interactor) { Hide(interactor.crosshairDot); Hide(interactor.handIcon); if (interactor.nameText) Hide(interactor.nameText.gameObject); }
        // The shared GameUI keeps the objective and dialogue visible above the wash effect.
        ownsControls = true;
        Mask = new WashMask();
        material = new Material(screenMaterial);
        blurRuntime = new Material(blurMaterial);
        material.SetTexture("_Mask", Mask.Texture);
        screen.material = material;
        EnsureTarget();
        // Keep a screen camera while the player camera renders into the live texture.
        // This also prevents Unity's "No cameras rendering" Game view overlay.
        var presentation = new GameObject("WashPresentationCamera", typeof(Camera));
        presentation.transform.SetParent(transform, false);
        presentationCamera = presentation.GetComponent<Camera>();
        presentationCamera.cullingMask = 0;
        presentationCamera.clearFlags = CameraClearFlags.SolidColor;
        presentationCamera.backgroundColor = Color.black;
        presentationCamera.depth = viewCamera.depth + 1;
        presentationCamera.targetDisplay = viewCamera.targetDisplay;
        presentationCamera.allowHDR = false;
        presentationCamera.allowMSAA = false;
        RenderPipelineManager.endCameraRendering += RenderBlur;
        overlay.gameObject.SetActive(true);
        water.Play();
        if (runningAudio) runningAudio.Stop();
        SetStage(Stage.WaterRunning);
    }

    void EnsureTarget()
    {
        int width = Mathf.Max(16, Screen.width), height = Mathf.Max(16, Screen.height);
        if (target && target.width == width && target.height == height) return;
        if (target) { viewCamera.targetTexture = previousTarget; target.Release(); Destroy(target); }
        FreeBlurTargets();
        target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "WashGame live camera", antiAliasing = 1 };
        target.Create();
        viewCamera.targetTexture = target;
        viewCamera.aspect = (float)width / height;
        screen.texture = target;
        blurHorizontal = new RenderTexture(Mathf.Max(4,width/4),Mathf.Max(4,height/4),0,RenderTextureFormat.ARGB32) { name="WashGame blur horizontal", filterMode=FilterMode.Bilinear };
        blurVertical = new RenderTexture(blurHorizontal.width,blurHorizontal.height,0,RenderTextureFormat.ARGB32) { name="WashGame blur vertical", filterMode=FilterMode.Bilinear };
        blurHorizontal.Create(); blurVertical.Create();
        material.SetTexture("_BlurredTex",blurVertical);
    }

    void RenderBlur(ScriptableRenderContext context, Camera camera)
    {
        if (!ownsControls || camera != viewCamera || !material || !target) return;
        // Two inexpensive continuous blur passes keep the live water moving without ghosted edges.
        Graphics.Blit(target,blurHorizontal,blurRuntime,0);
        Graphics.Blit(blurHorizontal,blurVertical,blurRuntime,1);
    }
    void FreeBlurTargets()
    {
        if(blurHorizontal){blurHorizontal.Release();Destroy(blurHorizontal);blurHorizontal=null;}
        if(blurVertical){blurVertical.Release();Destroy(blurVertical);blurVertical=null;}
    }

    void Update()
    {
        if (!ownsControls) return;
        // Settings can show its HUD again on close; keep WashGame's saved HUD hidden until exit.
        foreach (var entry in hidden) if (entry.Key && entry.Key.activeSelf) entry.Key.SetActive(false);
        bool isPaused = Time.timeScale <= 0;
        if (isPaused != paused)
        {
            paused = isPaused;
            if (runningAudio) { if (paused) runningAudio.Pause(); else runningAudio.UnPause(); }
            if (actionAudio) { if (paused) actionAudio.Pause(); else actionAudio.UnPause(); }
        }
        if (paused || lostFocus)
        {
            dragging = false; toolCursor.gameObject.SetActive(false);
            if (!paused && CurrentStage == Stage.Rinsing)
            { rinseTime += Time.deltaTime; if(rinseTime>=rinseSeconds)SetStage(Stage.TowelReady); }
            UpdateVisuals();
            return;
        }
        EnsureTarget();
        if (CurrentStage == Stage.Complete) return;
        ApplyCursorMode();
        if (CurrentStage == Stage.Rinsing)
        {
            rinseTime += Time.deltaTime;
            if (rinseTime >= rinseSeconds) SetStage(Stage.TowelReady);
        }
        bool painting = CurrentStage == Stage.Soaping || CurrentStage == Stage.Wiping;
        toolCursor.gameObject.SetActive(true);
        toolCursor.sizeDelta = painting ? new Vector2(90, 64) : new Vector2(6, 6);
        toolCursor.position = painting ? Input.mousePosition : new Vector3(Screen.width * .5f, Screen.height * .5f, 0);
        Vector2 uv = painting ? new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height) : new Vector2(.5f, .5f);
        bool inside = uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;
        if (Time.frameCount > enteredFrame && painting && inside && Input.GetMouseButton(0))
        {
            Paint(dragging ? lastUV : uv, uv);
            lastUV = uv; dragging = true;
        }
        else dragging = false;
        if (!painting && Time.frameCount > enteredFrame && Input.GetMouseButtonDown(0) && inside)
        {
            var found = TargetAt(uv);
            if (found) Click(found.action);
        }
        UpdateVisuals();
    }

    public WashTarget TargetAt(Vector2 uv)
    {
        if (!viewCamera) return null;
        if (!Physics.Raycast(viewCamera.ViewportPointToRay(uv), out var hit, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return null;
        var found = hit.collider.GetComponentInParent<WashTarget>();
        return found && found.game == this ? found : null;
    }

    public void Paint(Vector2 from, Vector2 to)
    {
        if (!ownsControls || Time.timeScale <= 0 || (CurrentStage != Stage.Soaping && CurrentStage != Stage.Wiping)) return;
        bool towel = CurrentStage == Stage.Wiping;
        Mask.Stroke(from, to, brushRadius, viewCamera.aspect, towel);
        if ((towel ? Mask.TowelCoverage : Mask.SoapCoverage) >= (towel ? requiredCoverage : soapRequiredCoverage))
        {
            actionAudio.PlayOneShot(dingSound);
            if (towel)
            {
                SetStage(Stage.Complete);
                OptionState.Instance.Lock(OptionType.Blur);
                UpdateVisuals();
                StartCoroutine(Finish());
            }
            else SetStage(Stage.RinseReady);
        }
    }

    void SetStage(Stage stage)
    {
        CurrentStage = stage; enteredFrame = Time.frameCount; dragging = false;
        var goal = stage switch
        {
            Stage.WaterRunning => TeamRoulette.Room3.ObjectiveStep.WetFace,
            Stage.Wet => TeamRoulette.Room3.ObjectiveStep.SelectSoap,
            Stage.Soaping => TeamRoulette.Room3.ObjectiveStep.ApplySoap,
            Stage.RinseReady => TeamRoulette.Room3.ObjectiveStep.RinseFace,
            Stage.Rinsing => TeamRoulette.Room3.ObjectiveStep.Rinsing,
            Stage.TowelReady => TeamRoulette.Room3.ObjectiveStep.SelectTowel,
            Stage.Wiping => TeamRoulette.Room3.ObjectiveStep.WipeFace,
            Stage.Complete => TeamRoulette.Room3.ObjectiveStep.WashComplete,
            _ => TeamRoulette.Room3.ObjectiveStep.Default
        };
        TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Wash, goal);
        if (stage == Stage.TowelReady)
        {
            if (water) water.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (runningAudio) runningAudio.Stop();
        }
        SetPaintingFreeze(stage == Stage.Soaping || stage == Stage.Wiping);
        ApplyCursorMode();
        UpdateVisuals();
    }

    void ApplyCursorMode()
    {
        if (!ownsControls || Time.timeScale <= 0 || lostFocus) return;
        bool painting = CurrentStage == Stage.Soaping || CurrentStage == Stage.Wiping;
        Cursor.lockState = painting ? CursorLockMode.Confined : CursorLockMode.Locked;
        Cursor.visible = false; // The overlay draws either the tool or the central crosshair.
    }

    void SetPaintingFreeze(bool freeze)
    {
        if (freeze == paintingFrozen) return;
        paintingFrozen = freeze;
        if (freeze)
        {
            paintingPlayerEnabled = player && player.enabled;
            paintingMotion = player ? player.GetComponent<PlayerMotionFeel>() : null;
            paintingMotionEnabled = paintingMotion && paintingMotion.enabled;
            if (player) player.enabled = false;
            if (paintingMotion) paintingMotion.enabled = false;
        }
        else
        {
            if (player) player.enabled = paintingPlayerEnabled;
            if (paintingMotion) paintingMotion.enabled = paintingMotionEnabled;
        }
    }

    void UpdateVisuals()
    {
        if (!material) return;
        bool finished = CurrentStage == Stage.Complete;
        bool wiping = CurrentStage == Stage.Wiping;
        float foam = CurrentStage == Stage.Soaping || CurrentStage == Stage.RinseReady ? 1 :
            CurrentStage == Stage.Rinsing ? 1 - Mathf.Clamp01(rinseTime / rinseSeconds) : 0;
        material.SetFloat("_Blur", BlurVisible ? 1 : 0);
        material.SetFloat("_FullFoam", CurrentStage == Stage.RinseReady || CurrentStage == Stage.Rinsing ? 1 : 0);
        material.SetFloat("_Radius", blurRadius);
        blurRuntime.SetFloat("_Radius", blurRadius);
        material.SetFloat("_Foam", foam);
        material.SetFloat("_Wet", CurrentStage >= Stage.Wet && !finished ? 1 : 0);
        material.SetFloat("_Wiping", wiping ? 1 : 0);
        material.SetFloat("_Aspect", viewCamera.aspect);
        toolImage.color = wiping ? new Color(.76f, .88f, 1, .85f) : CurrentStage == Stage.Soaping ? new Color(.79f, 1, .88f, .9f) : Color.white;
        float coverage = wiping ? Mask.TowelCoverage : Mask.SoapCoverage;
        progressFill.transform.parent.gameObject.SetActive(CurrentStage == Stage.Soaping || wiping);
        float progress = Mathf.Clamp01(coverage / (wiping ? requiredCoverage : soapRequiredCoverage));
        progressFill.fillAmount = progress;
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(progress, 1);
        progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
        var sharedUI = FindFirstObjectByType<TeamRoulette.UI.GameUI>();
        if (!sharedUI || !TeamRoulette.Room3.GameManager.Instance || instruction != sharedUI.objectiveText)
        {
        switch (CurrentStage)
        {
            case Stage.WaterRunning: instruction.text = "물 · 수도꼭지 · 세면대를 눌러 얼굴을 적시세요"; break;
            case Stage.Wet: instruction.text = "비누를 선택하세요"; break;
            case Stage.Soaping: instruction.text = "마우스를 누른 채 비누칠하세요"; break;
            case Stage.RinseReady: instruction.text = "띵! 물 · 수도꼭지 · 세면대를 눌러 헹구세요"; break;
            case Stage.Rinsing: instruction.text = "거품을 헹구는 중…"; break;
            case Stage.TowelReady: instruction.text = "수건을 선택하세요"; break;
            case Stage.Wiping: instruction.text = "마우스를 누른 채 수건으로 닦으세요"; break;
            case Stage.Complete: instruction.text = "세안 완료! 시야가 맑아졌어요"; break;
        }
        }
        // Step guidance uses the shared objective area; game instructions appear only on entry.
    }

    IEnumerator Finish()
    {
        yield return new WaitForSeconds(.65f);
        while (Input.GetMouseButton(0) || Time.timeScale <= 0) yield return null;
        Release();
        TeamRoulette.Room3.GameManager.ReportComplete(TeamRoulette.Room3.GameStage.Wash);
    }

    public void RestoreCompleted()
    {
        CurrentStage = Stage.Complete;
        overlay.gameObject.SetActive(false);
        if (OptionState.Instance) OptionState.Instance.Lock(OptionType.Blur);
    }

    public void Cancel()
    {
        if (!ownsControls || Time.timeScale <= 0) return;
        StopAllCoroutines();
        if (CurrentStage != Stage.Complete && OptionState.Instance)
        { if(savedBlur) OptionState.Instance.Unlock(OptionType.Blur); else OptionState.Instance.Lock(OptionType.Blur); }
        // An interrupted wash restores the Blur toggle that was present before entering.
        if (CurrentStage != Stage.Complete) CurrentStage = Stage.Idle;
        Release();
        TeamRoulette.Room3.ObjectiveManager.Request(TeamRoulette.Room3.GameStage.Wash, TeamRoulette.Room3.ObjectiveStep.Default);
    }

    void Release()
    {
        if (!ownsControls) return;
        ownsControls = false; dragging = false; paused = false;
        SetPaintingFreeze(false);
        if(player){player.canMove=savedMove;player.canLook=savedLook;player.externalCursorControl=savedCursorControl;}
        RenderPipelineManager.endCameraRendering -= RenderBlur;
        if(presentationCamera) { presentationCamera.enabled=false;Destroy(presentationCamera.gameObject);presentationCamera=null; }
        if (water) water.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (runningAudio) runningAudio.Stop();
        if (overlay) overlay.gameObject.SetActive(false);
        if (viewCamera)
        {
            viewCamera.targetTexture = previousTarget;
            viewCamera.aspect = cameraAspect;
            viewCamera.fieldOfView = cameraFov;
            viewCamera.transform.localPosition = cameraPosition;
            viewCamera.transform.localRotation = cameraRotation;
        }
        if (screen) { screen.texture = null; screen.material = null; }
        if (target) { target.Release(); Destroy(target); target = null; }
        FreeBlurTargets();
        if (material) { Destroy(material); material = null; }
        if (blurRuntime) { Destroy(blurRuntime); blurRuntime = null; }
        Mask?.Dispose(); Mask = null;
        foreach (var entry in blocked) if (entry.Key) entry.Key.enabled = entry.Value;
        blocked.Clear();
        foreach (var entry in hidden) if (entry.Key) entry.Key.SetActive(entry.Value);
        hidden.Clear();
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
    }
    void OnApplicationFocus(bool focus) { lostFocus = !focus; dragging = false; }
    void OnDisable() { OptionState.OnOptionChanged -= OnOptionChanged; StopAllCoroutines(); if (CurrentStage != Stage.Complete) CurrentStage = Stage.Idle; Release(); }
}
