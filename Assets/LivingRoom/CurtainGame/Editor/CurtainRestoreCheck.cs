using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad] public static class CurtainRestoreCheck {
 const string Request="Assets/LivingRoom/CurtainGame/Editor/RestoreCheck.request";
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static CurtainRestoreCheck(){EditorApplication.delayCall+=Begin;EditorApplication.playModeStateChanged+=OnPlay;}
 static void Begin(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Move(Request,Request+".started-"+DateTime.Now.Ticks);SessionState.SetBool("CurtainRestoreCheck",true);EditorApplication.EnterPlaymode();}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CurtainRestoreCheck",false)){SessionState.SetBool("CurtainRestoreCheck",false);EditorApplication.delayCall+=Verify;}}
 static void Verify(){string report="Curtain regression checks\n";Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);report+="PASS: "+label+"\n";};try{
 var g=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();var t=typeof(CurtainGameController);var p=g.player;var cc=p.GetComponent<CharacterController>();p.enabled=false;
 var direction=(Vector3)t.GetField("guideDirection",Flags).GetValue(g);var origin=g.startTrigger.bounds.center;
 g.endTrigger.enabled=false;
 t.GetProperty("Started").GetSetMethod(true).Invoke(g,new object[]{true});
 Action<float> place=distance=>{cc.enabled=false;p.transform.position=origin+direction*distance;cc.enabled=true;Physics.SyncTransforms();t.GetMethod("Update",Flags).Invoke(g,null);};
 place(.5f);check(Mathf.Abs(g.CurrentScale-Mathf.Max(g.minimumScale,1f-.5f*g.shrinkPerUnit))<.002f,"Half unit along current path applies configured shrink");
 place(2);check(Mathf.Abs(g.CurrentScale-Mathf.Max(g.minimumScale,1f-2f*g.shrinkPerUnit))<.002f,"Two units applies configured shrink");
 place(4);check(Mathf.Abs(g.CurrentScale-.2f)<.002f,"Four units reaches minimum 0.2");
 place(1);check(Mathf.Abs(g.CurrentScale-Mathf.Max(g.minimumScale,1f-g.shrinkPerUnit))<.002f,"Backward movement grows with configured shrink");
 place(-1);check(Mathf.Abs(g.CurrentScale-1)<.002f,"Retreat restores full scale");
 var dialogue=UnityEngine.Object.FindFirstObjectByType<TeamRoulette.Room3.DialogueManager>();check(dialogue!=null,"Shared dialogue manager exists");
 t.GetField("guideFrom",Flags).SetValue(g,g.dogStart);t.GetField("guideTo",Flags).SetValue(g,g.dogStart);t.GetField("guideMoving",Flags).SetValue(g,false);
 for(int i=0;i<3;i++){var to=(Vector3)t.GetField("guideTo",Flags).GetValue(g);cc.enabled=false;p.transform.position=to;cc.enabled=true;Physics.SyncTransforms();t.GetMethod("UpdateGuide",Flags).Invoke(g,new object[]{g.dogStepSeconds});check((int)t.GetField("guideDialogueIndex",Flags).GetValue(g)==i+2,"Guide step "+(i+1)+" queues next existing dialogue line");}
 t.GetMethod("SetCurtainsRevealed",Flags).Invoke(g,new object[]{true});
 check(!g.curtainL.gameObject.activeSelf&&!g.curtainR.gameObject.activeSelf&&g.animatedCurtainA.activeSelf&&g.animatedCurtainB.activeSelf,"Completion switches static curtains");
 check(g.animatedCurtainA.name=="curtaina_static_end"&&g.animatedCurtainB.name=="curtainb_static_end","Correct end curtain references");
 t.GetMethod("SetCurtainsRevealed",Flags).Invoke(g,new object[]{false});check(g.curtainL.gameObject.activeSelf&&!g.animatedCurtainA.activeSelf,"Initial curtain visibility restored");
 report+="ALL CHECKS PASSED\n";
 }catch(Exception e){report+="FAILED: "+e+"\n";Debug.LogException(e);}finally{File.WriteAllText("Assets/LivingRoom/CurtainGame/RestoreCheck.txt",report);EditorApplication.ExitPlaymode();}}
}


