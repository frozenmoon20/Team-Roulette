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















