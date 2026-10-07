using UnityEditor;
using UnityEngine;

namespace BrickBreaker.Balls.Testing
{
    // 학습용 Inspector 버튼. 게임에 빌드되는 효과 코드와 분리한다.
    [CustomEditor(typeof(BallEffectTestHarness))]
    public sealed class BallEffectTestHarnessEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var harness = (BallEffectTestHarness)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("공 종류를 바꾼 뒤에는 새 구간을 열고 공을 다시 등록하세요. Apply Hit은 같은 공으로 반복 적중합니다.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || harness.IsBusy))
            {
                if (GUILayout.Button("배치 초기화 및 선택한 공 등록"))
                    harness.PrepareFixture();
                if (GUILayout.Button("새 구간 시작 및 선택한 공 등록"))
                {
                    harness.BeginTestShot();
                    harness.RegisterTestBall();
                }
                if (GUILayout.Button("현재 구간에 다른 번호의 공 추가"))
                    harness.RegisterTestBall();
                if (GUILayout.Button("선택한 칸에 적중"))
                    harness.ApplySelectedHit();
                if (GUILayout.Button("구간 종료 및 화염 취소"))
                    harness.EndTestShot();
                if (GUILayout.Button("전체 자동 검증 실행"))
                    harness.RunAllCases();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실제 피해 합계", harness.TotalActualDamage.ToString());
            EditorGUILayout.LabelField("게이지 증가 합계", harness.TotalGaugeCharge.ToString());
            EditorGUILayout.LabelField("직접 적중 콤보", harness.TotalCombo.ToString());
            EditorGUILayout.LabelField("파괴된 블록 수", harness.TotalDestroyed.ToString());
            if (harness.Effects != null)
                EditorGUILayout.LabelField("남은 화염 예약", harness.Effects.PendingFireDamageCount.ToString());
            EditorGUILayout.HelpBox(harness.LastMessage, MessageType.None);
            if (harness.TestReport != null)
                EditorGUILayout.LabelField("자동 검증 결과", $"{harness.TestReport.passed} 통과 / {harness.TestReport.failed} 실패");

            if (Application.isPlaying)
                Repaint();
        }
    }
}
