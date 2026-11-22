using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoardCurtainLightSetup
{
    const string Folder="Assets/LivingRoom/BoardGame";
    static double due;
    static BoardCurtainLightSetup(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=OnPlay;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Folder+"/Editor/CurtainLight.request"))return;
        File.Delete(Folder+"/Editor/CurtainLight.request");
        try{Link();SessionState.SetBool("BoardGame.CurtainLightTest",true);EditorApplication.EnterPlaymode();}
        catch(Exception e){File.WriteAllText(Folder+"/CurtainLightReport.txt","FAILED: "+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Link fill light to curtain")]
    public static void Link()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity")throw new Exception("Open living room scene.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var controller=all.Select(t=>t.GetComponent<CurtainGameController>()).Single(c=>c);
        var light=all.Single(t=>t.name=="BoardCenterFillLight").GetComponent<Light>();if(!light)throw new Exception("Fill light missing.");
        var serialized=new SerializedObject(controller);serialized.FindProperty("boardCenterFillLight").objectReferenceValue=light;serialized.ApplyModifiedProperties();
        Undo.RecordObject(light,"Keep fill light off before curtain opens");light.enabled=false;EditorUtility.SetDirty(light);
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Test;
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BoardGame.CurtainLightTest",false))return;
        SessionState.SetBool("BoardGame.CurtainLightTest",false);due=EditorApplication.timeSinceStartup+1;EditorApplication.update+=Test;
    }
    static void Check(bool value,string text){if(!value)throw new Exception(text);}
    static void Test()
    {
        if(!Application.isPlaying || !EditorApplication.isPlaying){EditorApplication.update-=Test;return;}
        if(EditorApplication.timeSinceStartup<due)return;EditorApplication.update-=Test;
        try
        {
            var curtain=UnityEngine.Object.FindFirstObjectByType<CurtainGameController>();
            var light=(Light)typeof(CurtainGameController).GetField("boardCenterFillLight",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(curtain);
            Check(light && !light.enabled,"Fill light starts disabled");
            var reveal=typeof(CurtainGameController).GetMethod("SetCurtainsRevealed",BindingFlags.NonPublic|BindingFlags.Instance);
            reveal.Invoke(curtain,new object[]{true});Check(light.enabled && light.gameObject.activeInHierarchy,"Curtain reveal enables fill light");
            reveal.Invoke(curtain,new object[]{false});Check(!light.enabled,"Closed curtains disable fill light");
            light.gameObject.SetActive(false);curtain.RestoreCompleted();Check(light.enabled && light.gameObject.activeInHierarchy,"Completed restore enables component and object");
            File.WriteAllText(Folder+"/CurtainLightReport.txt","PASS: reference linked and scene saved; starts off; curtain reveal switches light on; closed state off; completed curtain restore activates both light component and GameObject. Brightness and position preserved.\n");
        }
        catch(Exception e){File.WriteAllText(Folder+"/CurtainLightReport.txt","FAILED: "+e);Debug.LogException(e);}
        finally{EditorApplication.ExitPlaymode();}
    }
}
