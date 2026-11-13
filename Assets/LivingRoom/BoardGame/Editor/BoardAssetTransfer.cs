using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BoardAssetTransfer
{
    const string Folder="Assets/LivingRoom/BoardGame";
    static double due;
    static BoardAssetTransfer() { EditorApplication.update+=Poll; EditorApplication.playModeStateChanged+=OnPlay; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/Editor/Transfer.request")) return;
        bool verifyOnly=File.ReadAllText(Folder+"/Editor/Transfer.request").Trim()=="verify";
        File.Delete(Folder+"/Editor/Transfer.request");
        try { if(!verifyOnly) Transfer(); SessionState.SetBool("BoardGame.TransferTest",true); EditorApplication.EnterPlaymode(); }
        catch(Exception e) { File.WriteAllText(Folder+"/TransferReport.txt","FAILED: "+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Transfer to purchased assets")]
    public static void Transfer()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Room3_LivingRoom.unity") throw new Exception("Open Room3_LivingRoom first.");
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var game=all.Select(t=>t.GetComponent<BoardGameController>()).First(g=>g);
        var board=all.Single(t=>t.name=="Painting_H"); var bin=all.Single(t=>t.name=="KP_coffee_mug");
        if(game.boardTarget.transform==board && game.trashTarget.transform==bin)
        {
            GroundItems(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); BoardGameSetup.Preview(game);
            File.WriteAllText(Folder+"/TransferReport.txt","Targets: Painting_H / KP_coffee_mug. Board metres: "+game.boardSize+". Floor items grounded.\n"); return;
        }
        var oldBoard=game.boardTarget; var oldBin=game.trashTarget;
        var oldSize=game.boardSize; var oldPosition=game.surface.position; var oldRotation=game.surface.rotation;
        Undo.RecordObject(game,"Transfer BoardGame assets");
        // Measure in a unit-scale frame so nonuniform prefab scales do not distort metres.
        var filter=board.GetComponentInChildren<MeshFilter>();
        if(!filter || !filter.sharedMesh) throw new Exception("Painting_H needs a mesh.");
        var local=filter.sharedMesh.bounds;
        Vector3 normal=board.forward;
        if(Vector3.Dot(normal,oldPosition-board.position)<0) normal=-normal;
        Quaternion rotation=Quaternion.LookRotation(normal,board.up);
        Bounds measured=new Bounds(); bool first=true;
        for(int n=0;n<8;n++)
        {
            Vector3 corner=local.center+Vector3.Scale(local.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
            Vector3 p=Quaternion.Inverse(rotation)*(filter.transform.TransformPoint(corner)-board.position);
            if(first){measured=new Bounds(p,Vector3.zero);first=false;}else measured.Encapsulate(p);
        }
        var size=new Vector2(measured.size.x*.90f,measured.size.y*.90f);
        if(size.x<.1f || size.y<.1f || measured.size.z>Mathf.Min(size.x,size.y)) throw new Exception("Painting_H surface orientation needs review: "+measured);
        float scale=Mathf.Min(size.x/oldSize.x,size.y/oldSize.y);
        Undo.RecordObject(game.surface,"Move board surface");
        game.surface.SetPositionAndRotation(board.position+rotation*new Vector3(measured.center.x,measured.center.y,measured.max.z+.006f),rotation);
        game.surface.localScale=Vector3.one; game.boardSize=size;
        foreach(var item in game.items)
        {
            Undo.RecordObjects(new UnityEngine.Object[]{item,item.transform},"Fit BoardGame items");
            item.size*=scale; item.transform.localScale*=scale;
            item.boardPosition*=scale;
            if(item.state==BoardItemState.Board)
                item.transform.SetPositionAndRotation(game.surface.TransformPoint(new Vector3(item.boardPosition.x,item.boardPosition.y,.015f+item.order*.001f)),rotation*Quaternion.Euler(0,0,item.angle));
            else
            {
                Vector3 previous=Quaternion.Inverse(oldRotation)*(item.transform.position-oldPosition);
                Vector3 p=game.surface.position+game.surface.right*previous.x*scale+game.surface.forward*Mathf.Max(.35f,previous.z*scale);
                p.y=item.transform.position.y;
                item.transform.SetPositionAndRotation(p,Quaternion.Euler(-90,0,item.angle));
            }
        }
        foreach(var pair in new[]{(obj:board,trash:false),(obj:bin,trash:true)})
        {
            Undo.SetTransformParent(pair.obj,game.transform,"Group purchased BoardGame asset");
            var target=pair.obj.GetComponent<BoardTarget>() ?? Undo.AddComponent<BoardTarget>(pair.obj.gameObject);
            Undo.RecordObject(target,"Connect BoardGame target"); target.game=game; target.trash=pair.trash; target.enabled=true;
            if(!pair.obj.GetComponentsInChildren<Collider>().Any()) throw new Exception(pair.obj.name+" needs a collider.");
            foreach(var c in pair.obj.GetComponentsInChildren<Collider>()) { Undo.RecordObject(c,"Enable target collider"); c.enabled=true; c.isTrigger=false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
            if(pair.trash) game.trashTarget=target; else game.boardTarget=target;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.obj);
        }
        foreach(var old in new[]{oldBoard,oldBin}) if(old && old!=game.boardTarget && old!=game.trashTarget) { Undo.RecordObject(old.gameObject,"Disable replaced BoardGame placeholder"); old.gameObject.SetActive(false); }
        GroundItems(game); game.RefreshOverlap(); EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        File.WriteAllText(Folder+"/TransferReport.txt","Transferred Painting_H and KP_coffee_mug. Board metres: "+size+"; item scale: "+scale+"; surface: "+game.surface.position+"; normal: "+normal+"\n");
        BoardGameSetup.Preview(game);
    }
    static void GroundItems(BoardGameController game)
    {
        Physics.SyncTransforms();
        foreach(var item in game.items.Where(i=>i.state==BoardItemState.Floor))
        {
            Vector3 origin=item.transform.position; origin.y=game.surface.position.y;
            var hits=Physics.RaycastAll(origin,Vector3.down,6,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<BoardItem>() || hit.collider.GetComponentInParent<BoardTarget>() || hit.normal.y<.9f || hit.point.y>game.surface.position.y-.8f) continue;
                Undo.RecordObject(item.transform,"Place paper on room floor");
                item.transform.position=hit.point+Vector3.up*.015f; break;
            }
        }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode) EditorApplication.update-=Test;
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BoardGame.TransferTest",false))return;
        SessionState.SetBool("BoardGame.TransferTest",false); due=EditorApplication.timeSinceStartup+2; EditorApplication.update+=Test;
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Test()
    {
        if(!Application.isPlaying || !EditorApplication.isPlaying){EditorApplication.update-=Test;return;}
        if(EditorApplication.timeSinceStartup<due)return;
        EditorApplication.update-=Test;
        var game=UnityEngine.Object.FindFirstObjectByType<BoardGameController>();
        try
        {
            Check(game.boardTarget.name=="Painting_H" && game.trashTarget.name=="KP_coffee_mug","New references");
            Check(game.items.Length==21,"21 items");
            // Test the migrated target at its actual progression stage; this only changes Play mode.
            var manager=TeamRoulette.Room3.GameManager.Instance;
            if(manager) typeof(TeamRoulette.Room3.GameManager).GetProperty("CurrentStage").SetValue(manager,TeamRoulette.Room3.GameStage.Board);
            Time.timeScale=1; game.enabled=true;
            game.boardTarget.Interact(); Check(game.Active,"Painting starts game");
            var item=game.items[0]; game.Pick(item); game.Discard(); game.Retrieve(); Check(game.Held==item,"Mug trash LIFO retrieve");
            var anchors=game.items.Where(i=>i!=item && i.state==BoardItemState.Board).ToArray();
            // Reproduce moving the scene's item group without updating serialized board positions.
            foreach(var anchor in anchors) anchor.transform.position+=game.surface.right*.17f;
            var before=anchors.Select(i=>game.surface.InverseTransformPoint(i.transform.position)).ToArray();
            Check(game.Place(Vector2.zero),"Place retrieved item on new board");
            for(int n=0;n<anchors.Length;n++)
            {
                var after=game.surface.InverseTransformPoint(anchors[n].transform.position);
                Check(Vector2.Distance(new Vector2(before[n].x,before[n].y),new Vector2(after.x,after.y))<.0001f,"Existing item shifted on placement: "+anchors[n].id);
            }
            Check(Vector2.Distance(game.surface.InverseTransformPoint(item.transform.position),Vector2.zero)<.0001f,"Placed item missed cursor coordinates");
            foreach(var i in game.items){game.Pick(i);game.Discard();}
            Check(game.TryFinish() && game.Completed && !game.Active,"Completion returns controls");
            File.WriteAllText(Folder+"/PlacementVerification.txt","PASS: board start, trash retrieve, placement at requested coordinates; all other items retain their horizontal and vertical positions after scene/group edits; completion.\n");
        }
        catch(Exception e){File.AppendAllText(Folder+"/TransferReport.txt","FAILED: "+e);Debug.LogException(e);}
        finally{if(game && game.Active) game.enabled=false;EditorApplication.ExitPlaymode();}
    }
}
