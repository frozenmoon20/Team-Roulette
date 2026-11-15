#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.UI;
using TeamRoulette.Room3.Nosework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.NoseworkEditor
{
    [InitializeOnLoad]
    public static class NoseworkSetup
    {
        public const string Folder="Assets/LivingRoom/NoseworkGame";
        const string Request=Folder+"/Editor/Install.request.txt";
        static NoseworkSetup() { EditorApplication.delayCall+=Install; }
        static void Install()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall+=Install; return; }
            if(!File.Exists(Request) || File.ReadAllText(Request).Trim()!="install") return;
            try { Configure(); File.WriteAllText(Request,"installed"); AssetDatabase.Refresh(); }
            catch(Exception ex) { File.WriteAllText(Request,"error: "+ex); Debug.LogException(ex); }
        }
        [MenuItem("Tools/Room3/NoseworkGame/Configure Current Scene")]
        public static void Configure()
        {
            var scene=SceneManager.GetSceneByName("Room3_OldLivingRoom");
            if(!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying) throw new InvalidOperationException("Room3_OldLivingRoom 씬을 열고 플레이를 종료해 주세요.");
            var nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var toy=nodes.Single(t=>t.name=="carrot_snuffle_game");
            var parts=toy.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Carrot_",StringComparison.Ordinal)).OrderBy(m=>m.name).ToArray();
            var player=nodes.Select(t=>t.GetComponent<FirstPersonPlayer>()).FirstOrDefault(p=>p);
            var dog=nodes.FirstOrDefault(t=>t.name=="Spitz_anim_IP");
            var camera=player ? player.GetComponentInChildren<Camera>(true):null;
            if(!player || !camera || !dog || parts.Length<6) throw new InvalidOperationException("Player / Camera / Spitz_anim_IP / 독립 당근 6개 이상이 필요합니다.");
            var report=new StringBuilder("노즈워크 구현 전 실제 씬 검사\n");
            foreach(var node in nodes.Where(t=>t==toy || t.IsChildOf(toy) || t==player.transform || t==dog || t==camera.transform))
                report.AppendLine(node.name+" | world="+node.position.ToString("F3")+" scale="+node.lossyScale+" | "+string.Join(",",node.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name)));
            report.AppendLine("당근 수: "+parts.Length+" / 각 당근의 몸통과 잎은 한 MeshFilter로 구성됨");
            report.AppendLine("씬 NavMeshAgent 수: "+nodes.Count(t=>t.GetComponent<UnityEngine.AI.NavMeshAgent>()));
            report.AppendLine("NavMesh triangulation vertices: "+UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length);
            report.AppendLine("Interaction: PlayerInteractor / IInteractable / 왼쪽 클릭");
            report.AppendLine("Room3 Manager: Resources/Room3GameManager, Flow.CompleteVisit(carrot_snuffle_game)");
            File.WriteAllText(Folder+"/Editor/BeforeSetup.txt",report.ToString(),new UTF8Encoding(false));
            var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="NoseworkGame");
            if(!root) { root=new GameObject("NoseworkGame"); SceneManager.MoveGameObjectToScene(root,scene); Undo.RegisterCreatedObjectUndo(root,"노즈워크 추가"); }
            var game=root.GetComponent<NoseworkGame>() ?? Undo.AddComponent<NoseworkGame>(root);
            Undo.RecordObject(game,"노즈워크 연결");
            game.toy=toy; game.player=player; game.playerCamera=camera; game.interactor=player.GetComponentInChildren<PlayerInteractor>(true);
            // The placeholder player was at world zero, below this room's floor (0.7m).
            // Give the memory scene a usable entry pose facing the existing toy.
            var boardBounds = new Bounds(toy.position,Vector3.zero);
            foreach(var renderer in toy.GetComponentsInChildren<Renderer>(true)) boardBounds.Encapsulate(renderer.bounds);
            var entry=root.transform.Find("Player Entry");
            if(!entry) { entry=new GameObject("Player Entry").transform; entry.SetParent(root.transform,false); }
            Vector3 viewSide=-toy.forward; viewSide.y=0; viewSide.Normalize();
            entry.position=new Vector3(boardBounds.center.x,boardBounds.min.y+.02f,boardBounds.center.z)+viewSide*1.15f;
            entry.rotation=Quaternion.LookRotation(-viewSide);
            Undo.RecordObject(player.transform,"노즈워크 입장 위치");
            player.transform.SetPositionAndRotation(entry.position,entry.rotation);
            if(player.cameraRoot) { Undo.RecordObject(player.cameraRoot,"노즈워크 입장 시선"); player.cameraRoot.localRotation=Quaternion.Euler(43,0,0); }
            var view=root.transform.Find("Game View");
            if(!view) { view=new GameObject("Game View").transform; view.SetParent(root.transform,false); }
            Undo.RecordObject(view,"사진 기준 노즈워크 시야");
            view.position=boardBounds.center+viewSide*1.45f+Vector3.up*1.2f;
            view.rotation=Quaternion.LookRotation(boardBounds.center-viewSide*.18f+Vector3.up*.12f-view.position);
            // The user has framed the reference in the current Scene view: keep that exact framing.
            var sceneCamera=SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.camera:null;
            if(sceneCamera && !sceneCamera.orthographic && Vector3.Distance(sceneCamera.transform.position,boardBounds.center)<10
                && Vector3.Dot(sceneCamera.transform.forward,(boardBounds.center-sceneCamera.transform.position).normalized)>.85f)
            {
                view.SetPositionAndRotation(sceneCamera.transform.position,sceneCamera.transform.rotation);
                game.gameFieldOfView=sceneCamera.fieldOfView;
            }
            game.gameView=view;
            game.sharedUI=nodes.Select(t=>t.GetComponent<GameUI>()).FirstOrDefault(ui=>ui && ui.hud && ui.objectiveText);
            var actor=root.GetComponent<NoseworkWinter>() ?? Undo.AddComponent<NoseworkWinter>(root); game.winter=actor;
            Undo.RecordObject(actor,"겨울이 연결"); actor.winter=dog; actor.animator=dog.GetComponentInChildren<Animator>(true);
            const string controller=Folder+"/WinterNosework.controller";
            if(!AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controller))
                if(!AssetDatabase.CopyAsset("Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller",controller)) throw new IOException("애니메이터 복사 실패");
            actor.controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controller);
            if(!actor.animator || !actor.controller) throw new InvalidOperationException("겨울이 Animator가 없습니다.");
            var pet=root.GetComponent<NoseworkPetting>() ?? Undo.AddComponent<NoseworkPetting>(root);
            Undo.RecordObject(pet,"쓰다듬기 연결"); game.petting=pet;
            pet.hand=player.GetComponentInChildren<PlayerHand>(true);
            var bones=dog.GetComponentsInChildren<Transform>(true);
            pet.head=bones.FirstOrDefault(t=>t.name=="head");
            pet.earLeft=bones.FirstOrDefault(t=>t.name=="ear_L" || t.name=="ear_l");
            pet.earRight=bones.FirstOrDefault(t=>t.name=="ear_R" || t.name=="ear_r");
            if(!pet.hand || !pet.hand.handModel || !pet.head) throw new InvalidOperationException("쓰다듬기에 필요한 손 모델 또는 겨울이 머리가 없습니다.");
            EditorUtility.SetDirty(pet);
            game.carrots=parts.Select((part,index)=> {
                var carrot=part.GetComponent<NoseworkCarrot>() ?? Undo.AddComponent<NoseworkCarrot>(part.gameObject);
                Undo.RecordObject(carrot,"당근 연결"); carrot.index=index; carrot.game=game;
                EnsureCollider(part,true); EditorUtility.SetDirty(carrot); return carrot;
            }).ToArray();
            foreach(var part in toy.GetComponentsInChildren<MeshFilter>(true).Except(parts))
            {
                var start=part.GetComponent<NoseworkStartPoint>() ?? Undo.AddComponent<NoseworkStartPoint>(part.gameObject);
                Undo.RecordObject(start,"받침 상호작용"); start.game=game; EnsureCollider(part); EditorUtility.SetDirty(start);
            }
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(actor); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패");
            report.AppendLine("설치 완료: 기존 오브젝트 유지 / 1,2,3단계 / 우측 2D 수집 UI / Esc 복구 / 완료 시 기존 매니저 복귀");
            File.WriteAllText(Folder+"/Editor/SetupReport.txt",report.ToString(),new UTF8Encoding(false));
            Selection.activeGameObject=root; Debug.Log("[Nosework] 설치 완료. 당근 "+parts.Length+"개",game);
        }
        static void EnsureCollider(MeshFilter mesh,bool exact=false)
        {
            if(exact)
            {
                var box=mesh.GetComponent<BoxCollider>();
                if(box) { Undo.RecordObject(box,"당근 선택 판정 보정"); box.enabled=false; }
                var surface=mesh.GetComponent<MeshCollider>();
                if(!surface) surface=Undo.AddComponent<MeshCollider>(mesh.gameObject);
                surface.sharedMesh=mesh.sharedMesh; surface.convex=false; surface.enabled=true; return;
            }
            if(mesh.GetComponent<Collider>()) return;
            var collider=Undo.AddComponent<BoxCollider>(mesh.gameObject);
            collider.center=mesh.sharedMesh.bounds.center; collider.size=mesh.sharedMesh.bounds.size;
        }
    }
}
#endif
