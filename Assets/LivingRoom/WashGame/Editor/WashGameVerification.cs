using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WashGameVerification
{
    const string Request = WashGameSetup.Folder + "/Editor/Action.request";
    const string Report = WashGameSetup.Folder + "/Reports/Verification.txt";
    static IEnumerator checks;
    static double next;
    static string report;
    static WashGameVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        if(string.IsNullOrWhiteSpace(File.ReadAllText(Request))) return;
        string stamp=File.GetLastWriteTimeUtc(Request).Ticks.ToString();
        if(SessionState.GetString("WashGame.RequestStamp","")!=stamp)
        {SessionState.SetString("WashGame.RequestStamp",stamp);AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);return;}
        string action=File.ReadAllText(Request).Trim();
        File.WriteAllText(Request,"");
        try
        {
            if(action=="setup") { WashGameSetup.Build(); Run(); }
            else if(action=="verify") Run();
            else if(action=="preview") Preview();
            else if(action=="connect") {WashGameSetup.AdoptExistingObjects();Run();}
            else if(action=="stop") EditorApplication.ExitPlaymode();
        }
        catch(Exception ex) { Directory.CreateDirectory(WashGameSetup.Folder+"/Reports"); File.WriteAllText(Report,"FAILED: "+ex); Debug.LogException(ex); }
    }
    [MenuItem("Tools/WashGame/2 - Verify full sequence")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
        ShowGameView();
        SessionState.SetString("WashGame.Pending","verify");EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/WashGame/3 - Play from sink")]
    public static void Preview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
        ShowGameView();
        SessionState.SetString("WashGame.Pending","preview");EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode) return;
        string pending=SessionState.GetString("WashGame.Pending","");SessionState.SetString("WashGame.Pending","");
        if(pending=="") return;
        report="WashGame Play Mode verification\n";
        checks=pending=="verify"?Verify():PreviewRoutine();
        next=EditorApplication.timeSinceStartup+.8;
        EditorApplication.update+=Pump;
    }
    static void ShowGameView()
    {
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var window=EditorWindow.GetWindow(type);window.Show();window.Focus();
    }
    static void Pump()
    {
        if(!EditorApplication.isPlaying) { EditorApplication.update-=Pump;return; }
        if(EditorApplication.timeSinceStartup<next) return;
        try
        {
            if(!checks.MoveNext()) { EditorApplication.update-=Pump;return; }
            next=EditorApplication.timeSinceStartup+Convert.ToDouble(checks.Current);
        }
        catch(Exception ex)
        {
            File.WriteAllText(Report,report+"FAILED: "+ex+"\n");Debug.LogException(ex);EditorApplication.update-=Pump;
            EditorApplication.ExitPlaymode();
        }
    }
    static void Check(bool condition,string label)
    {if(!condition)throw new Exception(label);report+="PASS: "+label+"\n";File.WriteAllText(Report,report);}
    static void Capture(string name)
    {ScreenCapture.CaptureScreenshot(WashGameSetup.Folder+"/Reports/"+name+".png");}
    static void Sweep(WashGameController g)
    {
        for(float y=0;y<=1.06f && (g.CurrentStage==WashGameController.Stage.Soaping || g.CurrentStage==WashGameController.Stage.Wiping);y+=.11f)
            g.Paint(new Vector2(0,y),new Vector2(1,y));
    }
    static IEnumerator PreviewRoutine()
    {
        Application.runInBackground=true;
        var g=Object.FindFirstObjectByType<WashGameController>();
        OptionState.Instance.Lock(OptionType.Blur);
        OptionState.Instance.Unlock(OptionType.Move);OptionState.Instance.Unlock(OptionType.RotateCamera);
        // Runtime positioning only; stop Play Mode restores the existing scene placement.
        var cc=g.player.GetComponent<CharacterController>();cc.enabled=false;
        g.player.transform.position=new Vector3(33,.5f,4.5f);
        g.player.transform.rotation=Quaternion.identity;cc.enabled=true;
        g.viewCamera.transform.LookAt(g.washView.position+g.washView.forward*2);
        yield return .3;
        g.Click(WashAction.Water);
        yield return .5;
    }
    static IEnumerator Verify()
    {
        Application.runInBackground=true;
        var g=Object.FindFirstObjectByType<WashGameController>();
        Check(g!=null,"WashGame exists in scene");
        Check(Object.FindObjectsByType<OptionState>(FindObjectsSortMode.None).Length==1 && Object.FindFirstObjectByType<OptionState>()==OptionState.Instance,"Exactly one active OptionState; Inspector target and runtime singleton match");
        var optionEditor=UnityEditor.Editor.CreateEditor(OptionState.Instance);
        Check(optionEditor.RequiresConstantRepaint(),"OptionState Inspector refreshes runtime dictionary automatically");
        Object.DestroyImmediate(optionEditor);
        Check(!ShaderUtil.ShaderHasError(g.screenMaterial.shader),"WashScreen shader compiled");
        Check(g.screenMaterial.passCount==1 && g.blurMaterial && !ShaderUtil.ShaderHasError(g.blurMaterial.shader),"Composite uses one UI pass; blur passes are isolated");
        Check(g.water && g.waterSound && g.dingSound,"Replaceable particle and audio references assigned");
        OptionState.Instance.Lock(OptionType.Blur);
        bool playerEnabled=g.player.enabled,interactorEnabled=g.interactor.enabled;
        var cameraPosition=g.viewCamera.transform.localPosition;var originalTarget=g.viewCamera.targetTexture;
        g.Click(WashAction.Soap);Check(g.CurrentStage==WashGameController.Stage.Idle,"Soap cannot skip water start");
        g.Click(WashAction.Water);yield return .5;
        Check(g.CurrentStage==WashGameController.Stage.WaterRunning && g.BlurVisible && g.water.isPlaying,"First click starts live blur and water particles");
        Check(g.player.enabled && g.player.canMove && !g.interactor.enabled && Cursor.lockState==CursorLockMode.Locked && !Cursor.visible,"Object selection locks the cursor for first-person aiming");
        Check(g.toolCursor.gameObject.activeSelf && g.toolCursor.sizeDelta==new Vector2(6,6),"Central crosshair remains visible during object selection");
        Check(!g.instruction.text.Contains("우클릭"),"Instructions no longer offer right-click exit");
        Capture("01-water-running");yield return .5;
        Check(OptionState.Instance.IsUnlocked(OptionType.Blur),"Blur checkbox is on during washing");
        Check(g.SplashCount==0 && !g.runningAudio.isPlaying,"Water particles start silently");
        OptionState.Instance.Lock(OptionType.Blur);yield return .2;
        Check(g.screen.material.GetFloat("_Blur")==0,"Unchecking OptionState Blur turns the shader off");
        OptionState.Instance.Unlock(OptionType.Blur);yield return .2;
        Check(g.screen.material.GetFloat("_Blur")==1,"Checking OptionState Blur turns the shader on");
        Check(g.player.externalCursorControl && g.player.canMove && OptionState.Instance.IsUnlocked(OptionType.Move),"Movement remains enabled while mouse paints");
        Check(g.player.enabled && g.player.canLook && OptionState.Instance.IsUnlocked(OptionType.RotateCamera),"RotateCamera stays checked and player look remains enabled during washing");
        Check(!g.transform.Find("SoapLabel") && !g.transform.Find("TowelLabel") && !g.transform.Find("WaterLabel"),"World labels removed");        Check(g.toolCursor.gameObject.activeSelf && g.toolCursor.sizeDelta==new Vector2(6,6),"Central crosshair remains visible during object selection");
        Check(!g.instruction.text.Contains("우클릭"),"Instructions no longer offer right-click exit");
        Capture("01-water-running");yield return .5;
        g.Click(WashAction.Towel);Check(g.CurrentStage==WashGameController.Stage.WaterRunning,"Out-of-order towel is ignored");
        g.Click(WashAction.Water);Check(g.CurrentStage==WashGameController.Stage.Wet,"Second water click wets face");
        g.Click(WashAction.Soap);
        Vector3 frozenPosition=g.viewCamera.transform.position;
        Quaternion frozenRotation=g.viewCamera.transform.rotation;
        yield return .3;
        Check(!g.player.enabled && g.viewCamera.transform.position==frozenPosition && g.viewCamera.transform.rotation==frozenRotation && OptionState.Instance.IsUnlocked(OptionType.RotateCamera),"Soap painting freezes camera without changing RotateCamera option");
        Check(Cursor.lockState==CursorLockMode.Confined && !Cursor.visible,"Painting confines the tool cursor to the game window");
        Check(g.progressFill.rectTransform.anchorMax.x==0 && !g.instruction.text.Replace("<size=65%>","").Contains("%"),"Gauge starts at left edge with no percentage text");
        g.Paint(new Vector2(.2f,.2f),new Vector2(.4f,.2f));float once=g.Mask.SoapCoverage;
        g.Paint(new Vector2(.2f,.2f),new Vector2(.4f,.2f));
        Check(once==g.Mask.SoapCoverage && once>0,"Repeated brush stroke never double-counts coverage");
        Time.timeScale=0;g.Paint(Vector2.zero,Vector2.one);g.Click(WashAction.Water);
        Check(g.Mask.SoapCoverage==once && g.CurrentStage==WashGameController.Stage.Soaping,"Pause blocks painting and stage changes");
        Time.timeScale=1;
        var ui=Object.FindFirstObjectByType<TeamRoulette.UI.GameUI>();
        if(ui)
        {
            ui.OpenSettings();yield return .2;
            Check(ui.IsPaused && Time.timeScale==0,"Settings menu pauses WashGame");
            ui.CloseSettings();yield return .2;
            Check(!ui.IsPaused && !ui.hud.activeSelf && !g.player.enabled && !g.interactor.enabled && g.Mask.SoapCoverage==once,"Closing settings preserves painting freeze, coverage and HUD");
        }
        g.Cancel();
        Check(!g.IsActive && !OptionState.Instance.IsUnlocked(OptionType.Blur),"Cancel does not unlock Blur");
        Check(g.player.enabled==playerEnabled && g.interactor.enabled==interactorEnabled && g.viewCamera.targetTexture==originalTarget && g.viewCamera.transform.localPosition==cameraPosition,"Cancel restores camera and input state");
        g.Click(WashAction.Water);g.Click(WashAction.Water);g.Click(WashAction.Soap);
        Sweep(g);yield return .3;
        Check(g.CurrentStage==WashGameController.Stage.RinseReady && g.Mask.SoapCoverage>=g.soapRequiredCoverage && OptionState.Instance.IsUnlocked(OptionType.Blur),"90% soap coverage fills the screen and keeps Blur checked");
        Check(g.screen.material.GetFloat("_FullFoam")==1,"Completed soap covers the entire screen");
        Check(g.player.enabled==playerEnabled && Cursor.lockState==CursorLockMode.Locked,"Soap completion restores movement and locked camera aiming");
        Capture("02-foam-complete");yield return .5;
        g.Click(WashAction.Water);Check(g.CurrentStage==WashGameController.Stage.Rinsing,"Water starts timed rinse");
        yield return g.rinseSeconds+.3;
        Check(g.CurrentStage==WashGameController.Stage.TowelReady,"Rinse removes foam and enables towel");
        Check(!g.water.isPlaying && g.water.particleCount==0,"Water is off before selecting the towel");
        Check(g.SplashCount==2 && !g.runningAudio.isPlaying,"Exactly two water splash sounds, no loop");
        g.Click(WashAction.Towel);
        Check(!g.player.enabled && OptionState.Instance.IsUnlocked(OptionType.RotateCamera),"Towel painting freezes camera without changing RotateCamera option");
        g.Paint(new Vector2(.1f,.3f),new Vector2(.7f,.6f));yield return .4;
        Check(g.CurrentStage==WashGameController.Stage.Wiping && g.Mask.TowelCoverage>0 && g.Mask.TowelCoverage<g.requiredCoverage && g.BlurVisible,"Partial wipe keeps uncleared blur while recording towel area");
        Check(g.screen.material.GetFloat("_Wiping")==1 && g.screen.material.GetTexture("_Mask")==g.Mask.Texture,"Droplets and blur share the towel mask");
        Capture("03-partial-wipe");yield return .5;
        Sweep(g);
        Check(g.CurrentStage==WashGameController.Stage.Complete && !OptionState.Instance.IsUnlocked(OptionType.Blur) && !g.BlurVisible,"90% towel coverage unchecks Blur and clears all remaining effects");
        Check(g.screen.material.GetFloat("_Wet")==0 && g.screen.material.GetFloat("_Blur")==0,"Completion immediately removes both effects");
        yield return 1;
        Check(!g.IsActive && g.viewCamera.targetTexture==originalTarget && g.player.enabled==playerEnabled && g.interactor.enabled==interactorEnabled,"Completion restores controls and releases render target");
        g.Click(WashAction.Water);Check(!g.IsActive,"Completed game cannot restart or relock Blur");
        File.WriteAllText(Report,report+"ALL CHECKS PASSED\n");
        EditorApplication.ExitPlaymode();
    }
}
