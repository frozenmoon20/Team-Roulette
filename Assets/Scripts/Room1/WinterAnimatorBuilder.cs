using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// (에디터 전용 도구) 겨울이용 애니메이터 컨트롤러를 자동으로 만든다.
// 원리: 강아지 에셋의 애니메이션 157개 중 겨울이가 쓰는 것만 골라, 상태(State) 이름을 붙여 컨트롤러에 넣는다.
//       상태 사이 화살표(Transition)는 만들지 않는다. 어떤 상태로 바꿀지는 DogAnimator 스크립트가 이름으로 직접 정한다.
//       → 컨트롤러를 손으로 하나하나 만들 필요가 없고, 애니메이션을 바꾸고 싶으면 아래 표만 고치고 다시 실행하면 된다.
// 사용: 위 메뉴 Tools > 겨울이 애니메이터 만들기 → Assets/Animations/Winter_Animator.controller 가 생김 (이미 있으면 내용만 새로 채움)
// 이 파일은 반드시 이름이 "Editor"인 폴더 안에 둬야 한다.
public static class WinterAnimatorBuilder
{
    const string SourceFolder = "Assets/StoreAssets/Dog - Pomeranian Spitz";   // 강아지 에셋 폴더
    const string OutFolder = "Assets/Animations";
    const string OutPath = "Assets/Animations/Winter_Animator.controller";

    // 상태 이름 (DogAnimator가 부르는 이름)  →  쓸 애니메이션 이름 (Arm_Spitz| 뒤 부분)
    static readonly string[,] Map =
    {
        // 기본 움직임
        { "Idle",      "Idle_1" },            // 서서 대기
        { "Walk",      "Walk_F_IP" },         // 걷기
        { "Trot",      "Trot_F_IP" },         // 종종걸음
        { "Run",       "Run_F_IP" },          // 달리기
        { "TurnL",     "Turn_L_IP" },         // 제자리에서 왼쪽으로 돌기
        { "TurnR",     "Turn_R_IP" },         // 제자리에서 오른쪽으로 돌기
        // 점프
        { "RunJump",   "Jump_Run_IP" },       // 달리며 점프 한 덩어리 (허들 점프에 사용)
        { "JumpStart", "JumpStart_F_IP" },    // (예비) 달리다 앞으로 도약
        { "Air",       "JumpAir_low_F" },     // 공중 (앞으로 기울어진 낮은 점프)
        { "Land",      "JumpLand_F_IP" },     // 앞으로 착지
        // 앉기
        { "SitStart",  "Sitting_start" },     // 앉기 시작
        { "Sit",       "Sitting_loop_1" },    // 앉아 있기
        { "SitEnd",    "Sitting_end" },       // 일어나기
        // 짖기·먹기
        { "Bark",      "Bark" },              // 짖기
        { "EatStart",  "EatDrink_start" },    // 먹으려고 고개 숙이기
        { "Eat",       "Eat_loop" },          // 먹기
        { "EatEnd",    "EatDrink_end" },      // 먹고 고개 들기
        // 다 먹고 쓰다듬받을 때 자세 후보 (상태 이름 = 애니메이션 이름. DogAnimator의 After Eat Start / Loop에서 골라 씀)
        { "Lie_belly_start",    "Lie_belly_start" },
        { "Lie_belly_loop_1",   "Lie_belly_loop_1" },
        { "Lie_belly_loop_2",   "Lie_belly_loop_2" },
        { "Crouch_Idle_start",  "Crouch_Idle_start" },
        { "Crouch_Idle_loop_1", "Crouch_Idle_loop_1" },
        { "Crouch_Idle_loop_2", "Crouch_Idle_loop_2" },
        { "Lie_start",          "Lie_start" },
        { "Lie_loop_1",         "Lie_loop_1" },
        { "Lie_loop_2",         "Lie_loop_2" },
        // 어슬렁 중 멈췄을 때 행동 후보 (상태 이름 = 애니메이션 이름. DogAnimator의 Pause Actions에서 골라 씀)
        { "Idle_2",         "Idle_2" },
        { "Idle_3",         "Idle_3" },
        { "Idle_4",         "Idle_4" },
        { "Idle_6",         "Idle_6" },
        { "Idle_7",         "Idle_7" },
        { "Scratching",     "Scratching" },
        { "Sitting_loop_2", "Sitting_loop_2" },
        { "Digging_loop",   "Digging_loop" },
    };

    [MenuItem("Tools/겨울이 애니메이터 만들기")]
    static void Build()
    {
        // 1) 에셋 폴더 안의 애니메이션을 이름으로 모아 둠 (FBX 안에 들어 있는 것까지)
        if (!AssetDatabase.IsValidFolder(SourceFolder))
        {
            Debug.LogError("겨울이 애니메이터: 강아지 에셋 폴더를 못 찾았어요 → " + SourceFolder);
            return;
        }
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { SourceFolder }))
        {
            foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                AnimationClip clip = a as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                string key = clip.name.Substring(clip.name.LastIndexOf('|') + 1);   // "Arm_Spitz|Walk_F_IP" → "Walk_F_IP"
                if (!clips.ContainsKey(key)) clips[key] = clip;
            }
        }

        // 2) 컨트롤러 준비 (이미 있으면 그대로 쓰고 상태만 비움 → 씬에 연결해 둔 것이 끊기지 않게)
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets", "Animations");
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(OutPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(OutPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

        // 3) 상태 추가
        AnimatorState idle = null;
        int missing = 0;
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            AnimatorState state = sm.AddState(Map[i, 0], new Vector3(300f + (i / 12) * 250f, 60f * (i % 12), 0f));
            if (clips.TryGetValue(Map[i, 1], out AnimationClip clip)) state.motion = clip;
            else { Debug.LogWarning("겨울이 애니메이터: '" + Map[i, 1] + "' 애니메이션을 못 찾았어요 (상태 " + Map[i, 0] + "는 비어 있음)"); missing++; }
            if (Map[i, 0] == "Idle") idle = state;
        }
        if (idle != null) sm.defaultState = idle;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Selection.activeObject = ctrl;
        Debug.Log("겨울이 애니메이터 만들기 완료: " + OutPath + (missing > 0 ? " (못 찾은 애니메이션 " + missing + "개, 위 경고 확인)" : ""));
    }
}