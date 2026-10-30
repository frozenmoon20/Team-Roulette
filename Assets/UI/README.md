공용 UI 사용 방법

씬에 배치
Assets/UI/GameUI.prefab을 씬의 Hierarchy에 한 개 배치합니다.
목표와 대사는 씬에 배치한 GameUI의 Scene UI Content에서 설정합니다.
씬별 내용은 공용 프리팹에 Apply하지 않습니다.
Team-Roulette 프로젝트의 플레이어와 오디오 시스템을 사용하는 프리팹입니다.

목표
Objectives에 Id와 Text를 입력합니다. Id는 중복되지 않게 지정합니다.
Visible은 처음부터 표시할지 여부이며, Completed는 처음에는 체크를 해제합니다.
표시 가능한 미완료 목표를 등록 순서대로 하나씩 보여줍니다.
행동 완료 이벤트에 씬의 GameUI를 연결하고 SceneUIContent.CompleteObjective(string)을 선택한 뒤 Id를 입력합니다.
숨긴 목표를 표시하려면 SceneUIContent.RevealObjective(string)을 사용합니다.
모두 완료하면 Completed Message의 문구를 표시합니다. 기본값은 목표 완료입니다.
새 씬에서는 해당 씬의 목표로 다시 시작합니다. Tab으로 숨기는 기능은 없습니다.

대사
Project 창에서 Create > Team Roulette > UI > 독백을 선택해 대사 파일을 만듭니다.
문장 수를 정하고 각 문장과 색상 코드를 입력합니다. 예: #FFFFFF, #124563
색상 코드를 비우거나 잘못 입력하면 흰색으로 표시합니다.
Scene UI Content의 Dialogue에 파일을 연결합니다.
재생할 이벤트에 SceneUIContent.PlayDialogue()를 연결합니다.
여러 대사 파일을 쓰려면 GameUI.ShowDialogue(DialogueSequence)에 원하는 파일을 연결합니다.
씬 시작 시 재생하려면 Play Dialogue On Start를 체크합니다.
On Dialogue Finished에는 마지막 대사가 끝난 뒤 실행할 이벤트를 연결합니다.
대사는 한 문장씩 2초간 표시되며 마지막 문장도 2초 뒤 사라집니다.
Space 입력은 필요 없으며, 대사 중 이동과 시점 회전은 유지됩니다.
대사 재생 중 새 재생 요청은 무시됩니다.

환경설정
ESC로 설정을 열고 닫습니다. 설정 중에는 게임과 대사 타이머가 멈춥니다.
배경음악 AudioSource는 Music Sources, 추가 효과음은 Effect Sources에 연결합니다.
기존 AudioManager, FirstPersonPlayer, PlayerMotionFeel은 자동으로 찾습니다.
추가로 멈출 입력 스크립트가 있다면 Extra Input To Block에 연결합니다.
Key Guides에서 조작 안내와 Unlock 조건을 지정합니다.
플레이 중 _Systems의 OptionState에서 해당 항목을 체크하면 ?가 지정된 키 안내로 바뀝니다.

발바닥 진행도
각 방의 행동 완료 이벤트에 씬의 GameUI를 연결합니다.
PawProgress.CompleteStage(int)를 선택하고 행동별로 서로 다른 번호 1~5를 입력합니다.
첫 완료는 큰 패드, 이후 완료는 발가락을 하나씩 추가합니다.
같은 번호를 여러 번 호출해도 한 번만 반영합니다.
씬이 바뀌어도 유지되며 새 플레이를 시작하면 초기화됩니다.
새 게임을 시작할 때는 PawProgress.ResetProgress()를 호출할 수 있습니다.
게임을 종료한 뒤의 진행도 저장은 포함하지 않습니다.

테스트 씬
Examples/UI_Workbench를 열고 Play를 누릅니다.
F1: 흰색과 하늘색 대사 예시
F2: 현재 목표 완료
F3: 조작키 안내 해금
F4: 발바닥 한 단계 추가
F5: 발바닥 초기화
ESC: 설정 열기와 닫기

공유
UI 폴더와 Assets/UI.meta, 각 파일의 .meta를 함께 올립니다.
Examples는 테스트용이며 실제 씬에는 GameUI 프리팹을 배치합니다.
