using UnityEngine;
using UnityEngine.UIElements;

// 필드의 각 칸에 어떤 벽돌이 있는지 관리하고, 원하는 칸에 벽돌을 생성하는 코드
public class GridManager : MonoBehaviour
{
    //8행*6열의 필드를 정의
    private const int Rows = 8;
    private const int Columns = 6;

    [SerializeField] private Block blockPrefab;
    [SerializeField] private Transform blockParent;

    private readonly Block[,] blocks = new Block[Rows, Columns];

    //블록의 위치를 유니티좌표로 변환
    public Vector2 GetCellPosition(int row, int column)
    {
        return new Vector2(
            column - 0.5f,
            Rows - row + 0.5f
        );
    }

    //생성확률결정
    private int RollBlockCount()
    {
        int roll = Random.Range(0, 100);

        if (roll < 15) return 2;//2개 15%
        if (roll < 35) return 3;//3개 20%
        if (roll < 65) return 4;//4개 30%
        if (roll < 95) return 5;//5개 30%

        return 6;//6개 5%
    }

    //턴 시작시 2행에 블록을 생성
    public void SpawnNewRow(int turn)
    {
        if (turn < 1)
        {
            Debug.LogWarning("턴은 1 이상이어야 합니다.", this);
            return;
        }

        // 기존 행이 내려간 뒤에만 새 행을 생성한다.
        for (int column = 0; column < Columns; column++)
        {
            if (blocks[1, column] != null)
            {
                Debug.LogWarning("2행이 비어 있지 않습니다.", this);
                return;
            }
        }

        int count = RollBlockCount();
        int health = 1 + 2 * (turn - 1);

        int[] columns = { 1, 2, 3, 4, 5, 6 };

        // 열 순서를 무작위로 섞는다.
        for (int i = 0; i < columns.Length - 1; i++)
        {
            int randomIndex = Random.Range(i, columns.Length);

            int temp = columns[i];
            columns[i] = columns[randomIndex];
            columns[randomIndex] = temp;
        }

        // 섞인 열 중 앞에서 count개만 사용한다.
        for (int i = 0; i < count; i++)
        {
            bool isHard = Random.Range(0, 100) < 20;
            bool isGold = Random.Range(0, 100) < 10;
            CreateBlock(2, columns[i], health, isHard, isGold);
        }
    }

    //블록 생성 함수
    public void CreateBlock(int row, int column, int health, bool isHard = false, bool isGold = false)
    {
        // 생성 가능한 플레이 영역은 2~7행, 1~6열이다.
        if (row < 2 || row > 7 ||
            column < 1 || column > Columns)
        {
            Debug.LogWarning("벽돌을 생성할 수 없는 칸입니다.", this);
            return;
        }

        if (blockPrefab == null)
        {
            Debug.LogWarning("Block Prefab을 연결해주세요.", this);
            return;
        }

        int rowIndex = row - 1;
        int columnIndex = column - 1;

        if (blocks[rowIndex, columnIndex] != null)
            return;

        Vector2 position = GetCellPosition(row, column);

        Block block = Instantiate(
            blockPrefab,
            new Vector3(position.x, position.y, 0f),
            Quaternion.identity,
            blockParent
        );

        block.Initialize(health, isHard, isGold);

        blocks[rowIndex, columnIndex] = block;
    }

    //2~6개중 선택해 블록을 생성하는 테스트
    [ContextMenu("Test Spawn Turn 1")]
    private void TestSpawnTurn1()
    {
        if (!Application.isPlaying)
            return;

        SpawnNewRow(1);
    }

    //단단한,골드 속성 블록 테스트
    [ContextMenu("Test Block Types")]
    private void TestBlockTypes()
    {
        if (!Application.isPlaying)
            return;

        CreateBlock(2, 1, 5, false, false);
        CreateBlock(2, 2, 5, true, false);
        CreateBlock(2, 3, 5, false, true);
        CreateBlock(2, 4, 5, true, true);

        for (int column = 0; column < 4; column++)
        {
            Block block = blocks[1, column];

            if (block == null)
                continue;

            Debug.Log(
                $"열 {column + 1}: " +
                $"체력 {block.MaxHealth}, " +
                $"단단함 {block.IsHard}, 골드 {block.IsGold}",
                block
            );
        }
    }
}
