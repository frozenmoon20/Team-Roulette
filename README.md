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

