#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DollInventory {
 [MenuItem("Tools/Room3/Doll Memory/Inspect Scene Assets")]
 public static void Run() {
 Directory.CreateDirectory(DollMemorySetup.Folder+"/Editor");
 var b=new StringBuilder();
 foreach(var name in new[]{"Room3_LivingRoom","Room3_OldLivingRoom","Room3_Toy"}) {
 var s=UnityEngine.SceneManagement.SceneManager.GetSceneByName(name); bool opened=!s.IsValid() || !s.isLoaded; if(opened) s=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive); b.AppendLine("SCENE "+name);
 foreach(var t in s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))) {
 if(t.name.Contains("toilet")||t.name.Contains("wash")||t.name.Contains("Wash")||t.name.Contains("duck")||t.name.Contains("Doll")||t.name.Contains("006")||t.name.Contains("Spitz_anim")||t.GetComponent<FirstPersonPlayer>()||t.name.Contains("Door")||t.name.Contains("door")||t.name.Contains("Doll")||t.name=="LivingRoom") {
 var bounds=new Bounds(t.position,Vector3.zero); foreach(var r in t.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
 b.AppendLine(t.name+" pos="+t.position.ToString("F3")+" rot="+t.eulerAngles+" scale="+t.lossyScale+" bounds="+bounds+" components="+string.Join(",",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)));
 }
 }
 b.AppendLine("NAV="+UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length); if(opened) EditorSceneManager.CloseScene(s,true);
 }
 var path="Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller";
 var ctrl=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
 foreach(var st in ctrl.layers[0].stateMachine.states) b.AppendLine("ANIM "+st.state.name);
 File.WriteAllText("Assets/LivingRoom/DollGame/Editor/Inventory.txt",b.ToString());
 }
}
#endif

