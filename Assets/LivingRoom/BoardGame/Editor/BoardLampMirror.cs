using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoardLampMirror
{
    const string Folder="Assets/LivingRoom/BoardGame";
    static BoardLampMirror(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Folder+"/Editor/MirrorLamp.request"))return;
        File.Delete(Folder+"/Editor/MirrorLamp.request");
        try{Apply();}catch(Exception e){File.WriteAllText(Folder+"/MirrorLampReport.txt","FAILED: "+e);Debug.LogException(e);}
    }
    static Bounds Bounds(Transform root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)throw new Exception("Lamp renderer missing.");
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Mirror second spotlight")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open living room scene.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var first=all.Single(t=>t.name=="Lamp_wall_desk");var second=all.Single(t=>t.name=="Lamp_wall_desk (1)");
        var source=first.GetComponentsInChildren<Light>(true).Single(l=>l.name=="BoardSpotLight");
        var targets=second.GetComponentsInChildren<Light>(true).Where(l=>l.name=="BoardSpotLight").ToArray();
        if(targets.Length>1)throw new Exception("Multiple second spotlights; select a unique target.");
        var game=all.Select(t=>t.GetComponent<BoardGameController>()).First(g=>g);
        Light target=targets.FirstOrDefault();
        if(!target)
        {
            var go=new GameObject("BoardSpotLight");Undo.RegisterCreatedObjectUndo(go,"Create mirrored spotlight");go.transform.SetParent(second,false);
            target=Undo.AddComponent<Light>(go);EditorUtility.CopySerialized(source,target);
        }
        var right=game.surface.right.normalized;
        Vector3 Mirror(Vector3 vector)=>vector-2*Vector3.Dot(vector,right)*right;
        Vector3 offset=source.transform.position-Bounds(first).center;
        Vector3 position=Bounds(second).center+Mirror(offset);
        Vector3 forward=Mirror(source.transform.forward),up=Mirror(source.transform.up);
        Undo.RecordObjects(new UnityEngine.Object[]{target,target.transform},"Mirror spotlight position and direction");
        target.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward,up));target.type=LightType.Spot;
        EditorUtility.SetDirty(target);PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        if(Vector3.Distance(target.transform.position,position)>.001f || Vector3.Angle(target.transform.forward,forward)>.01f)throw new Exception("Mirror validation failed.");
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        BoardGameSetup.Preview(game);File.Copy(Folder+"/Preview.png",Folder+"/MirroredLampPreview.png",true);
        File.WriteAllText(Folder+"/MirrorLampReport.txt","PASS: second BoardSpotLight mirrored relative to lamp centers along board horizontal axis. Position="+position+"; direction="+forward+"; intensity="+target.intensity+"; angle="+target.spotAngle+"; scene saved.\n");
        Selection.activeGameObject=target.gameObject;
    }
}
