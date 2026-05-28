using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MazeGenerator가 생성한 Maze 데이터를 GridLayoutGroup 기반 UI로 렌더링합니다.
///
/// [씬 설정 방법]
/// 1. Canvas 하위에 빈 GameObject를 만들고 이 스크립트를 부착합니다.
/// 2. Inspector에서 gridContainer에 GridLayoutGroup이 있는 RectTransform을 연결합니다.
/// 3. Inspector에서 cellPrefab에 Image 컴포넌트가 있는 프리팹을 연결합니다.
///    (기본 프리팹이 없으면 스크립트가 자동으로 생성합니다.)
/// 4. 각 요소의 색상을 Inspector에서 자유롭게 설정합니다.
/// 5. RenderMaze(maze) 또는 RenderMaze(matrix)를 호출하면 미로가 그려집니다.
/// </summary>
public class MazeRenderer : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결 오브젝트
    // ───────────────────────────────────────────────────────────

    [Header("── UI 연결 ──────────────────────────")]

    [Tooltip("GridLayoutGroup 컴포넌트가 부착된 컨테이너 RectTransform.\n" +
             "이 오브젝트 아래에 셀들이 자동 생성됩니다.")]
    [SerializeField] private RectTransform gridContainer;

    [Tooltip("각 셀로 사용할 프리팹 (Image 컴포넌트 필수).\n" +
             "비워두면 스크립트가 Image만 가진 기본 프리팹을 자동 생성합니다.")]
    [SerializeField] private GameObject cellPrefab;

    [Tooltip("미로 전체가 표시될 영역의 크기 (픽셀).\n" +
             "n에 따라 셀 크기가 자동 계산됩니다.\n" +
             "예: 600이면 600×600 안에 미로가 꽉 채워집니다.")]
    [SerializeField] private float boardSize = 600f;

    // ───────────────────────────────────────────────────────────
    // 요소별 색상 설정 (Inspector에서 수정 가능)
    // ───────────────────────────────────────────────────────────

    [Header("── 기본 요소 색상 ─────────────────────")]

    [Tooltip("길 (0): 플레이어가 이동할 수 있는 빈 칸입니다.")]
    [SerializeField] private Color colorRoad = new Color(0.95f, 0.93f, 0.88f);   // 밝은 베이지

    [Tooltip("벽 (1): 이동을 막는 칸입니다.")]
    [SerializeField] private Color colorWall = new Color(0.20f, 0.20f, 0.22f);   // 짙은 회색

    [Tooltip("시작 위치 (s): 플레이어가 출발하는 칸입니다.")]
    [SerializeField] private Color colorStart = new Color(0.20f, 0.75f, 0.30f);  // 초록

    [Tooltip("도착 위치 (d): 클리어 조건이 되는 목적지 칸입니다.")]
    [SerializeField] private Color colorDest = new Color(0.90f, 0.25f, 0.25f);   // 빨강

    [Header("── 판타지 테마 색상 ────────────────────")]

    [Tooltip("적 (e): 검으로 제거할 수 있는 적입니다.\n" +
             "벽과 같이 이동을 막지만, 이동 횟수 1을 소모해 제거할 수 있습니다.")]
    [SerializeField] private Color colorEnemy = new Color(0.85f, 0.15f, 0.55f);  // 자홍

    [Tooltip("점프대 (j): 진입 방향으로 2칸 앞으로 이동시킵니다.\n" +
             "2칸 앞이 막히면 1칸, 둘 다 막히면 제자리에 멈춥니다.\n" +
             "연속된 점프대는 연쇄 점프가 발생합니다.")]
    [SerializeField] private Color colorJumpPad = new Color(0.20f, 0.60f, 1.00f); // 하늘

    [Header("── 미래 테마 색상 ─────────────────────")]

    [Tooltip("레이저 (r): 활성/비활성 상태가 매 이동마다 전환됩니다.\n" +
             "초기 상태는 '활성'(벽 역할)이며,\n" +
             "비활성 상태일 때는 길처럼 통과할 수 있습니다.")]
    [SerializeField] private Color colorLaser = new Color(1.00f, 0.55f, 0.00f);  // 주황

    [Tooltip("포탈 쌍 색상 목록 (문자 '2'~'9', 최대 8쌍).\n" +
             "인덱스 0 = 문자 '2', 인덱스 7 = 문자 '9'에 해당합니다.\n" +
             "같은 색 포탈에 진입하면 반대쪽 포탈로 순간이동합니다.")]
    [SerializeField] private Color[] colorPortals = new Color[]
    {
        new Color(0.40f, 0.00f, 0.80f), // 보라
        new Color(0.00f, 0.80f, 0.80f), // 청록
        new Color(1.00f, 0.85f, 0.00f), // 노랑
        new Color(0.00f, 0.50f, 1.00f), // 파랑
        new Color(1.00f, 0.40f, 0.70f), // 분홍
        new Color(0.60f, 0.90f, 0.20f), // 연두
        new Color(0.60f, 0.35f, 0.10f), // 갈색
        new Color(0.70f, 0.70f, 0.70f), // 은색
    };

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    // 생성된 셀 Image 컴포넌트를 [행, 열] 순서로 캐싱
    private Image[,] cellImages;

    // 현재 렌더링된 미로 크기
    private int currentN = 0;

    // ───────────────────────────────────────────────────────────
    // 공개 API
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Maze 구조체를 받아 미로를 렌더링합니다.
    /// </summary>
    public void RenderMaze(Maze maze)
    {
        RenderMaze(maze.matrix);
    }

    /// <summary>
    /// char[,] 배열을 직접 받아 미로를 렌더링합니다.
    /// matrix[행, 열] 순서로 읽어 위→아래, 왼→오른 방향으로 그립니다.
    /// </summary>
    public void RenderMaze(char[,] matrix)
    {
        if (matrix == null)
        {
            Debug.LogWarning("[MazeRenderer] matrix가 null입니다.");
            return;
        }

        int n = matrix.GetLength(0);
        if (n != matrix.GetLength(1))
        {
            Debug.LogWarning("[MazeRenderer] matrix가 정사각형(n×n)이 아닙니다.");
            return;
        }

        // 크기가 바뀌었거나 셀이 아직 없으면 셀을 새로 생성
        if (currentN != n || cellImages == null)
        {
            BuildGrid(n);
        }

        // 색상 적용
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                cellImages[row, col].color = GetCellColor(matrix[row, col]);
            }
        }
    }

    /// <summary>
    /// 특정 셀 하나의 색상만 갱신합니다. (인게임 중 상태 변화에 활용)
    /// </summary>
    /// <param name="row">행 인덱스</param>
    /// <param name="col">열 인덱스</param>
    /// <param name="cellChar">갱신할 셀 문자</param>
    public void UpdateCell(int row, int col, char cellChar)
    {
        if (cellImages == null || row < 0 || row >= currentN || col < 0 || col >= currentN)
        {
            Debug.LogWarning($"[MazeRenderer] UpdateCell 범위 초과 또는 초기화 전: ({row}, {col})");
            return;
        }
        cellImages[row, col].color = GetCellColor(cellChar);
    }

    /// <summary>
    /// 현재 렌더링된 셀 오브젝트를 모두 삭제합니다.
    /// </summary>
    public void ClearGrid()
    {
        if (gridContainer == null) return;
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        cellImages = null;
        currentN = 0;
    }

    // ───────────────────────────────────────────────────────────
    // 내부: 그리드 생성
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// n×n 셀을 생성하고 GridLayoutGroup 설정을 적용합니다.
    /// </summary>
    private void BuildGrid(int n)
    {
        ClearGrid();
        currentN = n;

        // ── GridLayoutGroup 자동 설정 ──
        GridLayoutGroup glg = gridContainer.GetComponent<GridLayoutGroup>();
        if (glg == null)
            glg = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

        float spacing = 1f;
        float cellSize = Mathf.Floor((boardSize - spacing * (n - 1)) / n);
        cellSize = Mathf.Max(cellSize, 2f); // 최소 2픽셀 보장

        glg.cellSize       = new Vector2(cellSize, cellSize);
        glg.spacing        = new Vector2(spacing, spacing);
        glg.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = n;
        glg.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis      = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperLeft;

        // ── 컨테이너 크기 맞춤 ──
        float totalSize = cellSize * n + spacing * (n - 1);
        gridContainer.sizeDelta = new Vector2(totalSize, totalSize);

        // ── 셀 생성 ──
        // cellPrefab이 지정된 경우 Instantiate, 없으면 직접 생성
        // (임시 GameObject를 씬에 올렸다 삭제하는 방식을 제거)
        GameObject prefab = GetOrCreateCellPrefab();
        cellImages = new Image[n, n];

        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                GameObject cell;
                if (prefab != null)
                {
                    // Inspector에 프리팹이 지정된 경우
                    cell = Instantiate(prefab, gridContainer);
                }
                else
                {
                    // 프리팹 없음: gridContainer 자식으로 직접 생성
                    // typeof(RectTransform)을 포함해 생성해야 Canvas 계층에 올바르게 편입됨
                    cell = new GameObject("Cell", typeof(RectTransform));
                    cell.transform.SetParent(gridContainer, false);
                }

                cell.name = $"Cell_{row}_{col}";

                Image img = cell.GetComponent<Image>();
                if (img == null)
                    img = cell.AddComponent<Image>();

                cellImages[row, col] = img;
            }
        }
    }

    /// <summary>
    /// cellPrefab이 지정되어 있으면 반환, 없으면 null을 반환합니다.
    /// 셀은 프리팹 없이 직접 생성하는 방식으로 변경했습니다.
    ///
    /// [수정 내용]
    /// 기존에 new GameObject("TempCellPrefab")으로 임시 오브젝트를 만든 뒤
    /// Instantiate의 원본으로 사용하고 나중에 Destroy했으나,
    /// new GameObject()는 씬 루트에 실제 오브젝트를 생성하고
    /// scene.rootCount 조건이 항상 false라 삭제되지 않는 문제가 있었습니다.
    /// 프리팹 없이 gridContainer 자식으로 셀을 직접 생성하는 방식으로 교체했습니다.
    /// </summary>
    private GameObject GetOrCreateCellPrefab()
    {
        return cellPrefab; // null이면 BuildGrid에서 직접 생성
    }

    // ───────────────────────────────────────────────────────────
    // 내부: 색상 매핑
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 셀 문자에 대응하는 색상을 반환합니다.
    /// </summary>
    private Color GetCellColor(char c)
    {
        switch (c)
        {
            case '0': return colorRoad;
            case '1': return colorWall;
            case 's': return colorStart;
            case 'd': return colorDest;
            case 'e': return colorEnemy;
            case 'j': return colorJumpPad;
            case 'r': return colorLaser;
            default:
                // 포탈: '2'~'9'
                if (c >= '2' && c <= '9')
                {
                    int idx = c - '2'; // 0~7
                    if (colorPortals != null && idx < colorPortals.Length)
                        return colorPortals[idx];
                    // colorPortals 배열이 부족하면 흰색 반환
                    return Color.white;
                }
                // 알 수 없는 문자: 마젠타로 표시 (디버그용)
                Debug.LogWarning($"[MazeRenderer] 알 수 없는 셀 문자: '{c}'");
                return Color.magenta;
        }
    }

    // ───────────────────────────────────────────────────────────
    // 유틸: Inspector 버튼 (에디터 전용)
    // ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 테스트용 미로를 즉시 렌더링합니다.
    /// MazeRendererEditor.cs 없이도 Inspector의 컨텍스트 메뉴로 호출 가능합니다.
    /// </summary>
    [ContextMenu("테스트 미로 렌더링 (5×5)")]
    private void DebugRenderTest5()
    {
        char[,] testMatrix = {
            { '1','1','1','1','1' },
            { '1','s','0','e','1' },
            { '1','1','1','j','1' },
            { '1','r','0','0','1' },
            { '1','1','1','d','1' },
        };
        RenderMaze(testMatrix);
    }

    [ContextMenu("테스트 미로 렌더링 (5×5, 포탈)")]
    private void DebugRenderTestPortal()
    {
        char[,] testMatrix = {
            { '1','1','1','1','1' },
            { '1','s','0','2','1' },
            { '1','1','1','1','1' },
            { '1','2','0','0','1' },
            { '1','1','1','d','1' },
        };
        RenderMaze(testMatrix);
    }

    [ContextMenu("그리드 초기화")]
    private void DebugClear()
    {
        ClearGrid();
    }
#endif
}