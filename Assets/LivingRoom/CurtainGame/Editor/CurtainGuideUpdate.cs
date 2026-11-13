using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad] public static class CurtainGuideUpdate {
 static CurtainGuideUpdate(){EditorApplication.delayCall+=Run;}
 static void Run(){
 const string request="Assets/LivingRoom/CurtainGame/Editor/GuideUpdate.request";
 if(!File.Exists(request))return;
 if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")return;
 try{
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var game=all.Single(x=>x.name=="CurtainGame").GetComponent<CurtainGameController>();
 var a=all.Single(x=>x.name=="curtaina_static_end").gameObject;
 var b=all.Single(x=>x.name=="curtainb_static_end").gameObject;
 Undo.RecordObject(game,"Change curtain reveal targets");
 game.animatedCurtainA.SetActive(false);game.animatedCurtainB.SetActive(false);
 game.animatedCurtainA=a;game.animatedCurtainB=b;a.SetActive(false);b.SetActive(false);
 var d=Vector3.ProjectOnPlane(game.endTrigger.bounds.center-game.startTrigger.bounds.center,Vector3.up).normalized;
 game.dogStart=game.startTrigger.bounds.center+d*Mathf.Min(1,Vector3.Distance(game.startTrigger.bounds.center,game.endTrigger.bounds.center));game.dogStart.y=game.dog.transform.position.y;
 game.dogEndZ=game.endTrigger.bounds.center.z;
 EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Assets/LivingRoom/CurtainGame/GuideUpdateVerification.txt","Saved reveal targets: "+a.name+", "+b.name+"\nGuide start: "+game.dogStart+"; direction: "+d+"; end Z: "+game.dogEndZ+"\n");File.Move(request,request+".done");
 }catch(Exception e){Debug.LogException(e);File.Move(request,request+".failed");}
 }
}
