#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3.BonePlay;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.BoneEditor
{
    [InitializeOnLoad]
    public static class BoneCatchSetup
    {
        public const string Folder = "Assets/LivingRoom/BoneGame";
        const string Request = Folder + "/Editor/Install.request.txt";
        static BoneCatchSetup() { EditorApplication.delayCall += Install; }
        static void Install()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += Install; return; }
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install") return;
            try { Configure(); File.WriteAllText(Request, "installed"); AssetDatabase.Refresh(); }
            catch (Exception ex) { File.WriteAllText(Request, "error: " + ex); Debug.LogException(ex); }
        }
        [MenuItem("Tools/Room3/BoneGame/Configure Current Toy Scene")]
        public static void Configure()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Room3_Toy.unity");
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying) throw new InvalidOperationException("Room3_Toy 씬을 열고 플레이를 중지해주세요.");
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = nodes.Select(t => t.GetComponent<FirstPersonPlayer>()).FirstOrDefault(p => p);
            var dog = nodes.FirstOrDefault(t => t.name == "Spitz_anim_IP") ?? nodes.FirstOrDefault(t => t.name == "Winter");
            var toy = nodes.FirstOrDefault(t => t.name == "Bone_1") ?? nodes.FirstOrDefault(t => t.name == BoneCatchGame.ToyId);
            var ui = nodes.Select(t => t.GetComponent<GameUI>()).FirstOrDefault(u => u && u.hud && u.objectiveText);
            var camera = player ? player.GetComponentInChildren<Camera>(true) : null;
            var animator = dog ? dog.GetComponentInChildren<Animator>(true) : null;
            var mouth = dog ? dog.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mouth") : null;
            if (!player || !camera || !animator || !mouth || !toy || !ui) throw new InvalidOperationException("기존 Player / Camera / Winter / mouth / Bone_1 / GameUI를 찾지 못했습니다.");
            var controller = animator.runtimeAnimatorController;
            if (!controller) controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller");
            if (!controller) controller = MinimalController();
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "BoneGame");
            if (!root) { root = new GameObject("BoneGame"); SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "뼈다귀 캐치 추가"); }
            var game = root.GetComponent<BoneCatchGame>();
            if (!game) game = Undo.AddComponent<BoneCatchGame>(root);
            Undo.RecordObject(game, "뼈다귀 캐치 연결");
            game.player = player; game.playerCamera = camera; game.interactor = player.GetComponentInChildren<PlayerInteractor>(true);
            game.winter = dog; game.winterAnimator = animator; game.mouth = mouth; game.sceneBone = toy.gameObject; game.sharedUI = ui; game.existingController = controller;
            game.carriedEuler = Vector3.zero;
            var stand = root.transform.Find("Player Stand");
            if (!stand) { stand = new GameObject("Player Stand").transform; stand.SetParent(root.transform, false); }
            stand.SetPositionAndRotation(player.transform.position, player.transform.rotation); game.playerStand = stand;
            if (!game.squeakSource) game.squeakSource = root.GetComponent<AudioSource>();
            if (!game.squeakSource) game.squeakSource = Undo.AddComponent<AudioSource>(root);
            game.squeakSource.playOnAwake = false; game.squeakSource.spatialBlend = 0; game.squeakSource.volume = 1;
            if (!game.squeakClip) game.squeakClip = MakeSqueak();
            EditorUtility.SetDirty(game); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Room3_Toy 저장에 실패했습니다.");
            var report = new StringBuilder("뼈다귀 캐치 설정 완료\n");
            report.AppendLine("Player / Camera / Interaction / UI / Winter / mouth: 기존 인스턴스 재사용");
            report.AppendLine("Controller: " + AssetDatabase.GetAssetPath(controller));
            report.AppendLine("Bone: " + toy.name + " / prefab: " + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(toy));
            report.AppendLine("Input: 기존 Input System과 같은 Mouse 장치, 캐치 전용 클릭 액션");
            report.AppendLine("NavMeshAgent: " + nodes.Count(n => n.GetComponent<UnityEngine.AI.NavMeshAgent>()) + " / baked vertices: " + UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length);
            report.AppendLine("기존 NavMesh 경로가 있으면 회수에 사용, 없으면 가까운 평평한 마당에서 이동");
            report.AppendLine("기존 테니스공 / 터그 실행 여부는 방문 ID로 분기. 씬 직접 실행은 뼈다귀 미리보기");
            report.AppendLine("Room3 Manager: GameManager.Flow.CompleteVisit(뼈다귀 장난감)");
            report.AppendLine("Squeak Clip: " + AssetDatabase.GetAssetPath(game.squeakClip) + " (Inspector에서 교체 가능)");
            report.AppendLine("기본 성공 횟수: " + game.requiredSuccesses + " / 투척 시간: " + game.flightSeconds + "초");
            report.AppendLine("기존 오브젝트 삭제 및 저장된 위치 변경: 0 / 추가 씬 루트: BoneGame");
            var ac = controller as AnimatorController;
            if (ac) foreach (var state in new[] {game.idleState, game.biteState, game.leftState, game.rightState, game.jumpState, game.forwardState, game.pickupState, game.runState})
                report.AppendLine(state + ": " + ac.layers[0].stateMachine.states.Any(s => s.state.name == state));
            File.WriteAllText(Folder + "/Editor/SetupReport.txt", report.ToString(), new UTF8Encoding(false));
            Selection.activeGameObject = root; Debug.Log("[BoneCatch] 기존 오브젝트를 보존하고 캐치 게임을 연결했습니다.", game);
        }
        static RuntimeAnimatorController MinimalController()
        {
            const string path = Folder + "/WinterCatch.controller";
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path); if (existing) return existing;
            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] {"Assets/StoreAssets/Dog - Pomeranian Spitz"})
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>().ToArray();
            var names = new[] {"Idle_1", "Attack_Bite_IP", "Trot_R_IP", "Trot_L_IP", "Jump_Place_IP", "Jump_F_IP", "Pick_up", "Trot_F_IP"};
            var selected = names.Select(n => clips.FirstOrDefault(c => c.name == "Arm_Spitz|" + n)).ToArray();
            if (selected.Any(c => !c)) throw new InvalidOperationException("최소 캐치 애니메이션을 모두 찾지 못했습니다.");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            for (int i = 0; i < names.Length; i++) { var state = controller.layers[0].stateMachine.AddState("Arm_Spitz|" + names[i]); state.motion = selected[i]; }
            return controller;
        }
        static AudioClip MakeSqueak()
        {
            if (!AssetDatabase.IsValidFolder(Folder + "/Audio")) AssetDatabase.CreateFolder(Folder, "Audio");
            string path = Folder + "/Audio/BoneSqueak.wav";
            if (!File.Exists(path))
            {
                const int rate = 22050, count = 5733;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2); double phase = 0;
                    for (int i = 0; i < count; i++)
                    {
                        double t = i / (double)count; phase += 2 * Math.PI * (1750 - 1100 * t + 130 * Math.Sin(t * Math.PI * 3)) / rate;
                        double value = (Math.Sin(phase) + .2 * Math.Sin(phase * 2)) * Math.Pow(Math.Sin(t * Math.PI), 1.5) * .48;
                        writer.Write((short)(value * 32767));
                    }
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
#endif
