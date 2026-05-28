using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미로 내 플레이어 캐릭터를 제어합니다.
///
/// [씬 설정 방법]
/// 1. Canvas > GridContainer 와 같은 레벨(혹은 그 위)에 빈 GameObject를 만들고
///    이 스크립트를 부착합니다.
/// 2. Inspector에서 mazeRenderer, gridContainer를 연결합니다.
/// 3. playerVisual은 비워두면 원형 이미지를 자동 생성합니다.
/// 4. InitPlayer(maze) 를 호출하면 시작 위치에 플레이어가 배치됩니다.
///
/// [의존 관계]
///   MazeRenderer  — 셀 위치 계산 및 UpdateCell 호출
///   Maze (struct) — matrix, theme skill count 참조
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결
    // ───────────────────────────────────────────────────────────

    [Header("── 연결 오브젝트 ──────────────────────")]

    [Tooltip("MazeRenderer 컴포넌트 참조.\n셀 월드 위치 계산과 UpdateCell 호출에 사용됩니다.")]
    [SerializeField] private MazeRenderer mazeRenderer;

    [Tooltip("GridLayoutGroup이 부착된 컨테이너 RectTransform.\n셀 위치 계산의 기준점입니다.")]
    [SerializeField] private RectTransform gridContainer;

    [Tooltip("플레이어 비주얼로 사용할 Image 오브젝트.\n" +
             "비워두면 원형 Image를 자동 생성합니다.")]
    [SerializeField] private Image playerVisual;

    [Header("── 플레이어 비주얼 ─────────────────────")]

    [Tooltip("플레이어 색상입니다.")]
    [SerializeField] private Color playerColor = new Color(1f, 0.85f, 0.1f); // 노란색

    [Tooltip("플레이어 크기 비율 (0~1).\n" +
             "1이면 셀과 같은 크기, 0.7이면 셀의 70% 크기입니다.")]
    [SerializeField, Range(0.1f, 1f)] private float playerSizeRatio = 0.75f;

    [Header("── 이동 설정 ────────────────────────")]

    [Tooltip("한 칸 이동에 걸리는 시간 (초).\n" +
             "낮을수록 빠르게 이동합니다.")]
    [SerializeField] private float moveDuration = 0.12f;

    [Tooltip("점프대로 인한 슬라이드 이동 한 칸당 시간 (초).\n" +
             "일반 이동보다 약간 빠르게 설정하면 자연스럽습니다.")]
    [SerializeField] private float jumpSlideDuration = 0.08f;

    [Tooltip("포탈 이동 연출 시간 (초).")]
    [SerializeField] private float portalDuration = 0.15f;

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    // 현재 미로 데이터
    private char[,] matrix;
    private int     mazeN;

    // 플레이어 논리 위치 (행, 열)
    private int playerRow;
    private int playerCol;

    // 이동 횟수
    private int moveCount;

    // 이동 중 입력 잠금
    private bool isMoving;

    // 레이저 상태 추적: 이동 횟수(홀짝)로 판단하므로 별도 저장 불필요
    // — moveCount % 2 == 0 → 활성(벽), == 1 → 비활성(통과 가능)

    // 포탈 위치 맵 (문자 → 두 위치)
    private Dictionary<char, Vector2Int[]> portalMap;

    // 이벤트: 이동 횟수 변경 시 (UI 연동용)
    public System.Action<int> OnMoveCountChanged;
    // 이벤트: 클리어 시
    public System.Action OnGoalReached;

    // ───────────────────────────────────────────────────────────
    // Unity 생명주기
    // ───────────────────────────────────────────────────────────

    private void Awake()
    {
        // Inspector 연결 누락 조기 감지
        if (mazeRenderer == null)
            Debug.LogError("[PlayerController] mazeRenderer가 연결되지 않았습니다. Inspector를 확인하세요.");
        if (gridContainer == null)
            Debug.LogError("[PlayerController] gridContainer가 연결되지 않았습니다. Inspector를 확인하세요.");
    }

    // ───────────────────────────────────────────────────────────
    // 공개 API
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 미로 데이터를 받아 플레이어를 초기화하고 시작 위치에 배치합니다.
    /// </summary>
    public void InitPlayer(Maze maze)
    {
        InitPlayer(maze.matrix);
    }

    /// <summary>
    /// char[,] 배열을 직접 받아 플레이어를 초기화합니다.
    /// </summary>
    public void InitPlayer(char[,] m)
    {
        matrix    = m;
        mazeN     = m.GetLength(0);
        moveCount = 0;
        isMoving  = false;

        // 포탈 맵 구성
        BuildPortalMap();

        // 시작 위치('s') 탐색
        bool found = false;
        for (int r = 0; r < mazeN && !found; r++)
        {
            for (int c = 0; c < mazeN && !found; c++)
            {
                if (matrix[r, c] == 's')
                {
                    playerRow = r;
                    playerCol = c;
                    found = true;
                }
            }
        }

        if (!found)
        {
            Debug.LogWarning("[PlayerController] 시작 위치 's'를 찾을 수 없습니다.");
            playerRow = 1;
            playerCol = 1;
        }

        // 비주얼 준비
        EnsurePlayerVisual();
        SnapToCell(playerRow, playerCol);
        playerVisual.gameObject.SetActive(true);
    }

    /// <summary>
    /// 방향 입력을 받아 이동을 시도합니다.
    /// dir: 0=위(-row), 1=아래(+row), 2=왼쪽(-col), 3=오른쪽(+col)
    /// </summary>
    public void TryMove(int dir)
    {
        if (isMoving || matrix == null) return;

        int[] dRow = { -1, 1,  0, 0 };
        int[] dCol = {  0, 0, -1, 1 };

        int targetRow = playerRow + dRow[dir];
        int targetCol = playerCol + dCol[dir];

        if (!InBounds(targetRow, targetCol))
        {
            Debug.Log($"[PlayerController] 이동 불가: 범위 밖 ({targetRow}, {targetCol})");
            return;
        }

        char targetCell = matrix[targetRow, targetCol];

        // 이동 가능 여부 판단
        if (!CanEnter(targetRow, targetCol))
        {
            Debug.Log($"[PlayerController] 이동 불가: ({targetRow}, {targetCol}) = '{targetCell}'");
            return;
        }

        Debug.Log($"[PlayerController] 이동: ({playerRow},{playerCol}) → ({targetRow},{targetCol}) = '{targetCell}'");
        // 이동 시작
        StartCoroutine(MoveSequence(targetRow, targetCol, dir));
    }

    /// <summary>
    /// 현재 이동 횟수를 반환합니다.
    /// </summary>
    public int GetMoveCount() => moveCount;

    // ───────────────────────────────────────────────────────────
    // 이동 가능 여부 판단
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 해당 칸에 진입 가능한지 판단합니다.
    /// 레이저는 현재 moveCount(이동 전) 기준으로 판단합니다.
    /// </summary>
    private bool CanEnter(int row, int col)
    {
        if (!InBounds(row, col)) return false;
        char c = matrix[row, col];

        switch (c)
        {
            case '1': return false; // 벽

            case 'r':
                // 레이저: moveCount가 짝수 → 현재 활성(벽), 홀수 → 비활성(통과 가능)
                // 이동 후 moveCount가 증가하므로, "이동 직전" 기준인 현재 moveCount로 판단
                // 초기(moveCount=0, 짝수) → 활성 → 진입 불가
                // moveCount=1(홀수) → 비활성 → 진입 가능
                return (moveCount % 2 == 1);

            case 'e':
                // 적: 이동은 가능 (제거 처리는 MoveSequence에서)
                return true;

            default:
                return true; // '0','s','d','j','2'~'9' 모두 진입 가능
        }
    }

    // ───────────────────────────────────────────────────────────
    // 이동 코루틴 — 메인 시퀀스
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 한 번의 이동 입력에 대한 전체 이동 시퀀스를 처리합니다.
    /// 점프대 연쇄, 포탈 이동 등 모든 파생 이동을 순서대로 실행합니다.
    /// </summary>
    private IEnumerator MoveSequence(int targetRow, int targetCol, int dir)
    {
        isMoving = true;

        // ── 1단계: 기본 이동 (한 칸 슬라이드) ──
        yield return StartCoroutine(SlideTo(targetRow, targetCol, moveDistance: 1, moveDir: dir, duration: moveDuration));

        // 이전 위치 셀 복원 (시작점이면 's', 아니면 '0')
        RestoreCell(playerRow, playerCol);

        playerRow = targetRow;
        playerCol = targetCol;

        // 이동 횟수 증가 및 착지 처리
        char landedCell = matrix[playerRow, playerCol];
        bool countThisMove = true;

        // 적('e') 제거: 이동 횟수 소모 O (착지 = 제거)
        if (landedCell == 'e')
        {
            matrix[playerRow, playerCol] = '0';
            mazeRenderer.UpdateCell(playerRow, playerCol, '0');
        }

        if (countThisMove)
        {
            moveCount++;
            OnMoveCountChanged?.Invoke(moveCount);
        }

        // ── 2단계: 착지 후 특수 효과 처리 ──

        // 점프대('j') 처리
        if (landedCell == 'j')
        {
            yield return StartCoroutine(ProcessJumpPad(dir));
        }
        // 포탈('2'~'9') 처리
        else if (landedCell >= '2' && landedCell <= '9')
        {
            yield return StartCoroutine(ProcessPortal(landedCell));
        }

        // ── 3단계: 레이저 상태 갱신 (이동 횟수 변경에 따른 시각적 갱신) ──
        RefreshLaserVisuals();

        // ── 4단계: 도착점 체크 ──
        if (matrix[playerRow, playerCol] == 'd')
        {
            isMoving = false;
            OnGoalReached?.Invoke();
            yield break;
        }

        isMoving = false;
    }

    // ───────────────────────────────────────────────────────────
    // 특수 이동: 점프대
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 점프대 착지 후 슬라이드 이동을 처리합니다.
    /// 기획 규칙: 진입 방향으로 2칸 이동, 막히면 1칸, 그것도 막히면 제자리.
    /// 연속 점프대는 연쇄적으로 처리합니다.
    /// </summary>
    private IEnumerator ProcessJumpPad(int dir)
    {
        int[] dRow = { -1, 1,  0, 0 };
        int[] dCol = {  0, 0, -1, 1 };

        int depth = 0;
        int maxDepth = mazeN * mazeN; // 순환 방지

        while (depth < maxDepth)
        {
            int curRow = playerRow;
            int curCol = playerCol;

            // 2칸 앞 위치
            int r2 = curRow + dRow[dir] * 2;
            int c2 = curCol + dCol[dir] * 2;
            // 1칸 앞 위치
            int r1 = curRow + dRow[dir];
            int c1 = curCol + dCol[dir];

            int landRow, landCol;
            int slideSteps;

            if (InBounds(r2, c2) && IsPassableForJump(r2, c2))
            {
                landRow    = r2;
                landCol    = c2;
                slideSteps = 2;
            }
            else if (InBounds(r1, c1) && IsPassableForJump(r1, c1))
            {
                landRow    = r1;
                landCol    = c1;
                slideSteps = 1;
            }
            else
            {
                // 제자리 — 점프 없이 종료
                break;
            }

            // 점프 슬라이드 애니메이션 (중간 칸을 거쳐 이동)
            if (slideSteps == 2)
            {
                // 중간 칸(1칸)을 거쳐 목적지(2칸)로
                yield return StartCoroutine(SlideStepByStep(curRow, curCol, landRow, landCol, dir, jumpSlideDuration));
            }
            else
            {
                yield return StartCoroutine(SlideTo(landRow, landCol, slideSteps, dir, jumpSlideDuration));
            }

            RestoreCell(playerRow, playerCol);
            playerRow = landRow;
            playerCol = landCol;

            // 착지 위치가 또 점프대이면 연쇄
            if (matrix[playerRow, playerCol] == 'j')
            {
                depth++;
                continue;
            }

            // 착지 위치가 포탈이면 포탈 처리 후 종료
            char landedCell = matrix[playerRow, playerCol];
            if (landedCell >= '2' && landedCell <= '9')
            {
                yield return StartCoroutine(ProcessPortal(landedCell));
            }

            // 착지 위치가 적이면 제거
            if (landedCell == 'e')
            {
                matrix[playerRow, playerCol] = '0';
                mazeRenderer.UpdateCell(playerRow, playerCol, '0');
            }

            break;
        }
    }

    // ───────────────────────────────────────────────────────────
    // 특수 이동: 포탈
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 포탈 진입 시 반대쪽 포탈로 이동합니다.
    /// 연출: 페이드 아웃 → 순간이동 → 페이드 인
    /// </summary>
    private IEnumerator ProcessPortal(char portalChar)
    {
        if (!portalMap.ContainsKey(portalChar)) yield break;

        Vector2Int[] pair = portalMap[portalChar];
        Vector2Int other;

        if (pair[0].x == playerRow && pair[0].y == playerCol)
            other = pair[1];
        else
            other = pair[0];

        // 페이드 아웃
        yield return StartCoroutine(FadePlayer(1f, 0f, portalDuration * 0.5f));

        // 순간이동 (비주얼 위치만 즉시 변경)
        RestoreCell(playerRow, playerCol);
        playerRow = other.x;
        playerCol = other.y;
        SnapToCell(playerRow, playerCol);

        // 페이드 인
        yield return StartCoroutine(FadePlayer(0f, 1f, portalDuration * 0.5f));

        // 포탈 착지 후 또 다른 특수 칸이면 재처리
        char landedCell = matrix[playerRow, playerCol];
        if (landedCell == 'j')
        {
            // 포탈 착지 방향은 알 수 없으므로 점프 없이 제자리 유지
            // (기획 상 포탈 착지 후 방향이 정의되지 않은 케이스)
        }
    }

    // ───────────────────────────────────────────────────────────
    // 애니메이션 헬퍼
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 위치에서 (toRow, toCol)까지 duration 시간 동안 부드럽게 이동합니다.
    /// </summary>
    private IEnumerator SlideTo(int toRow, int toCol, int moveDistance, int moveDir, float duration)
    {
        RectTransform rt = playerVisual.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos   = GetCellAnchoredPosition(toRow, toCol);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseInOut 보간으로 자연스러운 가속/감속
            t = t * t * (3f - 2f * t);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        rt.anchoredPosition = endPos;
    }

    /// <summary>
    /// 2칸 슬라이드를 중간 칸을 거쳐 step-by-step으로 이동합니다.
    /// 시각적으로 2칸을 지나가는 모습이 보입니다.
    /// </summary>
    private IEnumerator SlideStepByStep(int fromRow, int fromCol,
                                         int toRow,   int toCol,
                                         int dir,     float stepDuration)
    {
        int[] dRow = { -1, 1,  0, 0 };
        int[] dCol = {  0, 0, -1, 1 };

        int midRow = fromRow + dRow[dir];
        int midCol = fromCol + dCol[dir];

        // 1칸
        yield return StartCoroutine(SlideTo(midRow, midCol, 1, dir, stepDuration));
        // 2칸
        yield return StartCoroutine(SlideTo(toRow, toCol, 1, dir, stepDuration));
    }

    /// <summary>
    /// 플레이어 Image의 알파값을 from → to로 부드럽게 전환합니다.
    /// </summary>
    private IEnumerator FadePlayer(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = playerVisual.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(from, to, t);
            playerVisual.color = c;
            yield return null;
        }

        c.a = to;
        playerVisual.color = c;
    }

    /// <summary>
    /// 플레이어를 애니메이션 없이 즉시 해당 셀로 이동합니다.
    /// </summary>
    private void SnapToCell(int row, int col)
    {
        if (playerVisual == null) return;
        playerVisual.rectTransform.anchoredPosition = GetCellAnchoredPosition(row, col);
    }

    // ───────────────────────────────────────────────────────────
    // 셀 위치 계산
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 셀 (row, col)의 anchoredPosition을 계산합니다.
    /// GridLayoutGroup의 cellSize와 spacing을 참조합니다.
    /// </summary>
    private Vector2 GetCellAnchoredPosition(int row, int col)
    {
        GridLayoutGroup glg = gridContainer.GetComponent<GridLayoutGroup>();
        if (glg == null)
        {
            Debug.LogWarning("[PlayerController] gridContainer에 GridLayoutGroup이 없습니다.");
            return Vector2.zero;
        }

        float cellW   = glg.cellSize.x;
        float cellH   = glg.cellSize.y;
        float spacingX = glg.spacing.x;
        float spacingY = glg.spacing.y;

        // GridLayoutGroup은 UpperLeft 기준, anchoredPosition은 중앙 pivot 기준
        // gridContainer의 pivot이 (0,1) (좌상단)이라 가정
        float x = col * (cellW + spacingX) + cellW * 0.5f;
        float y = -(row * (cellH + spacingY) + cellH * 0.5f);

        return new Vector2(x, y);
    }

    // ───────────────────────────────────────────────────────────
    // 셀 복원 (플레이어가 떠난 자리)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 떠난 셀을 원래 색상으로 복원합니다.
    /// 시작점('s')은 그대로 유지하고, 나머지는 matrix 값 기준으로 복원합니다.
    /// </summary>
    private void RestoreCell(int row, int col)
    {
        mazeRenderer.UpdateCell(row, col, matrix[row, col]);
    }

    // ───────────────────────────────────────────────────────────
    // 레이저 시각 갱신
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 이동 횟수(moveCount)에 따라 레이저 칸의 색상을 갱신합니다.
    /// moveCount 홀수: 비활성(밝게), 짝수: 활성(기본 레이저 색)
    /// MazeRenderer에 레이저 비활성 색상 접근자가 없으므로
    /// UpdateCell을 'r' 문자로 재호출 — 색상 변화는 MazeRenderer 확장 시 처리 가능.
    /// 현재는 활성/비활성을 PlayerController에서 직접 색상 오버라이드합니다.
    /// </summary>
    private void RefreshLaserVisuals()
    {
        if (matrix == null) return;
        bool laserActive = (moveCount % 2 == 0); // 짝수 이동 후 → 활성

        for (int r = 0; r < mazeN; r++)
        {
            for (int c = 0; c < mazeN; c++)
            {
                if (matrix[r, c] == 'r')
                {
                    // MazeRenderer.UpdateCell은 'r'에 대해 colorLaser를 적용함.
                    // 비활성 상태 표현을 위해 PlayerController에서 Image를 직접 접근.
                    Image cellImg = GetCellImage(r, c);
                    if (cellImg != null)
                    {
                        // 활성: 불투명(원래 레이저 색), 비활성: 반투명으로 구분
                        Color col = cellImg.color;
                        col.a = laserActive ? 1.0f : 0.35f;
                        cellImg.color = col;
                    }
                }
            }
        }
    }

    /// <summary>
    /// gridContainer에서 (row, col) 인덱스의 Image 컴포넌트를 가져옵니다.
    /// Cell 오브젝트 이름 규칙 "Cell_row_col"을 활용합니다.
    /// </summary>
    private Image GetCellImage(int row, int col)
    {
        Transform t = gridContainer.Find($"Cell_{row}_{col}");
        return t != null ? t.GetComponent<Image>() : null;
    }

    // ───────────────────────────────────────────────────────────
    // 포탈 맵 구성
    // ───────────────────────────────────────────────────────────

    private void BuildPortalMap()
    {
        portalMap = new Dictionary<char, Vector2Int[]>();
        if (matrix == null) return;

        var temp = new Dictionary<char, List<Vector2Int>>();
        for (int r = 0; r < mazeN; r++)
        {
            for (int c = 0; c < mazeN; c++)
            {
                char ch = matrix[r, c];
                if (ch >= '2' && ch <= '9')
                {
                    if (!temp.ContainsKey(ch)) temp[ch] = new List<Vector2Int>();
                    temp[ch].Add(new Vector2Int(r, c));
                }
            }
        }

        foreach (var kv in temp)
        {
            if (kv.Value.Count == 2)
                portalMap[kv.Key] = new Vector2Int[] { kv.Value[0], kv.Value[1] };
        }
    }

    // ───────────────────────────────────────────────────────────
    // 점프대 통과 가능 판정
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 점프대 착지 가능 여부: 벽('1')과 적('e')은 착지 불가.
    /// </summary>
    private bool IsPassableForJump(int row, int col)
    {
        if (!InBounds(row, col)) return false;
        char c = matrix[row, col];
        return c != '1' && c != 'e';
    }

    // ───────────────────────────────────────────────────────────
    // 플레이어 비주얼 생성
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// playerVisual이 없으면 원형 Image를 자동 생성합니다.
    /// gridContainer의 자식으로 생성되어 미로 위에 오버레이됩니다.
    ///
    /// [수정 내용]
    /// - GameObject를 직접 new 하면 씬 루트에 일반 Transform으로 생성되어
    ///   Canvas 계층에 편입되지 않는 문제가 있었습니다.
    /// - typeof(RectTransform)을 생성자에 전달해 처음부터 RectTransform을
    ///   포함한 채 생성하고, 즉시 gridContainer의 자식으로 배치합니다.
    /// - 앵커를 좌상단 (0,1)으로 고정해 GetCellAnchoredPosition의
    ///   계산 기준과 일치시켰습니다.
    /// </summary>
    private void EnsurePlayerVisual()
    {
        if (playerVisual != null)
        {
            ApplyPlayerSize();
            return;
        }

        // ── 핵심 수정 1: typeof(RectTransform) 포함해 생성 후 즉시 SetParent ──
        // new GameObject() 후 SetParent 하면 씬 루트에 일반 Transform으로 먼저
        // 생성되어 Canvas 계층에 편입되지 않는 문제가 있습니다.
        // RectTransform을 생성자에 포함하고 worldPositionStays=false로
        // SetParent 해야 로컬 좌표가 올바르게 초기화됩니다.
        GameObject go = new GameObject("Player", typeof(RectTransform));
        go.transform.SetParent(gridContainer, false); // worldPositionStays=false

        // 미로 셀들보다 위에 렌더링되도록 sibling index를 마지막으로
        go.transform.SetAsLastSibling();

        // Image 추가 및 색상/스프라이트 설정
        playerVisual               = go.AddComponent<Image>();
        playerVisual.color         = playerColor;
        playerVisual.sprite        = CreateCircleSprite(64);
        playerVisual.raycastTarget = false; // 셀 클릭 이벤트 투과

        // ── 핵심 수정 2: 앵커를 좌상단(0,1)으로 고정 ──
        // GetCellAnchoredPosition은 gridContainer pivot=(0,1) 기준으로
        // x는 오른쪽(+), y는 아래쪽(-)으로 계산합니다.
        // anchorMin/Max를 (0,1)로 맞춰야 anchoredPosition 기준점이 일치합니다.
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); // 좌상단
        rt.anchorMax = new Vector2(0f, 1f); // 좌상단
        rt.pivot     = new Vector2(0.5f, 0.5f); // 오브젝트 자체 중심 기준

        ApplyPlayerSize();
    }

    /// <summary>
    /// 셀 크기에 playerSizeRatio를 적용해 플레이어 크기를 설정합니다.
    /// </summary>
    private void ApplyPlayerSize()
    {
        GridLayoutGroup glg = gridContainer.GetComponent<GridLayoutGroup>();
        if (glg == null || playerVisual == null) return;

        float size = glg.cellSize.x * playerSizeRatio;
        playerVisual.rectTransform.sizeDelta = new Vector2(size, size);
    }

    /// <summary>
    /// 지정 해상도의 원형 Sprite를 런타임에 생성합니다. (에셋 불필요)
    /// </summary>
    private Sprite CreateCircleSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = resolution * 0.5f;
        float radius = center - 1f;

        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 안티앨리어싱: 경계 1픽셀을 부드럽게
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex,
                             new Rect(0, 0, resolution, resolution),
                             new Vector2(0.5f, 0.5f),
                             resolution);
    }

    // ───────────────────────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────────────────────

    private bool InBounds(int row, int col)
        => row >= 0 && row < mazeN && col >= 0 && col < mazeN;

    // ───────────────────────────────────────────────────────────
    // Unity 입력 처리 (키보드 WASD / 방향키)
    // ───────────────────────────────────────────────────────────

    private void Update()
    {
        // matrix == null: InitPlayer()가 아직 호출되지 않은 상태
        // 이 경우 입력을 받아도 동작하지 않습니다.
        // InitPlayer()를 Start() 또는 미로 생성 직후에 호출했는지 확인하세요.
        if (matrix == null)
        {
            // 키를 눌렀을 때만 경고 출력 (매 프레임 출력 방지)
            if (Input.anyKeyDown)
                Debug.LogWarning("[PlayerController] matrix가 null입니다. InitPlayer()를 먼저 호출하세요.");
            return;
        }

        if (isMoving) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            Debug.Log("[PlayerController] 위 이동 입력");
            TryMove(0);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            Debug.Log("[PlayerController] 아래 이동 입력");
            TryMove(1);
        }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Debug.Log("[PlayerController] 왼쪽 이동 입력");
            TryMove(2);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            Debug.Log("[PlayerController] 오른쪽 이동 입력");
            TryMove(3);
        }
    }
}