using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoardPhotoSetup
{
    const string Folder="Assets/LivingRoom/BoardGame";
    static readonly string[] Files={"Gyeoul_Close_Up","Gyeoul_Ball","Gyeoul_Birthday_Party","Gyeoul_Eating_Treat","Gyeoul_Sleeping","Gyeoul_Sleeping_In_Bed","Gyeoul_Sleeping_On_Bed","Gyeoul_Soaking_We","Gyeoul_Walking_With_Harness","GyeoulFrame","11_Gyeoul_With_One_Hand","12_Travel_Card","13_Concert_Tickets","14_Valley_Plan","15_Kindergarten_Award","16_Agility_Medal","17_Medicine_Schedule","18_Hospital_Memo_February_Transparent","19_Cute_Note","20_Late_Note","21_Paw_Print"};
    static readonly string[] Names={"겨울이_클로즈업","겨울이_공놀이","겨울이_생일파티","겨울이_간식","겨울이_배꼽낮잠","겨울이_방석낮잠","겨울이_침대낮잠","겨울이_목욕","겨울이_산책","겨울이_어질리티","겨울이_손에올린앞발","여행_기념카드","공연_티켓","계곡_여행계획","겨울이_유치원상장","겨울이_어질리티메달","겨울이_약시간메모","겨울이_병원메모","친구메모_겨울이귀여워","친구메모_다음엔늦지마","겨울이_발도장"};
    static BoardPhotoSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/Editor/Photos.request"))return;
        File.Delete(Folder+"/Editor/Photos.request");
        try { Apply(); }catch(Exception e){File.WriteAllText(Folder+"/PhotoReport.txt","FAILED: "+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Apply Gyeoul images")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open Room3_LivingRoom first.");
        var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BoardGameController>(true)).Single();
        if(game.items.Length!=21)throw new Exception("Expected 21 items.");
        // Use the purchased frame's current visible surface, including any later scene movement.
        var frame=game.boardTarget.GetComponentInChildren<MeshFilter>();
        if(!frame || !frame.sharedMesh)throw new Exception("Board mesh missing.");
        var bounds=frame.sharedMesh.bounds;var rotation=game.surface.rotation;var measured=new Bounds();
        for(int c=0;c<8;c++)
        {
            var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
            var p=Quaternion.Inverse(rotation)*(frame.transform.TransformPoint(corner)-frame.transform.position);
            if(c==0)measured=new Bounds(p,Vector3.zero);else measured.Encapsulate(p);
        }
        Undo.RecordObjects(new UnityEngine.Object[]{game,game.surface},"Align placement surface with painting");
        game.surface.position=frame.transform.position+rotation*new Vector3(measured.center.x,measured.center.y,measured.max.z+.006f);
        game.boardSize=new Vector2(measured.size.x*.9f,measured.size.y*.9f);
        foreach(var file in Files)if(!AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Gyeoul/"+file+".png"))throw new Exception("Missing image: "+file);
        Directory.CreateDirectory(Folder+"/Materials/Photos");Directory.CreateDirectory(Folder+"/Meshes/Photos");AssetDatabase.Refresh();
        var report=new System.Text.StringBuilder();
        for(int n=0;n<21;n++)
        {
            var item=game.items.Single(i=>i.id=="photo"+(n+1));
            string path=Folder+"/Gyeoul/"+Files[n]+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true; importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=true;
            importer.isReadable=true; importer.npotScale=TextureImporterNPOTScale.None; importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Bilinear; importer.mipmapEnabled=true; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); var pixels=texture.GetPixels32();
            int minX=texture.width,minY=texture.height,maxX=-1,maxY=-1;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>=128){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            if(maxX<minX)throw new Exception("Empty texture: "+path);
            float ratio=(float)(maxX-minX+1)/(maxY-minY+1);
            // Photos are the main memories; notes and medal are visibly smaller.
            float longest=n<11?.37f:n==14?.39f:n==15?.25f:n==12?.34f:n>=16?.28f:.35f;
            var size=ratio>=1?new Vector2(longest,longest/ratio):new Vector2(longest*ratio,longest);
            Undo.RecordObjects(new UnityEngine.Object[]{item,item.transform,item.gameObject},"Apply bulletin board image");
            item.name=Names[n]; item.size=size;
            var scale=item.transform.lossyScale;
            if(scale.x<=0 || scale.y<=0 || scale.z<=0)throw new Exception("Positive scale required.");
            var visual=item.transform.Find("Visual");if(!visual)throw new Exception("Missing Visual: "+item.id);
            Undo.RecordObject(visual,"Fit photo geometry"); visual.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);visual.localPosition=new Vector3(0,0,.005f/scale.z);visual.localRotation=Quaternion.identity;
            var mesh=new Mesh {name=Files[n]};
            float w=size.x*.5f,h=size.y*.5f;
            mesh.vertices=new[]{new Vector3(-w,-h,0),new Vector3(w,-h,0),new Vector3(w,h,0),new Vector3(-w,h,0)};
            // The paper faces +Z; reverse U so its front reads correctly from that side.
            float u0=(float)minX/texture.width,u1=(float)(maxX+1)/texture.width,v0=(float)minY/texture.height,v1=(float)(maxY+1)/texture.height;
            mesh.uv=new[]{new Vector2(u1,v0),new Vector2(u0,v0),new Vector2(u0,v1),new Vector2(u1,v1)};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
            string meshPath=Folder+"/Meshes/Photos/"+Files[n]+".asset";
            var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(savedMesh){EditorUtility.CopySerialized(mesh,savedMesh);UnityEngine.Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,meshPath);savedMesh=mesh;}
            Undo.RecordObject(visual.GetComponent<MeshFilter>(),"Apply photo mesh");visual.GetComponent<MeshFilter>().sharedMesh=savedMesh;
            string materialPath=Folder+"/Materials/Photos/"+Files[n]+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.SetFloat("_Cull",0);material.EnableKeyword("_ALPHATEST_ON");material.renderQueue=(int)RenderQueue.AlphaTest;material.SetOverrideTag("RenderType","TransparentCutout");EditorUtility.SetDirty(material);
            var renderer=visual.GetComponent<MeshRenderer>();Undo.RecordObject(renderer,"Apply photo material");renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            foreach(var label in item.GetComponentsInChildren<TMPro.TMP_Text>(true)){Undo.RecordObject(label.gameObject,"Hide placeholder name");label.gameObject.SetActive(false);}
            var box=item.hitbox as BoxCollider;if(!box)throw new Exception("Expected BoxCollider");Undo.RecordObject(box,"Fit photo click area");box.size=new Vector3(size.x/scale.x,size.y/scale.y,.012f/scale.z);box.center=Vector3.zero;
            Undo.RecordObject(item.outline,"Fit photo outline");var corners=BoardGeometry.Corners(Vector2.zero,size,0);for(int c=0;c<4;c++)item.outline.SetPosition(c,new Vector3(corners[c].x/scale.x,corners[c].y/scale.y,.009f/scale.z));
            if(item.state==BoardItemState.Board)
            {
                Vector3 p=game.surface.InverseTransformPoint(item.transform.position);float a=item.angle*Mathf.Deg2Rad;
                float ex=(Mathf.Abs(Mathf.Cos(a))*size.x+Mathf.Abs(Mathf.Sin(a))*size.y)*.5f,ey=(Mathf.Abs(Mathf.Sin(a))*size.x+Mathf.Abs(Mathf.Cos(a))*size.y)*.5f;
                p.x=Mathf.Clamp(p.x,-game.boardSize.x*.5f+ex+.002f,game.boardSize.x*.5f-ex-.002f);p.y=Mathf.Clamp(p.y,-game.boardSize.y*.5f+ey+.002f,game.boardSize.y*.5f-ey-.002f);
                item.transform.position=game.surface.TransformPoint(p);item.boardPosition=new Vector2(p.x,p.y);
                if(!BoardGeometry.Fits(item.boardPosition,item.size,item.angle,game.boardSize))throw new Exception("Item outside board: "+item.name);
            }
            EditorUtility.SetDirty(item);
            PrefabUtility.SaveAsPrefabAsset(item.gameObject,Folder+"/Prefabs/"+item.id+".prefab");
            importer.isReadable=false;importer.SaveAndReimport();
            report.AppendLine(item.id+" -> "+item.name+" : "+Files[n]+"; size="+size+"; crop="+minX+","+minY+","+maxX+","+maxY);
        }
        SpreadItems(game);
        foreach(var item in game.items) PrefabUtility.SaveAsPrefabAsset(item.gameObject,Folder+"/Prefabs/"+item.id+".prefab");
        game.RefreshOverlap();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");AssetDatabase.SaveAssets();
        BoardGameSetup.Preview(game);
        File.WriteAllText(Folder+"/PhotoReport.txt","PASS: 21 unique images assigned; aspect ratios and alpha bounds preserved; 21 click areas and outlines fitted; labels hidden; stable item IDs and initial board/floor states preserved.\n"+report);
    }
    static void SpreadItems(BoardGameController game)
    {
        // Mix keepsakes with photos, leaving space for the player to arrange the floor items.
        int[] board={0,14,2,18,8,11,1,15,10,12,4,17,9,20};
        Vector2[] points={new Vector2(-1.23f,.49f),new Vector2(-.61f,.51f),new Vector2(.02f,.47f),new Vector2(.64f,.52f),new Vector2(1.23f,.48f),new Vector2(-1.20f,.01f),new Vector2(-.60f,-.01f),new Vector2(.01f,.01f),new Vector2(.63f,-.02f),new Vector2(1.21f,.02f),new Vector2(-1.03f,-.48f),new Vector2(-.35f,-.50f),new Vector2(.36f,-.47f),new Vector2(1.04f,-.48f)};
        for(int k=0;k<board.Length;k++)
        {
            var item=game.items.Single(i=>i.id=="photo"+(board[k]+1));
            Undo.RecordObjects(new UnityEngine.Object[]{item,item.transform},"Spread board keepsakes");
            item.state=BoardItemState.Board;item.boardPosition=Vector2.Scale(points[k],new Vector2(game.boardSize.x/3.202f,game.boardSize.y/1.642f));item.angle=new[]{-6f,3f,-4f,7f,-5f}[k%5];item.order=k;
            item.transform.SetPositionAndRotation(game.surface.TransformPoint(new Vector3(item.boardPosition.x,item.boardPosition.y,.016f+k*.001f)),game.surface.rotation*Quaternion.Euler(0,0,item.angle));
            if(!BoardGeometry.Fits(item.boardPosition,item.size,item.angle,game.boardSize))throw new Exception("Spread item outside board: "+item.name);
            item.SetOutline(false);EditorUtility.SetDirty(item);
        }
        int floor=0;
        foreach(var item in game.items.Where(i=>!board.Contains(int.Parse(i.id.Substring(5))-1)))
        {
            Undo.RecordObjects(new UnityEngine.Object[]{item,item.transform},"Scatter floor keepsakes");
            item.state=BoardItemState.Floor;item.angle=new[]{-23f,16f,-9f,28f,-17f,8f,21f}[floor];
            float x=-1.20f+floor*.40f; float outward=new[]{.48f,.88f,.60f,1.12f,.73f,.45f,.98f}[floor++];
            Vector3 origin=game.surface.position+game.surface.right*x+game.surface.forward*outward;
            Vector3 position=origin;position.y=.03f;
            Physics.SyncTransforms();
            foreach(var hit in Physics.RaycastAll(origin,Vector3.down,6,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider.GetComponentInParent<BoardItem>() || hit.collider.GetComponentInParent<BoardTarget>() || hit.normal.y<.9f || hit.point.y>game.surface.position.y-.8f)continue;
                position=hit.point+Vector3.up*.015f;break;
            }
            item.transform.SetPositionAndRotation(position,Quaternion.Euler(-90,0,item.angle));item.SetOutline(false);EditorUtility.SetDirty(item);
        }
    }
}
