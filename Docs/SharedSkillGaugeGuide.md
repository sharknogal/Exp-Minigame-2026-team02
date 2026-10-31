# 공유 특수 능력 게이지 구현 안내

## 1 이번 작업의 범위

공 8종의 실제 피해 결과를 받아 공유 게이지를 충전하고, 기획서 5절의 조건에 맞춰 600을 소모하며 능력 발동을 요청한다. 임시 UI와 별도 테스트 씬을 포함한다.

공의 이동·조준·발사·반사·회수와 네 가지 특수 능력의 실제 공격은 이번 범위에 포함하지 않는다. 테스트 화면의 Begin Shot은 발사 구간을 시작한다는 알림이며, 실제 공을 생성하거나 움직이지 않는다. Activation requests는 발동 요청 횟수이며 블록에 특수 능력 피해를 줬다는 뜻이 아니다.

기준은 `main`의 `a67299b`이며 작업 브랜치는 `codex/shared-skill-gauge`다. 기존 팀원 코드, 공 효과 코드, Block 프리팹과 공용 씬을 수정하지 않고 새 파일로 연결했다. 이 기능은 별도 작업 브랜치에서 공유하고 main 병합은 PR 검토 후 진행한다.

## 2 디자이너와 프로그래머가 맡는 UI

UI는 사용자가 보는 정보와 조작하는 버튼 등의 화면 요소를 뜻한다. 디자이너는 모양·색·아이콘·글꼴·배치를 설계하고 필요한 이미지 리소스를 만든다. 프로그래머는 실제 게임 값에 따라 막대·글자·버튼 상태가 바뀌도록 연결한다. Unity에서 배치하는 업무는 팀의 협의에 따라 나눌 수 있다.

이 씬의 배경과 채움은 Unity 기본 도형을 사용한 임시 표현이다. 영문은 프로젝트의 기본 TMP 글꼴을 그대로 쓰기 위해 선택했다. 최종 아트나 확정된 게임 화면 디자인이 아니다.

디자인을 적용할 때는 Slider의 Background와 Fill 이미지, TMP 글꼴과 색상, RectTransform 배치를 교체하면 된다. SharedSkillGaugeView에 연결된 참조를 유지하거나 새 오브젝트에 다시 연결하면 충전 규칙은 바꾸지 않아도 된다. 게이지 컨트롤러는 화면을 감췄다고 비활성화되지 않도록 별도 게임 관리 오브젝트에 둔다.

## 3 Unity에서 직접 확인하는 순서

1. `Assets/_Project/Scenes/SharedSkillGaugeTest.unity`를 단독으로 연다. 기존 공 테스트 씬과 함께 실행하면 카메라와 전역 조명이 중복될 수 있다.
2. Game 창의 화면 비율을 9:16으로 설정한다. 이번 검증 환경에는 `Shared Gauge Portrait (1080x1920)` 선택지를 준비했다. 팀원의 Unity에는 해당 편집기 선택지가 없을 수 있으므로 직접 1080×1920 해상도를 추가하면 된다.
3. Play를 누른다. 체력 100인 블록 36개와 충전량 0이 표시된다.
4. `Begin Shot` → `Hit Block`을 누른다. 일반공은 체력 10을 깎고 게이지 10을 충전한다.
5. `Next Ball Type`으로 공 종류를 바꿔 같은 버튼으로 비교한다. 화염공의 추가 충전은 0.15·0.30·0.45초 뒤 실제 피해가 발생할 때 반영된다. 레이저의 첫 적중 상태는 공별로 발사 구간 동안 유지된다.
6. `End Shot`을 눌러도 충전량은 유지된다. 남은 화염 예약은 기존 공 시스템의 규칙대로 취소된다.

| 버튼 | 의미 |
| --- | --- |
| New Game | 게이지·능력 보유·발동 횟수와 테스트 블록 배치를 초기화 |
| Ability On / Off | 조준 단계에서 테스트용 능력 보유 여부 변경 |
| Begin Shot | 새 발사 구간 시작. 실제 공 이동 없음 |
| End Shot | 현재 구간 종료. 게이지는 유지 |
| Next Ball Type | 일반 → 폭발 → 감전 → 가로 → 세로 → 십자 → 화염 → 충전 순서 |
| Hit Block | 중앙 블록 또는 남아 있는 블록에 선택한 공의 효과 적용 |
| +1250 Test Charge | 다중 발동 규칙을 확인하는 테스트 충전. 실제 블록 피해 아님 |
| Run All Tests | 독립적인 임시 오브젝트로 자동 검증 실행 |

다중 발동은 `New Game` → `Ability On / Off` → `Begin Shot` → `+1250 Test Charge` 순서로 확인한다. 요청 2회, 잔량 50이 되어야 한다.

첫 능력 획득은 `New Game` → `Begin Shot` → `+1250 Test Charge` → `End Shot` → `Ability On / Off` 순서로 확인한다. 능력 미보유 때는 600까지만 저장되고 650은 버린다. 능력을 켜도 즉시 발동하지 않으며, 다음 `Begin Shot`에서 요청 1회와 잔량 0이 된다.

## 4 파일을 읽는 순서

핵심 게임 코드는 다음 세 파일이다. 각 파일은 한 가지 역할에 집중한다.

| 파일 | 역할 |
| --- | --- |
| `SharedSkillGauge.cs` | Unity 오브젝트와 무관한 충전·소모·발동 규칙과 상태 |
| `SharedSkillGaugeController.cs` | 기존 공 효과의 피해 이벤트를 받아 게이지로 전달 |
| `SharedSkillGaugeView.cs` | 게이지 변경 알림을 받아 막대와 숫자를 갱신 |

파일은 `Assets/_Project/Scripts/Skills/` 아래에 있다. 먼저 `SharedSkillGauge.AddBallCharge`를 읽고 `ResolveActivations`, `BeginShot`, `SetHasAbilities`, `ResetGame` 순서로 따라가면 된다. 그다음 Controller의 `HandleDamageBatch`에서 입력이 어디서 오는지, View의 `Refresh`에서 어떤 값을 보여 주는지 확인한다.

테스트 도구는 다음과 같다.

| 파일 | 역할 |
| --- | --- |
| `Testing/SharedSkillGaugeTestHarness.cs` | 화면 버튼 처리, 테스트 블록 배치, 발동 요청 수신 대역 |
| `Testing/SharedSkillGaugeTestSuite.cs` | 규칙과 실제 공 효과 연결을 검증하는 사례 |
| `Testing/Editor/SharedSkillGaugeSceneBuilder.cs` | 별도 테스트 씬과 임시 UI를 Unity API로 생성 |

Builder 메뉴는 `Tools > Brick Breaker > Create Shared Skill Gauge Test Scene`이다. 이미 씬이 있으면 덮어쓰지 않는다. `.meta`는 Unity가 생성한 에셋 식별 파일이므로 `.cs`나 씬과 함께 공유해야 한다.

## 5 충전과 발동 규칙

```text
BallEffectController.ApplyHit
  → Block.TakeDamage와 공 효과 처리
  → DamageBatchCompleted
  → SharedSkillGaugeController.HandleDamageBatch
  → SharedSkillGauge.AddBallCharge(batch.GaugeCharge)
  → 600씩 소모하며 AbilityActivationRequested
  → Changed 알림으로 화면 갱신
```

화염은 나중에 피해가 실제 적용될 때 동일한 피해 이벤트를 보낸다. `DamageBatch.GaugeCharge`에는 초과 피해 제외와 충전공 2배가 이미 반영돼 있다. `HitResult.ImmediateGaugeCharge`를 추가로 누적하거나 2배를 다시 곱하지 않는다.

능력 미보유 상태에서는 최대 600까지 충전하고 초과량을 버린다. 보유 상태에서는 한 묶음의 피해가 끝난 뒤 600마다 순서대로 발동을 요청한다. 1,250 충전 시 첫 요청 직전 잔량은 650, 두 번째 요청 직전은 50이다. 화면에는 처리가 끝난 최종 상태를 표시한다. 요청 한 번을 받으면 후속 능력 시스템이 그 시점의 보유 능력 전체를 실행해야 한다.

`SetHasAbilities`는 조준 단계에서만 반영한다. 이번에는 보유 능력의 개수·종류·강화를 관리하지 않으며 하나 이상 보유했는지만 받는다. 능력을 보유하지 않게 되는 경우는 테스트와 향후 초기화 연결을 위한 처리이며, 이번에 능력 판매 기능을 추가한 것은 아니다.

`EndShot`은 충전량을 지우지 않는다. 새 게임 초기화는 `BeginGame`에서만 호출한다. 비활성화된 컨트롤러는 구독을 해제하고 발사 상태를 종료하지만 충전량은 유지한다. 다시 활성화했다고 발사 상태를 자동 복구하지는 않는다.

발동 이벤트 안에서 새 게임이나 구간 종료가 발생하면 이전 처리의 남은 발동을 계속 보내지 않는다. 발동 중 재충전 요청도 무시한다. 이 방어가 있어도 특수 능력 피해를 공의 `ApplyHit`으로 처리해서는 안 된다. 공 효과·콤보가 재발동할 수 있기 때문이다. 후속 특수 능력이나 부활 제거는 별도의 피해 경로를 사용하고 공 피해 이벤트를 발행하지 않는다.

## 6 팀 코드와 나중에 연결할 부분

새 게이지 컨트롤러의 `effects` 필드에 실제 게임의 BallEffectController를 연결한다. 런타임에 연결해야 한다면 `Configure(effects)`를 사용한다. 연결을 여러 번 호출해도 이벤트를 중복 구독하지 않는다.

TurnManager에는 상태 변경 이벤트가 없으므로 현재 상태를 매 프레임 추측하지 않는다. 실제 발사와 회수를 총괄할 코드에서 팀원의 공개 함수와 새 게이지 공개 함수를 순서대로 호출하도록 연결한다. 아래는 연결 순서를 보여 주는 예시이며 공 이동 구현이 아니다.

```csharp
// 새 게임을 준비할 때
turnManager.BeginGame();
gaugeController.BeginGame();

// 조준/구매 중 능력 보유 상태가 변경됐을 때
gaugeController.SetHasAbilities(ownedAbilityCount > 0);

// 조준 상태이며 구매 선택이 끝났다는 것을 발사 담당이 확인한 뒤
turnManager.BeginShot();
ballEffects.BeginShot(shotId);
gaugeController.BeginShot();  // 대기 중인 능력 요청을 먼저 처리
// 공들을 RegisterBall로 등록하고, 그다음 실제 공 발사 시작

// 모든 공이 회수되어 전체 발사 구간이 끝날 때
ballEffects.EndShot(shotId);
gaugeController.EndShot();
turnManager.CompleteShot();  // 블록 하강과 다음 턴 처리
```

게이지가 내보내는 `AbilityActivationRequested`에 실제 능력 실행 함수를 연결하는 일은 후속 작업이다. 이벤트 구독과 해제는 반드시 짝을 맞춘다. 구매 화면에서 능력 획득 직후 직접 발동시키지 말고 게이지에 보유 상태만 알려 준다.

`shotId`는 같은 BallEffectController의 수명 동안 계속 증가시킨다. 게이지를 새 게임으로 초기화했다고 공 효과 쪽의 구간 번호를 0으로 되돌리지는 않는다. 또한 현재 팀원의 TurnManager.BeginGame은 Idle 상태에서만 시작하므로, 완전한 게임 재시작 흐름은 향후 별도로 연결해야 한다.

현재 GridManager는 Block.Destroyed에서 파괴 골드를 지급한다. 새 게이지가 DamageRecord를 보고 골드를 다시 지급하면 보상이 중복되므로 하지 않는다. 공 피해가 Block.TakeDamage를 거칠 때 이미 팀원의 보상 처리가 실행된 뒤 게이지 이벤트가 전달된다.

## 7 검증과 Git 공유

자동 검증은 기존 프로젝트와 같은 코루틴 기반 실행 도구다. Unity Test Runner의 NUnit 테스트는 아니다. 새 게이지 규칙, 실제 공 8종 피해 연결, 지연 화염, 구독 중복과 재활성화 등을 검사한다. 실제 실행 결과는 `Docs/SharedSkillGaugeTestResults.json`에 기록한다.

2026-10-28, Unity 6000.5.10f1에서 새 게이지 39개와 기존 공 효과 40개가 모두 통과했다. 실제 포인터 클릭으로 1,250 충전 시 요청 2회와 잔량 50, 이후 일반공 적중 시 블록 체력 90과 잔량 60을 확인했다. 기존 공 테스트에는 새 팀원 코드의 골드 연결을 맞추기 위해 Play 중에만 GoldManager를 연결했으며 기존 씬 파일에는 저장하지 않았다.

추가 파일만 올릴 때의 대상은 다음과 같다.

```text
Assets/_Project/Scripts/Skills.meta
Assets/_Project/Scripts/Skills/           (하위 .meta 포함)
Assets/_Project/Scenes/SharedSkillGaugeTest.unity
Assets/_Project/Scenes/SharedSkillGaugeTest.unity.meta
Docs/SharedSkillGaugeGuide.md
Docs/SharedSkillGaugeTestResults.json
```

기존 로컬 ProjectSettings 등의 변경, `.agents`, `.codex`, `.slnx`는 이 기능의 파일이 아니다. 전체 파일을 한 번에 추가하지 말고 위 범위를 선택한 뒤 변경 목록을 확인한다. 이번 작업은 기존 공 효과 PR에 자동으로 포함되지 않는다. 이 브랜치를 push한 뒤 main 대상의 별도 PR로 공유한다.
