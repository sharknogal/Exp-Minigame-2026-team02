using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrickBreaker.Balls.Testing
{
    // 테스트 씬의 사용 도구다. 게임의 발사/회수 시스템을 구현하는 클래스가 아니다.
    public sealed class BallEffectTestHarness : MonoBehaviour
    {
        [Header("테스트 씬 연결")]
        [SerializeField] private GridManager gridTemplate;
        [SerializeField] private BallEffectController effects;

        [Header("등록할 공")]
        [SerializeField] private BallEffectType ballType = BallEffectType.Normal;
        [Min(10)] [SerializeField] private int attackPower = 10;
        [Min(0)] [SerializeField] private int ballId = 1;

        [Header("적중시킬 칸")]
        [Range(2, 7)] [SerializeField] private int targetRow = 4;
        [Range(1, 6)] [SerializeField] private int targetColumn = 3;

        private readonly List<DamageBatch> batches = new List<DamageBatch>();
        private readonly List<double> batchTimes = new List<double>();
        private GridManager fixture;

        public BallEffectController Effects => effects;
        public GridManager Grid => fixture;
        public BallBoardLookup Board => effects.Board;
        public IReadOnlyList<DamageBatch> Batches => batches;
        public IReadOnlyList<double> BatchTimes => batchTimes;
        public int ShotId => effects.CurrentShotId;
        public bool IsBusy { get; private set; }
        public long TotalActualDamage { get; private set; }
        public long TotalGaugeCharge { get; private set; }
        public int TotalCombo { get; private set; }
        public int TotalDestroyed { get; private set; }
        public string LastMessage { get; private set; } = "Play를 눌러 테스트 배치를 준비하세요.";
        public BallEffectTestReport TestReport { get; private set; }
        public string TestReportJson => TestReport == null ? "{}" : JsonUtility.ToJson(TestReport, true);

        private void OnEnable()
        {
            if (effects != null)
                effects.DamageBatchCompleted += RecordBatch;
        }

        private IEnumerator Start()
        {
            yield return PrepareManualFixture();
        }

        private void OnDisable()
        {
            if (effects != null)
            {
                effects.DamageBatchCompleted -= RecordBatch;
                effects.EndShot(effects.CurrentShotId);
            }
        }

        [ContextMenu("Prepare Fixture")]
        public void PrepareFixture()
        {
            if (Application.isPlaying && !IsBusy)
                StartCoroutine(PrepareManualFixture());
        }

        private IEnumerator PrepareManualFixture()
        {
            IsBusy = true;
            yield return ResetFixture();
            IsBusy = false;
            BeginTestShot();
            RegisterTestBall();
            LastMessage = "체력 100 배치 준비 완료. 선택한 공을 등록했습니다.";
        }

        // 이전 블록의 지연 삭제가 끝난 뒤 새 GridManager와 배열을 만든다.
        internal IEnumerator ResetFixture(int health = 100, int randomSeed = 12345)
        {
            effects.EndShot(effects.CurrentShotId);
            if (fixture != null)
                Destroy(fixture.gameObject);

            yield return null;
            fixture = Instantiate(gridTemplate);
            fixture.name = "Test Blocks";
            fixture.gameObject.SetActive(true);
            // 이 테스트 템플릿은 자신의 자식으로만 블록을 생성한다.
            effects.Configure(fixture, fixture.transform, randomSeed);

            for (int row = 2; row <= 7; row++)
            {
                for (int column = 1; column <= 6; column++)
                    fixture.CreateBlock(row, column, health);
            }

            ClearReadout();
        }

        [ContextMenu("Begin Test Shot")]
        public void BeginTestShot()
        {
            if (!Application.isPlaying || IsBusy || fixture == null)
                return;

            effects.BeginShot(effects.CurrentShotId + 1);
            ClearReadout();
            LastMessage = "새 발사 구간 시작. Register Test Ball로 공을 등록하세요.";
        }

        [ContextMenu("Register Test Ball")]
        public void RegisterTestBall()
        {
            if (!Application.isPlaying || IsBusy)
                return;

            bool registered = effects.RegisterBall(ShotId, ballId, ballType, attackPower);
            LastMessage = registered
                ? $"공 {ballId}: {ballType}, 공격력 {attackPower} 등록 완료"
                : "등록 실패: 같은 번호가 있거나 구간/공격력이 유효하지 않습니다.";
        }

        [ContextMenu("Apply Hit")]
        public void ApplySelectedHit()
        {
            if (!Application.isPlaying || IsBusy || fixture == null)
                return;

            HitResult result = effects.ApplyHit(ShotId, ballId, Board.GetBlock(targetRow, targetColumn));
            LastMessage = result.IsValid
                ? $"({targetRow}, {targetColumn}) 적중: 즉시 피해 {result.ImmediateActualDamage}, 대상 파괴 {result.DirectTargetDestroyed}"
                : "적중 실패: 빈칸, 미등록 공 또는 종료된 구간입니다.";
            Debug.Log(LastMessage, this);
        }

        [ContextMenu("End Test Shot")]
        public void EndTestShot()
        {
            if (!Application.isPlaying || IsBusy)
                return;

            effects.EndShot(ShotId);
            LastMessage = "발사 구간 종료. 남은 화염 예약을 취소했습니다.";
        }

        [ContextMenu("Run All Cases")]
        public void RunAllCases()
        {
            if (Application.isPlaying && !IsBusy)
                StartCoroutine(RunSuite());
        }

        private IEnumerator RunSuite()
        {
            IsBusy = true;
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            TestReport = new BallEffectTestReport();
            try
            {
                var suite = new BallEffectTestSuite(this, TestReport);
                yield return suite.Run();
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                effects.EndShot(ShotId);
                IsBusy = false;
            }

            LastMessage = $"자동 검증: {TestReport.passed} 통과 / {TestReport.failed} 실패";
            Debug.Log(LastMessage, this);
            // 테스트가 끝나면 사용자가 바로 실험할 수 있도록 원래 배치로 돌아온다.
            yield return PrepareManualFixture();
        }

        private void RecordBatch(DamageBatch batch)
        {
            batches.Add(batch);
            batchTimes.Add(Time.timeAsDouble);
            TotalActualDamage += batch.ActualDamage;
            TotalGaugeCharge += batch.GaugeCharge;
            TotalCombo += batch.ComboDelta;
            TotalDestroyed += batch.DestroyedCount;
        }

        private void ClearReadout()
        {
            batches.Clear();
            batchTimes.Clear();
            TotalActualDamage = 0;
            TotalGaugeCharge = 0;
            TotalCombo = 0;
            TotalDestroyed = 0;
        }
    }
}
