using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class CurtainAssetSwap
{
 static CurtainAssetSwap(){EditorApplication.delayCall+=Run;}
 static void Run(){
 const string request="Assets/LivingRoom/CurtainGame/Editor/AssetSwap.request";
 if(!File.Exists(request))return;
 if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")return;
 try{
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 Func<string,Transform> find=n=>all.Single(x=>x.name==n);
 var game=find("CurtainGame").GetComponent<CurtainGameController>();
 Undo.RecordObject(game,"Swap CurtainGame assets");
 var window=find("WindowGame");
 var a=find("room2_curtaina");var b=find("room2_curtainb");
 Undo.SetTransformParent(a,window,"Keep animated curtain under WindowGame");
 Undo.SetTransformParent(b,window,"Keep animated curtain under WindowGame");
 Func<string,Transform,Transform> getStatic=(name,source)=>{
 var t=all.SingleOrDefault(x=>x.name==name);
 if(!t){var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LivingRoom/Assets/CurtainsStatic/"+name+".fbx");
 var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,source.parent);Undo.RegisterCreatedObjectUndo(obj,"Add static curtain");t=obj.transform;
 t.position=source.position;t.rotation=source.rotation;t.localScale=source.localScale;
 Undo.SetTransformParent(t,game.transform,"Group static curtain"); PrefabUtility.RecordPrefabInstancePropertyModifications(t);
 }return t;};
 var sa=getStatic("curtaina_static",a);var sb=getStatic("curtainb_static",b);
 game.curtainL.gameObject.SetActive(false);game.curtainR.gameObject.SetActive(false);
 game.curtainL=sa;game.curtainR=sb;
 game.dog.SetActive(false);game.dog=find("Spitz_anim_IP").gameObject;game.dog.SetActive(false);
 game.animatedCurtainA=a.gameObject;game.animatedCurtainB=b.gameObject;
 sa.gameObject.SetActive(true);sb.gameObject.SetActive(true);a.gameObject.SetActive(false);b.gameObject.SetActive(false);
 EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 File.Move(request,request+".done");
 File.WriteAllText("Assets/LivingRoom/CurtainGame/AssetSwapVerification.txt","Saved: Dog=Spitz_anim_IP; L=curtaina_static; R=curtainb_static. Static curtains active, original animated curtains inactive under WindowGame. Original model components retained.\n");
 Debug.Log("CurtainGame asset swap saved.");
 }catch(Exception e){Debug.LogException(e);File.Move(request,request+".failed");}
 }
}
