using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class PlaceAnimatedCurtains
{
 static PlaceAnimatedCurtains() { EditorApplication.delayCall += Run; }
 static void Run() {
 const string request="Assets/LivingRoom/CurtainGame/Editor/PlaceCurtains.request";
 if(!File.Exists(request)) return;
 if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Run; return; }
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity") return;
 try {
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var window=all.Single(x=>x.name=="WindowClose");
 var target=window.GetComponent<Renderer>().bounds;
 if(all.Any(x=>x.name=="Curtains Animated")) { var existing=all.Single(x=>x.name=="Curtains Animated"); var rr=existing.GetComponentsInChildren<Renderer>(true); var bb=rr[0].bounds; foreach(var r in rr.Skip(1)) bb.Encapsulate(r.bounds); existing.position+=Vector3.forward*(target.max.z+.08f-bb.min.z); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); File.Move(request,request+".adjusted"); return; }
 var group=new GameObject("Curtains Animated"); Undo.RegisterCreatedObjectUndo(group,"Place animated curtains");
 group.transform.SetParent(window.parent,true);
 group.transform.position=Vector3.zero; group.transform.rotation=Quaternion.identity; group.transform.localScale=new Vector3(1/window.parent.lossyScale.x,1/window.parent.lossyScale.y,1/window.parent.lossyScale.z);
 string report="";
 for(int i=0;i<2;i++) {
 var path="Assets/StoreAssets/Curtains Animated/POLYBOX/hazelwoodloft/Animation/room2_curtain"+(i==0?"a":"b")+".fbx";
 var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,group.transform);
 obj.transform.localPosition=Vector3.zero; obj.transform.localRotation=Quaternion.identity; obj.transform.localScale=Vector3.one;
 var rs=obj.GetComponentsInChildren<Renderer>(true); var b=rs[0].bounds; foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds);
 obj.transform.localScale=new Vector3(target.size.x*.51f/b.size.x,target.size.y*1.04f/b.size.y,1);
 b=rs[0].bounds; foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds);
 var desired=new Vector3(target.center.x+(i==0?-1:1)*target.size.x*.25f,target.center.y,target.min.z-.08f-b.extents.z);
 obj.transform.position+=desired-b.center;
 PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
 report+=obj.name+": "+obj.GetComponentsInChildren<Animation>(true).Length+" Animation, "+obj.GetComponentsInChildren<Animator>(true).Length+" Animator; source "+path+"\n";
 }
 Selection.activeGameObject=group;
 EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Assets/LivingRoom/CurtainGame/CurtainPlacement.txt",report+"Saved; original model prefab links and components retained.");
 File.Move(request,request+".done");
 Debug.Log("Animated curtains placed and saved. "+report);
 } catch(Exception e) { Debug.LogException(e); File.Move(request,request+".failed"); }
 }
}

