using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoardLampSetup
{
    const string Folder="Assets/LivingRoom/BoardGame";
    static BoardLampSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Folder+"/Editor/Lamp.request"))return;
        string action=File.ReadAllText(Folder+"/Editor/Lamp.request").Trim();
        File.Delete(Folder+"/Editor/Lamp.request");
        try{if(action=="center")ApplyCenterFill();else if(action=="point")ApplyPoint();else Apply();}catch(Exception e){File.WriteAllText(Folder+"/LampReport.txt","FAILED: "+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Add soft center fill light")]
    public static void ApplyCenterFill()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open living room scene.");
        var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BoardGameController>(true)).Single();
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/CenterFillBefore.png",true);
        var existing=game.transform.Find("BoardCenterFillLight");GameObject go;
        if(existing)go=existing.gameObject;else{go=new GameObject("BoardCenterFillLight");Undo.RegisterCreatedObjectUndo(go,"Add board center fill");go.transform.SetParent(game.transform,false);}
        Undo.RecordObject(go.transform,"Place board fill light");
        // Stand off the board so falloff is broad, rather than a small bright hotspot.
        go.transform.position=game.surface.TransformPoint(new Vector3(0,-.10f,.9f));
        var light=go.GetComponent<Light>();if(!light)light=Undo.AddComponent<Light>(go);
        Undo.RecordObject(light,"Configure subtle board fill");
        light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=new Color(1f,.92f,.82f);light.intensity=.35f;light.range=3.5f;
        light.shadows=LightShadows.None;light.cullingMask=-1;light.enabled=true;
        EditorUtility.SetDirty(light);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/CenterFillAfter.png",true);
        File.WriteAllText(Folder+"/CenterFillReport.txt","PASS: BoardCenterFillLight under BoardGame; realtime Point; intensity=0.35; range=3.5; warm neutral tint; no shadows; position="+go.transform.position+"; board-center offset=(0,-0.1,0.9); scene saved.\n");
        Selection.activeGameObject=go;
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Add second desk lamp point light")]
    public static void ApplyPoint()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open living room scene.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var lamp=all.Single(t=>t.name=="Lamp_wall_desk (1)");
        var game=all.Select(t=>t.GetComponent<BoardGameController>()).First(g=>g);
        var renderers=lamp.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new Exception("Lamp mesh missing.");
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/PointLampBefore.png",true);
        var existing=lamp.Find("BoardPointLight");GameObject go;
        if(existing)go=existing.gameObject;else{go=new GameObject("BoardPointLight");Undo.RegisterCreatedObjectUndo(go,"Add second lamp point light");go.transform.SetParent(lamp,false);}
        Undo.RecordObject(go.transform,"Place point light outside shade");
        go.transform.position=bounds.center+game.surface.forward*.18f+Vector3.down*.06f;
        var light=go.GetComponent<Light>();if(!light)light=Undo.AddComponent<Light>(go);
        Undo.RecordObject(light,"Configure warm point light");
        light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=new Color(1,.88f,.74f);light.intensity=1.5f;light.range=3;
        light.shadows=LightShadows.Soft;light.shadowStrength=.6f;light.shadowNormalBias=.15f;light.shadowBias=.03f;light.cullingMask=-1;light.enabled=true;
        EditorUtility.SetDirty(light);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/PointLampAfter.png",true);
        File.WriteAllText(Folder+"/PointLampReport.txt","PASS: realtime Point Light under Lamp_wall_desk (1); warm color; intensity=1.5; range=3; soft shadows; position="+go.transform.position+"; scene saved.\n");
        Selection.activeGameObject=go;
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Add desk lamp spotlight")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open living room scene.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var lamps=all.Where(t=>t.name=="Lamp_wall_desk").ToArray();
        if(lamps.Length!=1)throw new Exception("Expected one Lamp_wall_desk; found "+lamps.Length);
        var lamp=lamps[0];var game=all.Select(t=>t.GetComponent<BoardGameController>()).First(g=>g);
        var renderers=lamp.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new Exception("Lamp mesh missing.");
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/LampBefore.png",true);
        var existing=lamp.Find("BoardSpotLight");
        GameObject go;
        if(existing)go=existing.gameObject;else{go=new GameObject("BoardSpotLight");Undo.RegisterCreatedObjectUndo(go,"Add desk lamp spotlight");go.transform.SetParent(lamp,false);}
        Undo.RecordObject(go.transform,"Aim board spotlight");
        Vector3 direction=(game.surface.position-bounds.center).normalized;
        go.transform.position=bounds.center+direction*.08f+game.surface.forward*.22f;
        go.transform.rotation=Quaternion.LookRotation(game.surface.position-go.transform.position);
        var light=go.GetComponent<Light>();
        if(!light) light=Undo.AddComponent<Light>(go);
        Undo.RecordObject(light,"Configure warm spotlight");
        light.type=LightType.Spot;light.lightmapBakeType=LightmapBakeType.Realtime;light.color=new Color(1f,.88f,.74f);light.intensity=2.5f;
        light.range=Mathf.Max(4,Vector3.Distance(go.transform.position,game.surface.position)+2);
        light.spotAngle=105;light.innerSpotAngle=70;light.shadows=LightShadows.Soft;light.shadowStrength=.7f;light.shadowNormalBias=.2f;light.shadowBias=.03f;light.cullingMask=-1;light.enabled=true;
        EditorUtility.SetDirty(light);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/LampAfter.png",true);
        File.WriteAllText(Folder+"/LampReport.txt","PASS: one realtime Spot Light under Lamp_wall_desk; warm color; intensity="+light.intensity+"; range="+light.range+"; angle="+light.spotAngle+"; position="+go.transform.position+"; lamp bounds="+bounds+"; aimed at board="+game.surface.position+". Soft shadows; scene saved.\n");
        Selection.activeGameObject=go;
    }
}
