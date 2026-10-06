using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrickBreaker.Balls
{
    // 팀원의 private 배열에 접근하지 않는 연결용 클래스다.
    // 연결한 부모 아래의 블록 위치를 GridManager의 공개 좌표 함수와 비교한다.
    public sealed class BallBoardLookup
    {
        public const int Rows = 8;
        public const int Columns = 6;
        private const float PositionTolerance = 0.001f;

        private readonly GridManager grid;
        private readonly Transform blockRoot;
        private readonly List<Block> candidates = new List<Block>();

        public bool IsAvailable => grid != null && blockRoot != null;

        public BallBoardLookup(GridManager grid, Transform blockRoot)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (blockRoot == null)
                throw new ArgumentNullException(nameof(blockRoot));
            if (grid.gameObject.scene != blockRoot.gameObject.scene)
                throw new ArgumentException("격자와 블록 부모는 같은 씬에 있어야 합니다.", nameof(blockRoot));

            this.grid = grid;
            this.blockRoot = blockRoot;
        }

        public Block GetBlock(int row, int column)
        {
            if (!IsAvailable || row < 1 || row > Rows || column < 1 || column > Columns)
                return null;

            // 조회할 때 다시 읽으므로 생성/삭제/재배치를 별도 목록에 동기화하지 않는다.
            blockRoot.GetComponentsInChildren(false, candidates);
            Block found = null;
            foreach (Block block in candidates)
            {
                if (!IsLiveInBoard(block) || !IsAtCell(block, row, column))
                    continue;

                // 같은 칸에 두 블록이 겹쳤다면 임의의 한쪽에 피해를 주지 않는다.
                if (found != null)
                    return null;
                found = block;
            }

            return found;
        }

        public bool TryGetCell(Block block, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (!IsLiveInBoard(block))
                return false;

            for (int candidateRow = 1; candidateRow <= Rows; candidateRow++)
            {
                for (int candidateColumn = 1; candidateColumn <= Columns; candidateColumn++)
                {
                    if (!IsAtCell(block, candidateRow, candidateColumn))
                        continue;
                    if (GetBlock(candidateRow, candidateColumn) != block)
                        return false;

                    row = candidateRow;
                    column = candidateColumn;
                    return true;
                }
            }

            return false;
        }

        private bool IsLiveInBoard(Block block)
        {
            return IsAvailable && block != null && !block.IsDestroyed &&
                block.isActiveAndEnabled && block.transform.IsChildOf(blockRoot);
        }

        private bool IsAtCell(Block block, int row, int column)
        {
            Vector2 difference = (Vector2)block.transform.position - grid.GetCellPosition(row, column);
            return difference.sqrMagnitude <= PositionTolerance * PositionTolerance;
        }
    }
}
