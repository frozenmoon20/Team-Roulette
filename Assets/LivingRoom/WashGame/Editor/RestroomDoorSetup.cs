using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class RestroomDoorSetup
{
 static RestroomDoorSetup(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=OnPlay;}
 static double next; static int step; static RestroomDoor door; static Quaternion closed; static Vector3 hinge; static Bounds closedBounds;
 static void OnPlay(PlayModeStateChange state){
 if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("RestroomDoor.Verify",false))return;
 SessionState.SetBool("RestroomDoor.Verify",false);
 door=Object.FindFirstObjectByType<RestroomDoor>();closed=door.transform.rotation;hinge=door.transform.position;closedBounds=door.GetComponent<Renderer>().bounds;
 Physics.SyncTransforms();
 var normal=closedBounds.size.x<closedBounds.size.z?Vector3.right:Vector3.forward;
 string clickReport="";
 foreach(float side in new[]{-1f,1f}){
 var ray=new Ray(closedBounds.center+normal*side*.8f,-normal*side);
 bool hit=Physics.SphereCast(ray,.03f,out var result,2f);
 clickReport+=(hit && result.collider.GetComponentInParent<IInteractable>()==door?"PASS":"FAILED")+": door click from side "+side+" hits "+(hit?result.collider.name:"nothing")+"\n";
 }
 File.WriteAllText("Assets/LivingRoom/WashGame/Reports/DoorClickVerification.txt",clickReport);
 door.Interact();step=1;next=EditorApplication.timeSinceStartup+.8;EditorApplication.update+=Verify;
 }
 static void Verify(){
 if(EditorApplication.timeSinceStartup<next)return;
 string report="Assets/LivingRoom/WashGame/Reports/DoorVerification.txt";
 if(step==1){
 var openedBounds=door.GetComponent<Renderer>().bounds;
 if(!door.IsOpen||Quaternion.Angle(closed,door.transform.rotation)<90||door.transform.position!=hinge||Mathf.Abs(openedBounds.size.y-closedBounds.size.y)>.01f||Mathf.Abs(openedBounds.center.y-closedBounds.center.y)>.01f){File.WriteAllText(report,"FAILED: opening/hinge/vertical axis");Finish();return;}
 door.Interact();step=2;next=EditorApplication.timeSinceStartup+.8;
 }else{
 File.WriteAllText(report,!door.IsOpen && Quaternion.Angle(closed,door.transform.rotation)<.01f && door.GetComponentsInChildren<Collider>().Any(c=>c.enabled)?"PASS: Opens around vertical world axis without tipping. Door height and hinge preserved. Second click restores closed rotation; colliders enabled.":"FAILED: closing/collider");Finish();
 }
 }
 static void Finish(){EditorApplication.update-=Verify;EditorApplication.ExitPlaymode();}
 static void Poll(){
 const string marker="Assets/LivingRoom/WashGame/Editor/Door.request";
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(marker))return;
 string action=File.ReadAllText(marker).Trim();File.Delete(marker);
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 if(action=="water"){
 var wash=Object.FindFirstObjectByType<WashGameController>(FindObjectsInactive.Include);
 var sink=all.Single(x=>x.name=="T_sink" && x.GetComponentsInParent<Transform>(true).Any(p=>p.name=="Restroom"));
 var bounds=sink.GetComponent<Renderer>().bounds;
 var water=wash.water.transform;
 var before=water.position;
 var position=new Vector3(bounds.center.x,bounds.max.y-.01f,bounds.center.z-.06f);
 Undo.RecordObject(water,"Align wash water with tap");water.position=position;
 PrefabUtility.RecordPrefabInstancePropertyModifications(water);
 var hit=wash.transform.Find("WaterClickArea");if(hit){Undo.RecordObject(hit,"Align water click area");hit.position=position+Vector3.down*.18f;}
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Assets/LivingRoom/WashGame/Reports/WaterHeightFix.txt","Saved water outlet: "+before+" -> "+water.position+". Water click area aligned.\n");return;
 }
 if(action=="apply"){
 var t=all.Single(x=>x.name=="1F bathroom / Door_interior");
 var component=t.GetComponent<RestroomDoor>();if(!component)component=Undo.AddComponent<RestroomDoor>(t.gameObject);
 foreach(var child in t.GetComponentsInChildren<Transform>(true)){GameObjectUtility.SetStaticEditorFlags(child.gameObject,0);PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);}
 if(!t.GetComponentsInChildren<Collider>().Any())t.gameObject.AddComponent<BoxCollider>();
 EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 SessionState.SetBool("RestroomDoor.Verify",true);EditorApplication.EnterPlaymode();return;
 }
 File.WriteAllLines("Assets/LivingRoom/WashGame/Reports/DoorInventory.txt",all.Where(x=>x.name.ToLower().Contains("door")||x.GetComponentsInParent<Transform>(true).Any(p=>p.name=="Restroom")).Select(x=>string.Join("/",x.GetComponentsInParent<Transform>(true).Reverse().Select(p=>p.name))+" position="+x.position+" local="+x.localPosition+" bounds="+(x.GetComponent<Renderer>()?x.GetComponent<Renderer>().bounds.ToString():"none")));
 }
}
