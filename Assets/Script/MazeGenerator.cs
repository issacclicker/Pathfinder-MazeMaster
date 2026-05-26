using System;
using System.Collections.Generic;
using UnityEngine;

public enum Difficulty
{
    Easy,
    Normal,
    Hard
}

public struct Maze
{
    public Difficulty difficulty;
    public char[,] matrix;
    public int minerSkillCount;   // 광부 테마 곡괭이 횟수 (1~5)
    public int outlawSkillCount;  // 무법자 테마 폭탄 횟수 (1~5)

    public Maze(Difficulty difficulty, char[,] matrix, int miner = 0, int outlaw = 0)
    {
        this.difficulty = difficulty;
        this.matrix = matrix;
        this.minerSkillCount = miner;
        this.outlawSkillCount = outlaw;
    }
}

public class MazeGenerator : MonoBehaviour
{
    // ───────────────────────────────────────────
    // 테마 인덱스 상수
    // ───────────────────────────────────────────
    private const int THEME_BASIC   = 0;
    private const int THEME_MINER   = 1;
    private const int THEME_OUTLAW  = 2;
    private const int THEME_FANTASY = 3;
    private const int THEME_FUTURE  = 4;

    // 생성 재시도 최대 횟수 (검증 실패 시)
    private const int MAX_RETRY = 10000;

    // 상하좌우 방향 벡터
    private static readonly int[] DX = { -1, 1,  0, 0 };
    private static readonly int[] DY = {  0, 0, -1, 1 };

    // ───────────────────────────────────────────
    // 공개 API
    // ───────────────────────────────────────────

    /// <summary>
    /// n×n 미로를 생성합니다. 항상 클리어 가능한 미로를 반환합니다.
    /// </summary>
    /// <param name="n">미로 크기 (5 ~ 30)</param>
    /// <param name="themeFlags">크기 5의 테마 활성화 배열</param>
    public Maze GenerateMaze(int n, bool[] themeFlags)
    {
        // ── 입력 보정 ──
        n = Mathf.Clamp(n, 5, 30);

        if (themeFlags == null || themeFlags.Length < 5)
            themeFlags = new bool[] { true, false, false, false, false };

        // 상호 배타 규칙 적용
        // - 기본 테마가 켜지면 나머지는 모두 끔
        // - 광부와 무법자는 동시에 켤 수 없음 (무법자를 끔)
        if (themeFlags[THEME_BASIC])
        {
            for (int i = 1; i < 5; i++) themeFlags[i] = false;
        }
        else if (themeFlags[THEME_MINER] && themeFlags[THEME_OUTLAW])
        {
            themeFlags[THEME_OUTLAW] = false;
        }

        // 아무 테마도 켜지지 않은 경우 기본 테마로 폴백
        bool anyTheme = false;
        foreach (bool f in themeFlags) if (f) { anyTheme = true; break; }
        if (!anyTheme) themeFlags[THEME_BASIC] = true;

        // ── 스킬 횟수 결정 (1~5 랜덤) ──
        int minerCount  = themeFlags[THEME_MINER]  ? UnityEngine.Random.Range(1, 6) : 0;
        int outlawCount = themeFlags[THEME_OUTLAW] ? UnityEngine.Random.Range(1, 6) : 0;

        // ── 시작/도착 위치 고정 ──
        Vector2Int startPos = new Vector2Int(1, 1);
        Vector2Int destPos  = new Vector2Int(n - 2, n - 2);

        // ── 생성 + 검증 루프 ──
        char[,] matrix = null;
        bool valid = false;
        int retry = 0;

        while (!valid && retry < MAX_RETRY)
        {
            matrix = BuildMatrix(n);
            GenerateBaseMazeRoutes(matrix, n, startPos);
            matrix[startPos.x, startPos.y] = 's';
            matrix[destPos.x,  destPos.y]  = 'd';
            ApplyThemeElements(matrix, n, themeFlags, startPos, destPos);

            valid = ValidateMaze(matrix, n, themeFlags, startPos, destPos, minerCount, outlawCount);
            retry++;
        }

        if (!valid)
        {
            // MAX_RETRY 초과 시 안전망: 기본 DFS 미로만 반환 (클리어는 항상 보장됨)
            Debug.LogWarning($"[MazeGenerator] {MAX_RETRY}회 재시도 후에도 검증 실패. 기본 미로로 폴백합니다.");
            matrix = BuildMatrix(n);
            GenerateBaseMazeRoutes(matrix, n, startPos);
            matrix[startPos.x, startPos.y] = 's';
            matrix[destPos.x,  destPos.y]  = 'd';
        }

        Difficulty difficulty = CalculateDifficulty(n, themeFlags);
        return new Maze(difficulty, matrix, minerCount, outlawCount);
    }

    // ───────────────────────────────────────────
    // 미로 초기화 & 기본 경로 생성
    // ───────────────────────────────────────────

    /// <summary>모든 칸을 벽('1')으로 초기화한 행렬을 반환합니다.</summary>
    private char[,] BuildMatrix(int n)
    {
        char[,] m = new char[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                m[i, j] = '1';
        return m;
    }

    /// <summary>
    /// DFS(벽 허물기) 방식으로 기본 경로를 생성합니다.
    /// 홀수 좌표 셀을 방문하며 2칸씩 이동, 사이 벽을 '0'으로 개방합니다.
    /// </summary>
    private void GenerateBaseMazeRoutes(char[,] matrix, int n, Vector2Int start)
    {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        bool[,] visited = new bool[n, n];

        // DFS는 홀수 인덱스 좌표만 셀로 취급 (짝수 인덱스는 벽/통로 역할)
        // start가 홀수가 아니라면 (1,1)로 보정
        Vector2Int dfsStart = new Vector2Int(
            start.x % 2 == 0 ? start.x + 1 : start.x,
            start.y % 2 == 0 ? start.y + 1 : start.y
        );
        dfsStart.x = Mathf.Clamp(dfsStart.x, 1, n - 2);
        dfsStart.y = Mathf.Clamp(dfsStart.y, 1, n - 2);

        stack.Push(dfsStart);
        visited[dfsStart.x, dfsStart.y] = true;
        matrix[dfsStart.x, dfsStart.y] = '0';

        // 2칸씩 이동하는 방향 벡터 (DFS 셀 간 이동)
        int[] dx2 = { -2, 2,  0, 0 };
        int[] dy2 = {  0, 0, -2, 2 };

        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            List<int> dirs = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                int nx = cur.x + dx2[i];
                int ny = cur.y + dy2[i];
                if (nx > 0 && nx < n - 1 && ny > 0 && ny < n - 1 && !visited[nx, ny])
                    dirs.Add(i);
            }

            if (dirs.Count > 0)
            {
                int d = dirs[UnityEngine.Random.Range(0, dirs.Count)];
                int nx  = cur.x + dx2[d];
                int ny  = cur.y + dy2[d];
                int mid_x = cur.x + dx2[d] / 2;
                int mid_y = cur.y + dy2[d] / 2;

                matrix[mid_x, mid_y] = '0'; // 사이 벽 개방
                matrix[nx, ny]       = '0';
                visited[nx, ny]      = true;
                visited[mid_x, mid_y] = true;
                stack.Push(new Vector2Int(nx, ny));
            }
            else
            {
                stack.Pop();
            }
        }
    }

    // ───────────────────────────────────────────
    // 테마 요소 배치
    // ───────────────────────────────────────────

    /// <summary>
    /// 활성화된 테마 플래그에 따라 오브젝트를 배치합니다.
    /// 각 테마 요소는 시작·도착 위치를 침범하지 않습니다.
    /// </summary>
    private void ApplyThemeElements(char[,] matrix, int n, bool[] themeFlags,
                                    Vector2Int start, Vector2Int dest)
    {
        List<Vector2Int> roads = new List<Vector2Int>(); // 현재 길('0') 위치
        List<Vector2Int> walls = new List<Vector2Int>(); // 현재 벽('1') 위치

        RefreshPositionLists(matrix, n, start, dest, roads, walls);
        ShuffleList(roads);
        ShuffleList(walls);

        // ── 미래 테마: 레이저 → 포탈 순으로 배치 ──
        if (themeFlags[THEME_FUTURE])
        {
            // 레이저: 내부 벽의 최대 25% (최대 10개), 초기 상태 '활성'
            int laserCount = Mathf.Min(walls.Count / 4, 10);
            for (int i = 0; i < laserCount && walls.Count > 0; i++)
            {
                Vector2Int pos = walls[walls.Count - 1];
                walls.RemoveAt(walls.Count - 1);
                matrix[pos.x, pos.y] = 'r';
            }

            // 포탈: 0~7쌍, 문자 '2'~'9'
            int pairCount = UnityEngine.Random.Range(0, 8);
            int portalChar = '2';
            for (int p = 0; p < pairCount && roads.Count >= 2; p++)
            {
                Vector2Int p1 = roads[roads.Count - 1]; roads.RemoveAt(roads.Count - 1);
                Vector2Int p2 = roads[roads.Count - 1]; roads.RemoveAt(roads.Count - 1);
                char ch = (char)portalChar;
                matrix[p1.x, p1.y] = ch;
                matrix[p2.x, p2.y] = ch;
                portalChar++;
            }

            // 배치 후 리스트 갱신
            RefreshPositionLists(matrix, n, start, dest, roads, walls);
            ShuffleList(roads);
        }

        // ── 판타지 테마: 점프대 → 적 순으로 배치 ──
        if (themeFlags[THEME_FANTASY])
        {
            int maxEach = Mathf.FloorToInt(roads.Count * 0.5f);

            // 점프대 'j'
            int jumpCount = UnityEngine.Random.Range(0, maxEach + 1);
            for (int i = 0; i < jumpCount && roads.Count > 0; i++)
            {
                Vector2Int pos = roads[roads.Count - 1];
                roads.RemoveAt(roads.Count - 1);
                matrix[pos.x, pos.y] = 'j';
            }

            // 적 'e'
            int enemyCount = UnityEngine.Random.Range(0, maxEach + 1);
            for (int i = 0; i < enemyCount && roads.Count > 0; i++)
            {
                Vector2Int pos = roads[roads.Count - 1];
                roads.RemoveAt(roads.Count - 1);
                matrix[pos.x, pos.y] = 'e';
            }
        }

        // 광부·무법자 테마는 미로 구조(벽 배치) 자체를 바꾸지 않으므로
        // 검증 단계에서 스킬 사용 여부만 반영합니다.
    }

    /// <summary>현재 matrix 상태를 바탕으로 길/벽 위치 리스트를 갱신합니다.</summary>
    private void RefreshPositionLists(char[,] matrix, int n,
                                      Vector2Int start, Vector2Int dest,
                                      List<Vector2Int> roads, List<Vector2Int> walls)
    {
        roads.Clear();
        walls.Clear();
        for (int i = 1; i < n - 1; i++)
        {
            for (int j = 1; j < n - 1; j++)
            {
                if (i == start.x && j == start.y) continue;
                if (i == dest.x  && j == dest.y)  continue;
                char c = matrix[i, j];
                if (c == '0') roads.Add(new Vector2Int(i, j));
                else if (c == '1') walls.Add(new Vector2Int(i, j));
            }
        }
    }

    // ───────────────────────────────────────────
    // 통합 검증
    // ───────────────────────────────────────────

    /// <summary>
    /// 활성화된 테마들의 검증을 OR 조건으로 합산합니다.
    /// 하나라도 클리어 가능한 경로가 존재하면 유효한 미로입니다.
    /// </summary>
    private bool ValidateMaze(char[,] matrix, int n, bool[] themeFlags,
                               Vector2Int start, Vector2Int dest,
                               int minerCount, int outlawCount)
    {
        // 기본 테마 또는 아무 테마도 없을 때: 단순 BFS
        if (themeFlags[THEME_BASIC])
            return ValidateBaseBFS(matrix, n, start, dest);

        // 복수 테마: 하나라도 통과하면 유효
        if (themeFlags[THEME_MINER]   && ValidateWithMiner(matrix, n, start, dest, minerCount))
            return true;
        if (themeFlags[THEME_OUTLAW]  && ValidateWithOutlaw(matrix, n, start, dest, outlawCount))
            return true;
        if (themeFlags[THEME_FANTASY] && ValidateWithFantasy(matrix, n, start, dest))
            return true;
        if (themeFlags[THEME_FUTURE]  && ValidateWithFuture(matrix, n, start, dest))
            return true;

        // 어떤 테마도 클리어 불가
        return false;
    }

    // ───────────────────────────────────────────
    // 검증 1: 기본 BFS
    // ───────────────────────────────────────────

    /// <summary>
    /// 기본 테마 BFS: 벽('1')이 아닌 모든 칸을 통과 가능으로 처리합니다.
    /// 기본 테마는 테마 요소가 없으므로 실질적으로 '0', 's', 'd'만 존재하지만,
    /// 벽 여부만 판단하는 방식으로 구현해 안전성을 높입니다.
    /// </summary>
    private bool ValidateBaseBFS(char[,] matrix, int n, Vector2Int start, Vector2Int dest)
    {
        bool[,] visited = new bool[n, n];
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            if (cur.x == dest.x && cur.y == dest.y) return true;

            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + DX[d];
                int ny = cur.y + DY[d];
                if (!InBounds(nx, ny, n) || visited[nx, ny]) continue;
                // 벽('1')만 통과 불가, 나머지는 모두 이동 가능
                if (matrix[nx, ny] == '1') continue;
                visited[nx, ny] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return false;
    }

    // ───────────────────────────────────────────
    // 검증 2: 광부 테마 (Dijkstra, 벽 = 비용 1)
    // ───────────────────────────────────────────

    /// <summary>
    /// 벽('1')을 통과하는 비용을 1로 두는 가중치 BFS(0-1 BFS).
    /// 시작~도착 최소 비용 ≤ minerCount 이면 클리어 가능.
    /// </summary>
    private bool ValidateWithMiner(char[,] matrix, int n,
                                   Vector2Int start, Vector2Int dest, int minerCount)
    {
        int[,] dist = new int[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                dist[i, j] = int.MaxValue;

        dist[start.x, start.y] = 0;

        // 0-1 BFS: 비용 0은 앞, 비용 1은 뒤에 추가
        LinkedList<Vector2Int> deque = new LinkedList<Vector2Int>();
        deque.AddFirst(start);

        while (deque.Count > 0)
        {
            Vector2Int cur = deque.First.Value;
            deque.RemoveFirst();

            if (cur == dest) break;

            int curDist = dist[cur.x, cur.y];

            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + DX[d];
                int ny = cur.y + DY[d];
                if (!InBounds(nx, ny, n)) continue;

                char c = matrix[nx, ny];
                // 외곽 벽('1', 가장자리)은 통과 불가 — 내부 벽만 광부가 뚫을 수 있음
                bool isOuterWall = (nx == 0 || nx == n - 1 || ny == 0 || ny == n - 1);
                if (isOuterWall) continue;

                int cost = (c == '1') ? 1 : 0; // 벽이면 곡괭이 1회 소모
                int newDist = curDist + cost;

                if (newDist < dist[nx, ny])
                {
                    dist[nx, ny] = newDist;
                    if (cost == 0) deque.AddFirst(new Vector2Int(nx, ny));
                    else           deque.AddLast(new Vector2Int(nx, ny));
                }
            }
        }

        return dist[dest.x, dest.y] <= minerCount;
    }

    // ───────────────────────────────────────────
    // 검증 3: 무법자 테마 (폭탄 = 인접 4칸 동시 제거)
    // ───────────────────────────────────────────

    /// <summary>
    /// 무법자 테마 검증: 상태 = (위치, 남은 폭탄 수)인 BFS.
    ///
    /// 폭탄 1개를 사용하면 지정 위치 기준 인접 4칸의 벽이 제거됩니다.
    /// 폭탄 사용 조건:
    ///   - 플레이어 위치 기준 ±2 이내 (r, k ∈ [-2, 2])
    ///   - 플레이어 바로 인접 4칸에는 사용 불가
    ///   - 외곽(가장자리) 칸에는 사용 불가
    ///
    /// 핵심 설계:
    ///   폭탄 사용은 이동 횟수를 소모하지 않으므로, 현재 위치에서 폭탄을 써서
    ///   벽을 제거한 뒤 새로 이동 가능해진 칸들을 탐색합니다.
    ///   "벽을 제거한 상태"를 매트릭스에 직접 반영하는 대신,
    ///   (위치, 남은폭탄) 상태 공간에서 폭탄 사용을 "현재 위치의 상태 전이"로 표현합니다.
    ///   폭탄으로 제거 가능한 인접 벽 칸을 비용 0으로 이동 가능한 상태로 전이시켜
    ///   폭탄 범위 효과를 정확히 반영합니다.
    /// </summary>
    private bool ValidateWithOutlaw(char[,] matrix, int n,
                                    Vector2Int start, Vector2Int dest, int outlawCount)
    {
        // visited[x, y, 남은폭탄] — 같은 상태를 중복 탐색하지 않기 위함
        bool[,,] visited = new bool[n, n, outlawCount + 1];
        // BFS 큐: (x, y, 남은폭탄)
        Queue<Vector3Int> q = new Queue<Vector3Int>();

        visited[start.x, start.y, outlawCount] = true;
        q.Enqueue(new Vector3Int(start.x, start.y, outlawCount));

        while (q.Count > 0)
        {
            Vector3Int state = q.Dequeue();
            int cx = state.x, cy = state.y, bombs = state.z;

            if (cx == dest.x && cy == dest.y) return true;

            // ── 일반 이동: 벽이 아닌 칸으로 이동 ──
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (!InBounds(nx, ny, n)) continue;
                if (matrix[nx, ny] == '1') continue; // 벽은 일반 이동 불가
                if (visited[nx, ny, bombs]) continue;
                visited[nx, ny, bombs] = true;
                q.Enqueue(new Vector3Int(nx, ny, bombs));
            }

            // ── 폭탄 사용: 현재 위치 기준 ±2 범위 내 칸에 폭탄 설치 ──
            // 폭탄은 이동 횟수를 소모하지 않으므로, 폭탄 사용 후에도 같은 위치에서
            // 계속 이동할 수 있음. 폭탄으로 새로 뚫린 벽 칸을 이동 가능 상태로 추가.
            if (bombs > 0)
            {
                int newBombs = bombs - 1;

                for (int r = -2; r <= 2; r++)
                {
                    for (int k = -2; k <= 2; k++)
                    {
                        int tx = cx + r;
                        int ty = cy + k;
                        if (!InBounds(tx, ty, n)) continue;

                        // 폭탄 설치 불가 조건 체크
                        bool isOuter    = (tx == 0 || tx == n - 1 || ty == 0 || ty == n - 1);
                        bool isAdjacent = IsAdjacentTo(tx, ty, cx, cy);
                        if (isOuter || isAdjacent) continue;

                        // 폭탄 중심 (tx, ty)의 인접 4칸 중 벽인 칸을 파악
                        for (int d = 0; d < 4; d++)
                        {
                            int bx = tx + DX[d];
                            int by = ty + DY[d];
                            if (!InBounds(bx, by, n)) continue;

                            // 외곽 벽은 제거 불가
                            bool outerB = (bx == 0 || bx == n - 1 || by == 0 || by == n - 1);
                            if (outerB) continue;

                            // 해당 칸이 벽이면, 폭탄으로 제거 후 이동 가능해짐
                            // → (bx, by, newBombs) 상태로 전이
                            if (matrix[bx, by] == '1' && !visited[bx, by, newBombs])
                            {
                                visited[bx, by, newBombs] = true;
                                q.Enqueue(new Vector3Int(bx, by, newBombs));
                            }
                        }

                        // 폭탄 사용 후 현재 위치에서도 계속 탐색 (폭탄만 소모, 이동 없음)
                        // 아직 방문 안 한 (cx, cy, newBombs) 상태 추가
                        if (!visited[cx, cy, newBombs])
                        {
                            visited[cx, cy, newBombs] = true;
                            q.Enqueue(new Vector3Int(cx, cy, newBombs));
                        }
                    }
                }
            }
        }

        return false;
    }

    // ───────────────────────────────────────────
    // 검증 4: 판타지 테마 (적 제거 + 점프대 규칙)
    // ───────────────────────────────────────────

    /// <summary>
    /// 판타지 테마 검증 BFS.
    /// - '1': 통과 불가
    /// - '0', 's', 'd': 일반 이동
    /// - 'e'(적): 이동 가능 (검으로 제거)
    /// - 'j'(점프대): 진입 방향으로 2칸 앞 착지 (막히면 1칸, 그것도 막히면 제자리)
    ///
    /// 점프대 버그 수정:
    ///   점프대는 진입 방향에 따라 착지 위치가 달라지므로 visited 상태에
    ///   방향 정보를 포함해야 합니다. (x, y, dir) 형태의 상태로 관리하되,
    ///   점프대가 아닌 일반 칸은 dir=4(무방향)로 처리합니다.
    ///   이렇게 하면 같은 점프대를 서로 다른 방향에서 진입하는 경우를 모두 탐색합니다.
    /// </summary>
    private bool ValidateWithFantasy(char[,] matrix, int n, Vector2Int start, Vector2Int dest)
    {
        // visited[x, y, dir]: dir 0~3은 방향별 점프대 진입, dir 4는 일반 칸
        bool[,,] visited = new bool[n, n, 5];
        Queue<Vector3Int> q = new Queue<Vector3Int>();

        // 시작점은 일반 칸(dir=4)으로 등록
        visited[start.x, start.y, 4] = true;
        q.Enqueue(new Vector3Int(start.x, start.y, 4));

        while (q.Count > 0)
        {
            Vector3Int state = q.Dequeue();
            int cx = state.x, cy = state.y;

            if (cx == dest.x && cy == dest.y) return true;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (!InBounds(nx, ny, n)) continue;

                char c = matrix[nx, ny];
                if (c == '1') continue; // 벽 통과 불가

                if (c == 'j')
                {
                    // 점프대는 진입 방향(d)별로 상태를 구분
                    if (visited[nx, ny, d]) continue;
                    visited[nx, ny, d] = true;

                    // 착지 위치 계산 후 큐에 추가
                    Vector2Int landed = ResolveJumpPad(matrix, n, new Vector2Int(nx, ny), d, 0);
                    int landDir = 4; // 착지 위치는 일반 칸 취급
                    if (!visited[landed.x, landed.y, landDir])
                    {
                        visited[landed.x, landed.y, landDir] = true;
                        q.Enqueue(new Vector3Int(landed.x, landed.y, landDir));
                    }
                }
                else
                {
                    // 일반 칸(dir=4)으로 처리
                    if (visited[nx, ny, 4]) continue;
                    visited[nx, ny, 4] = true;
                    q.Enqueue(new Vector3Int(nx, ny, 4));
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 점프대 착지 위치를 계산합니다.
    /// padPos: 점프대 위치, dir: 진입 방향 인덱스, depth: 연속 점프 깊이 (순환 방지)
    /// </summary>
    private Vector2Int ResolveJumpPad(char[,] matrix, int n, Vector2Int padPos, int dir, int depth)
    {
        // 연속 점프 최대 깊이 제한 (순환 구조 방지)
        if (depth >= n * n) return padPos;

        // 점프대에서 같은 방향으로 2칸 앞
        int x2 = padPos.x + DX[dir] * 2;
        int y2 = padPos.y + DY[dir] * 2;

        if (InBounds(x2, y2, n) && IsPassableForJump(matrix[x2, y2]))
        {
            if (matrix[x2, y2] == 'j')
                return ResolveJumpPad(matrix, n, new Vector2Int(x2, y2), dir, depth + 1);
            return new Vector2Int(x2, y2);
        }

        // 2칸 앞이 막혔으면 1칸 앞 시도
        int x1 = padPos.x + DX[dir];
        int y1 = padPos.y + DY[dir];
        if (InBounds(x1, y1, n) && IsPassableForJump(matrix[x1, y1]))
        {
            if (matrix[x1, y1] == 'j')
                return ResolveJumpPad(matrix, n, new Vector2Int(x1, y1), dir, depth + 1);
            return new Vector2Int(x1, y1);
        }

        // 둘 다 막혔으면 점프대 자리에 멈춤
        return padPos;
    }

    /// <summary>점프대 착지 판정: 벽('1')과 적('e')은 착지 불가.</summary>
    private bool IsPassableForJump(char c)
    {
        return c != '1' && c != 'e';
    }

    // ───────────────────────────────────────────
    // 검증 5: 미래 테마 (포탈 + 레이저 홀짝 상태)
    // ───────────────────────────────────────────

    /// <summary>
    /// 미래 테마 BFS.
    /// 상태: (위치, 이동 홀짝 = step % 2)
    /// - 포탈: 같은 문자의 다른 포탈 위치로 순간이동
    /// - 레이저 'r': 활성(step이 짝수) = 통과 불가, 비활성(step이 홀수) = 통과 가능
    ///   * 초기 상태 '활성' = step 0(짝수)에서 활성
    /// </summary>
    private bool ValidateWithFuture(char[,] matrix, int n, Vector2Int start, Vector2Int dest)
    {
        // 포탈 쌍 맵 미리 구성: 문자 → 두 위치
        Dictionary<char, List<Vector2Int>> portalMap = BuildPortalMap(matrix, n);

        // 상태: (x, y, step%2) → visited
        bool[,,] visited = new bool[n, n, 2];
        Queue<Vector3Int> q = new Queue<Vector3Int>();

        int startStep = 0; // 초기 이동 횟수 0 (짝수 → 레이저 활성)
        q.Enqueue(new Vector3Int(start.x, start.y, startStep % 2));
        visited[start.x, start.y, startStep % 2] = true;

        while (q.Count > 0)
        {
            Vector3Int state = q.Dequeue();
            int cx = state.x, cy = state.y, parity = state.z;

            if (cx == dest.x && cy == dest.y) return true;

            int nextParity = 1 - parity; // 이동 후 홀짝 반전

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + DX[d];
                int ny = cy + DY[d];
                if (!InBounds(nx, ny, n)) continue;

                char c = matrix[nx, ny];

                if (c == '1') continue; // 일반 벽 통과 불가

                // 레이저: 다음 step의 홀짝으로 활성 여부 판단
                // 현재 step에서 이동 → 다음 step = parity가 반전됨
                // 레이저는 "한 번 움직일 때마다 전환" → 이 칸에 도착 시 nextParity 기준
                if (c == 'r')
                {
                    // nextParity == 0: 활성 (벽), nextParity == 1: 비활성 (통과 가능)
                    if (nextParity == 0) continue; // 레이저 활성 → 통과 불가
                }

                if (!visited[nx, ny, nextParity])
                {
                    visited[nx, ny, nextParity] = true;

                    // 포탈 처리: 포탈 칸에 착지 시 쌍 포탈로 이동
                    if (c >= '2' && c <= '9' && portalMap.ContainsKey(c))
                    {
                        List<Vector2Int> pair = portalMap[c];
                        Vector2Int other = (pair[0].x == nx && pair[0].y == ny) ? pair[1] : pair[0];
                        if (!visited[other.x, other.y, nextParity])
                        {
                            visited[other.x, other.y, nextParity] = true;
                            q.Enqueue(new Vector3Int(other.x, other.y, nextParity));
                        }
                    }
                    else
                    {
                        q.Enqueue(new Vector3Int(nx, ny, nextParity));
                    }
                }
            }
        }
        return false;
    }

    /// <summary>matrix에서 포탈 문자('2'~'9') 위치 쌍을 맵으로 구성합니다.</summary>
    private Dictionary<char, List<Vector2Int>> BuildPortalMap(char[,] matrix, int n)
    {
        var map = new Dictionary<char, List<Vector2Int>>();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                char c = matrix[i, j];
                if (c >= '2' && c <= '9')
                {
                    if (!map.ContainsKey(c)) map[c] = new List<Vector2Int>();
                    map[c].Add(new Vector2Int(i, j));
                }
            }
        }
        return map;
    }

    // ───────────────────────────────────────────
    // 유틸리티
    // ───────────────────────────────────────────

    private bool InBounds(int x, int y, int n)
        => x >= 0 && x < n && y >= 0 && y < n;

    private bool IsAdjacentTo(int ax, int ay, int bx, int by)
    {
        int dx = Mathf.Abs(ax - bx);
        int dy = Mathf.Abs(ay - by);
        return (dx + dy) == 1;
    }

    private char[,] CloneMatrix(char[,] src, int n)
    {
        char[,] clone = new char[n, n];
        Array.Copy(src, clone, src.Length);
        return clone;
    }

    private void ShuffleList(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    /// <summary>
    /// 난이도 계산 (임시 구현 — 기준 확정 후 교체 예정).
    /// </summary>
    private Difficulty CalculateDifficulty(int n, bool[] themeFlags)
    {
        if (n <= 10) return Difficulty.Easy;
        if (n <= 20) return Difficulty.Normal;
        return Difficulty.Hard;
    }
}