# 공 효과를 팀 GitHub에 처음 올리는 순서

이 문서는 현재 프로젝트의 공 효과 작업을 개인 브랜치에 올리고, 팀의 main에 합치는 절차다. 2026년 10월 7일 확인한 상태를 기준으로 작성했다. 아래 명령은 Windows PowerShell에서 한 줄씩 실행한다. 오류가 나오면 다음 줄을 계속 실행하지 않는다.

## 1 현재 확인한 상태

- 저장소: https://github.com/sharknogal/Exp-Minigame-2026-team02
- 현재 브랜치: `main`. 아직 이번 공 효과 작업은 커밋하지 않았다.
- `git fetch origin`으로 확인한 로컬 HEAD와 `origin/main`은 모두 `470081f`다. 이후 팀원이 push하면 바뀔 수 있다.
- Block.cs와 GridManager.cs는 Git 원본과 같다. 이전에 추가했던 GridManager 조회 함수 2개는 제거했다.
- 기존 Block 프리팹과 SampleScene에도 변경이 없다.
- 공 담당 폴더의 BallBoardLookup이 기존 공개 함수와 블록 부모를 통해 연결한다.
- Unity 6000.5.10f1 Play 모드에서 40개 사례 통과, 0개 실패. 이동과 물리 충돌은 범위 밖이다.
- 개인 도구 설정과 Unity 설정 파일도 작업 폴더에 보인다. 이번 커밋에는 넣지 않는다.

이번 작업에서는 브랜치 생성, staging, commit, push, PR 생성을 아직 실행하지 않았다. 아래 순서대로 직접 진행할 수 있다.

## 2 용어 6개

| 용어 | 여기서의 뜻 |
| --- | --- |
| main | 팀이 함께 사용하는 기준 브랜치 |
| 작업 브랜치 | 내가 만든 변경을 별도로 모으는 작업 줄기 |
| stage / git add | 다음 저장 기록에 넣을 파일만 선택하기 |
| commit | 선택한 파일의 상태를 내 컴퓨터의 Git 기록에 저장하기 |
| push | 내 commit을 GitHub의 지정한 브랜치에 전송하기 |
| Pull Request / PR | 내 브랜치의 변경을 main에 합쳐 달라고 제안하고 검토받는 화면 |

순서는 `작업 브랜치 → 파일 선택 → commit → push → PR → 검토 및 테스트 → merge`다. 작업 브랜치를 push하는 것만으로 main이 바뀌지는 않는다. GitHub도 이 브랜치와 PR 흐름을 안내한다. [GitHub flow](https://docs.github.com/en/get-started/using-github/github-flow)

## 3 PowerShell에서 프로젝트 열기

Unity의 Play를 정지한다. 아직 저장하지 않은 편집은 확인하고, 프로젝트 폴더에서 터미널을 연다. 경로 이동이 필요하면 다음 명령을 사용한다.

```powershell
Set-Location 'C:\Users\hijuh\Documents\Codex\Exp-Minigame-2026-team02'
git status --short
git branch --show-current
git remote -v
```

`main`과 위 팀 저장소 주소가 나오는지 확인한다. 이미 복제하고 연결한 프로젝트이므로 `git init`, 다시 clone, 새 저장소 생성은 필요하지 않다. status의 `??`는 Git 기록에 아직 추가하지 않은 파일, `M`은 변경으로 감지된 파일이다. M이 보인다고 모두 이번 작업 파일인 것은 아니다.

작성자 설정도 확인한다. GitHub 로그인과 commit 작성자 설정은 별개다.

```powershell
git config user.name
git config user.email
```

비어 있거나 본인 정보가 아니면 아래 두 값만 본인 것으로 바꿔 실행한다. `--local`은 이 프로젝트에만 적용한다. 이메일 공개를 원하지 않으면 GitHub Settings → Emails에 표시된 본인의 noreply 주소를 사용한다.

```powershell
git config --local user.name '본인의 이름'
git config --local user.email '본인의 GitHub 이메일'
```

## 4 작업 브랜치 만들기

```powershell
git switch -c codex/ball-effects
git branch --show-current
```

마지막 출력이 `codex/ball-effects`면 성공이다. 아직 커밋하지 않은 현재 파일들은 그대로 따라온다. 새 폴더가 생기거나 파일이 사라지는 것이 아니다. 이후 이번 공 효과 작업은 이 브랜치에서 이어 간다.

이미 존재하는 브랜치라는 오류가 나오면 같은 명령을 반복하지 말고 `git branch`로 확인한다. 그 브랜치가 본인의 이번 작업 브랜치일 때만 `git switch codex/ball-effects`로 이동한다. 이름이 같은 다른 사람의 브랜치를 사용하지 않는다.

## 5 내 파일만 선택하기

먼저 기존에 선택된 파일이 있는지 확인한다.

```powershell
git diff --cached --name-only
```

현재는 출력이 없는 상태다. 파일이 이미 나오면 그 목록을 먼저 확인한다. 원하지 않는 파일 하나를 선택에서 뺄 때는 `git restore --staged -- '파일경로'`를 쓴다. 이것은 파일 내용을 지우지 않고 선택만 해제한다. `--staged`를 빼면 의미가 달라지므로 그대로 사용한다.

이번에 선택할 경로는 다음과 같다.

```powershell
git add -- Assets/_Project/Scripts/Balls.meta Assets/_Project/Scripts/Balls
git add -- Assets/_Project/Scenes/BallEffectsTest.unity Assets/_Project/Scenes/BallEffectsTest.unity.meta
git add -- Docs/BallEffectsImplementationPlan.md Docs/BallEffectsReadingGuide.md Docs/BallEffectsTestResults.json Docs/BallEffectsGitHubGuide.md
```

폴더를 지정한 첫 명령은 내부 스크립트와 모든 `.meta`도 함께 선택한다. Unity의 `.meta`는 참조를 유지하는 식별 정보를 담으므로 함께 커밋한다. `Scenes.meta`는 이미 팀 저장소에 있으므로 새로 추가할 필요가 없다.

이번에는 `git add .`, `git add -A`, `git commit -a`를 사용하지 않는다. 의도와 다른 파일까지 들어가는 일을 피하려고 정확한 경로를 지정한 것이다.

| 이번에 포함 | 이번에는 제외 |
| --- | --- |
| Scripts/Balls 폴더 전체와 Balls.meta | Block.cs, GridManager.cs, 기존 Block 프리팹 |
| BallEffectsTest 씬과 그 meta | 팀 공용 SampleScene |
| 이 안내를 포함한 공 관련 문서 4개 | ProjectSettings, Assets/Settings |
| 새 스크립트와 폴더의 meta | .agents, .codex, .slnx, Library, Temp, Logs |

ProjectSettings와 Packages는 Unity 프로젝트에서 원래 공유할 수 있는 중요한 파일이다. 이번 기능에 필요한 변경이 없어서 이번 커밋에서 제외하는 것이며, 앞으로 무조건 무시하라는 뜻은 아니다.

## 6 commit 전에 선택 내용 확인하기

```powershell
git diff --cached --name-status
git diff --cached --stat
git diff --cached -- Assets/_Project/Scripts/Block.cs Assets/_Project/Scripts/GridManager.cs
git diff --cached --check
```

이번 최초 커밋의 목록에는 새 파일을 뜻하는 `A`만 있어야 한다. 위에서 선택한 공 폴더, 전용 씬, 문서 외의 파일이 있으면 선택을 해제한다. 세 번째 명령은 아무 변경도 출력하지 않아야 한다. 마지막 명령은 공백 문제 등을 확인하며, 내용의 정상 동작까지 증명하는 테스트는 아니다.

필요하면 `git diff --cached`로 실제 코드를 확인한다. 긴 화면에서 빠져나오려면 `q`를 누른다. `git diff`는 선택하지 않은 변경, `git diff --cached`는 commit에 들어갈 변경을 보여 준다.

Unity에서 결과를 다시 보고 싶으면 전용 BallEffectsTest 씬을 열고 Play → Ball Effect Test Controls 선택 → **전체 자동 검증 실행**을 누른다. 완료 결과가 40 통과 / 0 실패인지 확인한 뒤 Play를 정지한다. 테스트는 해당 씬의 Play 모드 도구이며 NUnit Test Runner 검증은 아니다.

## 7 내 컴퓨터에 commit 만들기

```powershell
git commit -m "feat: add eight ball effects and block interaction tests"
git log -1 --oneline
git show --stat --oneline HEAD
git status --short
```

첫 명령이 성공해야 다음으로 진행한다. 본인이 추가한 commit 메시지가 나타나고, 선택한 파일만 기록되었는지 확인한다. 다른 설정 파일의 M이나 개인 도구 폴더의 ??가 남아 있어도 이번 commit에 들어간 것은 아니다. 파일을 없애려고 Discard, reset --hard, clean 등을 실행하지 않는다.

## 8 최신 팀 코드 상태 확인하고 push하기

```powershell
git fetch origin
git log --oneline HEAD..origin/main
git diff --name-status origin/main...HEAD
```

fetch는 GitHub의 최신 기록을 가져온다. 현재 작업 파일을 main 내용으로 덮어쓰지 않는다. 두 번째 명령에 출력이 없으면 팀 main의 commit이 현재 작업의 기반에 모두 포함된 상태다. 이 문서 작성 시점에는 팀 main에 새로운 commit이 없었다. 세 번째 명령에서는 내 브랜치가 제안하는 파일 목록을 다시 확인한다.

팀의 새 commit이 보이면 내 브랜치를 먼저 push해 Draft PR로 검토받을 수 있다. 최종 merge 전에는 최신 main을 반영하고 Unity 검증을 다시 해야 한다. 미완료 로컬 변경이 많은 상태에서 `git pull`이나 merge를 무작정 실행하지 않는다. 업데이트 방법은 11절에 있다.

본인 브랜치를 올리는 명령은 다음 하나다.

```powershell
git push -u origin codex/ball-effects
```

`origin`은 팀 GitHub 주소의 별명이다. 마지막 인수가 업로드할 브랜치이므로 이 명령은 `main`을 직접 수정하지 않는다. `-u`는 이후 push할 대상 브랜치를 연결한다.

로그인 창이 나타나면 팀 저장소에 쓰기 권한이 있는 본인 GitHub 계정으로 로그인한다. Git Credential Manager가 설정되어 있으면 브라우저 로그인으로 인증할 수 있다. 이 저장소에 대한 읽기 성공은 쓰기 권한까지 보장하지 않는다. `403`/`Permission denied`가 나오면 팀 저장소 관리자가 본인 계정에 협업 권한을 부여했는지 확인한다. 비밀번호나 토큰을 코드, 문서, 채팅에 적지 않는다. [GitHub 인증 안내](https://docs.github.com/en/get-started/git-basics/caching-your-github-credentials-in-git)

`non-fast-forward` 또는 `rejected`가 나오면 같은 이름의 원격 브랜치에 내 로컬에 없는 기록이 있을 수 있다. 강제 push로 덮어쓰지 말고 오류와 원격 브랜치 기록부터 확인한다.

## 9 GitHub에서 Pull Request 만들기

1. 팀 저장소 웹페이지를 연다.
2. push 후 표시되는 **Compare & pull request**를 누른다. 없으면 **Pull requests → New pull request**를 연다.
3. **base: main**, **compare: codex/ball-effects**인지 확인한다. 팀이 별도 통합 브랜치를 쓰기로 정했다면 base는 그 브랜치로 맞춘다.
4. Files changed에서 내 공 폴더, 전용 씬, 문서만 있는지 확인한다. Block.cs, GridManager.cs, 기존 프리팹, 공용 씬이 들어가면 원인을 확인하기 전에는 합치지 않는다.
5. 아래 제목과 설명을 넣고 **Create pull request**를 누른다. 최신 main 반영이나 팀 검토가 남았으면 **Create draft pull request**를 선택한다.
6. 블록 담당자 또는 팀 통합 담당자에게 PR 검토를 요청한다. 아직 연결하지 않은 게임 씬까지 완료했다고 쓰지 않는다.

제목 예시:

```text
공 8종 효과와 블록 상호작용 테스트 추가
```

설명 예시:

```text
지정한 Block에 적중을 전달하면 일반공을 포함한 8종 공의 피해와 추가 효과를 처리합니다.
이동과 충돌 없이 전용 BallEffectsTest 씬에서 검증할 수 있습니다.

Block.cs, GridManager.cs, 기존 Block 프리팹과 SampleScene은 수정하지 않았습니다.
BallBoardLookup이 GridManager.GetCellPosition과 해당 보드의 블록 부모를 이용해 칸을 조회합니다.
피해는 기존 Block.TakeDamage를 호출합니다.

초기화: effects.Configure(grid, blocksParent)
호출 순서: BeginShot → RegisterBall → ApplyHit → 전체 구간 종료 시 EndShot
블록 하강이 끝나 격자 칸에 놓인 뒤 적중을 전달해야 합니다.

검증: Unity 6000.5.10f1 전용 씬 Play 모드 40개 통과 / 0개 실패.
결과: Docs/BallEffectsTestResults.json
사용법: Docs/BallEffectsReadingGuide.md

감전 대상은 중복 없는 무작위 선택, 구간 종료 시 남은 화염 예약은 취소하는 가정입니다.
공 이동·물리 충돌·반사·공 프리팹 및 실제 게임 씬 통합은 후속 작업입니다.
```

## 10 검토 후 합치고 다음 작업 시작하기

PR의 리뷰, 팀의 최신 main 반영, 필요한 Unity 검증이 완료되면 팀에서 정한 통합 담당자가 merge한다. merge는 공유 main이 실제로 바뀌는 단계다. 리뷰 요청이나 PR 생성만으로 합쳐지는 것은 아니다.

팀원이 수정 의견을 주면 같은 작업 브랜치에서 필요한 파일만 고치고 add → commit → `git push`를 다시 한다. 기존 PR에 새 commit이 반영된다. 이미 push한 commit을 처음부터 다시 작성하거나 강제 push할 필요는 없다.

merge가 끝나면 Unity를 닫고, 로컬의 남은 변경을 먼저 확인한다. `git status --short`가 비어 있는 상태라면 아래 순서로 최신 main으로 돌아온다.

```powershell
git switch main
git pull --ff-only origin main
```

현재 프로젝트에는 별도 로컬 설정 변경이 남을 수 있으므로, 곧바로 이 두 명령을 실행하지 않아도 된다. 남은 파일이 무엇인지 확인하고 보관하거나 정리한 뒤 진행한다. 다음 기능을 시작할 때는 최신 main에서 `git switch -c codex/다음-기능-이름`으로 새 브랜치를 만든다. merge된 이전 작업 브랜치에 계속 쌓지 않는다.

## 11 팀 코드가 바뀌었거나 충돌이 났을 때

다른 파일만 추가해도 팀원이 공개 함수의 이름이나 블록 배치 방식을 바꾸면 실행상의 문제가 생길 수 있다. Git의 글자 단위 충돌이 없다는 것과 Unity에서 정상 작동한다는 것은 별개다. 최종 merge 전에 최신 팀 코드와 테스트해야 한다.

아래는 공 작업을 이미 commit한 뒤, 내 작업 브랜치에 최신 main을 가져오는 절차다. **Unity를 닫고 `git status --short`가 비어 있는 상태에서만** 진행한다. 남아 있는 변경이 있으면 먼저 내용을 확인하고 필요한 작업은 적절한 브랜치에 commit하거나 별도로 보관한다. 모든 파일을 일괄 commit해서 깨끗하게 만들지는 않는다.

```powershell
git switch codex/ball-effects
git fetch origin
git merge --no-edit origin/main
```

merge는 최신 팀 commit을 내 작업 브랜치에 합친다. 공유 main에는 push하지 않는다. 성공하면 Unity를 다시 열어 컴파일과 40개 검증을 확인하고 내 브랜치를 push한다.

충돌이 생기면 다음 명령으로 해당 파일만 확인한다.

```powershell
git status
git diff --name-only --diff-filter=U
```

처음이라 판단하기 어렵거나 팀원 코드·공용 씬에서 충돌했다면, 임의로 **Accept Ours / Accept Theirs**를 누르지 않는다. 위의 깨끗한 상태에서 시작한 merge를 취소하려면 아래 명령을 실행한다.

```powershell
git merge --abort
```

그 뒤 오류와 충돌 파일 목록을 블록 담당자와 함께 확인한다. 팀원의 최신 공개 함수에 맞춰 내 BallBoardLookup/호출 코드를 고치는 방향을 먼저 검토한다. 직접 해결하기로 합의했다면 파일의 충돌 표시를 정리하고, 해결한 파일만 add → commit → Unity 검증 → 내 브랜치 push를 진행한다. `.unity`, `.prefab`, `.meta` 충돌은 참조가 끊길 수 있으므로 담당자와 함께 처리한다. [Git merge 공식 문서](https://git-scm.com/docs/git-merge)

이 절차에서 `git push --force`, `git reset --hard`, 전체 변경 폐기는 필요하지 않다. 오류가 났을 때 보낼 정보는 실행한 명령, 오류 문장, `git status` 결과면 충분하다.

## 12 팀에 전달할 연결 계약

| 연결하는 쪽 | 공 코드와 약속할 것 |
| --- | --- |
| 블록 담당 | 기존 TakeDamage, 체력/속성 읽기, GetCellPosition을 사용한다. 해당 보드의 블록 부모를 연결하고 발사 전 정확한 칸에 배치한다 |
| 이후 공 이동 담당 | 유효 적중마다 ApplyHit 한 번 호출. Block.TakeDamage를 따로 다시 호출하지 않는다 |
| 발사 구간 담당 | 증가하는 shotId, 공별 ballId를 제공한다. 전체 구간 종료 때만 EndShot 호출 |
| 게이지/보상 담당 | DamageBatchCompleted 이벤트의 실제 피해와 파괴 기록을 한 번만 집계한다 |
| 씬 통합 담당 | 공용 씬 연결은 담당자가 작업한다. 이번 PR은 전용 테스트 씬만 추가한다 |

이 연결 계약을 읽을 자세한 예시는 [코드 읽기 안내](BallEffectsReadingGuide.md)에 있다.
