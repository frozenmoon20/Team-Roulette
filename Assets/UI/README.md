공용 UI 사용 방법

씬에 배치
GameUI.prefab을 씬의 Hierarchy에 한 개 배치합니다.
목표와 독백은 씬에 배치한 GameUI의 Scene UI Content에서 설정합니다.
씬별 내용은 공용 프리팹에 Apply하지 않습니다.

조작
ESC: 설정 열기와 닫기, 게임 일시정지와 재개
Tab: 목표 목록 펼치기와 접기
Space: 다음 독백으로 진행
마우스 휠: 목표 목록 스크롤
독백 중에는 이동, 시점 회전, 상호작용이 차단됩니다.

목표
Scene UI Content의 Objectives에 항목을 추가합니다.
Id: 중복되지 않는 목표 이름
Text: 표시할 문구
Visible: 처음부터 표시할지 여부
Completed: 완료 여부. 처음에는 체크를 해제합니다.

완료 이벤트에 씬의 GameUI를 연결하고 SceneUIContent.CompleteObjective(string)을 선택한 뒤 Id를 입력합니다.
숨겨진 목표를 표시할 때는 SceneUIContent.RevealObjective(string)을 사용합니다.
완료된 목표는 체크, 회색 글씨, 취소선으로 표시되고 맨 아래로 이동합니다.
진행 수는 완료한 목표 / 표시된 전체 목표입니다.

독백
Project 창의 Create > Team Roulette > UI > 독백으로 대사 파일을 만듭니다.
Lines에 문장을 입력하고 Scene UI Content의 Dialogue에 연결합니다.
재생할 이벤트에 SceneUIContent.PlayDialogue()를 연결합니다.
씬 시작 시 재생하려면 Play Dialogue On Start를 체크합니다.
독백 종료 이벤트는 On Dialogue Finished에 연결합니다.

설정 연결
배경음악 AudioSource는 Game UI의 Music Sources에 연결합니다.
효과음은 기존 AudioManager를 사용하며, 추가 AudioSource는 Effect Sources에 연결합니다.
마우스 감도와 카메라 흔들림은 기존 FirstPersonPlayer와 PlayerMotionFeel을 자동으로 찾습니다.
Key Guides에서 조작 안내와 해금 조건을 설정합니다. 해금 전에는 ?로 표시됩니다.
추가 입력 스크립트는 Extra Input To Block에 연결하면 설정과 독백 중 비활성화됩니다.
설정값은 PC에 저장됩니다. 목표 진행의 저장과 불러오기는 별도 구현이 필요합니다.

예시와 공유
Examples/UI_Workbench에서 재생해 확인합니다.
F1: 독백, F2: 목표 완료, F3: 조작 안내 해금
GitHub에 Assets/UI 폴더 전체와 Assets/UI.meta를 함께 올립니다. 하위 .meta 파일도 포함합니다.
