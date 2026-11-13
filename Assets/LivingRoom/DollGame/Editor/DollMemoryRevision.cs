#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TeamRoulette.Room3.DollMemory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class DollMemoryRevision {
 const string Report="Assets/LivingRoom/DollGame/Editor/Revision.txt";
 static DollMemoryRevision(){EditorApplication.delayCall+=Run;}
 static Transform Point(Transform root,string name,Vector3 p,Vector3 look){var t=root.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(root,false);}t.position=p;t.rotation=Quaternion.LookRotation(look-p);return t;}
 static T Add<T>(GameObject o)where T:Component{var c=o.GetComponent<T>();return c?c:o.AddComponent<T>();}
 static void Run(){
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}if(File.Exists(Report))return;
 try{
 var old=SceneManager.GetSceneByName("Room3_OldLivingRoom");bool closeOld=!old.IsValid()||!old.isLoaded;if(closeOld)old=EditorSceneManager.OpenScene("Assets/Scenes/Room3_OldLivingRoom.unity",OpenSceneMode.Additive);
 var yard=SceneManager.GetSceneByName("Room3_Toy");bool closeYard=!yard.IsValid()||!yard.isLoaded;if(closeYard)yard=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);
 var n=old.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var y=yard.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
 var game=n.Select(t=>t.GetComponent<DollMemorySequence>()).First(g=>g);var binding=y.Select(t=>t.GetComponent<DollYardBindings>()).First(g=>g);
 var surface=game.washerButton.Find("Button Surface");if(surface)UnityEngine.Object.DestroyImmediate(surface.gameObject);
 var backing=game.transform.Find("Drum Interior");if(backing)UnityEngine.Object.DestroyImmediate(backing.gameObject);
 var button=Add<DollWasherButton>(game.washerButton.gameObject);button.memory=game;EditorUtility.SetDirty(button);
 var washer=n.First(t=>t && t.name=="Washingmachine");game.washerRenderer=washer.GetComponent<Renderer>();var center=game.washerRenderer.bounds.center;var front=Vector3.ProjectOnPlane(washer.forward,Vector3.up).normalized;
 game.drum.position=center+front*.38f-Vector3.up*.06f;game.drum.rotation=Quaternion.LookRotation(front);
 game.washerView.position=center+front*1.28f+washer.right*.32f+Vector3.up*.25f;game.washerView.rotation=Quaternion.LookRotation(game.drum.position+Vector3.up*.04f-game.washerView.position);
  var bath=n.FirstOrDefault(t=>t && t.GetComponent<RestroomDoor>()) ?? n.First(t=>t && t.name.Contains("1F bathroom") && t.name.Contains("Door_interior")); var bathInteraction=Add<RestroomDoor>(bath.gameObject);EditorUtility.SetDirty(bathInteraction);var bc=bath.GetComponent<Renderer>().bounds.center;
 game.washerRoute[0].position=new Vector3(bc.x+.8f,2.05f,bc.z);game.washerRoute[1].position=new Vector3(bc.x+.45f,2.05f,bc.z);game.washerRoute[2].position=new Vector3(bc.x-.55f,2.05f,bc.z);
 var path="Assets/LivingRoom/DollGame/WasherCutaway.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("TeamRoulette/DollWasherAperture"));AssetDatabase.CreateAsset(mat,path);}
 var source=game.washerRenderer.sharedMaterial;if(source){Texture texture=source.mainTexture;if(!texture&&source.HasProperty("_MainTex"))texture=source.GetTexture("_MainTex");if(!texture&&source.HasProperty("_BaseColorMap"))texture=source.GetTexture("_BaseColorMap");mat.SetTexture("_BaseMap",texture);}mat.SetColor("_BaseColor",Color.white);
 game.washerSeat.position=center+front*.95f-washer.right*.65f;game.washerSeat.position=new Vector3(game.washerSeat.position.x,.7f,game.washerSeat.position.z);game.washerSeat.rotation=Quaternion.LookRotation(center-game.washerSeat.position);
 mat.SetVector("_DrumCenter",center+front*.51f-Vector3.up*.06f);mat.SetVector("_DrumAxis",front);mat.SetFloat("_DrumRadius",.23f);EditorUtility.SetDirty(mat);game.washerCutaway=mat;
 var oldDoor=n.First(t=>t && t.name=="Door_exterior_windowed.001");game.interiorDoor=Add<DollDoor>(oldDoor.gameObject);var db=oldDoor.GetComponent<Renderer>().bounds;
 var doorCenter=new Vector3(db.center.x,.7f,db.center.z);
 game.interiorEntry=Point(game.transform,"House Entry Outside",doorCenter+Vector3.forward*.85f,doorCenter);
 game.insideDoor=Point(game.transform,"House Entry Inside",doorCenter-Vector3.forward*.85f,game.cushion.position);
 game.entranceView=Point(game.transform,"House Entrance View",doorCenter+new Vector3(2,1.1f,-2.4f),doorCenter+Vector3.up*.5f);
 var yardLeaf=y.First(t=>t && t.name=="Door_exterior_windowed.001");binding.door=Add<DollDoor>(yardLeaf.gameObject);var yb=yardLeaf.GetComponent<Renderer>().bounds;var yp=new Vector3(yb.center.x,.5f,yb.center.z);
  var yhouse=y.First(t=>t && t.name=="suburb_house");var hb=new Bounds(yhouse.position,Vector3.zero);foreach(var r in yhouse.GetComponentsInChildren<Renderer>())hb.Encapsulate(r.bounds);
 var outward=yb.size.x>yb.size.z?Vector3.forward:Vector3.right;if(Vector3.Dot(yp-hb.center,outward)<0)outward=-outward;
 binding.houseDoor.position=yp+outward*.85f;binding.insidePoint=Point(binding.transform,"House Door Inside",yp-outward*.85f,yp-outward*2);
 var turn=outward.z!=0?new Vector3(binding.dogStand.position.x,.0f,yp.z+outward.z*2):new Vector3(yp.x+outward.x*2,0,binding.dogStand.position.z);
 binding.approachPoint=Point(binding.transform,"House Outside Approach",turn,yp);
 binding.view.position=binding.dryingPoint.position+new Vector3(2.25f,.35f,-1.4f);binding.view.rotation=Quaternion.LookRotation(binding.dogStand.position+Vector3.up*.65f-binding.view.position);
  var nearSide=Vector3.ProjectOnPlane(binding.view.position-binding.dryingPoint.position,Vector3.up).normalized;
 binding.dogStand.position=binding.dryingPoint.position+nearSide*.95f;binding.dogStand.position=new Vector3(binding.dogStand.position.x,0,binding.dogStand.position.z);binding.dogStand.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(binding.dryingPoint.position-binding.dogStand.position,Vector3.up));
 EditorUtility.SetDirty(game);EditorUtility.SetDirty(binding);EditorUtility.SetDirty(game.interiorDoor);EditorUtility.SetDirty(binding.door);
 EditorSceneManager.MarkSceneDirty(old);EditorSceneManager.MarkSceneDirty(yard);EditorSceneManager.SaveScene(old);EditorSceneManager.SaveScene(yard);AssetDatabase.SaveAssets();
 File.WriteAllText(Report,"PASS: manual walk; collider-only washer button; removed drum overlay; aperture material; actual exterior door hinges and camera anchors\nWasher view="+game.washerView.position+"\nOld door="+doorCenter+"\nYard door="+yp);
 if(closeOld)EditorSceneManager.CloseScene(old,true);if(closeYard)EditorSceneManager.CloseScene(yard,true);
 }catch(Exception e){Debug.LogException(e);File.WriteAllText(Report,"FAIL: "+e);}
 }
}
#endif
