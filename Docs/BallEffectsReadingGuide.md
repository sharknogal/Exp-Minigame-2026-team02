# 공 효과 코드 읽기와 실습 안내

일반공을 포함한 8종 공의 피해와 블록 상호작용을 구현했다. 공 이동이나 물리 충돌 없이 지정한 블록에 적중을 요청하는 구조다. 처음에는 모든 파일을 읽지 말고, **피해 한 번이 어떤 순서로 처리되는지**부터 따라가면 된다.

Unity 6000.5.10f1의 실제 Play 모드에서 자동 검증 40개가 통과했다. 테스트 씬과 Inspector 버튼을 통해 폭발공의 중심 체력 90, 주변 체력 95, 총 피해 50도 확인했다. 전체 결과는 [검증 기록](BallEffectsTestResults.json)에 저장했다.

## 1 먼저 Unity에서 확인하기

1. Project에서 `_Project → Scenes → BallEffectsTest` 씬을 연다. 현재 에디터에는 이 씬을 열어 두었다.
2. Play를 누른다. 일반 블록 36개가 체력 100으로 생성된다. Play 전에는 템플릿만 있어 블록이 보이지 않는 것이 정상이다.
3. Hierarchy에서 **Ball Effect Test Controls**를 선택한다.
4. Inspector의 **Ball Effect Test Harness**에서 `Ball Type`을 `Explosion`으로 바꾼다. `Attack Power`는 10, `Ball Id`는 1, `Target Row`는 4, `Target Column`은 3으로 둔다.
5. **배치 초기화 및 선택한 공 등록**을 누르고 준비 완료 메시지를 기다린다.
6. **선택한 칸에 적중**을 누른다. 중심이 90, 주변 8칸이 95가 되고 Inspector의 실제 피해와 게이지 증가 합계는 50, 콤보는 1이 된다.

화면의 맨 위 블록 줄은 2행이다. 4행 3열은 화면에서 위에서 세 번째 줄, 왼쪽에서 세 번째 블록이다.

공 종류 대응은 다음과 같다.

| Inspector 값 | 기획서 이름 |
| --- | --- |
| Normal | 일반공 |
| Explosion | 폭발공 |
| Electric | 감전공 |
| HorizontalLaser | 가로 레이저공 |
| VerticalLaser | 세로 레이저공 |
| CrossLaser | 십자 레이저공 |
| Fire | 화염공 |
| Charge | 충전공 |

같은 공의 재적중은 **선택한 칸에 적중**을 다시 누른다. 공 종류나 공격력을 바꾸면 **새 구간 시작 및 선택한 공 등록**을 눌러 새 설정을 등록한다. 이 버튼은 블록 체력을 복구하지 않는다. 체력까지 초기화하려면 배치 초기화 버튼을 사용한다.

같은 발사 구간에 공을 추가하려면 `Ball Id`를 다른 번호로 바꾸고 **현재 구간에 다른 번호의 공 추가**를 누른다. 공 종류가 같아도 번호가 다르면 각자 레이저 첫 적중 상태를 가진다.

## 2 코드를 읽을 순서

| 순서 | 파일 | 이 파일에서 이해할 질문 |
| --- | --- | --- |
| 1 | [BallDamageService.cs](../Assets/_Project/Scripts/Balls/BallDamageService.cs) | 체력 3에 피해 10을 줬는데 실제 피해가 3인 이유는 무엇인가 |
| 2 | [BallEffectController.cs](../Assets/_Project/Scripts/Balls/BallEffectController.cs) | ApplyHit 한 번이 어떤 순서로 직접 피해와 추가 효과를 실행하는가 |
| 3 | [BallAreaEffects.cs](../Assets/_Project/Scripts/Balls/BallAreaEffects.cs) | 폭발의 주변 칸과 레이저의 행과 열을 어떻게 고르는가 |
| 4 | [BallEffectTypes.cs](../Assets/_Project/Scripts/Balls/BallEffectTypes.cs) | 공 종류와 공 하나의 첫 적중 상태를 어떻게 보관하는가 |
| 5 | [BallDamageReport.cs](../Assets/_Project/Scripts/Balls/BallDamageReport.cs) | 함수 반환값과 피해 이벤트에는 각각 무엇을 담는가 |
| 6 | [BallFireScheduler.cs](../Assets/_Project/Scripts/Balls/BallFireScheduler.cs) | 3회의 예약 피해를 언제 실행하고 언제 취소하는가 |
| 7 | [BallBoardLookup.cs](../Assets/_Project/Scripts/Balls/BallBoardLookup.cs) | 팀원 코드 수정 없이 어떤 부모의 어떤 칸을 조회하는가 |

첫 번째 파일은 24줄이다. `target.TakeDamage(amount)` 앞에서 체력을 기억하고, 호출 뒤 남은 체력과 비교하는 부분부터 읽어 보자. 여기서 체력 감소와 파괴는 팀원의 `Block`이 처리한다. 이 파일은 그 결과를 기록한다.

효과와 블록 연결 코드는 위 일곱 파일이다. `Testing` 폴더는 실험 도구와 검증 코드이므로 효과 흐름을 이해한 다음 읽어도 된다.

## 3 일반공 적중 한 번 따라가기

`BallEffectController.ApplyHit`은 아래 순서로 읽는다.

1. 올바른 발사 구간인지, 등록된 공인지, 격자에 살아 있는 대상이 있는지 검사한다. 실패하면 아무 피해도 주지 않고 무효 결과를 반환한다.
2. `BallBoardLookup.TryGetCell`로 대상의 행과 열을 기억한다. 이후 블록이 파괴돼도 폭발이나 레이저의 중심 칸이 남는다.
3. 공의 첫 적중 여부를 기억하고 `HasHitBlock`을 true로 바꾼다.
4. `BallDamageService.Apply`로 직접 피해를 한 번 적용한다.
5. `ApplyAdditionalEffect`로 들어간다. 일반공은 추가 효과가 없으므로 여기서 더 하는 일이 없다.
6. 피해 기록을 `DamageBatch`로 묶고 즉시 결과를 만든다.
7. 결과 이벤트를 알리고 호출자에게 `HitResult`를 반환한다.

`ApplyAdditionalEffect`의 switch 문에서 Explosion 부분으로 이동하면, 일반공과 비교해 무엇이 추가되는지 확인할 수 있다. 다른 효과를 이해할 때도 공통 직접 피해 부분을 다시 구현할 필요가 없다.

## 4 격자와 블록의 역할

[GridManager.cs](../Assets/_Project/Scripts/GridManager.cs)와 `Block.cs`, 기존 `Block` 프리팹과 공용 씬은 수정하지 않았다. 초기 구현에서 GridManager에 추가했던 조회 함수는 제거하고, 새 `BallBoardLookup.cs`가 조회를 맡도록 바꿨다.

`effects.Configure(grid, blocksParent)`에서 기존 GridManager와 해당 보드의 블록 부모를 연결한다. 그 뒤 `effects.Board.GetBlock(4, 3)`은 이 부모 아래의 4행 3열 블록을 반환한다. 범위 밖, 빈칸, 비활성 또는 파괴된 대상은 null이다. 같은 칸에 두 블록이 겹쳐도 null이다.

조회는 현재 블록의 월드 XY 위치와 공개 함수 `GridManager.GetCellPosition`의 결과를 비교한다. 8행 × 6열, 허용 오차 0.001 Unity 단위를 사용한다. 팀원의 private 배열에 접근하지 않고, Physics2D나 Reflection도 사용하지 않는다. 따라서 해당 부모에는 그 보드의 블록만 넣고, 블록 하강이 끝나 칸에 배치된 뒤 적중을 전달해야 한다. 격자 규격이 바뀌거나 이동 중 피해가 필요해지면 이 연결 파일을 함께 보완한다.

매번 자식 목록을 다시 읽으므로 새 블록이나 재배치가 반영된다. 블록 생성과 GridManager 내부 배열의 갱신은 계속 블록 담당자의 책임이다. 테스트에서 임시로 위치를 바꾸는 사례는 조회 기능만 확인하며 실제 하강 기능은 아니다.

`Block.TakeDamage`가 파괴를 결정하면 `IsDestroyed`를 바로 true로 만들지만 실제 GameObject 삭제는 프레임 끝에 이루어진다. 그래서 피해 함수는 null뿐 아니라 `IsDestroyed`도 검사한다. 같은 프레임에 다른 효과가 들어와도 이미 파괴된 블록에서 추가 체력이나 보상을 얻지 않게 하는 부분이다.

## 5 발사 구간과 공 번호

`shotId`는 모든 공이 함께 참여하는 발사 구간 번호다. `ballId`는 그 구간 안에서 공 하나를 식별하는 번호다. 예를 들어 같은 구간의 가로 레이저공 1번과 2번은 각각 첫 적중 효과를 쓸 수 있다.

구간 번호는 0 이상에서 계속 증가시킨다. 새 구간은 `effects.CurrentShotId + 1`로 시작하면 된다. 같은 번호를 다시 시작하거나 같은 공을 다시 등록해 첫 적중 효과를 충전할 수는 없다. 늦게 도착한 이전 구간의 적중이나 종료 요청도 현재 구간에 영향을 주지 않는다.

컨트롤러를 비활성화하면 현재 구간과 예약을 닫는다. `Configure`로 다른 격자를 연결할 때도 기존 예약을 정리한다. 일반적인 공 하나의 회수에서는 이 컨트롤러를 끄거나 `EndShot`을 호출하지 않는다.

## 6 화염은 공의 수명과 분리한다

화염 적중은 대상과 공격력, 실행 시각을 기억한 예약 3개를 만든다. `BallEffectController.Update`는 게임 시간이 흐를 때 `BallFireScheduler.Tick`을 호출한다. 예약 시각이 된 항목은 추가 피해 경로로 실행한다.

예약은 적중 시각에 0.15초, 0.30초, 0.45초를 더해 계산한다. 이전 피해 실행 시각에 다시 0.15초를 더하지 않으므로 느린 프레임 때문에 전체 예약이 계속 밀리지 않는다. 한 프레임에 여러 예약 시각을 지나쳤다면 도래한 항목을 순서대로 처리한다.

다시 적중하면 새 예약이 추가된다. 대상이 파괴되면 그 대상의 예약은 실행하지 않는다. 같은 칸에 새 블록을 만들어도 이전 블록의 예약 피해를 물려받지 않는다. 전체 구간 종료 시 남은 예약을 지운다.

이 클래스가 개별 공 GameObject를 참조하지 않는 이유는 공이 먼저 회수돼도 전체 발사가 끝나지 않았다면 화염이 계속되어야 하기 때문이다. `Time.timeScale`이 0이면 게임 시간과 예약 처리도 멈춘다.

## 7 반환값과 이벤트를 다르게 쓰기

`HitResult`는 호출자가 방금 적중의 결과를 판단하는 데 쓴다. 나중에 이동을 만들면 `IsValid`와 `DirectTargetDestroyed`를 보고 반사 또는 관통을 결정한다. 직접 피해 후 즉시 레이저에 의해 파괴된 경우도 포함한다. 미래의 화염 피해는 이 결과에 포함하지 않는다.

`DamageBatchCompleted`는 피해가 실제 적용될 때 발생하는 C# 이벤트다. 게이지나 보상 담당자가 이 이벤트를 구독한다. 직접 적중과 즉시 효과는 한 묶음이고, 화염은 각 예약 피해가 실행될 때 별도 묶음으로 전달된다.

실제 피해와 게이지를 누적할 때는 이벤트를 한 번만 사용한다. 반환값의 합계까지 다시 더하면 두 번 집계된다. 테스트 하네스의 `RecordBatch`가 구독 예시다. 이 코드는 통계만 누적하며 실제 골드 지급이나 게이지 600에서의 능력 발동은 구현하지 않았다.

상위 시스템은 묶음의 파괴 기록으로 보상을 처리한 뒤 게이지를 충전하고 능력을 발동하면 된다. 능력이 대상을 즉시 없앨 수 있다면 최종 반사 판단 전에 생존 여부도 확인한다.

## 8 나중에 호출할 때의 예시

아래 코드는 Play 중에 `effects`, `grid`, 해당 보드의 블록 부모 `blocksParent` 참조가 준비된 상황을 가정한 호출 예시다. 이동이나 충돌 검사는 포함하지 않는다.

```csharp
using BrickBreaker.Balls;

// 격자 연결은 초기화할 때 한 번 한다.
effects.Configure(grid, blocksParent);

int shotId = effects.CurrentShotId + 1;
effects.BeginShot(shotId);
effects.RegisterBall(shotId, 1, BallEffectType.Explosion, 10);

Block target = effects.Board.GetBlock(4, 3);
HitResult result = effects.ApplyHit(shotId, 1, target);

// 전체 발사 구간이 종료되는 시점에 호출한다.
effects.EndShot(shotId);
```

나중에는 `target`을 실제 충돌에서 얻은 Block으로 바꾼다. 충돌 코드에서 `Block.TakeDamage`를 따로 호출하지 않는다. 위 `ApplyHit` 내부에서 이미 직접 피해를 처리하기 때문이다. 같은 물리 접촉에서 적중을 여러 번 전달하지 않는 처리는 이후 충돌 구현의 책임이다.

## 9 C++ 기초와 연결해서 읽기

| 문법 | 이 코드에서의 의미 |
| --- | --- |
| `namespace BrickBreaker.Balls` | 다른 팀원 코드와 이름이 겹치지 않도록 묶는다 |
| `internal` | 같은 C# 어셈블리 내부에서 쓰는 구현 세부 사항이다 |
| `public int BallId { get; }` | 외부에서는 읽기만 가능한 값이다. 여기서는 생성자에서 정한다 |
| `Dictionary<int, BallEffectState>` | 공 번호로 공의 상태를 찾는 표다. C++의 map 계열과 비슷한 용도다 |
| `List<DamageRecord>` | 피해 기록 수만큼 늘어나는 배열 같은 목록이다 |
| `event Action<DamageBatch>` | 피해가 끝났다는 알림에 다른 함수가 연결될 수 있다 |
| `IEnumerator`와 `yield return` | 테스트에서 다음 프레임 또는 일정 시간까지 기다린 뒤 이어서 실행한다 |

핵심 효과 함수들은 일반적인 조건문과 반복문으로 작성했다. 첫 실습에서는 `ApplyExplosion`의 중심 칸을 건너뛰는 조건과, `GetBlock`이 빈칸을 반환하는 상황만 따라가도 범위 피해의 동작을 이해할 수 있다.

## 10 자동 검증과 사용한 가정

Inspector의 **전체 자동 검증 실행**을 누르면 테스트마다 새 격자를 만든다. 수초 동안 테스트를 실행한 뒤 Console과 Inspector에 통과 및 실패 수가 나타난다. 완료 후에는 일반적인 수동 실험을 계속할 수 있도록 체력 100 배치를 복구한다.

이 검증은 Unity Test Runner에 등록하는 NUnit 테스트가 아니라, 전용 테스트 씬의 Play 모드에서 실행하는 통합 검증 도구다. `BallEffectTestSuite`의 각 사례는 실제 Block과 GridManager를 사용하고 기대값을 비교한다. 효과 코드와 별도로 적은 40개 사례에는 경계 칸, 초과 피해, 첫 적중 초기화, 화염의 실행 시각과 취소, 일시정지와 콜백 중 종료가 포함된다.

기획서에서 미정이거나 서로 다른 규칙은 계획서의 가정을 유지했다. 감전은 중복 없는 무작위 선택이며, 발사 구간 종료 시 남은 화염 예약은 취소한다. 물리 이동, 충돌, 반사, 완성형 공 프리팹과 추가 시각 연출은 후속 작업이다.

원래 [구현 계획](BallEffectsImplementationPlan.md)과 이 안내를 함께 보면 요구사항이 어느 코드에 반영됐는지 비교할 수 있다.
