# WashGame

파일: Assets/LivingRoom/WashGame. 씬 오브젝트: WashGame 하위.

- Room3_LivingRoom 시작 위치: (33, 0.5, 4.5).
- 세면대 클릭: 무음 물줄기, OptionState.Blur 체크.
- 물 클릭: 첫 물소리 및 얼굴 적시기. 비누 선택 후 화면 90% 칠하기.
- 비누는 흰색 50% 반투명. 완료 시 띵 소리와 함께 화면 전체를 같은 색으로 덮습니다.
- 물 클릭: 두 번째 물소리 및 헹구기. 수건 선택 후 화면 90% 닦기.
- 닦은 영역의 물방울과 블러를 함께 지웁니다. 완료 시 띵 소리, 남은 효과 제거, Blur 체크 해제.
- 진행도는 왼쪽 끝에서 채워지는 게이지만 표시합니다. 라벨과 퍼센트 숫자는 없습니다.
- 비누칠과 수건 닦기 외 단계에서는 WASD 이동과 카메라 회전이 가능합니다. 칠하는 단계에서는 화면을 고정하고 마우스로 칠하기만 합니다. 별도 카메라 위치로 순간 이동하지 않습니다.
- 물·비누·수건 선택은 중앙 조준점으로 합니다. 비누칠·수건 닦기 중에는 화면을 고정하고 도구 커서를 게임 창 안에 제한합니다. 우클릭으로 중단하지 않으며 Esc로 설정을 엽니다.

## OptionState
WashGame에서 Blur는 체크됨 = 효과 활성, 체크 해제 = 효과 꺼짐입니다. Inspector 토글도 세안 중 화면에 반영됩니다. 완료 여부는 별도 Complete 단계로 관리합니다.

## 조절
Soap Required Coverage: 비누 0.9. Required Coverage: 수건 0.9.
Brush Radius, Blur Radius, Rinse Seconds 조절 가능.
WaterParticles와 WaterClickArea는 독립적이며 효과를 교체할 수 있습니다.
Water Sound / Ding Sound는 자체 합성 임시 효과음입니다. 지속 물소리는 재생하지 않습니다.

## 검증
Tools > WashGame > 2 - Verify full sequence
Tools > WashGame > 3 - Play from sink
Reports/Verification.txt와 단계별 PNG에 결과를 저장합니다.

비누칠(Soaping)과 수건 닦기(Wiping) 단계에서는 화면을 고정합니다. 이동, 회전, 걷기 흔들림을 잠시 중단하며 해당 단계 완료/중단 시 원래 상태로 복구합니다. OptionState.Move/RotateCamera 체크는 유지됩니다.
