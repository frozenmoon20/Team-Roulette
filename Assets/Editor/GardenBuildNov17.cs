#if UNITY_EDITOR
using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using UnityEditor.SceneManagement;using System.IO;using System.Linq;
[InitializeOnLoad]public static class GardenBuildNov17{
const string P="Assets/StoreAssets/Suburb Neighborhood House Pack Modular/SuburbNeighborhoodHousePack/Prefabs/Props_exterior/";const string T="Assets/StoreAssets/SP City Park 01/Bohn Studios/_Common/Prefabs/OWA Trees 01/";static Transform root;static int count;
static GardenBuildNov17(){EditorApplication.update+=Run;}
static Bounds B(GameObject g){var rr=g.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
static GameObject Add(string group,string path,float x,float z,float yaw=0,float scale=1,float ground=0){var par=root.Find(group);if(!par){par=new GameObject(group).transform;par.SetParent(root,false);}var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),par);g.transform.SetPositionAndRotation(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));g.transform.localScale=Vector3.one*scale;var b=B(g);g.transform.position+=new Vector3(x-b.center.x,ground-b.min.y,z-b.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);count++;return g;}
static void Prop(string group,string n,float x,float z,float yaw=0,float scale=1,float y=0){Add(group,P+n+".prefab",x,z,yaw,scale,y);}
static void Run(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("Temp/garden-redo-finished.txt"))return;var s=SceneManager.GetActiveScene();if(s.name!="Room3_Toy")return;var old=s.GetRootGameObjects().Single(g=>g.name=="GardenDecoration");EditorSceneManager.SaveScene(s,"Temp/GardenBeforeNov17.unity",true);Undo.IncrementCurrentGroup();int ug=Undo.GetCurrentGroup();Undo.RegisterFullObjectHierarchyUndo(old,"Redesign Garden");root=new GameObject("GardenDecoration").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Redesign Garden");old.name="PreviousGarden_Inactive";old.transform.SetParent(root,true);old.SetActive(false);
// Trees inside the rear lawn corners, clear of roads and the central play area.
Add("Trees",T+"Set 05/Tree_08.prefab",7.4f,-22.2f,30,.42f);Add("Trees",T+"Set 07/Tree_04__1_.prefab",-7.5f,-22.2f,135,.45f);
Add("Trees",T+"Set 08/Tree_02__1_.prefab",8.3f,-12.2f,25,.75f);Add("Trees",T+"Set 08/Tree_02__1_.prefab",-8.3f,-13.5f,160,.75f);
foreach(float side in new[]{-1f,1f}){foreach(float z in new[]{-16f,-19f,-23.8f}){Prop("BorderPlanting","Plant_bush_B",side*8.8f,z,40,.9f);Prop("BorderPlanting","Plant_fern",side*8.1f,z-.6f,75,.8f);Prop("BorderPlanting","Plant_flowers_white",side*8.2f,z+.7f,20,.85f);}foreach(float z in new[]{-11f,-17.5f,-24f})Prop("GardenLights","Lamp_garden_pilar",side*9,z,0,.85f);}
foreach(float x in new[]{-6f,-3f,0f,3f,6f}){Prop("RearFlowerBorder","Plant_bush_hedge",x,-25.1f,0,.75f);Prop("RearFlowerBorder","Plant_flowers_purple",x,-24.4f,20,.8f);}
// A quiet seating corner, leaving the broad central lawn open.
Prop("RestCorner","Sun_table",-6,-10.8f);Prop("RestCorner","Sun_umbrella",-6,-10.8f);Prop("RestCorner","Sun_chair",-7.15f,-10.8f,90);Prop("RestCorner","Sun_chair",-4.85f,-10.8f,270);
Prop("RestCorner","Plant_box_A",-8.5f,-10.4f,35,.65f);
foreach(float x in new[]{-5.8f,5.8f}){Prop("RearFlowerIslands","Flowerbed_round",x,-20.8f,0,.8f);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Prop("RearFlowerIslands",i%2==0?"Plant_flowers_purple":"Plant_flowers_white",x+.38f*Mathf.Cos(a),-20.8f+.38f*Mathf.Sin(a),i*60,.55f,.27f);}}
// Small trees on the front corner lawns, outside the existing Garden decorations.
Add("FrontCornerTrees",T+"Set 08/Tree_02__1_.prefab",-8.6f,8.5f,25,.55f);Add("FrontCornerTrees",T+"Set 08/Tree_02__1_.prefab",8.6f,8.5f,190,.55f);
Undo.CollapseUndoOperations(ug);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);Selection.activeGameObject=root.gameObject;
Render(new Vector3(-23,24,-40),new Vector3(0,0,-12),"Temp/garden-redo-back.png");Render(new Vector3(22,18,26),new Vector3(0,1,-5),"Temp/garden-redo-front.png");File.WriteAllText("Temp/garden-redo-finished.txt","Added "+count+" prefabs; previous garden archived inactive; saved.");}
static void Render(Vector3 eye,Vector3 target,string path){var go=new GameObject("preview");var c=go.AddComponent<Camera>();c.transform.position=eye;c.transform.LookAt(target);c.fieldOfView=55;var rt=RenderTexture.GetTemporary(1200,900,24);var old=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tx=new Texture2D(1200,900,TextureFormat.RGB24,false);tx.ReadPixels(new Rect(0,0,1200,900),0,0);tx.Apply();File.WriteAllBytes(path,tx.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tx);Object.DestroyImmediate(go);}
}
#endif
