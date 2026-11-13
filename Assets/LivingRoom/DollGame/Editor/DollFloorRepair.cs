#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class DollFloorRepair {
 const string Folder="Assets/LivingRoom/DollGame/Editor/";
 static DollFloorRepair(){EditorApplication.delayCall+=Auto;}
 static void Auto(){
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Auto;return;}
 if(File.Exists(Folder+"FloorRepair.txt"))return;
 try{Repair();}catch(Exception e){Debug.LogException(e);}
 }
 [MenuItem("Tools/Room3/Doll Memory/Repair Memory Floor")]
 public static void Repair(){
 var scene=SceneManager.GetSceneByName("Room3_OldLivingRoom");bool opened=!scene.IsValid()||!scene.isLoaded;
 if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Room3_OldLivingRoom.unity",OpenSceneMode.Additive);
 var nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
 var house=nodes.First(t=>t.name=="suburb_house");var log=new StringBuilder();int added=0;
 foreach(var mesh in house.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh&&m.name.IndexOf("Floor_",StringComparison.OrdinalIgnoreCase)>=0)){
 if(mesh.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger))continue;
 var box=Undo.AddComponent<BoxCollider>(mesh.gameObject);var bounds=mesh.sharedMesh.bounds;box.center=bounds.center;
 var scale=mesh.transform.lossyScale;
 box.size=new Vector3(Mathf.Max(bounds.size.x,.06f/Mathf.Max(.00001f,Mathf.Abs(scale.x))),Mathf.Max(bounds.size.y,.06f/Mathf.Max(.00001f,Mathf.Abs(scale.y))),Mathf.Max(bounds.size.z,.06f/Mathf.Max(.00001f,Mathf.Abs(scale.z))));
 box.enabled=true;box.isTrigger=false;added++;EditorUtility.SetDirty(box);
 }
 Physics.SyncTransforms();log.AppendLine("Added floor colliders: "+added);
 var cols=house.GetComponentsInChildren<Collider>(true);
 foreach(var t in nodes.Where(t=>t.name=="Chase Entry"||t.name.StartsWith("Chase ")||t.GetComponent<FirstPersonPlayer>())){
 var ray=new Ray(t.position+Vector3.up*1.5f,Vector3.down);var hits=cols.Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).Select(c=>{bool ok=c.Raycast(ray,out var h,3);return new{ok,h};}).Where(h=>h.ok).OrderBy(h=>h.h.distance).ToArray();
 if(hits.Length==0)throw new Exception("Missing floor at "+t.name+" "+t.position);
 log.AppendLine("PASS: "+t.name+" floor="+hits[0].h.point+" collider="+hits[0].h.collider.name);
 }
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
 File.WriteAllText(Folder+"FloorRepair.txt",log.ToString());if(opened)EditorSceneManager.CloseScene(scene,true);
 }
}
#endif
