#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class DollFloorRevert {
 static DollFloorRevert(){EditorApplication.delayCall+=Run;}
 static void Run(){
 const string report="Assets/LivingRoom/DollGame/Editor/FloorRevert.txt";
 if(File.Exists(report))return;
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 var log="";
 foreach(var name in new[]{"Room3_LivingRoom","Room3_OldLivingRoom","Room3_Toy"}){
 var s=SceneManager.GetSceneByName(name);bool opened=!s.IsValid()||!s.isLoaded;if(opened)s=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);
 var house=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="suburb_house");
 string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(house.gameObject);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 log+=name+" prefab="+path+" sourceColliders="+(prefab?prefab.GetComponentsInChildren<Collider>(true).Length:0)+" instanceColliders="+house.GetComponentsInChildren<Collider>(true).Length+"\n";
 if(name=="Room3_OldLivingRoom"){
 int count=0;foreach(var box in house.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name.IndexOf("Floor_",System.StringComparison.OrdinalIgnoreCase)>=0).ToArray()){Object.DestroyImmediate(box);count++;}
 log+="Reverted added floor colliders: "+count+"\n";EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
 }
 if(opened)EditorSceneManager.CloseScene(s,true);
 }File.WriteAllText(report,log);
 }
}
#endif
