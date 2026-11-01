using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BoardGameVerification
{
    static double due;
    static BoardGameVerification() { EditorApplication.playModeStateChanged+=OnPlay; }
    public static void Schedule() { SessionState.SetBool("BoardGame.Verify",true); EditorApplication.EnterPlaymode(); }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update-=Run;
            EditorApplication.update-=VerifyRestart;
            SessionState.SetBool("BoardGame.VerifyRestart",false);
            return;
        }
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("BoardGame.VerifyExit",false))
        {
            SessionState.SetBool("BoardGame.VerifyExit",false);
            EditorApplication.delayCall+=()=>
            {
                var g=UnityEngine.Object.FindFirstObjectByType<BoardGameController>();
                bool restored=g && g.interactor.enabled && g.otherInputs.All(input=>!input || input.enabled);
                File.AppendAllText(BoardGameSetup.Folder+"/Verification.txt",(restored?"PASS":"FAILED")+": shared inputs remain enabled in Edit mode after verification.\n");
            };
            return;
        }
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("BoardGame.Verify",false)) return;
        SessionState.SetBool("BoardGame.Verify",false); due=EditorApplication.timeSinceStartup+1; EditorApplication.update+=Run;
    }
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); }
    static void Run()
    {
        if(!EditorApplication.isPlaying || !Application.isPlaying) { EditorApplication.update-=Run; return; }
        if(EditorApplication.timeSinceStartup<due) return;
        EditorApplication.update-=Run;
        BoardGameController g=null;
        try
        {
            g=UnityEngine.Object.FindFirstObjectByType<BoardGameController>();
            Check(g && g.items.Length==21,"21 objects");
            Check(!g.Completed,"Use a fresh initial game to run verification");
            Check(g.items.Count(i=>i.state==BoardItemState.Floor)==7,"Seven floor items");
            Check(g.RefreshOverlap(),"Initial disorder overlaps");
            Check(Mathf.Abs(BoardGeometry.Overlap(Vector2.zero,Vector2.one,0,Vector2.zero,Vector2.one,45)-.828427f)<.001f,"Rotated polygon area");
            Check(!BoardGeometry.Fits(new Vector2(.4f,0),Vector2.one,45,Vector2.one*2),"Rotated edge containment");
            Check(BoardGeometry.Overlap(Vector2.zero,Vector2.one,0,new Vector2(.8f,0),Vector2.one,0)<.25f,"Small overlap allowed");
            bool move=g.player.canMove, look=g.player.canLook;
            g.boardTarget.Interact(); Check(g.Active && !g.interactor.enabled && g.player.canMove==move && g.player.canLook==look,"Board click: active="+g.Active+" enabled="+g.isActiveAndEnabled+" shared="+g.interactor.enabled+" time="+Time.timeScale+" held="+(PlayerHand.Instance ? PlayerHand.Instance.Held : null)+" targetGame="+(g.boardTarget.game==g)+" trash="+g.boardTarget.trash+" playing="+Application.isPlaying);
            Check(g.CompletionIssue()=="바닥에 아직 정리되지 않은 것이 남아있다.","Floor message");
            var floorItem=g.items.First(i=>i.state==BoardItemState.Floor);
            g.Pick(floorItem); Check(g.Drop() && floorItem.state==BoardItemState.Floor && floorItem.hitbox.enabled,"Drop to floor");
            g.Pick(floorItem); Check(g.Held==floorItem,"Pick dropped item again"); g.Discard(); g.Retrieve(); g.Place(new Vector2(1.8f,0));
            var a=g.items[0]; var b=g.items[1]; var c=g.items[2];
            g.Pick(a); Check(g.Held==a && !a.hitbox.enabled,"Detach existing board item");
            Check(g.CompletionIssue()=="손에 든 것을 마저 정리하자.","Held message");
            Check(!g.Place(new Vector2(100,0)) && g.Held==a,"Reject outside board");
            g.Discard(); g.Pick(b); g.Discard(); g.Pick(c); g.Discard();
            g.Retrieve(); Check(g.Held==c,"LIFO C"); Check(g.Place(Vector2.zero),"Retrieve and reattach");
            g.Retrieve(); Check(g.Held==b,"LIFO B"); g.Place(Vector2.zero);
            g.Retrieve(); Check(g.Held==a,"LIFO A"); g.Place(Vector2.zero);
            Check(g.RefreshOverlap() && a.outline.enabled && b.outline.enabled,"Overlap allowed with red borders");
            foreach(var i in g.items.Where(i=>i.state==BoardItemState.Floor).ToArray()) { g.Pick(i); g.Discard(); }
            Check(g.CompletionIssue()=="겹쳐서 잘 안 보여.","Overlap completion message");
            foreach(var i in g.items.Where(i=>i.state==BoardItemState.Board).ToArray()) { g.Pick(i); g.Discard(); }
            Check(g.TrashCount==21 && g.CompletionIssue()==null,"All-trash valid");
            g.Retrieve(); var saved=g.Held; saved.angle=27; Check(g.Place(new Vector2(.3f,.2f)),"Arbitrary final layout");
            g.TickFinish(true,1.9f); Check(!g.Completed,"G requires two seconds"); g.TickFinish(false,0); g.TickFinish(true,.2f); Check(!g.Completed,"G release resets progress");
            g.TickFinish(true,1.8f); Check(g.Completed && !g.Active,"Completion without saving");
            Check(g.interactor.enabled && g.player.canMove==move && g.player.canLook==look,"Input restored");
            g.Begin(); Check(!g.Active,"Completed board cannot reopen");
            File.WriteAllText(BoardGameSetup.Folder+"/Verification.txt","PASS: 21 items; initial floor/board disorder; exact rotated overlap; rotated boundaries; small overlap; input ownership; detach; held/floor/overlap messages; invalid placement; LIFO C/B/A; retrieve and reattach; red borders; all-trash completion allowed; G hold/reset; no persistence; input restore; completed lockout.\n");
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(g.gameObject.scene.path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            due=EditorApplication.timeSinceStartup+2;
            EditorApplication.update+=VerifyRestart;
        }
        catch(Exception e) { File.WriteAllText(BoardGameSetup.Folder+"/Verification.txt","FAILED: "+e); Debug.LogException(e); EditorApplication.ExitPlaymode(); }
    }
    static void VerifyRestart()
    {
        if(!EditorApplication.isPlaying || !Application.isPlaying) { EditorApplication.update-=VerifyRestart; return; }
        if(EditorApplication.timeSinceStartup<due) return;
        EditorApplication.update-=VerifyRestart;
        try
        {
            var g=UnityEngine.Object.FindFirstObjectByType<BoardGameController>();
            Check(g && !g.Completed && !g.Active && !g.Held && g.TrashCount==0,"Restart clears completion and hands/trash");
            Check(g.items.Count(i=>i.state==BoardItemState.Floor)==7 && g.items.Count(i=>i.state==BoardItemState.Board)==14,"Restart restores initial floor/board distribution");
            Check(g.items.All(i=>i.gameObject.activeSelf && i.hitbox.enabled),"Restart restores discarded objects");
            Check(g.interactor.enabled && g.otherInputs.All(input=>!input || input.enabled),"Restart leaves shared inputs enabled");
            g.Begin(); Check(g.Active,"Restart allows starting again");
            // Release owned inputs before Unity tears down/restores the scene, not during teardown.
            g.enabled=false;
            Check(g.interactor.enabled && g.otherInputs.All(input=>!input || input.enabled),"Disabling BoardGame restores all inputs");
            g.enabled=true;
            SessionState.SetBool("BoardGame.VerifyExit",true);
            File.AppendAllText(BoardGameSetup.Folder+"/Verification.txt","PASS: scene reload restores initial 14 board / 7 floor items, empty trash, no held item, incomplete state and start interaction; disabling BoardGame restores inputs.\n");
        }
        catch(Exception e) { File.WriteAllText(BoardGameSetup.Folder+"/Verification.txt","FAILED: "+e); Debug.LogException(e); }
        finally { EditorApplication.ExitPlaymode(); }
    }
}
