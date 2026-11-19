using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad] public static class CurtainClickSetup {
 const string R="Assets/LivingRoom/CurtainGame/Editor/ClickSetup.request";
 static CurtainGameController g;static Vector3 scale;static float speed,jump;static double finish;static string report;
 static CurtainClickSetup(){EditorApplication.delayCall+=Setup;EditorApplication.playModeStateChanged+=OnPlay;}
 static void Setup(){if(!File.Exists(R)||EditorApplication.isPlayingOrWillChangePlaymode)return;
 try{
 var game=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();if(!game)return;
 Undo.RecordObject(game,"Convert curtain game to click");
 game.dog=null;game.guideAnimationController=null;
 foreach(var trigger in new[]{game.startTrigger,game.endTrigger})if(trigger){Undo.RecordObject(trigger.gameObject,"Disable old curtain trigger");trigger.gameObject.SetActive(false);}
 game.startTrigger=null;game.endTrigger=null;
 foreach(var curtain in new[]{game.curtainL,game.curtainR}){
 var target=curtain.GetComponent<CurtainClickTarget>()??Undo.AddComponent<CurtainClickTarget>(curtain.gameObject);Undo.RecordObject(target,"Connect curtain click");target.game=game;EditorUtility.SetDirty(target);
 if(curtain.GetComponentsInChildren<Collider>(true).Length==0){
 var box=Undo.AddComponent<BoxCollider>(curtain.gameObject);var bounds=new Bounds();bool first=true;
 foreach(var renderer in curtain.GetComponentsInChildren<Renderer>(true)){var b=renderer.bounds;for(int i=0;i<8;i++){var v=curtain.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}}
 box.center=bounds.center;box.size=Vector3.Max(bounds.size,Vector3.one*.02f);
 }
 curtain.gameObject.SetActive(true);
 }
 game.animatedCurtainA.SetActive(false);game.animatedCurtainB.SetActive(false);
 EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
 File.Move(R,R+".started-"+DateTime.Now.Ticks);SessionState.SetBool("CurtainClickCheck",true);EditorApplication.EnterPlaymode();
 }catch(Exception e){Debug.LogException(e);}}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CurtainClickCheck",false)){SessionState.SetBool("CurtainClickCheck",false);EditorApplication.delayCall+=CheckStart;}}
 static void Assert(bool ok,string s){if(!ok)throw new Exception(s);report+="PASS: "+s+"\n";}
 static void CheckStart(){report="";try{
 g=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();scale=g.player.transform.localScale;speed=g.player.moveSpeed;jump=g.player.jumpHeight;
 Assert(!g.dog&&!g.startTrigger&&!g.endTrigger,"No dog or trigger dependency");Assert(!g.Started&&!g.PlayingFinale,"Approach does not auto-start");
 Assert(g.curtainL.GetComponent<CurtainClickTarget>()&&g.curtainR.GetComponent<CurtainClickTarget>(),"Both closed curtains use shared click interface");
 g.curtainL.GetComponent<CurtainClickTarget>().Interact();Assert(g.PlayingFinale&&!g.player.enabled,"Click starts light sequence and locks movement");Assert(!g.TryOpen(),"Repeated click ignored");
 finish=EditorApplication.timeSinceStartup+g.finaleSeconds+1;EditorApplication.update+=CheckFinish;
 }catch(Exception e){Done(e);}}
 static void CheckFinish(){if(EditorApplication.timeSinceStartup<finish)return;EditorApplication.update-=CheckFinish;try{
 Assert(g.Completed&&!g.PlayingFinale&&g.player.enabled,"Sequence completes and restores movement");Assert(g.player.transform.localScale==scale&&g.player.moveSpeed==speed&&g.player.jumpHeight==jump,"Player scale, speed and jump remain unchanged");
 Assert(!g.curtainL.gameObject.activeSelf&&!g.curtainR.gameObject.activeSelf&&g.animatedCurtainA.activeSelf&&g.animatedCurtainB.activeSelf,"Closed curtains turn off and end curtains turn on");
 Assert(g.whiteScreen.alpha==0&&!g.TryOpen(),"Brightness restored; sequence runs once");Done(null);
 }catch(Exception e){Done(e);}}
 static void Done(Exception e){File.WriteAllText("Assets/LivingRoom/CurtainGame/ClickVerification.txt",report+(e==null?"ALL CHECKS PASSED":"FAILED: "+e));if(e!=null)Debug.LogException(e);EditorApplication.ExitPlaymode();}
}
