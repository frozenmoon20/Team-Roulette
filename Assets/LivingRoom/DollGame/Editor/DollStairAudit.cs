#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class DollStairAudit{
 static DollStairAudit(){EditorApplication.delayCall+=Run;}
 static void Run(){const string path="Assets/LivingRoom/DollGame/Editor/StairAudit.txt";if(File.Exists(path))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 var s=SceneManager.GetSceneByName("Room3_Toy");bool opened=!s.IsValid()||!s.isLoaded;if(opened)s=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);var log=new StringBuilder();
 foreach(var t in s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.ToLower().Contains("stair")||t.name.ToLower().Contains("step")||t.name.ToLower().Contains("porch"))){var r=t.GetComponent<Renderer>();if(r)log.AppendLine(t.name+" pos="+t.position+" bounds="+r.bounds+" colliders="+string.Join(",",t.GetComponents<Collider>().Select(c=>c.GetType().Name+":"+c.enabled)));}
 var b=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TeamRoulette.Room3.DollMemory.DollYardBindings>(true)).First();log.AppendLine("stand="+b.dogStand.position+" door="+b.houseDoor.position+" inside="+b.insidePoint.position);
 File.WriteAllText(path,log.ToString());if(opened)EditorSceneManager.CloseScene(s,true);
 }
}
#endif
