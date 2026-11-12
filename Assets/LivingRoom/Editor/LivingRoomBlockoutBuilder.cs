using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only, one-shot builder. The resulting scene contains only ordinary primitives/empties.
[InitializeOnLoad]
public static class LivingRoomBlockoutBuilder
{
    const string Request = "Library/LivingRoomBlockout.request";
    const string Report = "Library/LivingRoomBlockout.report.txt";
    const string Target = "Assets/Scenes/Room3_LivingRoom.unity";
    const string AssetsFolder = "Assets/Blockouts/LivingRoom";
    static Material wall, wood, tile, accent;
    static readonly StringBuilder log = new StringBuilder();

    static LivingRoomBlockoutBuilder() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Poll;
        File.Move(Request, Request + ".consumed-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        try { Build(); }
        catch (Exception e) { File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
    }

    static Transform Group(string name, Transform parent)
    {
        var obj = new GameObject(name);
        if (parent) obj.transform.SetParent(parent, false);
        return obj.transform;
    }
    static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = mat;
        return obj;
    }
    static GameObject Box(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, Material mat)
    {
        if (x1 <= x0 || y1 <= y0 || z1 <= z0) throw new InvalidOperationException("Invalid dimensions: " + name);
        return Cube(parent, name, new Vector3((x0+x1)/2, (y0+y1)/2, (z0+z1)/2), new Vector3(x1-x0,y1-y0,z1-z0), mat);
    }
    static Material Mat(string name, Color color)
    {
        string path = AssetsFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        mat = new Material(shader) { name = name, color = color };
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .1f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
    // Opening tuple: along-wall start/end, bottom/top. Full-height openings remain truly empty.
    static void Wall(Transform parent, string name, bool alongX, float from, float to, float cross, params Vector4[] holes)
    {
        var group = Group(name, parent);
        float cursor = from;
        int part = 0;
        Action<float,float,float,float,string> segment = (a,b,lo,hi,label) => {
            if (b-a < .001f || hi-lo < .001f) return;
            if (alongX) Box(group, (++part)+"_"+label, a,b,lo,hi,cross,cross+1,wall);
            else Box(group, (++part)+"_"+label,cross,cross+1,lo,hi,a,b,wall);
        };
        foreach (var h in holes.OrderBy(v => v.x))
        {
            segment(cursor,h.x,0,3.7f,"벽");
            segment(h.x,h.y,0,h.z,"창문_아래벽");
            segment(h.x,h.y,h.w,3.7f,"개구부_위벽");
            var marker = Group("개구부_폭"+(h.y-h.x).ToString("0.##")+"_높이"+(h.w-h.z).ToString("0.##")+"_아래높이"+h.z.ToString("0.##"),group);
            marker.localPosition = alongX ? new Vector3((h.x+h.y)/2,(h.z+h.w)/2,cross+.5f) : new Vector3(cross+.5f,(h.z+h.w)/2,(h.x+h.y)/2);
            cursor = h.y;
        }
        segment(cursor,to,0,3.7f,"벽");
    }
    static void Beam(Transform parent, string name, Vector3 a, Vector3 b)
    {
        var obj = Cube(parent,name,(a+b)/2,new Vector3(.1f,(b-a).magnitude,.1f),accent);
        obj.transform.localRotation = Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
    }
    static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != Target) throw new InvalidOperationException("Expected " + Target + ", active " + scene.path);
        var oldRoots = scene.GetRootGameObjects();
        if (oldRoots.Any(o => o.name == "LivingRoom" && o.transform.Find("01_Floors_전체40x16"))) throw new InvalidOperationException("Blockout already exists; refusing to duplicate.");
        Directory.CreateDirectory(AssetsFolder);
        AssetDatabase.Refresh();
        wall = Mat("Blockout_Wall",new Color(.69f,.72f,.74f));
        wood = Mat("Blockout_Floor",new Color(.48f,.36f,.24f));
        tile = Mat("Blockout_WetFloor",new Color(.32f,.43f,.47f));
        accent = Mat("Blockout_Reference",new Color(.95f,.53f,.18f));
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create LivingRoom primitive blockout");
        var root = Group("LivingRoom",null);
        SceneManager.MoveGameObjectToScene(root.gameObject,scene);
        foreach (var obj in oldRoots)
        {
            if (!(obj.name == "LivingRoom" || obj.name == "Floor" || obj.name == "Capsule" || obj.name == "Wall" || obj.name.StartsWith("Wall ("))) continue;
            if (obj.GetComponentsInChildren<MonoBehaviour>(true).Length != 0) { log.AppendLine("Preserved scripted root: "+obj.name); continue; }
            log.AppendLine("Replaced previous geometry: " + obj.name);
            Undo.DestroyObjectImmediate(obj);
        }
        var floors = Group("01_Floors_전체40x16",root);
        Box(floors,"전체바닥_40x16_두께0.3_윗면Y0",0,40,-.3f,0,0,16,wood);
        Box(floors,"현관_내부6x8",1,7,0,.01f,7,15,tile);
        Box(floors,"거실_내부13x8",8,21,0,.01f,7,15,wood);
        Box(floors,"방2_내부13x5",1,14,0,.01f,1,6,wood);
        Box(floors,"공용복도_내부15x5",15,30,0,.01f,1,6,wood);
        Box(floors,"주방_내부11x8",28,39,0,.01f,7,15,tile);
        Box(floors,"화장실_내부8x5",31,39,0,.01f,1,6,tile);
        var ext = Group("02_ExteriorWalls_두께1_높이3.7",root);
        Wall(ext,"앞벽_길이40_방창3x1.6_복도창3x1.6",true,0,40,0,new Vector4(5,8,1,2.6f),new Vector4(18,21,1,2.6f));
        Wall(ext,"뒤벽_길이40_거실창5x1.8_주방창3x1.4",true,0,40,15,new Vector4(13,18,1,2.8f),new Vector4(33,36,1.3f,2.7f));
        Wall(ext,"왼벽_길이14_현관문2x2.6",false,1,15,0,new Vector4(7.5f,9.5f,0,2.6f));
        Wall(ext,"오른벽_길이14",false,1,15,39);
        var inner = Group("03_InteriorWalls_두께1_높이3.7",root);
        Wall(inner,"현관-거실_길이8_통로폭3",false,7,15,7,new Vector4(7,10,0,3.7f));
        Wall(inner,"거실-계단_길이8",false,7,15,21);
        Wall(inner,"통로-주방_길이8_입구폭4",false,7,15,27,new Vector4(7,11,0,3.7f));
        Wall(inner,"방2_뒤벽14_문2x2.6",true,1,15,6,new Vector4(10,12,0,2.6f));
        Wall(inner,"방2_오른벽5",false,1,6,14);
        Wall(inner,"화장실_뒤벽9",true,30,39,6);
        Wall(inner,"화장실_왼벽5_문1.6x2.6",false,1,6,30,new Vector4(2.5f,4.1f,0,2.6f));
        var stairs = Group("04_Stairs_폭3_길이7_상승4_20단",root);
        for (int i=1;i<=20;i++)
            Box(stairs,"계단_"+i.ToString("00")+"_폭3_디딤0.35_단높이0.2_윗면Y"+(i*.2f).ToString("0.0"),22,25,0,i*.2f,7+(i-1)*.35f,7+i*.35f,wood);
        Box(stairs,"상부참_3x1_윗면Y4",22,25,3.7f,4,14,15,wood);
        var rails = Group("난간_높이1.1_두께0.1_통로폭2",stairs);
        Beam(rails,"오른쪽_상부난간",new Vector3(25.05f,1.3f,7),new Vector3(25.05f,5.1f,14));
        Beam(rails,"오른쪽_중간난간",new Vector3(25.05f,.75f,7),new Vector3(25.05f,4.55f,14));
        for(int i=0;i<=5;i++)
        {
            float z=7+i*1.4f;
            float height = i==5 ? 4 : (Mathf.Floor((z-7)/.35f)+1)*.2f;
            Box(rails,"난간기둥_"+i,25,25.1f,height,height+1.1f,z-.05f,z+.05f,accent);
        }
        Beam(rails,"상부참_오른쪽난간",new Vector3(25.05f,5.1f,14),new Vector3(25.05f,5.1f,15));
        var refs = Group("05_References_가구없음",root);
        var human = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        human.name="사람크기비교_1x2x1_충돌없음";
        human.transform.SetParent(refs,false);
        human.transform.localPosition=new Vector3(16,1.01f,10);
        human.GetComponent<Renderer>().sharedMaterial=accent;
        human.GetComponent<Collider>().enabled=false;
        Group("빈통로_계단오른쪽_폭2_X25-27_Z7-15",refs).localPosition=new Vector3(26,0,11);
        Group("빈통로_거실-공용복도_폭6_X15-21",refs).localPosition=new Vector3(18,0,6.5f);
        Group("현관문_2x2.6_문짝생략",refs).localPosition=new Vector3(.5f,0,8.5f);
        var ceiling = Group("06_Ceiling_높이3.7_두께0.3_확인시활성화",root);
        Box(ceiling,"천장_왼쪽_21x16",0,21,3.7f,4,0,16,wall);
        Box(ceiling,"천장_오른쪽_14x16",26,40,3.7f,4,0,16,wall);
        Box(ceiling,"천장_앞쪽_5x6",21,26,3.7f,4,0,6,wall);
        Box(ceiling,"천장_뒤쪽_5x1",21,26,3.7f,4,15,16,wall);
        ceiling.gameObject.SetActive(false);
        Group("계단실개구부_5x9_X21-26_Z6-15",refs).localPosition=new Vector3(23.5f,4,10.5f);
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Create LivingRoom blockout");
        Undo.CollapseUndoOperations(undo);
        Physics.SyncTransforms();
        Validate(root,ceiling);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Final scene save failed.");
        Selection.activeGameObject=root.gameObject;
        var view=SceneView.lastActiveSceneView;
        if(view) view.LookAt(new Vector3(20,0,8),Quaternion.Euler(65,0,0),25,true);
        SceneView.RepaintAll();
        log.AppendLine("SUCCESS: saved " + scene.path);
        File.WriteAllText(Report,log.ToString());
        Debug.Log("LivingRoom blockout created and saved. " + Report);
    }
    static void Validate(Transform root,Transform ceiling)
    {
        int primitives=0;
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!mf.sharedMesh || !(mf.sharedMesh.name=="Cube" || mf.sharedMesh.name=="Capsule")) throw new Exception("Non-primitive mesh: "+mf.name);
            primitives++;
        }
        var checks = new[]{ new Vector3(.5f,1.3f,8.5f),new Vector3(11,1.3f,6.5f),new Vector3(30.5f,1.3f,3.3f),new Vector3(7.5f,1,8.5f),new Vector3(27.5f,1,9),new Vector3(18,1,6.5f) };
        foreach(var p in checks)
        {
            var hits=Physics.OverlapBox(p,new Vector3(.49f,.95f,.49f));
            if(hits.Any(c=>c.transform.IsChildOf(root))) throw new Exception("Blocked passage at "+p);
        }
        if(root.localScale!=Vector3.one) throw new Exception("Root scale changed");
        log.AppendLine("Validated: "+primitives+" primitive meshes; six passages clear for 1x2x1 reference; root scale 1; 20 steps, rise 4, run 7, width 3.");
        log.AppendLine("Ceiling intentionally inactive for layout review. No scene backup created. Player, UI and systems unchanged.");
    }
}
