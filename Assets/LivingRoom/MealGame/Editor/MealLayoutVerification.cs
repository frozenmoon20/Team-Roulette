using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MealLayoutVerification
{
    const string Report="Assets/LivingRoom/MealGame/Reports/LayoutVerification_20261119.txt";
    static IEnumerator checks;
    static double next;
    static string log;
    static MealLayoutVerification(){EditorApplication.playModeStateChanged+=OnPlay;}
    [InitializeOnLoadMethod]
    static void ResumeVerification()
    {
        EditorApplication.delayCall+=()=>{
            const string folder="Assets/LivingRoom/MealGame/";
            if(File.Exists(folder+"Layout.pending")||!File.Exists(folder+"LayoutVerify.pending"))return;
            File.Delete(folder+"LayoutVerify.pending");Run();
        };
    }
    [MenuItem("Tools/MealGame/Verify counter layout and transparent timer")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool("MealLayout.Verify",true);EditorApplication.EnterPlaymode();
    }
    static void OnPlay(PlayModeStateChange s)
    {
        if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("MealLayout.Verify",false))return;
        SessionState.SetBool("MealLayout.Verify",false);log="MealGame counter layout and timer verification\n";
        checks=Verify();next=EditorApplication.timeSinceStartup+1;EditorApplication.update+=Pump;
    }
    static void Pump()
    {
        if(!EditorApplication.isPlaying){EditorApplication.update-=Pump;return;}
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(checks.MoveNext()){next=EditorApplication.timeSinceStartup+Convert.ToDouble(checks.Current);return;}
            Finish("ALL CHECKS PASSED\n");
        }
        catch(Exception e){Debug.LogException(e);Finish("FAILED: "+e+"\n");}
    }
    static void Finish(string result)
    {
        File.WriteAllText(Report,log+result);EditorApplication.update-=Pump;EditorApplication.ExitPlaymode();
    }
    static void Check(bool value,string text){if(!value)throw new Exception(text);log+="PASS: "+text+"\n";}
    static IEnumerator Verify()
    {
        var g=Object.FindFirstObjectByType<MealGameController>();
        var pie=g.timerPanel.GetComponentInChildren<MealPieTimer>(true);
        Check(g&&pie&&pie.game==g,"All timer and MealGame references are assigned");
        foreach(string group in new[]{"Furniture","Items","PlacementPoints","UI"})Check(g.transform.Find(group),"Hierarchy group: "+group);
        Check(g.jamToast.transform.IsChildOf(g.plate.transform)&&g.toastOnPlatePoint.IsChildOf(g.plate.transform),"Plate assembly references remain intact");
        var plate=MealGeometry.BoundsOf(g.plate.transform.Find("PlateBigBreakfast").gameObject);
        var cup=MealGeometry.BoundsOf(g.emptyCup);
        var counter=g.GetComponentsInChildren<MealTarget>(true).First(t=>t.enabled&&t.action==MealAction.Counter);
        var top=MealGeometry.BoundsOf(counter.gameObject);
        Check(Mathf.Abs(plate.min.y-top.max.y)<.01f&&Mathf.Abs(cup.min.y-top.max.y)<.01f,"Plate and cup rest on countertop");
        Vector3 across=Vector3.ProjectOnPlane(cup.center-plate.center,Vector3.up).normalized;
        Check(Vector3.Dot(g.jamPoint.position-cup.center,across)>.1f,"Jam placement is beyond the cup, matching the red spot");
        Check(g.plate.transform.parent.name=="Items"&&g.emptyCup.transform.parent.name=="Items","Dish hierarchy is grouped without losing references");
        var rt=g.timerPanel.GetComponent<RectTransform>();
        float diameter=rt.rect.width*rt.lossyScale.x;
        Check(diameter>=.20f&&diameter<=.30f,"Timer diameter is reduced to 0.25 world units");
        Check(g.timerPanel.GetComponentsInChildren<Graphic>(true).All(x=>x==pie||!x.enabled),"Old rectangular background and digits are disabled");
        var populate=typeof(MealPieTimer).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null);
        using(var mesh=new VertexHelper())
        {
            pie.Preview(1);populate.Invoke(pie,new object[]{mesh});int full=mesh.currentVertCount;
            Check(full>100,"Full countdown draws a circular sector");
            pie.Preview(.5f);populate.Invoke(pie,new object[]{mesh});
            Check(mesh.currentVertCount<full,"The drawn area shrinks with remaining time");
            var stream=new List<UIVertex>();mesh.GetUIVertexStream(stream);
            Check(stream.All(v=>v.position.x<=.001f),"The elapsed half has no geometry and is transparent");
            pie.Preview(0);populate.Invoke(pie,new object[]{mesh});Check(mesh.currentVertCount==0,"Finished timer is completely transparent");
        }
        pie.Preview(1);
        typeof(TeamRoulette.Room3.GameManager).GetProperty("Instance").SetValue(null,null);
        g.player.enabled=false;
        var feel=g.player.GetComponent<PlayerMotionFeel>();if(feel)feel.enabled=false;
        g.GetComponent<MealInput>().enabled=false;g.interactor.enabled=false;
        OptionState.Instance.Unlock(OptionType.Hand);OptionState.Instance.Unlock(OptionType.Grab);Time.timeScale=1;
        yield return .1;
        g.Click(MealAction.Fridge);Check(PlayerHand.Instance.Held==g.rawToast,"Existing hand pickup is still callable");
        g.Click(MealAction.Toaster);yield return .1;
        Check(g.CookingStarted&&g.timerPanel.activeInHierarchy&&pie.Remaining>.95f,"Ten-second countdown starts full");
        Canvas.ForceUpdateCanvases();
        var liveMesh=pie.canvasRenderer.GetMesh();
        Check(liveMesh && liveMesh.vertexCount>100&&liveMesh.bounds.size.x>80,"CanvasRenderer receives a visible, full-sized timer mesh");

        g.Click(MealAction.Fridge);Check(PlayerHand.Instance.Held==g.jam,"Fridge still gives jam");
        g.Click(MealAction.Counter);yield return .1;
        var jam=MealGeometry.BoundsOf(g.jam.gameObject);
        Check(g.JamOnCounter&&Vector3.Distance(new Vector3(jam.center.x,jam.min.y,jam.center.z),g.jamPoint.position)<.005f,"Placing jam uses the new red-spot marker");
        Check(!PlayerHand.Instance.Held,"Placing jam frees the existing hand");
        var cam=g.interactor.playerCamera.transform;
        Vector3 center=(plate.center+g.jamPoint.position)*.5f;
        cam.position=center+Vector3.Cross(Vector3.up,across)*.95f+Vector3.up*.35f;
        cam.LookAt(center);
        yield return .25;
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(),"MealGame_CounterPreview.png"));
        yield return .5;
        yield return 4.5;
        Check(pie.Remaining>.25f&&pie.Remaining<.65f,"Pie countdown decreases during real gameplay");
        int waits=0;while(!g.ToastReady&&waits++<100)yield return .1;
        yield return .15;
        Check(g.ToastReady&&pie.Remaining==0,"The timer reaches transparent zero when toast is ready");
        Check(g.cookedToast.gameObject.activeSelf&&!g.bakingToast.activeSelf,"Toast completion behavior remains intact");
        // Render a stable 70% preview for visual QA, then restore the live binding.
        pie.game=null;pie.Preview(.7f);
        cam.position=pie.transform.position-pie.transform.forward*.65f;
        cam.LookAt(pie.transform.position,pie.transform.up);
        yield return .25;
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(),"MealGame_TimerPreview.png"));
        yield return .5;
        pie.game=g;pie.Preview(0);
    }
}
