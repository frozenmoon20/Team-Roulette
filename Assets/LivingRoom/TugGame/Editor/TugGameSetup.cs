#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamRoulette.Room3.TugPlay;
using TeamRoulette.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TeamRoulette.Room3.TugEditor
{
    [InitializeOnLoad]
    public static class TugGameSetup
    {
        public const string Folder = "Assets/LivingRoom/TugGame";
        const string Request = Folder + "/Editor/Install.request.txt";
        static TugGameSetup() { EditorApplication.delayCall += Install; }
        static void Install()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += Install; return; }
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "install") return;
            try { Configure(); File.WriteAllText(Request, "installed"); AssetDatabase.Refresh(); }
            catch (Exception ex) { File.WriteAllText(Request, "error: " + ex); Debug.LogException(ex); }
        }

        [MenuItem("Tools/Room3/TugGame/Configure Current Toy Scene")]
        public static void Configure()
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Room3_Toy.unity");
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying)
                throw new InvalidOperationException("플레이 모드를 종료하고 Room3_Toy 씬을 열어주세요.");
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = nodes.Select(t => t.GetComponent<FirstPersonPlayer>()).FirstOrDefault(p => p);
            var winter = nodes.FirstOrDefault(t => t.name == "Spitz_anim_IP") ?? nodes.FirstOrDefault(t => t.name == "Winter");
            var rope = nodes.FirstOrDefault(t => t.name == "DogAccesories-001") ?? nodes.FirstOrDefault(t => t.name.Contains("TugRope"));
            if (!player || !winter || !rope) throw new InvalidOperationException("Player / Winter / DogAccesories-001 연결을 확인해주세요.");
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "TugGame");
            if (!root) { root = new GameObject("TugGame"); SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "로프 놀이 추가"); }
            var game = root.GetComponent<TugGame>();
            if (!game) game = Undo.AddComponent<TugGame>(root);
            Undo.RecordObject(game, "로프 놀이 연결");
            game.player = player; game.playerCamera = player.GetComponentInChildren<Camera>(true);
            game.interactor = player.GetComponentInChildren<PlayerInteractor>(true);
            game.winter = winter; game.winterAnimator = winter.GetComponentInChildren<Animator>(true);
            var bones = winter.GetComponentsInChildren<Transform>(true);
            game.head = bones.FirstOrDefault(t => t.name == "head"); game.mouth = bones.FirstOrDefault(t => t.name == "mouth") ?? game.head;
            game.sceneRope = rope.gameObject;
            game.sharedUI = nodes.Select(t => t.GetComponent<GameUI>()).FirstOrDefault(ui => ui && ui.hud && ui.objectiveText);
            game.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            game.existingController = game.winterAnimator.runtimeAnimatorController;
            if (!game.existingController)
                game.existingController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/StoreAssets/Dog - Pomeranian Spitz/Red_Deer/Dogs/Spitz/Dog/FBX/Spitz_AContr_Demo.controller");
            if (!game.existingController || !game.mouth || !game.head || !game.sharedUI)
                throw new InvalidOperationException("기존 Animator / 머리·입 / GameUI 연결을 찾을 수 없습니다.");
            var stand = root.transform.Find("Player Stand");
            if (!stand) { stand = new GameObject("Player Stand").transform; stand.SetParent(root.transform, false); }
            stand.SetPositionAndRotation(player.transform.position, player.transform.rotation); game.playerStand = stand;
            if (!AssetDatabase.IsValidFolder(Folder + "/Prefabs")) AssetDatabase.CreateFolder(Folder, "Prefabs");
            const string source = "Assets/LivingRoom/Assets/DogAccessories/DogAccesories-001_TugRope.prefab";
            const string dest = Folder + "/Prefabs/TugRope.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(dest) && !AssetDatabase.CopyAsset(source, dest))
                throw new IOException("준비된 리깅 로프 Prefab 복사에 실패했습니다.");
            if (game.ropePrefabs == null || game.ropePrefabs.Length == 0)
                game.ropePrefabs = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(dest) };
            if (!game.barkSource) game.barkSource = root.GetComponent<AudioSource>();
            if (!game.barkSource) game.barkSource = Undo.AddComponent<AudioSource>(root);
            game.barkSource.playOnAwake = false; game.barkSource.spatialBlend = 0; game.barkSource.volume = .45f;
            EnsureBark(game);
            EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene);
            // All original objects remain. This adds one scene root and leaves BallGame settings intact.
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Room3_Toy 저장 실패");
            File.WriteAllText(Folder + "/Editor/SetupReport.txt", Report(game), new UTF8Encoding(false));
            Selection.activeGameObject = root;
            Debug.Log("[TugGame] Room3_Toy 연결 완료. 기존 시스템과 Rig 유지.", game);
        }

        static void EnsureBark(TugGame game)
        {
            if (game.barkClip) return;
            game.barkClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Dog Bark_01.mp3");
            if (game.barkClip) return;
            // Local stand-in until a recorded winter bark is assigned in the Inspector.
            if (!AssetDatabase.IsValidFolder(Folder + "/Audio")) AssetDatabase.CreateFolder(Folder, "Audio");
            string path = Folder + "/Audio/Bark_Placeholder.wav";
            if (!File.Exists(path))
            {
                int rate = 22050, samples = rate * 7 / 10;
                using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                    writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                    var random = new System.Random(21); double phase = 0;
                    for (int i = 0; i < samples; i++)
                    {
                        double t = i / (double)rate, local = t < .32 ? t : t - .37;
                        double env = local >= 0 && local < .22 ? Math.Sin(Math.PI * local / .22) * Math.Exp(-local * 10) : 0;
                        phase += 2 * Math.PI * (260 - 150 * Math.Min(1, Math.Max(0, local) / .22)) / rate;
                        double value = (Math.Sin(phase) + .4 * Math.Sin(phase * 2.7) + .5 * (random.NextDouble() * 2 - 1)) * env * .38;
                        writer.Write((short)(Math.Max(-1, Math.Min(1, value)) * 32767));
                    }
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            game.barkClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        public static string Report(TugGame game)
        {
            var text = new StringBuilder();
            text.AppendLine("Room3_Toy 로프 터그놀이");
            text.AppendLine("Player: " + game.player.name + " / Camera: " + game.playerCamera.name);
            text.AppendLine("Input: " + game.inputActions.name + " (공유 에셋 유지, WASD 전용 액션)");
            text.AppendLine("Winter: " + game.winter.name + " / Controller: " + game.existingController.name);
            text.AppendLine("Mouth: " + game.mouth.name + " / Head: " + game.head.name);
            text.AppendLine("Mouth world: " + game.mouth.position + " / Winter scale: " + game.winter.lossyScale);
            text.AppendLine("Rope: " + AssetDatabase.GetAssetPath(game.ropePrefabs[0]));
            text.AppendLine("Manager: 기존 GameManager.Flow.CompleteVisit(로프)");
            text.AppendLine("Rounds: " + game.roundCount + " x " + game.successesPerRound);
            text.AppendLine("Audio: " + AssetDatabase.GetAssetPath(game.barkClip));
            var controller = game.existingController as UnityEditor.Animations.AnimatorController;
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            foreach (var state in new[] { game.idleState, game.biteState, game.braceState, game.leftState, game.rightState, game.forwardState, game.backState, game.walkState, game.barkState })
                text.AppendLine(state + ": " + states.Contains(state));
            return text.ToString();
        }
    }
}
#endif
