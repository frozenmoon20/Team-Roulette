using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BoardGameSetup
{
    public const string Folder="Assets/LivingRoom/BoardGame";
    const string ScenePath="Assets/Scenes/Room3_LivingRoom.unity";
    static BoardGameSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        string request=Folder+"/Editor/Setup.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { BuildAndVerify(); } catch(Exception e) { File.WriteAllText(Folder+"/Verification.txt","FAILED: "+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Set up and verify")]
    public static void BuildAndVerify()
    {
        Build(); BoardGameVerification.Schedule();
    }
    [MenuItem("Tools/LivingRoom/BoardGame/Set up scene")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)
        {
            if(scene.isDirty) throw new Exception("Save the current scene before opening Room3_LivingRoom.");
            scene=EditorSceneManager.OpenScene(ScenePath);
        }
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        if(all.Any(t=>t.name=="BoardGame"))
        {
            var existing=all.First(t=>t.name=="BoardGame").GetComponent<BoardGameController>();
            Undo.RecordObject(existing,"Restore BoardGame controller"); existing.enabled=true; EditorUtility.SetDirty(existing);
            existing.ConfigureOverlayLayout();
            // Restore the edit-time defaults after an interrupted BoardGame verification.
            if(existing.interactor) { Undo.RecordObject(existing.interactor,"Restore BoardGame input"); existing.interactor.enabled=true; EditorUtility.SetDirty(existing.interactor); PrefabUtility.RecordPrefabInstancePropertyModifications(existing.interactor); }
            foreach(var input in existing.otherInputs) if(input) { Undo.RecordObject(input,"Restore BoardGame input"); input.enabled=true; EditorUtility.SetDirty(input); }
            if(existing.interactor)
            {
                if(existing.interactor.nameText) { Undo.RecordObject(existing.interactor.nameText,"Clear placeholder hint"); existing.interactor.nameText.text=""; PrefabUtility.RecordPrefabInstancePropertyModifications(existing.interactor.nameText); }
                if(existing.interactor.handIcon) { Undo.RecordObject(existing.interactor.handIcon,"Clear idle hand icon"); existing.interactor.handIcon.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(existing.interactor.handIcon); }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            // One-time migration of the initial generated placeholders, before any player save.
            string marker=Folder+"/Editor/PlaceholderLayoutV2.txt";
            if(!File.Exists(marker))
            {
                foreach(var item in existing.items)
                {
                    var label=item.GetComponentInChildren<TextMeshPro>();
                    label.enableAutoSizing=true; label.fontSizeMin=.2f; label.fontSizeMax=1.5f; label.textWrappingMode=TextWrappingModes.NoWrap;
                    int n=Array.IndexOf(existing.items,item);
                    if(n>=6 && n<14)
                    {
                        item.boardPosition=new Vector2(-1.1f+(n-6)*.22f,-.48f);
                        item.transform.position=existing.surface.TransformPoint(new Vector3(item.boardPosition.x,item.boardPosition.y,.015f+n*.001f));
                    }
                    PrefabUtility.SaveAsPrefabAsset(item.gameObject,Folder+"/Prefabs/"+item.id+".prefab");
                }
                existing.RefreshOverlap(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                File.WriteAllText(marker,"Applied"); Preview(existing);
            }
            return;
        }
        var board=all.Single(t=>t.name=="게시판"); var bin=all.Single(t=>t.name=="쓰레기통");
        Directory.CreateDirectory(Folder+"/Materials"); Directory.CreateDirectory(Folder+"/Prefabs");
        var root=new GameObject("BoardGame"); Undo.RegisterCreatedObjectUndo(root,"Create BoardGame");
        var game=root.AddComponent<BoardGameController>();
        Undo.SetTransformParent(board,root.transform,"Group board"); Undo.SetTransformParent(bin,root.transform,"Group trash");
        game.player=Object.FindFirstObjectByType<FirstPersonPlayer>();
        game.interactor=Object.FindFirstObjectByType<PlayerInteractor>(); game.viewCamera=game.interactor.playerCamera;
        game.otherInputs=Object.FindObjectsByType<MealInput>(FindObjectsSortMode.None).Cast<Behaviour>().ToArray();
        game.boardTarget=Undo.AddComponent<BoardTarget>(board.gameObject); game.boardTarget.game=game;
        game.trashTarget=Undo.AddComponent<BoardTarget>(bin.gameObject); game.trashTarget.game=game; game.trashTarget.trash=true;
        var surface=new GameObject("PlacementSurface").transform; surface.SetParent(root.transform);
        surface.SetPositionAndRotation(board.position-board.forward*.02f,Quaternion.LookRotation(-board.forward,board.up));
        game.surface=surface; game.boardSize=new Vector2(board.lossyScale.x-.14f,board.lossyScale.y-.14f);
        var itemRoot=new GameObject("Items").transform; itemRoot.SetParent(root.transform,false);
        var red=Material("OverlapOutline",new Color(1,.05f,.04f),true);
        var font=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(t=>t.font).FirstOrDefault(f=>f && f.HasCharacter('정'));
        if(!font) font=TMP_Settings.defaultFontAsset;
        var sizes=new[] {new Vector2(.65f,.78f),new Vector2(.62f,.72f),new Vector2(.57f,.42f),new Vector2(.24f,.36f),new Vector2(.45f,.32f),new Vector2(.43f,.32f),new Vector2(.35f,.36f),new Vector2(.40f,.25f),new Vector2(.38f,.46f),new Vector2(.26f,.28f),new Vector2(.18f,.48f),new Vector2(.42f,.18f),new Vector2(.32f,.18f),new Vector2(.27f,.25f),new Vector2(.31f,.23f),new Vector2(.22f,.22f),new Vector2(.14f,.38f),new Vector2(.43f,.30f),new Vector2(.23f,.24f),new Vector2(.32f,.38f),new Vector2(.31f,.24f)};
        game.items=new BoardItem[21];
        for(int n=0;n<21;n++)
        {
            var go=new GameObject("photo"+(n+1)); go.transform.SetParent(itemRoot,false);
            var item=go.AddComponent<BoardItem>(); game.items[n]=item; item.id=go.name; item.size=sizes[n]; item.angle=(n%5-2)*13; item.order=n;
            var box=go.AddComponent<BoxCollider>(); box.size=new Vector3(item.size.x,item.size.y,.012f); item.hitbox=box;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name="Visual"; visual.transform.SetParent(go.transform,false);
            Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.transform.localScale=new Vector3(item.size.x,item.size.y,.008f);
            visual.GetComponent<Renderer>().sharedMaterial=Material("Photo"+(n%6),Color.HSVToRGB((n%6)*.13f,.22f,.95f),false);
            var label=new GameObject("Label",typeof(TextMeshPro)); label.transform.SetParent(go.transform,false); label.transform.localPosition=new Vector3(0,0,.006f); label.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=label.GetComponent<TextMeshPro>(); text.font=font; text.text=item.id; text.fontSize=1.5f; text.enableAutoSizing=true; text.fontSizeMin=.2f; text.fontSizeMax=1.5f; text.textWrappingMode=TextWrappingModes.NoWrap; text.color=new Color(.15f,.14f,.13f); text.alignment=TextAlignmentOptions.Center; text.rectTransform.sizeDelta=item.size;
            var lineObject=new GameObject("OverlapOutline"); lineObject.transform.SetParent(go.transform,false);
            var line=lineObject.AddComponent<LineRenderer>(); item.outline=line; line.sharedMaterial=red; line.useWorldSpace=false; line.loop=true; line.widthMultiplier=.009f; line.positionCount=4; line.enabled=false;
            var corners=BoardGeometry.Corners(Vector2.zero,item.size,0); for(int c=0;c<4;c++) line.SetPosition(c,new Vector3(corners[c].x,corners[c].y,.009f));
            PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Prefabs/"+item.id+".prefab");
            if(n<14)
            {
                item.state=BoardItemState.Board;
                item.boardPosition=n<6 ? new Vector2(-1.65f+n*.61f,.27f) : new Vector2(-1.1f+(n-6)*.22f,-.48f);
                go.transform.SetPositionAndRotation(surface.TransformPoint(new Vector3(item.boardPosition.x,item.boardPosition.y,.015f+n*.001f)),surface.rotation*Quaternion.Euler(0,0,item.angle));
            }
            else
            {
                item.state=BoardItemState.Floor;
                Vector3 p=surface.position+surface.right*(-1.55f+(n-14)*.48f)+surface.forward*(.7f+(n%3)*.3f); p.y=.03f;
                go.transform.SetPositionAndRotation(p,Quaternion.Euler(-90,0,item.angle));
            }
        }
        var canvasGo=new GameObject("BoardGameUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); canvasGo.transform.SetParent(root.transform,false);
        canvasGo.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; canvasGo.GetComponent<Canvas>().sortingOrder=150;
        var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        game.overlay=canvasGo;
        game.instruction=Text("Instructions",canvasGo.transform,font,new Vector2(.5f,1),new Vector2(0,-65),new Vector2(1500,100),26);
        game.progress=Text("FinishHold",canvasGo.transform,font,new Vector2(1,1),new Vector2(-170,-145),new Vector2(300,55),26);
        game.prompt=Text("ContextAction",canvasGo.transform,font,new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,65),25);
        game.prompt.alignment=TextAlignmentOptions.Left;
        game.ConfigureOverlayLayout();
        game.RefreshOverlap(); canvasGo.SetActive(false);
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"/Editor/PlaceholderLayoutV2.txt","Applied"); Preview(game);
    }
    static Material Material(string name,Color color,bool unlit)
    {
        string path=Folder+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path); if(material) return material;
        var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material=new Material(shader); material.color=color; if(material.HasProperty("_Cull")) material.SetFloat("_Cull",0);
        AssetDatabase.CreateAsset(material,path); return material;
    }
    static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,Vector2 anchor,Vector2 position,Vector2 size,float fontSize)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>(); t.font=font; t.fontSize=fontSize; t.alignment=TextAlignmentOptions.Center; t.color=Color.white; t.raycastTarget=false;
        t.outlineWidth=.2f; t.outlineColor=new Color32(20,20,20,255);
        var r=t.rectTransform; r.anchorMin=r.anchorMax=anchor; r.anchoredPosition=position; r.sizeDelta=size;
        return t;
    }
    public static void Preview(BoardGameController game)
    {
        var go=new GameObject("BoardGame preview camera"); var camera=go.AddComponent<Camera>();
        camera.transform.position=game.surface.position+game.surface.forward*5+Vector3.down*.3f+game.surface.right*.65f;
        camera.transform.LookAt(game.surface.position+Vector3.down*.5f+game.surface.right*.65f); camera.fieldOfView=57;
        var rt=new RenderTexture(1400,900,24); var old=RenderTexture.active;
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; var texture=new Texture2D(1400,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1400,900),0,0); texture.Apply(); File.WriteAllBytes(Folder+"/Preview.png",texture.EncodeToPNG()); Object.DestroyImmediate(texture); }
        finally { RenderTexture.active=old; camera.targetTexture=null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
    }
}
