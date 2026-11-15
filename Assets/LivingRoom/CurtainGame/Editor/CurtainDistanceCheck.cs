using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
[InitializeOnLoad] public static class CurtainDistanceCheck {
 const string R="Assets/LivingRoom/CurtainGame/Editor/Distance.request";const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static CurtainDistanceCheck(){EditorApplication.delayCall+=Begin;EditorApplication.playModeStateChanged+=OnPlay;}
 static void Begin(){if(!File.Exists(R)||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Move(R,R+".started-"+DateTime.Now.Ticks);SessionState.SetBool("CurtainDistanceCheck",true);EditorApplication.EnterPlaymode();}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CurtainDistanceCheck",false)){SessionState.SetBool("CurtainDistanceCheck",false);EditorApplication.delayCall+=Verify;}}
 static void Verify(){string report="";Action<bool,string> check=(ok,n)=>{if(!ok)throw new Exception(n);report+="PASS: "+n+"\n";};try{
 var g=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();var t=g.GetType();var p=g.player;var cc=p.GetComponent<CharacterController>();p.enabled=false;
 t.GetProperty("Started").GetSetMethod(true).Invoke(g,new object[]{true});
 var dir=(Vector3)t.GetField("guideDirection",F).GetValue(g);var start=g.startTrigger.bounds.center;
 var e=g.endTrigger.bounds.extents;float length=Vector3.Dot(g.endTrigger.bounds.center-start,dir)-Mathf.Abs(dir.x)*e.x-Mathf.Abs(dir.z)*e.z-cc.radius*g.minimumScale;
 Action<float> place=f=>{cc.enabled=false;p.transform.position=start+dir*length*f;cc.enabled=true;Physics.SyncTransforms();t.GetMethod("Update",F).Invoke(g,null);};
 place(0);check(Mathf.Abs(g.CurrentScale-1)<.002f,"Full size at route start");
 place(.5f);check(Mathf.Abs(g.CurrentScale-.6f)<.002f,"Halfway to curtain gives size 0.6");
 float held=g.CurrentScale;for(int i=0;i<120;i++)t.GetMethod("Update",F).Invoke(g,null);check(Mathf.Abs(g.CurrentScale-held)<.0001f,"Stationary player does not shrink over time");
 place(.9f);check(g.CurrentScale>.2f&&g.CurrentScale<.3f,"Minimum is not reached early");
 place(.25f);check(Mathf.Abs(g.CurrentScale-.8f)<.002f,"Retreat restores size based on route position");
 place(1);check(Mathf.Abs(g.CurrentScale-.2f)<.002f,"Minimum 0.2 at curtain contact");
 report+="ALL CHECKS PASSED\n";
 }catch(Exception e){report+="FAILED: "+e;Debug.LogException(e);}finally{File.WriteAllText("Assets/LivingRoom/CurtainGame/DistanceVerification.txt",report);EditorApplication.ExitPlaymode();}}
}
