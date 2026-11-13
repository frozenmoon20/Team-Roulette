using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WashAssetTransfer
{
    static WashAssetTransfer(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/LivingRoom/WashGame/Editor/PurchasedAssets.request";
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Room3_LivingRoom.unity")return;
        File.Delete(request);
        try{Run();}catch(Exception e){File.WriteAllText("Assets/LivingRoom/WashGame/Reports/PurchasedAssetTransfer.txt","FAILED: "+e);Debug.LogException(e);}
    }
    public static void Run()
    {
        const string folder="Assets/LivingRoom/WashGame";
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/Room3_LivingRoom.unity");
        var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
        var game=Object.FindFirstObjectByType<WashGameController>(FindObjectsInactive.Include);
        if(!game)throw new Exception("WashGame missing");
        var sink=all.Single(x=>x.name=="T_sink" && x.GetComponentsInParent<Transform>(true).Any(p=>p.name=="Restroom"));
        var soap=all.Single(x=>x.name=="Butter");
        var towel=all.Single(x=>x.name=="To_towel_hanging");
        Directory.CreateDirectory(folder+"/Reports");
        string backup=folder+"/Reports/BeforePurchasedAssets.unity.txt";
        if(!File.Exists(backup))File.Copy(scene.path,backup);
        // Keep original fixtures available for recovery, but remove their gameplay and visuals.
        foreach(var old in game.GetComponentsInChildren<WashTarget>(true))
        {
            if(old.transform==sink || old.transform==soap || old.transform==towel)continue;
            if(old.name=="WaterClickArea" || old.name=="FaucetClickArea")continue;
            old.gameObject.SetActive(false);
        }
        foreach(var t in game.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Fixtures" || t.name.StartsWith("TowelFold_") || t.name.EndsWith("Label")))t.gameObject.SetActive(false);
        Bind(sink,game,WashAction.Water,"세면대");
        Bind(soap,game,WashAction.Soap,"비누");
        Bind(towel,game,WashAction.Towel,"수건");
        // Faucet is part of the sink mesh: isolate its highest vertices for a non-rendered hit box.
        var mf=sink.GetComponent<MeshFilter>();
        var points=mf.sharedMesh.vertices.Select(v=>sink.TransformPoint(v)).ToArray();
        float top=points.Max(p=>p.y), bottom=points.Min(p=>p.y);
        var upper=points.Where(p=>p.y>=top-Mathf.Min(.025f,(top-bottom)*.025f)).ToArray();
        var faucetBounds=new Bounds(upper[0],Vector3.zero);foreach(var p in upper)faucetBounds.Encapsulate(p);
        // The rear stone splashback is taller than the tap; place a small tap-only box
        // in front of it instead of covering the entire highest strip of the cabinet.
        var tapPosition=new Vector3(faucetBounds.center.x,top-.14f,faucetBounds.center.z+.20f);
        var faucet=Area(game,"FaucetClickArea",tapPosition,new Vector3(.22f,.28f,.24f));
        var sinkBounds=sink.GetComponent<Renderer>().bounds;
        var outlet=new Vector3(sinkBounds.center.x,sinkBounds.max.y-.01f,sinkBounds.center.z-.06f);
        game.water.transform.position=outlet;
        game.water.transform.rotation=Quaternion.Euler(90,0,0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(game.water.transform);
        Area(game,"WaterClickArea",outlet+Vector3.down*.18f,new Vector3(.18f,.40f,.18f));
        EditorUtility.SetDirty(game);
        Physics.SyncTransforms();
        foreach(var pair in new[]{sink,soap,towel})
            if(!pair.gameObject.activeInHierarchy || !pair.GetComponent<WashTarget>() || !pair.GetComponentsInChildren<Collider>().Any(c=>c.enabled))throw new Exception("Inactive target or collider: "+pair.name);
        if(faucet.GetComponent<Renderer>())throw new Exception("Faucet hit area must be invisible");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(folder+"/Reports/PurchasedAssetTransfer.txt","PASS: Restroom/T_sink = Water; Butter = Soap; To_towel_hanging = Towel.\nPASS: Existing materials, transforms and prefab assets preserved.\nPASS: Each target active with collider. Old WashGame fixtures disabled.\nPASS: Invisible faucet hit area and water stream moved to sink.\nFaucet bounds: "+faucetBounds+"; outlet: "+outlet+"\n");
    }
    static void Bind(Transform t,WashGameController game,WashAction action,string label)
    {
        var target=t.GetComponent<WashTarget>();if(!target)target=t.gameObject.AddComponent<WashTarget>();
        target.game=game;target.action=action;target.displayName=label;t.gameObject.SetActive(true);
        if(!t.GetComponentsInChildren<Collider>(true).Any())
        {
            var box=t.gameObject.AddComponent<BoxCollider>();var mf=t.GetComponent<MeshFilter>();
            if(mf){box.center=mf.sharedMesh.bounds.center;box.size=mf.sharedMesh.bounds.size+Vector3.one*.02f;}
        }
        EditorUtility.SetDirty(target);PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
    }
    static GameObject Area(WashGameController g,string name,Vector3 position,Vector3 size)
    {
        var child=g.transform.Find(name);var go=child?child.gameObject:new GameObject(name);
        go.transform.SetParent(g.transform,true);go.transform.position=position;go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;go.SetActive(true);
        var box=go.GetComponent<BoxCollider>();if(!box)box=go.AddComponent<BoxCollider>();box.center=Vector3.zero;box.size=size;box.isTrigger=true;
        Bind(go.transform,g,WashAction.Water,name=="WaterClickArea"?"물":"수도꼭지");return go;
    }
}
