#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class DollFloorAudit {
 static DollFloorAudit(){EditorApplication.delayCall+=Run;}
 static void Run(){
 if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 const string path="Assets/LivingRoom/DollGame/Editor/FloorAudit.txt"; if(File.Exists(path))return;
 var report=new StringBuilder();
 foreach(var name in new[]{"Room3_LivingRoom","Room3_OldLivingRoom","Room3_Toy"}){
 var scene=SceneManager.GetSceneByName(name); bool opened=!scene.IsValid()||!scene.isLoaded; if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);
 var nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
 var house=nodes.First(t=>t.name=="suburb_house"); var cols=house.GetComponentsInChildren<Collider>(true);
 report.AppendLine(name+" houseActive="+house.gameObject.activeInHierarchy+" colliders="+cols.Length+" enabled="+cols.Count(c=>c.enabled)+" meshes="+house.GetComponentsInChildren<MeshFilter>(true).Length);
 foreach(var c in cols.Take(12))report.AppendLine(c.name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" trigger="+c.isTrigger);
 foreach(var t in nodes.Where(t=>t.name=="Chase Entry"||t.GetComponent<FirstPersonPlayer>())){
 report.AppendLine(t.name+" pos="+t.position);var ray=new Ray(t.position+Vector3.up*2,Vector3.down);
 foreach(var c in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true)))if(c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.Raycast(ray,out var hit,6)) report.AppendLine("FLOOR "+c.name+" hit="+hit.point);
 }
 if(opened)EditorSceneManager.CloseScene(scene,true);
 } File.WriteAllText(path,report.ToString()); }
}
#endif
