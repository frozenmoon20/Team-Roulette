using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LivingRoomGridRebuild
{
    const string Home="Assets/LivingRoom";
    const string Request=Home+"/Editor/Rebuild.request.txt";
    static readonly StringBuilder report=new StringBuilder();
    static Material wall,floor;
    static LivingRoomGridRebuild(){ EditorApplication.update+=Run; }
    static float Snap(float v)=>Mathf.Round(v*2)/2;
    static Vector3 Snap(Vector3 v)=>new Vector3(Snap(v.x),Snap(v.y),Snap(v.z));
    static Transform Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static GameObject Cube(Transform p,string name,Vector3 position,Vector3 size,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.transform.SetParent(p,false);
        g.transform.localPosition=position;g.transform.localScale=size;
        g.name=name+" ["+size.x+"x"+size.y+"x"+size.z+"]";
        if(mat)g.GetComponent<Renderer>().sharedMaterial=mat;
        return g;
    }
    static void Box(Transform p,string name,float x0,float x1,float y0,float y1,float z0,float z1,Material mat)
    {
        Cube(p,name,new Vector3((x0+x1)/2,(y0+y1)/2,(z0+z1)/2),new Vector3(x1-x0,y1-y0,z1-z0),mat);
    }
    // Opening boundaries are integers so both centers and sizes stay on the half-unit grid.
    static void Wall(Transform p,string name,bool x,float from,float to,float cross,params Vector4[] holes)
    {
        var g=Group(name,p);float cursor=from;int i=0;
        Action<float,float,float,float,string> part=(a,b,lo,hi,label)=>{
            if(b<=a||hi<=lo)return;
            if(x)Box(g,(++i)+"_"+label,a,b,lo,hi,cross,cross+1,wall);
            else Box(g,(++i)+"_"+label,cross,cross+1,lo,hi,a,b,wall);
        };
        foreach(var h in holes.OrderBy(v=>v.x)){
            part(cursor,h.x,0,4,"벽");part(h.x,h.y,0,h.z,"창아래벽");part(h.x,h.y,h.w,4,"상부벽");
            var m=Group("개구부_폭"+(h.y-h.x)+"_높이"+(h.w-h.z),g);
            m.localPosition=x?new Vector3((h.x+h.y)/2,(h.z+h.w)/2,cross+.5f):new Vector3(cross+.5f,(h.z+h.w)/2,(h.x+h.y)/2);
            cursor=h.y;
        }
        part(cursor,to,0,4,"벽");
    }
    static void MoveAsset(string from,string to)
    {
        if(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(from)))return;
        if(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(to)))throw new Exception("Destination already exists: "+to);
        var error=AssetDatabase.MoveAsset(from,to);if(error!="")throw new Exception(error);
        report.AppendLine("Moved: "+from+" -> "+to);
    }
    static void Run()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().path!="Assets/Scenes/Room3_LivingRoom.unity")return;
        EditorApplication.update-=Run;
        MoveAsset(Request,Home+"/Editor/Rebuild.consumed-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt");
        Directory.CreateDirectory(Home+"/Reports");
        try{Build();}catch(Exception e){report.AppendLine("FAILED: "+e);Debug.LogException(e);}
        File.WriteAllText(Home+"/Reports/Rebuild.txt",report.ToString());
        AssetDatabase.Refresh();
    }
    static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        var root=scene.GetRootGameObjects().Single(g=>g.name=="LivingRoom").transform;
        if(root.position!=Vector3.zero||root.rotation!=Quaternion.identity||root.lossyScale!=Vector3.one)throw new Exception("Unexpected root transform; preserve layout rather than guessing.");
        string[] names={"Sofa","Window","세면대","Table","TV","Calender","게시판","Toys","Door"};
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var props=names.Select(n=>all.Single(t=>t.name==n)).ToArray();
        foreach(var t in props)if(!t.GetComponent<MeshFilter>()||!t.GetComponent<MeshFilter>().sharedMesh)throw new Exception("Missing user object mesh: "+t.name);
        int[] ids=props.Select(p=>p.GetInstanceID()).ToArray();
        int[] componentCounts=props.Select(p=>p.GetComponents<Component>().Length).ToArray();
        // Preserve GUIDs and material references while consolidating assistant-created assets.
        MoveAsset("Assets/Blockouts/LivingRoom",Home+"/Materials");
        MoveAsset("Assets/Scripts/Editor/LivingRoomBlockoutBuilder.cs",Home+"/Editor/LivingRoomBlockoutBuilder.cs");
        if(AssetDatabase.IsValidFolder("Assets/Blockouts")&&Directory.GetFileSystemEntries("Assets/Blockouts").Length==0)AssetDatabase.DeleteAsset("Assets/Blockouts");
        if(File.Exists("Library/LivingRoomBlockout.report.txt")){
            File.Move("Library/LivingRoomBlockout.report.txt",Home+"/Reports/OriginalBuild.txt");
        }
        foreach(var archiveFile in Directory.GetFiles("Library","LivingRoomBlockout.request.consumed-*"))
            File.Move(archiveFile,Home+"/Reports/"+Path.GetFileName(archiveFile)+".txt");
        wall=AssetDatabase.LoadAssetAtPath<Material>(Home+"/Materials/Blockout_Wall.mat");
        floor=AssetDatabase.LoadAssetAtPath<Material>(Home+"/Materials/Blockout_Floor.mat");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("LivingRoom half-unit blockout rebuild");
        var propGroup=Group("07_TriggerObjects_사용자큐브유지",root);
        foreach(var t in props){
            Vector3 before=t.position;Vector3 scale=t.localScale;
            Undo.SetTransformParent(t,propGroup,"Preserve user trigger cubes");Undo.RecordObject(t,"Snap trigger cube");
            t.position=Snap(before);t.localScale=Snap(scale);
            report.AppendLine("Preserved cube "+t.name+" (#"+t.GetInstanceID()+"): "+before+" -> "+t.position+"; size "+t.localScale);
        }
        // Only replace known generated geometry; user objects have already been preserved above.
        foreach(var t in root.Cast<Transform>().ToArray())
            if(new[]{"01_","02_","03_","04_","05_","06_"}.Any(prefix=>t.name.StartsWith(prefix)))Undo.DestroyObjectImmediate(t.gameObject);
        var f=Group("01_Floors_전체40x16_바닥윗면0",root);
        Cube(f,"전체바닥",new Vector3(20,-.5f,8),new Vector3(40,1,16),floor);
        // Rooms use empties instead of paper-thin overlapping floor plates.
        foreach(var r in new[]{new Vector4(4,11,6,8),new Vector4(14.5f,11,13,8),new Vector4(7.5f,3.5f,13,5),new Vector4(22.5f,3.5f,15,5),new Vector4(33.5f,11,11,8),new Vector4(35,3.5f,8,5)})
        {
            string[] roomNames={"현관","거실","방2","공용복도","주방","화장실"};
            int index=f.childCount-1;
            Group(roomNames[index]+"_내부"+r.z+"x"+r.w,f).localPosition=new Vector3(r.x,0,r.y);
        }
        var exterior=Group("02_ExteriorWalls_두께1_높이4",root);
        Wall(exterior,"앞벽_40",true,0,40,0,new Vector4(5,8,1,3),new Vector4(18,21,1,3));
        // User resized Window to x=13..20, y=0..3 and Door to z=8..10, y=0..3.
        Wall(exterior,"뒤벽_40_사용자Window폭7",true,0,40,15,new Vector4(13,20,0,3),new Vector4(33,36,1,3));
        Wall(exterior,"왼벽_14_사용자Door위치반영",false,1,15,0,new Vector4(8,10,0,3));
        Wall(exterior,"오른벽_14",false,1,15,39);
        var inside=Group("03_InteriorWalls_두께1_높이4",root);
        Wall(inside,"현관-거실_통로3",false,7,15,7,new Vector4(7,10,0,4));
        Wall(inside,"거실-계단",false,7,15,21);
        Wall(inside,"통로-주방_입구4",false,7,15,27,new Vector4(7,11,0,4));
        Wall(inside,"방2_뒤벽_문2x3",true,1,15,6,new Vector4(10,12,0,3));
        Wall(inside,"방2_오른벽",false,1,6,14);
        Wall(inside,"화장실_뒤벽",true,30,39,6);
        Wall(inside,"화장실_왼벽_문2x3",false,1,6,30,new Vector4(2,4,0,3));
        var stairs=Group("04_Stairs_폭3_상승4_8단_단높이0.5",root);
        for(int i=0;i<8;i++){
            Cube(stairs,"계단_"+(i+1).ToString("00")+"_윗면"+((i+1)*.5f),new Vector3(23.5f,i*.5f,7+i),new Vector3(3,1,1),floor);
        }
        Cube(stairs,"상부참_윗면4",new Vector3(23.5f,3.5f,14.5f),new Vector3(3,1,1),floor);
        // Coarse cube side guards, no fine railings or off-grid angled beams.
        for(int i=0;i<8;i++)Cube(stairs,"오른쪽난간표시_"+(i+1),new Vector3(25.5f,1+i*.5f,7+i),new Vector3(1,1,1),wall);
        var refs=Group("05_References_기준치수",root);
        var human=GameObject.CreatePrimitive(PrimitiveType.Capsule);human.name="사람_1x2x1_크기비교_충돌없음";human.transform.SetParent(refs,false);human.transform.localPosition=new Vector3(16,1,10);human.GetComponent<Collider>().enabled=false;
        Group("계단옆통로_폭1.5_난간포함",refs).localPosition=new Vector3(26.5f,0,11);
        Group("거실-공용복도_통로폭6",refs).localPosition=new Vector3(18,0,6.5f);
        var roof=Group("06_Ceiling_아랫면높이4_확인시활성화",root);
        Cube(roof,"천장_왼쪽",new Vector3(10.5f,4.5f,8),new Vector3(21,1,16),wall);
        Cube(roof,"천장_오른쪽",new Vector3(33,4.5f,8),new Vector3(14,1,16),wall);
        Cube(roof,"천장_앞쪽",new Vector3(23.5f,4.5f,3),new Vector3(5,1,6),wall);
        Cube(roof,"천장_뒤쪽",new Vector3(23.5f,4.5f,15.5f),new Vector3(5,1,1),wall);
        roof.gameObject.SetActive(false);
        Validate(root,props,ids,componentCounts);
        Undo.CollapseUndoOperations(undo);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
        Selection.activeGameObject=root.gameObject;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(20,0,8),Quaternion.Euler(60,0,0),24,true);
        report.AppendLine("SUCCESS: saved existing scene; no scene backup; nine original cubes preserved; all LivingRoom local/world positions and scales use half units; wall tops=4.");
        Debug.Log("LivingRoom rebuilt: "+Home+"/Reports/Rebuild.txt");
    }
    static void Validate(Transform root,Transform[] props,int[] ids,int[] counts)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true)){
            if((t.position-Snap(t.position)).sqrMagnitude>.000001f||(t.localPosition-Snap(t.localPosition)).sqrMagnitude>.000001f||(t.localScale-Snap(t.localScale)).sqrMagnitude>.000001f)throw new Exception("Off-grid transform: "+t.name);
            var mesh=t.GetComponent<MeshFilter>();if(mesh&&!props.Contains(t)&&mesh.sharedMesh.name!="Cube"&&mesh.sharedMesh.name!="Capsule")throw new Exception("Unexpected mesh: "+t.name);
        }
        for(int i=0;i<props.Length;i++)if(props[i].GetInstanceID()!=ids[i]||props[i].GetComponents<Component>().Length!=counts[i])throw new Exception("User object identity/components changed");
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer.transform.IsChildOf(root.Find("02_ExteriorWalls_두께1_높이4"))||renderer.transform.IsChildOf(root.Find("03_InteriorWalls_두께1_높이4"))){
            if(renderer.bounds.max.y>4.001f)throw new Exception("Wall above height 4");
        }
        report.AppendLine("Verified cube identity + component counts, primitive meshes, half-unit transforms and wall heights.");
    }
}
