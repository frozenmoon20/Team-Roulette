**2026/10/04** 



**<깃허브 에셋 저장소 (Team-Roulette-Assets)>**



**이전에 말씀드린 라이선스 문제가 있기 때문에 에셋스토어 에셋이나 라이선스가 애매한 에셋은** 

**공개 저장소에 올릴 수 없어서, 비공개 저장소 Team-Roulette-Assets를 따로 만들었습니다.**

**프로젝트의 `Assets/StoreAssets` 폴더가 바로 이 비공개 저장소입니다. 공개 저장소(Team-Roulette)는 `.gitignore`로 이 폴더를 통째로 제외했습니다.**



**- 들어 있는 것: VR Hands(손), Computer, Desk Table, Starter Assets, UI 아이콘(손 아이콘, 마우스 아이콘)**



**- 초대 받으시면 이 저장소에서 꼭 파일 받으셔야합니다!** 



**- 받는 방법 (처음 한 번만)**



**1. 메일로 온 Team-Roulette-Assets 초대를 수락합니다.**

**2. Unity를 완전히 종료합니다.**

**3. GitHub Desktop에서 File → Clone repository → URL 탭을 엽니다.**

&#x20;

&#x20;  **- Repository URL: frozenmoon20/Team-Roulette-Assets**

&#x20;  **- Local path: 내 프로젝트 폴더 안의 `Assets\\StoreAssets`가 되도록 칸에 직접 입력합니다.**

&#x20;    **예: `C:\\...\\Team-Roulette\\Assets\\StoreAssets`**

&#x20;    **(Choose 버튼으로 고르면 끝에 저장소 이름이 붙으니, 꼭 직접 고쳐 주세요.)**

&#x20;  **- Clone을 누릅니다.**

&#x20;  **- "폴더가 이미 있다"며 실패하면, 프로젝트의 `Assets\\StoreAssets` 폴더를 지우고** 

**다시 Clone 해 주세요.**





**4. Clone이 끝나면 GitHub Desktop이 StoreAssets 화면으로 바뀝니다. 왼쪽 위 Current repository를 Team-Roulette로 다시 바꾸고 Fetch → Pull을 합니다.** 

**(손이 든 Player 프리팹, 새 스크립트를 받게 됩니다.)**



**5. Unity를 열고 다음을 확인합니다.**

&#x20;  **- Project 창에 `Assets/StoreAssets/Hands`가 있는지**

&#x20;  **- Console에 빨간 에러가 없는지**



**\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_**





**<플레이어 움직임 패치>**



**걷기·달리기에 가속과 감속이 생겨, 속도가 서서히 붙고 서서히 줄어듭니다.** 

**그리고 걸음에 맞춰 카메라가 살짝 흔들립니다. (PlayerMotionFeel)**

**대화나 미니게임을 시작할 때 StopMovement()를 부르면 미끄러지지 않고 바로 멈춥니다.**

**(예: FindFirstObjectByType<FirstPersonPlayer>().StopMovement();)**

**<1인칭 손 패치>**



**일단은 무료 애셋 VR Hands를 Camera -> HandRoot 아래에 배치했고 오른손만 보이게** 

**왼손은 임시 조치로 scale을 0으로 뒀습니다.**

**손 모델링은 OptionState에서 Hand가 해금되는 순간 나타납니다. 해금 안해두면 활성화 안됩니다.**



**애셋의 애니메이션 쓰기보단 좀 더 간단하게 표현하는게 좋을거 같아서 HandPose.cs를 통해**

**손가락 움직임을 표현해뒀습니다.**

**손 모델도 마찬가지로 에셋스토어 에셋이라 공개 저장소엔 올리지 않겠습니다.** 



**\* 손 적용 방법**

**1. 위의 "깃허브 에셋 저장소 → 받는 방법"을 먼저 해 주세요.**



**2. 내 씬의 플레이어는 `Assets/Player/Player` 프리팹을 놓아 주세요.**

&#x20;  **- 손, 손 조명, 조준점, 이름 표시, 해금 알림(PlayerHUD)이 모두 이 프리팹 안에 들어 있어서, 따로 연결할 것이 없습니다.** 



**혹시 아직도 Starter Assets의 PlayerCapsule쓰시면 지우시고 Player 프리팹 사용해주세요**



**3. 내 씬에 원래 있던 Canvas에 `FocusPoint`, `UnlockNotice`가 있다면 지워 주세요.**

**Player 안에 이미 있어서 두 개씩 보이게 됩니다.**



**4. 씬에 `Assets/Prefabs/\_Systems` 프리팹이 있어야 합니다. (OptionState가 들어 있습니다)**



**5. 확인: Play → Hierarchy에서 `\_Systems/OptionState` 선택 → Inspector에서 Hand 체크 → 화면 아래에 손이 나오면 성공입니다.**



**!!주의**

**- Player → PlayerCameraRoot → Camera → HandRoot 아래는 건드리지 말아 주세요. 손 위치와 받쳐 드는 자세가 맞춰져 있습니다.**

**- Player 프리팹을 수정하거나 Apply하지 말아 주세요. 바꿔야 할 것이 있으면** 

**나중에 회의할 때 말씀해주세요**





**\* 잡기 기능 사용 방법**



**\[동작 방식]**

**- 화면 가운데로 2m 안의 물건을 조준하면 화면 아래에 이름이 뜹니다.**

**- 잡을 수 있는 물건이면 조준점이 손 아이콘으로 바뀝니다.**

**- 좌클릭: 상호작용, 물건 들기, 놓기 / 우클릭: 들고 있는 물건을 원래 자리로 되돌리기**

**- 물건을 들고 있는 동안에는 놓을 자리 말고는 클릭이 막히고 다른 오브젝트와 상호작용 시**

**"먼저 내려놓자."가 뜹니다.**



**- 들 수 있는 물건을 클릭했을 때**

**(이 부분은 개별적으로 대화 스크립트가 나오게 변경할 예정 일단 지금은 임시로 넣어둔 기능입니다.)**



&#x20; **Hand 해금 전: "컵이다."처럼 이름만 나옵니다.** 

&#x20; **Hand 해금, Grab 해금 전: "손에 힘이 들어가지 않는다."**

&#x20; **Hand, Grab 둘 다 해금: 손바닥 위에 얹어서 듭니다.**





**\[공통]**

**- 모든 상호작용 물건에는 Collider가 있어야 합니다. 없으면 조준도 클릭도 안 됩니다.**

**- 컴포넌트 체크를 끄면 그 물건은 상호작용 대상에서 빠집니다.**





**만들고 싶은 물건에 따라 붙이는 컴포넌트가 다릅니다.(일단 대화창 ui가 따로 없어서 임시로 만든 기능입니다. 나중에 화면 아래 문구가 나오는 것은 삭제 할 예정이긴하나 일단 다른 분들도 임시로 사용하시면 좋을 거 같습니다)**



**- 클릭하면 대사나 동작이 나오는 물건 (창문, 컴퓨터 등) → 예시 1**

**- 손으로 집어 드는 물건 (컵, 사진, 쓰레기 등) → 예시 2**

**- 들고 있는 물건을 받는 자리 (쓰레기통, 선반 등) → 예시 3**







**\[예시 1. 창문 - 클릭하면 대사가 나오는 물건]**

**1. Project 창에서 우클릭 → Create → MonoBehaviour Script → 이름을 WindowClick으로 정합니다.**

&#x20;  **(파일 이름과 코드 안의 class 이름이 같아야 합니다)**

**2. 파일을 열어 내용을 아래로 바꿉니다.**





**using UnityEngine;**



**public class WindowClick : MonoBehaviour, IInteractable**

**{**

&#x20;   **public void Interact()**

&#x20;   **{**

&#x20;       **PlayerInteractor.ShowHint("창문이 열리지 않는다.");**

&#x20;   **}**

**}**





**3. 창문에 WindowClick을 붙입니다. 이제 창문을 클릭하면 화면 아래에 "창문이 열리지 않는다."가 뜹니다.**

**4. 조준했을 때 "창문"이라는 이름도 띄우려면 Add Component → InteractName을 붙이고 Display Name에 창문이라고 씁니다.**

**- ShowHint 줄 대신 해금, 문 열기, 미니게임 시작처럼 원하는 코드를 넣으면 됩니다.**

**5. 아마  상호작용 오브젝트라 ConditionalInteractable.cs를 상속받으셨겠지만 만일 그렇지** 

**않으셨다면 안뜰겁니다.**



**\[예시 2. 컵 - 손으로 집어 드는 물건]**

**1. 컵에 Add Component → GrabbableItem을 붙입니다.**

**2. Display Name에 컵이라고 씁니다.**

**- 클릭하면 오른손이 뒤집히고 컵이 손바닥 위에 올라갑니다. 기본으로 컵 바닥의 가운데가 손바닥 중앙에 닿게 자동으로 놓입니다.**

**- 우클릭하면 컵이 원래 자리로 돌아갑니다.**

**- 컵을 들고 있는 동안에는 놓을 자리(예시 3) 말고는 클릭이 막히고 "먼저 내려놓자."가 뜹니다.**

**- 컵 아래에 있는 자식 오브젝트는 전부 같이 들립니다.**

**- 이름 칸이 이미 있으니 InteractName은 붙이지 않습니다.**



**\* 손 위에서 어색할 때  Position Offset, Rotation Offset**



**물건이 손바닥에 파묻히거나, 옆으로 눕거나, 한쪽으로 치우쳐 보이면** 

**GrabbableItem 스크립트를 붙인 오브젝트를 눌러 inspector 창에 GrabbableItem 항목을 보면**

**Position Offset, Rotation Offset가 보일 겁니다. 이를 조정하면 됩니다.**



**<조정하는 순서>**

**1. Play → Hand, Grab 해금 → 물건을 들어서 어떻게 보이는지 확인합니다.**

**2. Inspector에서 값을 조금씩 바꿉니다. (위치는 0.01 단위, 각도는 15\~90 단위로)**

**3. 우클릭으로 되돌렸다가 다시 클릭해서 듭니다. 들고 있는 동안 바꾼 값은 다시 들어야 반영됩니다.**

**4. 마음에 들면 GrabbableItem 컴포넌트 이름을 우클릭 → Copy Component 를 누릅니다.**

**5. Play를 멈추고, 같은 컴포넌트를 우클릭 → Paste Component Values 를 누릅니다.**

&#x20;  **(Play 중에 바꾼 값은 멈추면 사라지기 때문에 이 과정이 필요합니다)**



**\[예시 3. 쓰레기통 - 들고 있는 물건을 받는 자리]**

**1. 쓰레기통에 Add Component → PlaceSpot을 붙입니다.**

**2. Display Name에 쓰레기통이라고 씁니다.**

**3. Accept Items 옆의 + 를 눌러 칸을 만들고, 넣을 물건(예시 2처럼 GrabbableItem을 붙인 물건)을 드래그합니다. 여러 개 넣을 수 있습니다.**

**4. Hide On Place를 체크합니다. 넣는 순간 물건이 사라집니다.**

**5. (선택) 다 넣었을 때 무언가 일어나게 하려면 On All Placed의 + 를 누르고, 대상 오브젝트를 드래그한 뒤 실행할 함수를 고릅니다.**

**- 선반처럼 물건을 세워 두고 싶으면: Hide On Place를 해제하고, 놓일 자리에 빈 오브젝트를 만들어 초록 화살표(Y축)가 위를 향하게 둔 뒤 Place Point에 드래그합니다. 비워 두면 이 오브젝트 위치에 놓입니다.**

**- Place Point는 Scale이 (1, 1, 1)인 오브젝트 아래에 두세요. 아니면 놓인 물건이 찌그러질 수 있습니다.**

**- Wrong Item Message: 목록에 없는 물건을 들고 클릭했을 때 나오는 문구입니다.**

**- 목록의 물건을 다 받은 자리는 그 뒤로 조준되지 않습니다.(이는 나중에 수정할 예정입니다.)**





**\[참고 - 미리 만들어 둔 예시 프리팹]**

**Assets/Prefabs/Obj\_Prefabs에 세팅이 끝난 예시가 있습니다.** 

**씬에 끌어다 놓고 Inspector를 열어 보시면 칸을 어떻게 채웠는지 바로 보입니다.**



**- Test\_Obj\_Move : 예시1**

**- TestCube: 예시 2**

**- TreshBin, MoodLamp : 예시 3 (프리팹에서는 Accept Items가 비어 있으니, 씬에 놓은 TestCube를 Hierarchy 창에서 끌어다 넣어 주세요. Project 창의 프리팹을 넣으면 동작하지 않습니다)**











**\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_**





**2026/09/24**



**<하프톤 셰이더 및 공용 시스템 정리>**



**(개발 리소스들이 더 많아지기 전에 간단하게나마 OptionState부분 수정 및 하프톤 머티리얼을 pc 렌더링 전체에 적용했습니다.**

**번거로우시겠지만 크게 개발이 진행되지 않으셨다면 이 버전을 다운받아주세요!)**



* **새로운 씬을 만들고 개발 하실 때**



**\*Assets/Prefabs/\_Systems 프리팹을 씬에 하나 끌어다 놓으면 됩니다.**

**\*OptionState, AudioManager, SceneTransition, HalftoneController가 들어있고**

**\*씬 전환 시에도 유지됩니다.**

**\*이걸 안 넣으면 OptionState.Instance가 null이라 상호작용이 전혀 동작하지 않습니다.**

**\*(플레이어, 조명, Canvas 등은 씬마다 따로 배치하시면 됩니다)**





**옵션 변경 이벤트 추가**



**\*OptionState에 옵션이 해금/잠길 때 알려주는 이벤트를 추가했습니다.**

**\*옵션 상태에 반응해야 하는 기능(사운드, UI, 연출 등)은 Update에서 매번**

**\*확인하지 말고 이 이벤트를 구독하면 됩니다.**



**<방법>**

**\*void OnEnable()  { OptionState.OnOptionChanged += 내함수; }**

**\*void OnDisable() { OptionState.OnOptionChanged -= 내함수; }**

**\*void 내함수(OptionType type, bool isUnlocked) { ... }**



**- 전달값: (어떤 옵션인지, 해금됐으면 true**

**- OnDisable에서 -= 를 빼먹으면 씬 전환 후 에러가 나니 꼭 짝으로 써주세요.**

**- 기존 Unlock(), Lock(), IsUnlocked()는 그대로라 기존 코드는 영향 없습니다.**



**<OptionType enum 정리>**



**항목을 한 줄에 하나씩 적는 형태로 바꿨습니다.**

**여러 명이 동시에 항목을 추가해도 Git 충돌이 잘 안 나게 하기 위함입니다.**

**현재 조작/그래픽 항목만 확정되어 있고 사운드 쪽은 비어 있습니다.**





**<하프톤 연출>**



**Assets/Shader/SG\_Halftone (셰이더), M\_Halftone (머티리얼)**

**Assets/Scripts/Core/HalftoneController.cs (제어)**



**게임 시작 시 화면 전체가 흑백 하프톤이고, 옵션이 해금될수록 옅어져서\***

**전부 해금되면 원래 화면이 됩니다.**



**- OptionType에 항목을 추가하면 자동 반영되니 따로 손댈 것 없습니다.**

**- 도트 크기나 대비는 M\_Halftone의 값으로 조절합니다.(추후 하프톤 연출이 조금 이상한 것 같으면 말씀해주세요)**

**- 전환 속도는 \_Systems 안의 HalftoneController에서 Fade Speed로 조절합니다.**

**- PC\_Renderer에 Full Screen Pass Renderer Feature로 등록되어 있습니다. 작업하다 이 체크를 껐으면 커밋 전에 꼭 되돌려주세요.**









**\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_**



**2026/09/21**



**<공동 시스템 가이드>**



**일반적으론 대부분 스크립트에 주석을 달아뒀기 때문에 전체 코드 복사해서**

**AI를 활용한다면 보다 쉽게 구조가 이해갈듯합니다. 애초에 공용적으로**

**활용 될 시스템들이라 쉬운 방식으로 설계했습니다.**



**그래도 혹시 몰라서 일단 필수적으로 알아둬야할 사항들에 대해선**

**아래와 같이 정리했습니다.**



* **폴더 구조**

**- Assets/Scripts/Core — 모든 방이 공통으로 쓰는 기반 시스템**

**- Assets/Scripts/Room1 — 방1 전용 오브젝트 스크립트**

**- Assets/Scripts/Test — 테스트용 폴더**

**- Assets/Scripts/Editor — 에디터 전용 도구 (인스펙터 커스터마이징 등)**



* **상호작용 오브젝트 제작 시**



**모든 상호작용 오브젝트는 ConditionalInteractable.cs를 상속받아 만들면 됩니다.**



**한번 상호작용 하고 끝나는 오브젝트**

**-> OnUnlocked()만 override하면 됩니다.**



**반복 조작이 되는 오브젝트( ex. 문을 열고 닫기 or 스위치 끄고켜기)**

**-> OnUnlocked()와 OnRepeatInteract() 둘 다 override (Door.cs 참고)**



**오브젝트 제작에 간단한 예시들은 Door.cs, ComputerDesk.cs, MoveTrigger.cs를**

**참조하시면 됩니다.**



* **해금 능력 관련**



**오브젝트와 상호작용 이후 해금되는 능력들의 종류를 추가하려면**

**OptionState.cs의 Enum 타입의 OptionType에 값을 추가하면 됩니다.**



**-능력을 해금시킬 땐**

**OptionState.Instance.Unlock(OptionType.//해금시킬 값);**

**-능력이 해금됐는지 확인 할 때**

**OptionState.Instance.IsUnlocked(OptionType.//해금시킨 값);**

**-오브젝트가 특정 능력을 요구하게 하려면, 에디터에서 그 오브젝트의**

**inspector에서 Required Options 리스트에 추가하면 됩니다.**

**만일 없다면 그 오브젝트의 스크립트가 ConditionalInteractable.cs를 상속받았는지 확인해주세요**



* **테스트 시(커스텀 에디터)**



**플레이 모드에서 움직이지도 화면 전환이 되지도 않을텐데 이는 Hierarchy의 OptionState 오브젝트를 클릭하면, 인스펙터에 각 옵션의 체크박스가 떠서 직접
켜고 끄며 테스트할 수 있습니다 (커스텀 에디터로 구현)**



* **그 외**



**ConditionalInteractable은 조건 충족 시 즉시 hasUnlocked = true로 바뀝니다.**

**실패 가능한 미니게임을 붙일 경우, 실패 시 재시도하게 하려면**

**OnRepeatInteract()에 재시도 로직을 직접 구현해야 합니다.**

&#x20;

**Core/AudioManager.cs는 소리 재생 통로만 있고
실제 사운드 파일은 아직 없습니다.**

**AudioManager.Instance.PlaySFX(clip) 형태로 어디서든 호출 가능합니다.**



**Core/SceneTransition.cs도 뼈대만 있고,
실제 씬 전환 테스트는 다른 씬이 개발되고 이에 관련한 작업 시 진행 예정입니다.**

