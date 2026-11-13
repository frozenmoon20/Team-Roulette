#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3.DollMemory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class DollPolishSetup {
 const string Folder="Assets/LivingRoom/DollGame/";
 static DollPolishSetup(){EditorApplication.delayCall+=Run;}
 static void Run(){
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}if(File.Exists(Folder+"Editor/Polish.txt"))return;
 try{
 var sceneView=SceneView.lastActiveSceneView;if(!sceneView)sceneView=SceneView.sceneViews.Cast<SceneView>().FirstOrDefault(v=>v.camera);var referenceCamera=sceneView?sceneView.camera:null;
 if(referenceCamera && referenceCamera.transform.position.sqrMagnitude<.01f){EditorApplication.delayCall+=Run;return;}
 Vector3 refPosition=referenceCamera?referenceCamera.transform.position:Vector3.zero;Quaternion refRotation=referenceCamera?referenceCamera.transform.rotation:Quaternion.identity;
 var old=SceneManager.GetSceneByName("Room3_OldLivingRoom");bool closeOld=!old.IsValid()||!old.isLoaded;if(closeOld)old=EditorSceneManager.OpenScene("Assets/Scenes/Room3_OldLivingRoom.unity",OpenSceneMode.Additive);
 var yard=SceneManager.GetSceneByName("Room3_Toy");bool closeYard=!yard.IsValid()||!yard.isLoaded;if(closeYard)yard=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);
 var game=old.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DollMemorySequence>(true)).First();var binding=yard.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DollYardBindings>(true)).First();
 var log=new StringBuilder();
 foreach(var mat in game.washerRenderer.sharedMaterials){log.AppendLine("BEFORE washer: "+AssetDatabase.GetAssetPath(mat)+" shader="+mat.shader.name+" queue="+mat.renderQueue);foreach(var key in new[]{"_Surface","_SurfaceType","_ZWrite","_Smoothness","_Metallic"})if(mat.HasProperty(key))log.AppendLine(key+"="+mat.GetFloat(key));}
 var path=Folder+"WasherOpaque.mat";var body=AssetDatabase.LoadAssetAtPath<Material>(path);if(!body){body=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(body,path);}
 var texture=AssetDatabase.LoadAssetAtPath<Texture>("Assets/StoreAssets/Suburb Neighborhood House Pack Modular/SuburbNeighborhoodHousePack/Textures/WashingMachine_BaseColor.tga");
 body.SetTexture("_BaseMap",texture);body.SetColor("_BaseColor",Color.white);body.SetFloat("_Surface",0);body.SetFloat("_AlphaClip",0);body.SetFloat("_ZWrite",1);body.SetFloat("_SrcBlend",1);body.SetFloat("_DstBlend",0);body.SetFloat("_Smoothness",.08f);body.SetFloat("_Metallic",0);body.SetFloat("_SpecularHighlights",0);body.SetFloat("_EnvironmentReflections",0);body.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");body.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");body.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");body.renderQueue=2000;body.SetOverrideTag("RenderType","Opaque");
 game.washerRenderer.sharedMaterials=game.washerRenderer.sharedMaterials.Select(m=>body).ToArray();game.washerRenderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
 game.washerCutaway.SetTexture("_BaseMap",texture);game.washerCutaway.SetColor("_BaseColor",Color.white);
  var wc=game.washerRenderer.bounds.center;var wf=game.drum.forward;
 game.washerSeat.position=wc+wf*1.05f+game.washerRenderer.transform.right*-.55f;game.washerSeat.position=new Vector3(game.washerSeat.position.x,.7f,game.washerSeat.position.z);
 game.washerView.position=wc+wf*1.28f+game.washerRenderer.transform.right*.32f+Vector3.up*.25f;game.washerView.rotation=Quaternion.LookRotation(game.drum.position+Vector3.up*.04f-game.washerView.position);
 game.washerSeat.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(game.drum.position-game.washerSeat.position,Vector3.up));
 // Check the actual controller, rather than silently assuming sitting states exist.
 var animator=game.winter.animator;var ctrl=animator.runtimeAnimatorController;
 log.AppendLine("Winter controller="+(ctrl?AssetDatabase.GetAssetPath(ctrl):"none"));
 var fallback=game.winter.fallbackController as UnityEditor.Animations.AnimatorController;
 if(fallback)foreach(var state in fallback.layers[0].stateMachine.states.Where(s=>s.state.name.Contains("Sitting")))log.AppendLine(state.state.name+" motion="+(state.state.motion?state.state.motion.name:"NONE"));
 var rack=binding.dryingPoint.position;var towardOutside=Vector3.ProjectOnPlane(binding.houseDoor.position-binding.insidePoint.position,Vector3.up).normalized;
 var facadeRight=Vector3.Cross(Vector3.up,towardOutside);
  binding.view.position=rack+towardOutside*3.8f+facadeRight*1.4f+Vector3.up*.7f;
 var framing=Vector3.Lerp(rack,binding.houseDoor.position,.35f);framing.y=1.05f;
 binding.view.rotation=Quaternion.LookRotation(framing-binding.view.position);
 var stand=rack+towardOutside*1.05f;stand.y=0;binding.dogStand.position=stand;binding.dogStand.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(rack-stand,Vector3.up));
 var bed=old.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="DogAccesories-006");
 var bb=bed.GetComponent<Renderer>().bounds;float seatY=bb.center.y;
 var collider=bed.GetComponent<MeshCollider>();bool temporary=!collider;if(temporary){collider=bed.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=bed.GetComponent<MeshFilter>().sharedMesh;}
 if(collider.Raycast(new Ray(new Vector3(bb.center.x,bb.max.y+1,bb.center.z),Vector3.down),out var hit,3))seatY=hit.point.y+.025f;
 if(temporary)UnityEngine.Object.DestroyImmediate(collider);
 game.cushion.position=new Vector3(bb.center.x,seatY,bb.center.z);
 var target=game.cushion.position+Vector3.up*.22f;
 log.AppendLine("Reference view="+refPosition+" rotation="+refRotation.eulerAngles+" bed="+bb.center);
 game.restView.position=game.cushion.position+new Vector3(.05f,1.45f,2.1f);game.restView.rotation=Quaternion.LookRotation(target-game.restView.position);
 if(referenceCamera && Vector3.Dot(refRotation*Vector3.forward,(bb.center-refPosition).normalized)>.75f){game.restView.SetPositionAndRotation(refPosition,refRotation);log.AppendLine("Used current Scene view for photo 2");}
 game.cushion.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(game.restView.position-game.cushion.position,Vector3.up));
 log.AppendLine("Drying view="+binding.view.position+" euler="+binding.view.eulerAngles+" rack="+rack+" door="+binding.houseDoor.position);log.AppendLine("Rest view="+game.restView.position+" euler="+game.restView.eulerAngles);
 EditorUtility.SetDirty(body);EditorUtility.SetDirty(game.washerCutaway);EditorUtility.SetDirty(game.washerRenderer);EditorUtility.SetDirty(game);EditorUtility.SetDirty(binding);
 EditorSceneManager.MarkSceneDirty(old);EditorSceneManager.MarkSceneDirty(yard);EditorSceneManager.SaveScene(old);EditorSceneManager.SaveScene(yard);AssetDatabase.SaveAssets();File.WriteAllText(Folder+"Editor/Polish.txt",log.ToString());
 if(closeOld)EditorSceneManager.CloseScene(old,true);if(closeYard)EditorSceneManager.CloseScene(yard,true);
 }catch(Exception e){File.WriteAllText(Folder+"Editor/Polish.txt","FAIL: "+e);Debug.LogException(e);}
 }
}
#endif
