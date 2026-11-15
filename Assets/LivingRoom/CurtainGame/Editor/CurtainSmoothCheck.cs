using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad] public static class CurtainSmoothCheck {
 const string Request="Assets/LivingRoom/CurtainGame/Editor/Smooth.request";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static CurtainSmoothCheck(){EditorApplication.delayCall+=Begin;EditorApplication.playModeStateChanged+=OnPlay;}
 static void Begin(){if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
 var g=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();if(!g)return;
 Undo.RecordObject(g,"Smooth CurtainGame motion");g.scaleChangePerSecond=.25f;g.dogStepSeconds=1.5f;
 g.guideAnimationController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller");
 EditorUtility.SetDirty(g);EditorSceneManager.MarkSceneDirty(g.gameObject.scene);EditorSceneManager.SaveScene(g.gameObject.scene);
 File.Move(Request,Request+".started-"+DateTime.Now.Ticks);SessionState.SetBool("CurtainSmoothCheck",true);EditorApplication.EnterPlaymode();}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CurtainSmoothCheck",false)){SessionState.SetBool("CurtainSmoothCheck",false);EditorApplication.delayCall+=Verify;}}
 static void Verify(){string report="";Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);report+="PASS: "+name+"\n";};try{
 var g=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();var t=g.GetType();var cc=g.player.GetComponent<CharacterController>();g.player.enabled=false;g.endTrigger.enabled=false;
 t.GetProperty("Started").GetSetMethod(true).Invoke(g,new object[]{true});
 var direction=(Vector3)t.GetField("guideDirection",F).GetValue(g);
 cc.enabled=false;g.player.transform.position=g.startTrigger.bounds.center+direction*2;cc.enabled=true;Physics.SyncTransforms();
 t.GetMethod("Update",F).Invoke(g,null);
 check(g.CurrentScale>.98f,"First shrink frame does not snap to target size");
 float before=g.CurrentScale;for(int i=0;i<20;i++)t.GetMethod("Update",F).Invoke(g,null);
 check(g.CurrentScale<=before&&g.CurrentScale>g.minimumScale,"Shrink progresses gradually across frames");
 g.dog.SetActive(true);var animator=g.dog.GetComponentInChildren<Animator>();
 check(animator&&animator.runtimeAnimatorController&&!animator.applyRootMotion,"Guide animation controller connected; root motion cannot override path");
 check(animator.HasState(0,Animator.StringToHash("Base Layer.Arm_Spitz|Walk_F_IP")),"Original walking state available");
 t.GetField("guideFrom",F).SetValue(g,g.dogStart);t.GetField("guideTo",F).SetValue(g,g.dogStart);t.GetField("guideMoving",F).SetValue(g,false);g.dog.transform.position=g.dogStart;
 cc.enabled=false;g.player.transform.position=g.dogStart;cc.enabled=true;
 t.GetMethod("UpdateGuide",F).Invoke(g,new object[]{10f});
 float moved=Vector3.Distance(g.dog.transform.position,g.dogStart);
 check(moved>0&&moved<.02f,"Even a long frame advances guide incrementally, without teleporting");
 for(int i=0;i<35;i++)t.GetMethod("UpdateGuide",F).Invoke(g,new object[]{.05f});
 check(Mathf.Abs(Vector3.Distance(g.dog.transform.position,g.dogStart)-1)<.002f,"Guide walks the full one unit and stops");
 check((int)t.GetField("guideDialogueIndex",F).GetValue(g)==2,"One completed step still queues exactly one dialogue");
 report+="ALL CHECKS PASSED\n";
 }catch(Exception e){report+="FAILED: "+e;Debug.LogException(e);}finally{File.WriteAllText("Assets/LivingRoom/CurtainGame/SmoothVerification.txt",report);EditorApplication.ExitPlaymode();}}
}
