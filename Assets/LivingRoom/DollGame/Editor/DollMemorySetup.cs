#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.UI;
using TeamRoulette.Room3.DollMemory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DollMemorySetup
{
    public const string Folder="Assets/LivingRoom/DollGame";
    static DollMemorySetup() { EditorApplication.delayCall+=Auto; }
    static void Auto()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Auto; return; }
        if(!File.Exists(Folder+"/Editor/Install.request") || File.ReadAllText(Folder+"/Editor/Install.request").Trim()!="install") return;
        try { Configure(); File.WriteAllText(Folder+"/Editor/Install.request","installed"); }
        catch(Exception e) { File.WriteAllText(Folder+"/Editor/Install.request","error: "+e); Debug.LogException(e); }
    }
    static Transform[] Nodes(Scene s) => s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
    static Transform Point(Transform root,string name,Vector3 position,Vector3 look)
    {
        var t=root.Find(name); if(!t) { t=new GameObject(name).transform; t.SetParent(root,false); }
        t.position=position; if((look-position).sqrMagnitude>.001f) t.rotation=Quaternion.LookRotation(look-position); return t;
    }
    static GameObject Root(Scene s,string name) { var go=s.GetRootGameObjects().FirstOrDefault(r=>r.name==name); if(!go) { go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,s); } return go; }
    static T Component<T>(GameObject go) where T:Component { var c=go.GetComponent<T>(); return c ? c : go.AddComponent<T>(); }
    static Material Material(string name,Color color)
    {
        string path=Folder+"/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); mat.color=color; AssetDatabase.CreateAsset(mat,path); } return mat;
    }
    [MenuItem("Tools/Room3/Doll Memory/Install or Update")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 정지한 뒤 설치하세요.");
        Scene old=SceneManager.GetSceneByName("Room3_OldLivingRoom"),yard=SceneManager.GetSceneByName("Room3_Toy");
        bool closeOld=!old.IsValid()||!old.isLoaded,closeYard=!yard.IsValid()||!yard.isLoaded;
        if(closeOld) old=EditorSceneManager.OpenScene("Assets/Scenes/Room3_OldLivingRoom.unity",OpenSceneMode.Additive);
        if(closeYard) yard=EditorSceneManager.OpenScene("Assets/Scenes/Room3_Toy.unity",OpenSceneMode.Additive);
        var n=Nodes(old); var y=Nodes(yard);
        var washer=n.Single(t=>t.name=="Washingmachine"); var cushion=n.Single(t=>t.name=="DogAccesories-006");
        var dog=n.Single(t=>t.name=="Spitz_anim_IP"); var player=n.Select(t=>t.GetComponent<FirstPersonPlayer>()).First(p=>p);
        var rack=y.Single(t=>t.name=="To_toilet_shelf"); var dryingDuck=y.Single(t=>t.name=="08_duck_prefab");
        var root=Root(old,"DollMemory"); var game=Component<DollMemorySequence>(root); var winter=Component<DollWinter>(root);
        game.player=player; game.playerCamera=player.GetComponentInChildren<Camera>(true); game.interactor=player.GetComponentInChildren<PlayerInteractor>(true);
        game.sharedUI=n.Select(t=>t.GetComponent<GameUI>()).First(ui=>ui && ui.objectiveText && ui.hud); game.winter=winter;
        winter.dog=dog; winter.animator=dog.GetComponent<Animator>();
        winter.mouth=dog.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Mouth.R") ?? dog.GetComponentsInChildren<Transform>(true).First(t=>t.name=="head");
        winter.fallbackController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller");
        // The old placeholder scene never had a duck. Use the very same purchased prefab.
        var duck=n.FirstOrDefault(t=>t.name=="08_duck_prefab");
        if(!duck)
        {
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(dryingDuck.gameObject);
            if(string.IsNullOrEmpty(path)) throw new InvalidOperationException("마당의 오리 프리팹 연결이 없습니다.");
            duck=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),old)).transform;
            duck.name="08_duck_prefab"; duck.position=new Vector3(2.7f,.7f,-2.7f); duck.localScale=dryingDuck.lossyScale;
        }
        winter.restOffset=new Vector3(0,.02f,.17f);
        var bones=dog.GetComponentsInChildren<Transform>(true);
        var nose=bones.FirstOrDefault(t=>t.name=="nose"); var head=bones.FirstOrDefault(t=>t.name=="head");
        if(nose && head) { winter.mouth=Point(head,"Doll Mouth Attachment",nose.position+dog.forward*.065f-Vector3.up*.025f,nose.position+dog.forward); winter.mouthOffset=Vector3.zero; }
        game.sceneDoll=duck.gameObject; Component<DollMemoryStart>(duck.gameObject).memory=game;
        game.entry=Point(root.transform,"Chase Entry",new Vector3(2.6f,.73f,-4.9f),dog.position+Vector3.up*.5f);
        game.entry.rotation=Quaternion.Euler(0,0,0);
        game.chasePoints=new[]{Point(root.transform,"Chase A",new Vector3(2.8f,.7f,-1.8f),Vector3.zero),Point(root.transform,"Chase B",new Vector3(1.2f,.7f,-1.8f),Vector3.zero),Point(root.transform,"Chase C",new Vector3(1.6f,.7f,-4.4f),Vector3.zero)};
        Vector3 front=washer.forward; front.y=0; front.Normalize();
        Vector3 center=washer.GetComponent<Renderer>().bounds.center;
        Vector3 drum=center+front*.51f+Vector3.down*.06f;
        game.drum=Point(root.transform,"Washer Drum",drum,drum+front);
        game.washerButton=Point(root.transform,"Washer Start Button",center+front*.51f+Vector3.up*.4f+washer.right*.25f,center+front*2);
        var button=Component<SphereCollider>(game.washerButton.gameObject); button.radius=.105f;
        var marker=game.washerButton.Find("Button Surface");if(marker)UnityEngine.Object.DestroyImmediate(marker.gameObject);
        var backing=root.transform.Find("Drum Interior");if(backing)UnityEngine.Object.DestroyImmediate(backing.gameObject);
        Component<DollWasherButton>(game.washerButton.gameObject).memory=game;
        game.washerView=Point(root.transform,"Washer View",center+front*1.35f+Vector3.up*.78f,center);
        game.washerSeat=Point(root.transform,"Winter Washer Seat",new Vector3(center.x+.62f,.7f,center.z-.97f),center);
        game.washerRoute=new[]{Point(root.transform,"Washer Route 1",new Vector3(-2.5f,2.05f,-2.2f),new Vector3(-2.8f,1.5f,.5f)),Point(root.transform,"Washer Route 2",new Vector3(-2.8f,2.05f,.5f),center),Point(root.transform,"Washer Route 3",new Vector3(-3.7f,2.05f,.5f),center)};
        var cb=cushion.GetComponent<Renderer>().bounds;
        float seatY=cb.center.y;
        var seatCollider=cushion.GetComponent<MeshCollider>(); bool temporary=!seatCollider;
        if(temporary) { seatCollider=cushion.gameObject.AddComponent<MeshCollider>(); seatCollider.sharedMesh=cushion.GetComponent<MeshFilter>().sharedMesh; }
        if(seatCollider.Raycast(new Ray(new Vector3(cb.center.x,cb.max.y+1,cb.center.z),Vector3.down),out var seatHit,3)) seatY=seatHit.point.y+.025f;
        if(temporary) UnityEngine.Object.DestroyImmediate(seatCollider);
        game.cushion=Point(root.transform,"Cushion Rest",new Vector3(cb.center.x,seatY,cb.center.z),new Vector3(cb.center.x,seatY,cb.center.z-1));
        game.restView=Point(root.transform,"Rest View",game.cushion.position+new Vector3(1.15f,.8f,-.95f),game.cushion.position+Vector3.up*.23f);
        var yardRoot=Root(yard,"DollMemoryYard"); var binding=Component<DollYardBindings>(yardRoot);
        binding.dryingPoint=Point(yardRoot.transform,"Drying Point",dryingDuck.position,dryingDuck.position+dryingDuck.forward);
        binding.houseDoor=Point(yardRoot.transform,"House Door",new Vector3(10.3f,.5f,1),new Vector3(9,1,1));
        binding.dogStand=Point(yardRoot.transform,"Winter Drying Stand",new Vector3(rack.position.x+.72f,0,rack.position.z),rack.position);
        binding.view=Point(yardRoot.transform,"Drying View",rack.position+new Vector3(2.5f,1.45f,-1.3f),rack.position+Vector3.up*.8f);
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(winter); EditorUtility.SetDirty(binding);
        EditorSceneManager.MarkSceneDirty(old); EditorSceneManager.MarkSceneDirty(yard);
        EditorSceneManager.SaveScene(old); EditorSceneManager.SaveScene(yard); AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"/Editor/SetupReport.txt","설치 완료\n기존 세탁기: "+washer.position+"\n기존 쿠션: "+cushion.position+"\n기존 건조대: "+rack.position+"\n사용자 지정 인형 건조 위치: "+dryingDuck.position+"\nNavMesh 없음: 거실 지정 지점 + 장애물 검사\n기존 Spitz Animator 재사용\n기존 오브젝트 삭제/배치 변경 없음\n",new UTF8Encoding(false));
        if(closeOld) EditorSceneManager.CloseScene(old,true); if(closeYard) EditorSceneManager.CloseScene(yard,true);
    }
}
#endif

