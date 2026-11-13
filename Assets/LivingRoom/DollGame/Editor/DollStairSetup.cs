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
[InitializeOnLoad]public static class DollStairSetup{
 const string Report="Assets/LivingRoom/DollGame/Editor/StairSetup.txt";
 static DollStairSetup(){EditorApplication.delayCall+=Run;}
 static float Extent(Bounds b,Vector3 d)=>Mathf.Abs(d.x)*b.extents.x+Mathf.Abs(d.z)*b.extents.z;
 static void Run(){
 if(File.Exists(Report))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Run;return;}
 try{
 var s=SceneManager.GetSceneByName("Room3_Toy");bool opened=!s.IsValid()||!s.isLoaded;if(opened)s=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);
 var nodes=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var b=nodes.Select(t=>t.GetComponent<DollYardBindings>()).First(v=>v);
 var steps=nodes.Select(t=>t.GetComponent<Renderer>()).Where(r=>r && r.name.StartsWith("Entry / concrete step",StringComparison.Ordinal)).OrderBy(r=>r.bounds.max.y).ToArray();
 if(steps.Length<3)throw new Exception("외부 현관 계단 3개를 찾지 못했습니다.");
 var surfaces=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).Where(c=>c.enabled&&!c.isTrigger&&!c.GetComponentInParent<FirstPersonPlayer>()&&!c.GetComponentInParent<Animator>()).ToArray();
 var outward=Vector3.ProjectOnPlane(b.houseDoor.position-b.insidePoint.position,Vector3.up).normalized;
 var positions=new System.Collections.Generic.List<Vector3>();var low=steps[0].bounds;
 positions.Add(low.center+outward*(Extent(low,outward)+.5f));
 for(int i=0;i<steps.Length;i++){
 var bounds=steps[i].bounds;float outer=Vector3.Dot(bounds.center,outward)+Extent(bounds,outward);
 float inner=i+1<steps.Length?Vector3.Dot(steps[i+1].bounds.center,outward)+Extent(steps[i+1].bounds,outward):Vector3.Dot(bounds.center,outward)-Extent(bounds,outward);
 var p=bounds.center+outward*((outer+inner)*.5f-Vector3.Dot(bounds.center,outward));positions.Add(p);
 }
 var top=steps[steps.Length-1].bounds;positions.Add(top.center-outward*(Extent(top,outward)+.3f));positions.Add(b.houseDoor.position);
 var routeRoot=b.transform.Find("Stair Return Route");if(!routeRoot){routeRoot=new GameObject("Stair Return Route").transform;routeRoot.SetParent(b.transform,false);}
 var log=new StringBuilder();var route=new Transform[positions.Count];
 for(int i=0;i<positions.Count;i++){
 var p=positions[i];var ray=new Ray(new Vector3(p.x,1.6f,p.z),Vector3.down);float ground=float.NegativeInfinity;string source="";
 foreach(var c in surfaces)if(c.Raycast(ray,out var hit,2)&&hit.normal.y>.6f&&hit.point.y>ground){ground=hit.point.y;source=c.name;}
 if(float.IsNegativeInfinity(ground))throw new Exception("바닥 지지가 없는 경로: "+p);
 p.y=ground+.025f;var point=routeRoot.Find("Point "+i);if(!point){point=new GameObject("Point "+i).transform;point.SetParent(routeRoot,false);}point.position=p;route[i]=point;log.AppendLine(i+": "+p+" surface="+source);
 }
 if(route[route.Length-1].position.y-route[0].position.y<.4f)throw new Exception("계단 높이 상승 확인 실패");
 b.returnRoute=route;b.walkingSurfaces=surfaces;EditorUtility.SetDirty(b);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);File.WriteAllText(Report,log.ToString());if(opened)EditorSceneManager.CloseScene(s,true);
 }catch(Exception e){File.WriteAllText(Report,"FAIL: "+e);Debug.LogException(e);}
 }
}
#endif
