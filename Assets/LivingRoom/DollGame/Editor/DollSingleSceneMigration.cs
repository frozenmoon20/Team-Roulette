#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3.DollMemory;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time authoring migration. The resulting scene has no source-scene dependency.
[InitializeOnLoad]
public static class DollSingleSceneMigration
{
    const string Folder="Assets/LivingRoom/DollGame/Editor/";
    static DollSingleSceneMigration(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(!File.Exists(Folder+"SingleScene.request") || File.ReadAllText(Folder+"SingleScene.request").Trim()!="migrate")return;
        File.WriteAllText(Folder+"SingleScene.request","running");
        try{Migrate();File.WriteAllText(Folder+"SingleScene.request","passed");}
        catch(Exception e){File.WriteAllText(Folder+"SingleScene.request","failed");File.WriteAllText(Folder+"SingleSceneReport.txt",e.ToString());Debug.LogException(e);}
    }
    static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    static T Add<T>(GameObject go) where T:Component => go.GetComponent<T>()??go.AddComponent<T>();
    [MenuItem("Tools/Room3/Doll Memory/Migrate to Toy Scene")]
    public static void Migrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode before migration.");
        var yard=SceneManager.GetSceneByName("Room3_Toy");bool closeYard=!yard.IsValid()||!yard.isLoaded;
        if(closeYard)yard=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);
        var existing=All<DollMemorySequence>(yard).FirstOrDefault();
        if(existing){Validate(existing);File.WriteAllText(Folder+"SingleSceneReport.txt","PASS: already migrated; local references validated.");if(closeYard)EditorSceneManager.CloseScene(yard,true);return;}
        var old=SceneManager.GetSceneByName("Room3_OldLivingRoom");bool closeOld=!old.IsValid()||!old.isLoaded;
        if(closeOld)old=EditorSceneManager.OpenScene("Assets/Scenes/Room3_OldLivingRoom.unity",OpenSceneMode.Additive);
        var source=All<DollMemorySequence>(old).Single();
        var nodes=All<Transform>(yard);
        var yardBinding=All<DollYardBindings>(yard).Single();
        var player=All<FirstPersonPlayer>(yard).Single();
        var dog=nodes.Single(t=>t.name=="Spitz_anim_IP");
        var duck=nodes.Single(t=>t.name=="08_duck_prefab");
        var door=nodes.Single(t=>t.name==source.interiorDoor.name);
        var rotation=door.rotation*Quaternion.Inverse(source.interiorDoor.transform.rotation);
        Vector3 Map(Vector3 point)=>door.position+rotation*(point-source.interiorDoor.transform.position);
        var log=new StringBuilder();
        log.AppendLine("Source house door="+source.interiorDoor.transform.position+" Toy door="+door.position+" rotation="+rotation.eulerAngles);
        // Keep a recoverable scene snapshot before authoring changes.
        Directory.CreateDirectory("Temp/DollSingleSceneMigration");
        File.Copy(yard.path,"Temp/DollSingleSceneMigration/Room3_Toy.before.unity",true);
        var root=UnityEngine.Object.Instantiate(source.gameObject);root.name="DollMemory";
        SceneManager.MoveGameObjectToScene(root,yard);
        root.transform.SetPositionAndRotation(Map(source.transform.position),rotation*source.transform.rotation);
        var game=root.GetComponent<DollMemorySequence>();
        var props=new GameObject("DollMemory Props");SceneManager.MoveGameObjectToScene(props,yard);
        Transform ResolveProp(Transform original)
        {
            var found=nodes.FirstOrDefault(t=>t.name==original.name);
            if(found){log.AppendLine("Reused: "+found.name+" "+found.position);return found;}
            var copy=UnityEngine.Object.Instantiate(original.gameObject,props.transform);copy.name=original.name;
            copy.transform.SetPositionAndRotation(Map(original.position),rotation*original.rotation);copy.transform.localScale=original.lossyScale;
            log.AppendLine("Copied: "+copy.name+" "+copy.transform.position);return copy.transform;
        }
        var washer=ResolveProp(source.washerRenderer.transform);
        var sourceBed=All<Transform>(old).Single(t=>t.name=="DogAccesories-006");
        var bed=ResolveProp(sourceBed);
        // Preserve the existing camera framing relative to the actual reused furniture.
        var washerDelta=washer.position-Map(source.washerRenderer.transform.position);
        foreach(var anchor in new[]{game.drum,game.washerButton,game.washerView,game.washerSeat})anchor.position+=washerDelta;
        var bedDelta=bed.position-Map(sourceBed.position);game.cushion.position+=bedDelta;game.restView.position+=bedDelta;
        game.player=player;game.playerCamera=player.GetComponentInChildren<Camera>(true);game.interactor=player.GetComponentInChildren<PlayerInteractor>(true);
        game.sharedUI=All<GameUI>(yard).First(ui=>ui.hud&&ui.objectiveText);
        game.sceneDoll=duck.gameObject;game.yardBinding=yardBinding;game.previewWhenOpenedDirectly=false;
        game.interiorDoor=Add<DollDoor>(door.gameObject);
        game.washerRenderer=washer.GetComponent<Renderer>();game.washerRenderer.sharedMaterials=source.washerRenderer.sharedMaterials;
        var winter=game.winter;winter.dog=dog;winter.animator=dog.GetComponent<Animator>();
        var sourceMouth=source.winter.mouth;
        var parent=dog.GetComponentsInChildren<Transform>(true).Single(t=>t.name==sourceMouth.parent.name);
        var mouth=parent.Find(sourceMouth.name);if(!mouth){mouth=new GameObject(sourceMouth.name).transform;mouth.SetParent(parent,false);}
        mouth.localPosition=sourceMouth.localPosition;mouth.localRotation=sourceMouth.localRotation;mouth.localScale=sourceMouth.localScale;winter.mouth=mouth;
        var start=new GameObject("Winter Chase Entry").transform;start.SetParent(root.transform,false);start.SetPositionAndRotation(Map(source.winter.dog.position),rotation*source.winter.dog.rotation);game.winterEntry=start;
        var bathroom=nodes.Single(t=>t.name.Contains("1F bathroom")&&t.name.Contains("Door_interior"));
        Add<RestroomDoor>(bathroom.gameObject);
        Add<DollMemoryStart>(duck.gameObject).memory=game;
        game.washerButton.GetComponent<DollWasherButton>().memory=game;
        // The aperture shader stores a world-space center; use a dedicated Toy material.
        const string cutawayPath="Assets/LivingRoom/DollGame/WasherCutawayToy.mat";
        var cutaway=AssetDatabase.LoadAssetAtPath<Material>(cutawayPath);
        if(!cutaway){cutaway=new Material(source.washerCutaway);AssetDatabase.CreateAsset(cutaway,cutawayPath);}
        cutaway.SetVector("_DrumCenter",Map(source.washerCutaway.GetVector("_DrumCenter"))+washerDelta);
        cutaway.SetVector("_DrumAxis",rotation*(Vector3)source.washerCutaway.GetVector("_DrumAxis"));game.washerCutaway=cutaway;EditorUtility.SetDirty(cutaway);
        Validate(game);
        log.AppendLine("PASS: all serialized game references belong to Room3_Toy or project assets.");
        log.AppendLine("Player entry="+game.entry.position+" winter="+start.position+" washer="+washer.position+" bed="+bed.position);
        EditorUtility.SetDirty(game);EditorUtility.SetDirty(winter);EditorUtility.SetDirty(game.interiorDoor);
        EditorSceneManager.MarkSceneDirty(yard);EditorSceneManager.SaveScene(yard);AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"SingleSceneReport.txt",log.ToString());
        if(closeOld)EditorSceneManager.CloseScene(old,true);if(closeYard)EditorSceneManager.CloseScene(yard,true);
    }
    static void Validate(DollMemorySequence game)
    {
        if(!game.player||!game.playerCamera||!game.interactor||!game.sharedUI||!game.winter||!game.winter.dog||!game.winter.mouth||!game.yardBinding||!game.winterEntry||!game.washerRenderer||!game.interiorDoor)throw new Exception("Missing required local reference.");
        foreach(var component in game.GetComponentsInChildren<Component>(true))
        {
            if(!component)throw new Exception("Missing component on migrated content.");
            var serialized=new SerializedObject(component);var property=serialized.GetIterator();
            while(property.Next(true))
            {
                if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                var value=property.objectReferenceValue;
                var go=value as GameObject;if(value is Component target)go=target.gameObject;
                if(go && go.scene.IsValid() && go.scene!=game.gameObject.scene)throw new Exception("Cross-scene reference: "+component.name+"."+property.propertyPath);
            }
        }
    }
}
#endif
