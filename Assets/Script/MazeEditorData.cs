using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유저 에디터에서 생성/편집하는 미로의 전체 데이터를 담는 클래스입니다.
/// Maze 구조체와 별도로 관리하며, JSON 직렬화를 지원합니다.
///
/// Maze 구조체와의 차이:
///   - mazeName, bgTheme, themeFlags 등 에디터 전용 필드 포함
///   - char[,] 대신 List(string)으로 matrix를 저장 (JsonUtility 직렬화 지원)
///   - 포탈 쌍 정보를 별도 리스트로 관리
/// </summary>
[Serializable]
public class MazeEditorData
{
    [Header("── 기본 정보 ──────────────────────────")]

    [Tooltip("유저가 지정한 미로 이름")]
    public string mazeName = "New Maze";

    [Tooltip("미로 크기 (n×n, 5~30)")]
    public int mazeSize = 5;

    [Tooltip("배경 테마 이름 (추후 확장용, 현재는 문자열 저장)")]
    public string bgTheme = "Default";

    [Tooltip("저장 일시 (ISO 8601)")]
    public string savedAt = "";

    [Header("── 테마 플래그 ─────────────────────────")]

    [Tooltip("인덱스 0:기본, 1:광부, 2:무법자, 3:판타지, 4:미래")]
    public bool[] themeFlags = new bool[] { true, false, false, false, false };

    [Tooltip("광부 테마 곡괭이 사용 횟수 (1~5, 광부 테마 활성 시에만 유효)")]
    public int minerSkillCount = 0;

    [Tooltip("무법자 테마 폭탄 사용 횟수 (1~5, 무법자 테마 활성 시에만 유효)")]
    public int outlawSkillCount = 0;

    [Header("── 미로 행렬 ─────────────────────────")]

    /// <summary>
    /// 미로 행렬을 행(row) 단위 문자열 리스트로 저장합니다.
    /// JsonUtility는 char[,]를 직렬화하지 못하므로 List-string 형태로 변환합니다.
    /// 예: ["11111", "1s001", "11101", "111d1", "11111"]
    /// </summary>
    public List<string> matrixRows = new List<string>();

    [Header("── 포탈 쌍 정보 ──────────────────────")]

    /// <summary>
    /// 포탈 쌍 목록. 각 쌍은 (문자, 위치A, 위치B)로 구성됩니다.
    /// 쌍이 완성되지 않은 포탈(단독 배치)은 이 리스트에 포함되지 않습니다.
    /// </summary>
    public List<PortalPair> portalPairs = new List<PortalPair>();

    // ───────────────────────────────────────────────────────────
    // 변환 유틸리티
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// char[,] 행렬을 List-string으로 변환해 matrixRows에 저장합니다.
    /// </summary>
    public void SetMatrix(char[,] matrix)
    {
        int n = matrix.GetLength(0);
        mazeSize = n;
        matrixRows.Clear();

        for (int r = 0; r < n; r++)
        {
            string row = "";
            for (int c = 0; c < n; c++)
                row += matrix[r, c];
            matrixRows.Add(row);
        }
    }

    /// <summary>
    /// matrixRows를 char[,]로 복원합니다.
    /// </summary>
    public char[,] GetMatrix()
    {
        int n = matrixRows.Count;
        char[,] matrix = new char[n, n];

        for (int r = 0; r < n; r++)
        {
            string row = matrixRows[r];
            for (int c = 0; c < n && c < row.Length; c++)
                matrix[r, c] = row[c];
        }
        return matrix;
    }

    /// <summary>
    /// 현재 에디터 데이터를 Maze 구조체로 변환합니다.
    /// (게임 플레이 시 사용)
    /// </summary>
    public Maze ToMaze()
    {
        char[,] matrix = GetMatrix();
        return new Maze(
            difficulty:  Difficulty.Easy, // 유저 제작 미로는 난이도를 별도 계산 필요
            matrix:      matrix,
            miner:       minerSkillCount,
            outlaw:      outlawSkillCount
        );
    }

    /// <summary>
    /// 현재 themeFlags 배열을 기반으로 활성 테마 이름 목록을 반환합니다.
    /// </summary>
    public List<string> GetActiveThemeNames()
    {
        string[] names = { "기본", "광부", "무법자", "판타지", "미래" };
        var result = new List<string>();
        for (int i = 0; i < themeFlags.Length && i < names.Length; i++)
            if (themeFlags[i]) result.Add(names[i]);
        return result;
    }

    /// <summary>
    /// 저장 유효성을 검사합니다.
    /// 시작점('s')과 도착점('d')이 각각 1개씩 있어야 합니다.
    /// </summary>
    public (bool valid, string reason) Validate()
    {
        if (string.IsNullOrWhiteSpace(mazeName))
            return (false, "미로 이름이 비어있습니다.");

        if (matrixRows.Count == 0)
            return (false, "미로 데이터가 없습니다.");

        int startCount = 0, destCount = 0;
        foreach (string row in matrixRows)
        {
            foreach (char c in row)
            {
                if (c == 's') startCount++;
                if (c == 'd') destCount++;
            }
        }

        if (startCount != 1) return (false, $"시작 위치(s)가 {startCount}개입니다. 정확히 1개여야 합니다.");
        if (destCount  != 1) return (false, $"도착 위치(d)가 {destCount}개입니다. 정확히 1개여야 합니다.");

        // 완성되지 않은 포탈 쌍 확인
        char[,] matrix = GetMatrix();
        int n = matrix.GetLength(0);
        Dictionary<char, int> portalCount = new Dictionary<char, int>();
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                char ch = matrix[r, c];
                if (ch >= '2' && ch <= '9')
                {
                    if (!portalCount.ContainsKey(ch)) portalCount[ch] = 0;
                    portalCount[ch]++;
                }
            }
        }
        foreach (var kv in portalCount)
            if (kv.Value != 2)
                return (false, $"포탈 '{kv.Key}'의 개수가 {kv.Value}개입니다. 정확히 2개(한 쌍)여야 합니다.");

        return (true, "");
    }
}

// ───────────────────────────────────────────────────────────
// 포탈 쌍 데이터
// ───────────────────────────────────────────────────────────

[Serializable]
public class PortalPair
{
    [Tooltip("포탈 문자 ('2'~'9')")]
    public char portalChar;

    [Tooltip("첫 번째 포탈 위치 (행, 열)")]
    public SerializableVector2Int posA;

    [Tooltip("두 번째 포탈 위치 (행, 열)")]
    public SerializableVector2Int posB;

    public PortalPair(char c, Vector2Int a, Vector2Int b)
    {
        portalChar = c;
        posA = new SerializableVector2Int(a);
        posB = new SerializableVector2Int(b);
    }
}

/// <summary>
/// JsonUtility가 Vector2Int를 직렬화하지 못하므로 대체 구조체를 사용합니다.
/// </summary>
[Serializable]
public struct SerializableVector2Int
{
    public int row;
    public int col;

    public SerializableVector2Int(Vector2Int v) { row = v.x; col = v.y; }
    public Vector2Int ToVector2Int() => new Vector2Int(row, col);
}