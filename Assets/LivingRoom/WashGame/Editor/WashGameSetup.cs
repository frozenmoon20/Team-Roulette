using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WashGameSetup
{
    public const string Folder = "Assets/LivingRoom/WashGame";
    public const string ScenePath = "Assets/Scenes/Room3_LivingRoom.unity";
    static WashGameController game;
    static TMP_FontAsset font;

    [MenuItem("Tools/WashGame/1 - Set up current scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new Exception("Open Room3_LivingRoom first.");
        if (Object.FindFirstObjectByType<WashGameController>(FindObjectsInactive.Include)) throw new Exception("WashGame already exists. Existing settings preserved.");
        var transforms = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
        var sink = transforms.Single(t => t.name == "세면대").gameObject;
        var bounds = sink.GetComponent<Renderer>().bounds;
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
        var interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (!player || !interactor || !interactor.playerCamera) throw new Exception("Missing player/camera/interactor.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/UI Korean SDF.asset");
        if (!font) font = TMP_Settings.defaultFontAsset;
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Audio");
        Directory.CreateDirectory(Folder + "/Reports");
        AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up WashGame");
        var root = New("WashGame", null);
        game = Undo.AddComponent<WashGameController>(root);
        game.player = player; game.interactor = interactor; game.viewCamera = interactor.playerCamera;
        game.otherInputs = Object.FindObjectsByType<MealInput>(FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Behaviour>().ToArray();
        Undo.SetTransformParent(sink.transform, root.transform, "Group WashGame sink");
        Target(sink, WashAction.Water, "세면대 · 세안 시작");
        var fixtures = New("Fixtures", root.transform).transform;
        Vector3 c = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        var porcelain = Mat("Porcelain", new Color(.81f,.9f,.91f));
        var basin = Mat("Basin", new Color(.18f,.33f,.36f));
        var chrome = Mat("Faucet", new Color(.58f,.69f,.73f), .8f);
        var soapMat = Mat("Soap", new Color(.48f,.92f,.70f));
        var towelMat = Mat("Towel", new Color(.52f,.72f,.95f));
        Cube("Basin", fixtures, c + new Vector3(0,.014f,0), new Vector3(1.05f,.025f,.56f), basin, WashAction.Water);
        Cube("BasinRimBack", fixtures, c+new Vector3(0,.07f,.33f),new Vector3(1.18f,.13f,.1f),porcelain,WashAction.Water);
        Cube("BasinRimFront", fixtures,c+new Vector3(0,.07f,-.33f),new Vector3(1.18f,.13f,.1f),porcelain,WashAction.Water);
        Cube("BasinRimLeft", fixtures,c+new Vector3(-.56f,.07f,0),new Vector3(.09f,.13f,.57f),porcelain,WashAction.Water);
        Cube("BasinRimRight", fixtures,c+new Vector3(.56f,.07f,0),new Vector3(.09f,.13f,.57f),porcelain,WashAction.Water);
        Cube("Faucet", fixtures,c+new Vector3(0,.31f,.35f),new Vector3(.1f,.56f,.1f),chrome,WashAction.Water);
        Cube("FaucetSpout", fixtures,c+new Vector3(0,.56f,.20f),new Vector3(.12f,.1f,.36f),chrome,WashAction.Water);
        var soap = Cube("비누", fixtures,c+new Vector3(-.79f,.075f,-.05f),new Vector3(.29f,.13f,.23f),soapMat,WashAction.Soap);
        var towel = Cube("수건", fixtures,c+new Vector3(.79f,.065f,-.03f),new Vector3(.30f,.11f,.48f),towelMat,WashAction.Towel);
        for(int i=0;i<3;i++) Cube("TowelFold_"+i,fixtures,c+new Vector3(.79f,.126f,-.18f+i*.13f),new Vector3(.27f,.006f,.015f),porcelain,WashAction.Towel);
        var view = New("WashView",root.transform).transform;
        view.position = c + new Vector3(0,1.05f,-1.55f);
        view.LookAt(c+new Vector3(0,.08f,0));
        game.washView = view;
        Label("SoapLabel", soap.transform.position+new Vector3(0,.24f,0), "비누",root.transform,view);
        Label("TowelLabel", towel.transform.position+new Vector3(0,.24f,0), "수건",root.transform,view);
        Label("WaterLabel",c+new Vector3(0,.79f,.25f),"물",root.transform,view);
        var waterGo = New("WaterParticles",root.transform);
        waterGo.transform.position=c+new Vector3(0,.50f,.04f);
        waterGo.transform.rotation=Quaternion.Euler(90,0,0);
        var ps = Undo.AddComponent<ParticleSystem>(waterGo);
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.playOnAwake=false; main.loop=true; main.startLifetime=.30f; main.startSpeed=1.6f;
        main.startSize=.035f; main.gravityModifier=.45f; main.startColor=new Color(.6f,.86f,1,.8f); main.maxParticles=300;
        var emission=ps.emission; emission.rateOverTime=150;
        var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=2; shape.radius=.026f;
        var waterMat = Mat("Water",new Color(.6f,.85f,1),0,"Universal Render Pipeline/Particles/Unlit");
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=waterMat;
        game.water=ps;
        var hit=New("WaterClickArea",root.transform);
        hit.transform.position=c+new Vector3(0,.28f,.04f);
        var box=Undo.AddComponent<BoxCollider>(hit); box.size=new Vector3(.18f,.45f,.18f); box.isTrigger=true;
        Target(hit,WashAction.Water,"물");
        game.actionAudio=Undo.AddComponent<AudioSource>(root); game.actionAudio.playOnAwake=false; game.actionAudio.spatialBlend=0;
        game.runningAudio=Undo.AddComponent<AudioSource>(waterGo); game.runningAudio.playOnAwake=false;
        game.runningAudio.loop=true; game.runningAudio.spatialBlend=0; game.runningAudio.volume=.18f;
        game.waterSound=Sound("WaterSplash",1.3f,false);
        game.dingSound=Sound("WashDing",.9f,true);
        game.runningAudio.clip=game.waterSound;
        BuildUI(root.transform);
        foreach(var ui in Object.FindObjectsByType<TeamRoulette.UI.GameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(ui,"Register WashGame audio");
            ui.effectSources=ui.effectSources.Concat(new[]{game.actionAudio,game.runningAudio}).Distinct().ToArray();
            EditorUtility.SetDirty(ui);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
        }
        AdoptExistingObjects();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject=root;
        File.WriteAllText(Folder+"/Reports/Setup.txt","WashGame installed in Room3_LivingRoom. Sink transform preserved. Coverage 80%. All created assets under WashGame.\n");
        Debug.Log("WashGame setup complete.");
    }

    static GameObject New(string name,Transform parent)
    {var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Create WashGame"); go.transform.SetParent(parent,false); return go;}

    [MenuItem("Tools/WashGame/4 - Connect existing wash objects")]
    public static void AdoptExistingObjects()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first.");
        game=Object.FindFirstObjectByType<WashGameController>(FindObjectsInactive.Include);
        if(!game) return;
        foreach(string labelName in new[]{"SoapLabel","TowelLabel","WaterLabel"})
        {var label=game.transform.Find(labelName);if(label)Undo.DestroyObjectImmediate(label.gameObject);}
        Undo.RecordObject(game,"Apply WashGame revisions");game.soapRequiredCoverage=.9f;game.requiredCoverage=.9f;
        Undo.RecordObject(game.player.transform,"Set living room start position");
        game.player.transform.position=new Vector3(33,.5f,4.5f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(game.player.transform);
        Undo.RecordObject(game.player,"Enable player movement");game.player.enabled=true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(game.player);
        Undo.RecordObject(game.progressFill,"Use edge-to-edge gauge");
        game.progressFill.sprite=null;game.progressFill.type=Image.Type.Simple;
        game.progressFill.rectTransform.anchorMin=Vector2.zero;game.progressFill.rectTransform.anchorMax=new Vector2(0,1);
        game.progressFill.rectTransform.offsetMin=game.progressFill.rectTransform.offsetMax=Vector2.zero;
        var progressTrack = (RectTransform)game.progressFill.transform.parent;
        Undo.RecordObject(progressTrack,"Place wash completion gauge at top");
        progressTrack.anchorMin=new Vector2(.3f,.9f);progressTrack.anchorMax=new Vector2(.7f,.915f);
        progressTrack.offsetMin=progressTrack.offsetMax=Vector2.zero;
        PrefabUtility.RecordPrefabInstancePropertyModifications(progressTrack);
        if(game.runningAudio){Undo.RecordObject(game.runningAudio,"Disable looping water audio");game.runningAudio.Stop();game.runningAudio.loop=false;game.runningAudio.clip=null;game.runningAudio.playOnAwake=false;}
        string blurPath=Folder+"/Materials/WashBlur.mat";
        var blur=AssetDatabase.LoadAssetAtPath<Material>(blurPath);
        if(!blur){blur=new Material(Shader.Find("WashGame/SeparableBlur"));AssetDatabase.CreateAsset(blur,blurPath);}
        Undo.RecordObject(game,"Connect wash blur");game.blurMaterial=blur;EditorUtility.SetDirty(game);
        var scene=game.gameObject.scene;
        var all=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach(string name in new[]{"비누","수건","수도꼭지","거울"})
        {
            var existing=all.FirstOrDefault(t=>t && t.name==name && !t.IsChildOf(game.transform));
            if(!existing) continue;
            if(name=="비누" || name=="수건")
            {
                foreach(var generated in game.GetComponentsInChildren<WashTarget>(true).Where(t=>t.name==name).ToArray())
                    Undo.DestroyObjectImmediate(generated.gameObject);
            }
            if(name=="수건") foreach(var fold in game.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("TowelFold_")).ToArray()) Undo.DestroyObjectImmediate(fold.gameObject);
            if(name=="수도꼭지")
            {
                var generated=game.transform.Find("Fixtures/Faucet");
                if(generated)Undo.DestroyObjectImmediate(generated.gameObject);
            }
            Undo.SetTransformParent(existing,game.transform,"Group existing wash object");
            if(name!="거울")
            {
                Target(existing.gameObject,name=="비누"?WashAction.Soap:name=="수건"?WashAction.Towel:WashAction.Water,name);
                var renderer=existing.GetComponent<Renderer>();
                if(renderer)
                {
                    Undo.RecordObject(renderer,"Color wash object");
                    renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+(name=="비누"?"Soap":name=="수건"?"Towel":"Faucet")+".mat");
                }
            }
        }
        Undo.RecordObject(game.washView,"Frame wash objects");
        var sink=game.GetComponentsInChildren<WashTarget>().First(t=>t.name=="세면대").GetComponent<Renderer>().bounds;
        game.washView.position=new Vector3(sink.center.x-.5f,sink.max.y+1.50f,sink.center.z-3.20f);
        game.washView.LookAt(new Vector3(sink.center.x-.5f,sink.max.y+.48f,sink.center.z));
        foreach(var pair in new[]{new[]{"SoapLabel","비누"},new[]{"TowelLabel","수건"}})
        {
            var label=game.transform.Find(pair[0]);
            var item=game.GetComponentsInChildren<WashTarget>().First(t=>t.name==pair[1]);
            if(label){Undo.RecordObject(label,"Position wash label");var b=item.GetComponent<Renderer>().bounds;label.position=new Vector3(b.center.x,b.max.y+.16f,b.center.z-.08f);label.rotation=game.washView.rotation;}
        }
        var waterLabel=game.transform.Find("WaterLabel");if(waterLabel){Undo.RecordObject(waterLabel,"Orient water label");waterLabel.rotation=game.washView.rotation;}
        var scaler=game.overlay.GetComponent<CanvasScaler>();Undo.RecordObject(scaler,"Scale readable wash UI");scaler.matchWidthOrHeight=1;
        Undo.RecordObject(game.instruction,"Fit wash instructions");game.instruction.enableAutoSizing=true;game.instruction.fontSizeMin=18;game.instruction.fontSizeMax=30;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 s,Material m,WashAction action)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(go,"Create WashGame fixture");
        go.name=name; go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=s;
        go.GetComponent<Renderer>().sharedMaterial=m; Target(go,action,name);return go;
    }
    static void Target(GameObject go,WashAction action,string name)
    {var t=Undo.AddComponent<WashTarget>(go);t.game=game;t.action=action;t.displayName=name;}
    static Material Mat(string name,Color color,float metallic=0,string shader="Universal Render Pipeline/Lit")
    {
        string path=Folder+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(shader));m.color=color;if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);AssetDatabase.CreateAsset(m,path);}return m;
    }
    static void Label(string name,Vector3 p,string caption,Transform parent,Transform view)
    {
        var go=New(name,parent);go.transform.position=p;go.transform.rotation=view.rotation;
        var t=Undo.AddComponent<TextMeshPro>(go);t.font=font;t.text=caption;t.fontSize=2;t.alignment=TextAlignmentOptions.Center;
        t.color=new Color(.85f,1,1);t.rectTransform.sizeDelta=new Vector2(.5f,.2f);
    }
    static RectTransform UI(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Create WashGame UI");
        var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
    }
    static void BuildUI(Transform parent)
    {
        var rt=UI("WashOverlay",parent,Vector2.zero,Vector2.one);
        var canvas=Undo.AddComponent<Canvas>(rt.gameObject);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;
        var scale=Undo.AddComponent<CanvasScaler>(rt.gameObject);scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=1;
        game.overlay=canvas;
        var screen=UI("LiveScreen",rt,Vector2.zero,Vector2.one);
        game.screen=Undo.AddComponent<RawImage>(screen.gameObject);game.screen.raycastTarget=false;
        string path=Folder+"/Materials/WashScreen.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("WashGame/LiveWashScreen"));AssetDatabase.CreateAsset(m,path);}game.screenMaterial=m;
        var panel=UI("InstructionPanel",rt,new Vector2(.14f,.76f),new Vector2(.86f,.89f));
        var image=Undo.AddComponent<Image>(panel.gameObject);image.color=new Color(.025f,.06f,.075f,.90f);image.raycastTarget=false;
        var label=UI("Instruction",panel,new Vector2(.02f,.12f),new Vector2(.98f,.95f));
        game.instruction=Undo.AddComponent<TextMeshProUGUI>(label.gameObject);game.instruction.font=font;
        game.instruction.fontSize=30;game.instruction.alignment=TextAlignmentOptions.Center;game.instruction.raycastTarget=false;
        var track=UI("Progress",rt,new Vector2(.3f,.9f),new Vector2(.7f,.915f));
        var trackImage=Undo.AddComponent<Image>(track.gameObject);trackImage.color=new Color(.05f,.13f,.16f,.9f);trackImage.raycastTarget=false;
        var fill=UI("Fill",track,Vector2.zero,Vector2.one); game.progressFill=Undo.AddComponent<Image>(fill.gameObject);
        game.progressFill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        game.progressFill.type=Image.Type.Filled;game.progressFill.fillMethod=Image.FillMethod.Horizontal;game.progressFill.color=new Color(.4f,.94f,.8f);game.progressFill.raycastTarget=false;
        game.toolCursor=UI("HeldTool",rt,Vector2.zero,Vector2.zero);game.toolCursor.sizeDelta=new Vector2(90,64);
        game.toolImage=Undo.AddComponent<Image>(game.toolCursor.gameObject);game.toolImage.raycastTarget=false;
        game.toolImage.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        game.toolCursor.gameObject.SetActive(false);rt.gameObject.SetActive(false);
    }
    static AudioClip Sound(string name,float seconds,bool ding)
    {
        string path=Folder+"/Audio/"+name+".wav";
        if(!File.Exists(path))
        {
            const int rate=22050;int samples=(int)(rate*seconds);var random=new System.Random(2701);
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples*2);double low=0;
                for(int i=0;i<samples;i++)
                {
                    double t=(double)i/rate;double n=random.NextDouble()*2-1;low=.84*low+.16*n;
                    double envelope=Math.Min(1,t*35)*Math.Min(1,(seconds-t)*20);
                    double v=ding ? (Math.Sin(2*Math.PI*1320*t)+.45*Math.Sin(2*Math.PI*1980*t))*Math.Exp(-6*t)*.42 :
                        (low*.95+n*.08+.04*Math.Sin(2*Math.PI*(520*t+2*Math.Sin(13*t))))*(.65+.2*Math.Sin(23*t));
                    w.Write((short)(Math.Max(-1,Math.Min(1,v*envelope))*26000));
                }
            }
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
