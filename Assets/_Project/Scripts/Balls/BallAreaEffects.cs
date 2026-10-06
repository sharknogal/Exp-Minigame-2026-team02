using System.Collections.Generic;
using UnityEngine;

namespace BrickBreaker.Balls
{
    // 이동이나 물리 검색 없이, 격자의 칸을 골라 추가 피해만 적용한다.
    internal sealed class BallAreaEffects
    {
        private readonly BallBoardLookup grid;
        private readonly System.Random random;

        public BallAreaEffects(BallBoardLookup grid, System.Random random)
        {
            this.grid = grid;
            this.random = random;
        }

        public void ApplyExplosion(int row, int column, int shotId,
            BallEffectState ball, List<DamageRecord> records)
        {
            for (int targetRow = row - 1; targetRow <= row + 1; targetRow++)
            {
                for (int targetColumn = column - 1; targetColumn <= column + 1; targetColumn++)
                {
                    if (targetRow == row && targetColumn == column)
                        continue;

                    DamageCell(targetRow, targetColumn, ball.AttackPower / 2, shotId, ball, records);
                }
            }
        }

        public void ApplyElectric(int row, int column, int shotId,
            BallEffectState ball, List<DamageRecord> records)
        {
            var candidates = new List<Vector2Int>();
            for (int targetRow = row - 2; targetRow <= row + 2; targetRow++)
            {
                for (int targetColumn = column - 2; targetColumn <= column + 2; targetColumn++)
                {
                    if (targetRow == row && targetColumn == column)
                        continue;

                    if (grid.GetBlock(targetRow, targetColumn) != null)
                        candidates.Add(new Vector2Int(targetRow, targetColumn));
                }
            }

            int count = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int selectedIndex = random.Next(candidates.Count);
                Vector2Int cell = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex); // 이미 선택한 블록은 다시 뽑지 않는다.
                DamageCell(cell.x, cell.y, ball.AttackPower, shotId, ball, records);
            }
        }

        public void ApplyRow(int row, int shotId, BallEffectState ball, List<DamageRecord> records)
        {
            for (int column = 1; column <= BallBoardLookup.Columns; column++)
                DamageCell(row, column, ball.AttackPower / 2, shotId, ball, records);
        }

        public void ApplyColumn(int column, int shotId, BallEffectState ball, List<DamageRecord> records)
        {
            for (int row = 1; row <= BallBoardLookup.Rows; row++)
                DamageCell(row, column, ball.AttackPower / 2, shotId, ball, records);
        }

        private void DamageCell(int row, int column, int amount, int shotId,
            BallEffectState ball, List<DamageRecord> records)
        {
            DamageRecord record = BallDamageService.Apply(
                grid.GetBlock(row, column), amount, shotId, ball, row, column, false);

            if (record != null)
                records.Add(record);
        }
    }
}
