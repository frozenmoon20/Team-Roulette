#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3;
using TeamRoulette.Room3.BallPlay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.BallPlayEditor
{
    [InitializeOnLoad]
    public static class BallGameSetup
    {
        const string Folder = "Assets/LivingRoom/BallGame";
        const string Request = Folder + "/Editor/Install.request.txt";
        const string FlowPath = "Assets/LivingRoom/Systems/Runtime/Room3FlowController.cs";
        const string ScenePath = "Assets/Scenes/Room3_Toy.unity";
        static BallGameSetup() { EditorApplication.delayCall += ProcessRequest; }

        static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += ProcessRequest; return; }
            if (!File.Exists(Request)) return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "install" && request != "configure") return;
            try
            {
                if (request == "install")
                {
                    bool changed = ConnectFlow();
                    File.WriteAllText(Request, "configure");
                    if (changed) { AssetDatabase.Refresh(); return; }
                }
                ConfigureScene();
                File.WriteAllText(Request, "installed");
                AssetDatabase.Refresh();
            }
            catch (Exception error) { File.WriteAllText(Request, "error: " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Room3/BallGame/Configure Current Toy Scene")]
        public static void ConfigureScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Room3_Toy 씬을 연 뒤 실행해주세요.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료해주세요.");
            var roots = scene.GetRootGameObjects();
            var nodes = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = roots.SelectMany(x => x.GetComponentsInChildren<FirstPersonPlayer>(true)).FirstOrDefault();
            var winter = nodes.FirstOrDefault(x => x.name == "Spitz_anim_IP") ?? nodes.FirstOrDefault(x => x.name == "Winter");
            var ball = nodes.FirstOrDefault(x => x.name == "Ball_3" || x.name == "테니스공");
            if (!player || !winter || !ball) throw new InvalidOperationException("기존 Player / Spitz_anim_IP / Ball_3를 확인해주세요.");
            var animator = winter.GetComponentInChildren<Animator>(true);
            if (!animator) throw new InvalidOperationException("겨울이 Animator가 없습니다.");
            var camera = player.GetComponentInChildren<Camera>(true);
            if (!camera) throw new InvalidOperationException("기존 플레이어 카메라가 없습니다.");
            var root = roots.FirstOrDefault(x => x.name == "BallGame");
            if (!root) { root = new GameObject("BallGame"); SceneManager.MoveGameObjectToScene(root,scene); Undo.RegisterCreatedObjectUndo(root,"테니스공 미니게임 추가"); }
            var game = root.GetComponent<BallGame>(); if (!game) game = Undo.AddComponent<BallGame>(root);
            Undo.RecordObject(game,"테니스공 미니게임 연결");
            game.player = player; game.playerCamera = camera; game.interactor = player.GetComponentInChildren<PlayerInteractor>(true);
            game.winter = winter; game.winterAnimator = animator; game.ball = ball.gameObject;
            game.mouth = winter.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "mouth")
                ?? winter.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "head");
            game.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            string controllerPath = "Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller";
            game.existingWinterController = animator.runtimeAnimatorController ? animator.runtimeAnimatorController
                : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (!game.existingWinterController) throw new InvalidOperationException("겨울이의 기존 데모 Animator Controller를 찾지 못했습니다.");
            var stand = FindOrCreate(root.transform,"Player Stand");
            // The existing player was placed by the user next to the house, looking into the yard.
            stand.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            game.playerStand = stand;
            var area = FindOrCreate(root.transform,"Play Area");
            area.position = new Vector3(-11.5f,stand.position.y,0); area.rotation=Quaternion.identity;
            var box = area.GetComponent<BoxCollider>(); if (!box) box=Undo.AddComponent<BoxCollider>(area.gameObject);
            box.isTrigger=true; box.center=new Vector3(0,.7f,0); box.size=new Vector3(13,1.4f,16);
            game.playArea=box;
            // Collision surface belongs to this game only. It does not change the existing ground mesh/material.
            var floor = FindOrCreate(root.transform,"Yard Collision Floor");
            floor.position=new Vector3(-10.65f,stand.position.y-.07f,0); floor.rotation=Quaternion.identity;
            var floorBox=floor.GetComponent<BoxCollider>(); if(!floorBox) floorBox=Undo.AddComponent<BoxCollider>(floor.gameObject);
            floorBox.isTrigger=false; floorBox.size=new Vector3(17.7f,.1f,18); floorBox.center=Vector3.zero;
            int addedColliders=0;
            // Fence/house meshes need collision for a genuine 3D throw and for local NavMesh baking.
            foreach (var environment in roots.Where(x => x.name == "Fence" || x.name == "suburb_house"))
                foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
                    if(filter.sharedMesh && !filter.GetComponent<Collider>())
                    {
                        var collider=Undo.AddComponent<MeshCollider>(filter.gameObject); collider.sharedMesh=filter.sharedMesh; addedColliders++;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);
                    }
            game.requiredSuccesses=5;
            EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene);
            string checkpoint=Path.Combine("Temp","BallGame_PreSetup.unity");
            if(!File.Exists(checkpoint) && File.Exists(ScenePath)) File.Copy(ScenePath,checkpoint);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Room3_Toy 저장 실패");
            var report=new StringBuilder();
            report.AppendLine("Room3_Toy 테니스공 미니게임 설정 완료");
            report.AppendLine("기존 Player: "+player.name+" @ "+player.transform.position);
            report.AppendLine("Camera: "+camera.name+" / Controller: "+player.GetType().Name);
            report.AppendLine("Input: "+(game.inputActions?game.inputActions.name:"Mouse.current fallback"));
            report.AppendLine("Interaction: "+(game.interactor?game.interactor.name:"없음"));
            report.AppendLine("Winter: "+winter.name+" / Animator 기존 연결: "+(animator.runtimeAnimatorController?animator.runtimeAnimatorController.name:"없음"));
            report.AppendLine("재사용 Animator: "+game.existingWinterController.name+" / In Place, applyRootMotion=false (런타임 한정)");
            report.AppendLine("Mouth: "+(game.mouth?game.mouth.name:"없음"));
            report.AppendLine("마당 범위: "+box.bounds+" / 충돌 보강: "+addedColliders);
            report.AppendLine("기존 NavMeshSurface: "+nodes.Count(x=>x.GetComponents<Component>().Any(c=>c && c.GetType().Name=="NavMeshSurface")));
            report.AppendLine("기존 씬 오브젝트 삭제: 0 / 공, 겨울이, 플레이어의 영구 위치 변경: 0");
            var controller=game.existingWinterController as UnityEditor.Animations.AnimatorController;
            if(controller)
            {
                var states=controller.layers[0].stateMachine.states.Select(x=>x.state.name).ToArray();
                foreach(var state in new[]{game.idleState,game.runState,game.pickupState,game.putDownState,game.barkState,game.sitStartState,game.sitState,game.curiousState})
                    report.AppendLine(state+": "+states.Contains(state));
            }
            File.WriteAllText(Folder+"/Editor/SetupReport.txt",report.ToString(),new UTF8Encoding(false));
            Debug.Log("[BallGame] "+report,game);
            Selection.activeGameObject=root;
        }

        static Transform FindOrCreate(Transform parent,string name)
        {
            var existing=parent.Find(name); if(existing)return existing;
            var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"테니스공 미니게임 구성"); go.transform.SetParent(parent,false);return go.transform;
        }

        static bool ConnectFlow()
        {
            string source=File.ReadAllText(FlowPath,Encoding.UTF8);
            if(source.Contains("public bool CompleteVisit(string id)")) return false;
            const string visitGuard="                // Only the exact object used to enter can finish this visit.";
            const string returnSignature="        IEnumerator ReturnHome()";
            if(!source.Contains(visitGuard)||!source.Contains(returnSignature)) throw new InvalidOperationException("Room3FlowController가 예상 구조와 다릅니다. 자동 연결을 중지했습니다.");
            var backup=Folder+"/Editor/Room3FlowController.before.txt";
            if(!File.Exists(backup))File.WriteAllText(backup,source,new UTF8Encoding(false));
            source=source.Replace(visitGuard,
                "                // Tennis is completed by its independent 3D game, never by a second click.\n"+
                "                if (VisitingObject == \"테니스공\") return;\n"+visitGuard);
            string api="        // The scene-local mini-game calls this only after its final fetch and celebration.\n"+
                "        public bool CompleteVisit(string id)\n        {\n"+
                "            if (IsTransitioning || string.IsNullOrEmpty(id) || VisitingObject != id\n"+
                "                || !visitScene.IsValid() || !visitScene.isLoaded || SceneManager.GetActiveScene() != visitScene) return false;\n"+
                "            StartCoroutine(ReturnHome());\n            return true;\n        }\n\n";
            source=source.Replace(returnSignature,api+returnSignature);
            File.WriteAllText(FlowPath,source,new UTF8Encoding(false));
            Debug.Log("[BallGame] 기존 Room3 왕복 이동에 테니스 완료 API와 재클릭 방지 연결만 추가했습니다.");
            return true;
        }
    }
}
#endif
