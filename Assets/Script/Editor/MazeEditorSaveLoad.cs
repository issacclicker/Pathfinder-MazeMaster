using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// MazeEditorData를 JSON 파일로 저장하고 불러오는 클래스입니다.
///
/// 저장 경로: Application.persistentDataPath/MazeEditor/
/// 파일 확장자: .maze.json
///
/// 인게임 파일 브라우저:
///   LoadFileList()로 저장된 파일 목록을 가져오고,
///   LoadByFileName()으로 선택한 파일을 불러옵니다.
///   별도의 OS 파일 탐색기를 열지 않습니다.
/// </summary>
public class MazeEditorSaveLoad : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // 상수
    // ───────────────────────────────────────────────────────────

    private const string SAVE_FOLDER    = "MazeEditor";
    private const string FILE_EXTENSION = ".maze.json";

    // ───────────────────────────────────────────────────────────
    // 저장 경로
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 폴더 절대 경로를 반환합니다.
    /// 폴더가 없으면 자동 생성합니다.
    /// </summary>
    public static string SaveDirectory
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, SAVE_FOLDER);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ───────────────────────────────────────────────────────────
    // 저장
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// MazeEditorData를 JSON 파일로 저장합니다.
    /// 파일명은 data.mazeName을 기반으로 생성합니다.
    /// 같은 이름의 파일이 있으면 덮어씁니다.
    /// </summary>
    /// <returns>(성공 여부, 저장된 파일 경로 또는 오류 메시지)</returns>
    public static (bool success, string message) Save(MazeEditorData data)
    {
        // 저장 전 유효성 검사
        var (valid, reason) = data.Validate();
        if (!valid)
            return (false, $"저장 실패: {reason}");

        try
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            string fileName = SanitizeFileName(data.mazeName) + FILE_EXTENSION;
            string filePath = Path.Combine(SaveDirectory, fileName);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            Debug.Log($"[MazeEditorSaveLoad] 저장 완료: {filePath}");
            return (true, filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MazeEditorSaveLoad] 저장 오류: {e.Message}");
            return (false, $"저장 중 오류 발생: {e.Message}");
        }
    }

    // ───────────────────────────────────────────────────────────
    // 불러오기
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 폴더에 있는 .maze.json 파일 목록을 반환합니다.
    /// 인게임 파일 브라우저에서 이 목록을 표시합니다.
    /// </summary>
    /// <returns>파일 정보 목록 (표시 이름, 파일명, 저장 일시)</returns>
    public static List<SaveFileInfo> LoadFileList()
    {
        var result = new List<SaveFileInfo>();

        try
        {
            string[] files = Directory.GetFiles(SaveDirectory, $"*{FILE_EXTENSION}");

            foreach (string filePath in files)
            {
                try
                {
                    string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                    MazeEditorData data = JsonUtility.FromJson<MazeEditorData>(json);

                    result.Add(new SaveFileInfo
                    {
                        displayName = data.mazeName,
                        fileName    = Path.GetFileName(filePath),
                        savedAt     = data.savedAt,
                        mazeSize    = data.mazeSize
                    });
                }
                catch
                {
                    // 손상된 파일은 건너뜀
                    result.Add(new SaveFileInfo
                    {
                        displayName = Path.GetFileNameWithoutExtension(filePath) + " (손상됨)",
                        fileName    = Path.GetFileName(filePath),
                        savedAt     = "",
                        mazeSize    = 0
                    });
                }
            }

            // 저장 일시 내림차순 정렬 (최신순)
            result.Sort((a, b) => string.Compare(b.savedAt, a.savedAt, StringComparison.Ordinal));
        }
        catch (Exception e)
        {
            Debug.LogError($"[MazeEditorSaveLoad] 파일 목록 로드 오류: {e.Message}");
        }

        return result;
    }

    /// <summary>
    /// 파일명으로 MazeEditorData를 불러옵니다.
    /// LoadFileList()에서 반환된 SaveFileInfo.fileName을 전달합니다.
    /// </summary>
    /// <returns>(성공 여부, MazeEditorData 또는 null)</returns>
    public static (bool success, MazeEditorData data, string message) LoadByFileName(string fileName)
    {
        try
        {
            string filePath = Path.Combine(SaveDirectory, fileName);

            if (!File.Exists(filePath))
                return (false, null, $"파일을 찾을 수 없습니다: {fileName}");

            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            MazeEditorData data = JsonUtility.FromJson<MazeEditorData>(json);

            if (data == null)
                return (false, null, "파일 파싱에 실패했습니다.");

            Debug.Log($"[MazeEditorSaveLoad] 불러오기 완료: {filePath}");
            return (true, data, "");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MazeEditorSaveLoad] 불러오기 오류: {e.Message}");
            return (false, null, $"불러오기 중 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 파일명으로 저장 파일을 삭제합니다.
    /// </summary>
    public static (bool success, string message) DeleteByFileName(string fileName)
    {
        try
        {
            string filePath = Path.Combine(SaveDirectory, fileName);

            if (!File.Exists(filePath))
                return (false, $"파일을 찾을 수 없습니다: {fileName}");

            File.Delete(filePath);
            Debug.Log($"[MazeEditorSaveLoad] 삭제 완료: {filePath}");
            return (true, "삭제되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MazeEditorSaveLoad] 삭제 오류: {e.Message}");
            return (false, $"삭제 중 오류 발생: {e.Message}");
        }
    }

    // ───────────────────────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 파일명으로 사용할 수 없는 문자를 '_'로 치환합니다.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
    }
}

// ───────────────────────────────────────────────────────────
// 파일 목록 표시용 데이터
// ───────────────────────────────────────────────────────────

/// <summary>
/// 인게임 파일 브라우저에서 각 저장 파일의 요약 정보를 표시하는 데이터 클래스입니다.
/// </summary>
public class SaveFileInfo
{
    /// <summary>유저가 지정한 미로 이름 (UI 표시용)</summary>
    public string displayName;

    /// <summary>실제 파일명 (LoadByFileName 호출 시 사용)</summary>
    public string fileName;

    /// <summary>저장 일시 문자열</summary>
    public string savedAt;

    /// <summary>미로 크기</summary>
    public int mazeSize;

    /// <summary>UI 표시용 포맷 문자열</summary>
    public string ToDisplayString()
    {
        string date = string.IsNullOrEmpty(savedAt) ? "알 수 없음" : savedAt.Replace("T", " ");
        return $"{displayName}  ({mazeSize}×{mazeSize})  |  {date}";
    }
}