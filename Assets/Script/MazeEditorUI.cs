using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유저 미로 에디터의 메인 컨트롤러입니다.
/// MazeEditorGrid / MazeEditorItemBar / MazeEditorSaveLoad를 조율합니다.
///
/// [씬 계층 구조]
/// Canvas
///  ├─ EditorPanel              ← 이 스크립트 부착
///  │   ├─ TopBar
///  │   │   ├─ MazeNameInput
///  │   │   ├─ SizeInput
///  │   │   ├─ ApplyBtn
///  │   │   ├─ SaveBtn
///  │   │   └─ LoadBtn
///  │   ├─ GridContainer        ← MazeRenderer + MazeEditorGrid 부착
///  │   └─ RightPanel           ← MazeEditorItemBar 부착
///  └─ FileBrowserPanel         ← 인게임 파일 브라우저 (별도 패널)
///      ├─ FileListContainer
///      │   └─ FileItemPrefab
///      ├─ CloseBtn
///      └─ DeleteBtn
/// </summary>
public class MazeEditorUI : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결
    // ───────────────────────────────────────────────────────────

    [Header("── 서브시스템 ──────────────────────────")]

    [Tooltip("MazeRenderer 컴포넌트")]
    [SerializeField] private MazeRenderer mazeRenderer;

    [Tooltip("MazeEditorGrid 컴포넌트")]
    [SerializeField] private MazeEditorGrid editorGrid;

    [Tooltip("MazeEditorItemBar 컴포넌트")]
    [SerializeField] private MazeEditorItemBar itemBar;

    [Header("── TopBar UI ─────────────────────────")]

    [Tooltip("미로 이름 입력 필드")]
    [SerializeField] private TMP_InputField mazeNameInput;

    [Tooltip("미로 크기(n) 입력 필드")]
    [SerializeField] private TMP_InputField mazeSizeInput;

    [Tooltip("크기 적용 버튼")]
    [SerializeField] private Button applyBtn;

    [Tooltip("저장 버튼")]
    [SerializeField] private Button saveBtn;

    [Tooltip("불러오기 버튼")]
    [SerializeField] private Button loadBtn;

    [Tooltip("상태 메시지 표시 텍스트 (저장 성공/실패 등)")]
    [SerializeField] private TMP_Text statusText;

    [Header("── 인게임 파일 브라우저 ─────────────────")]

    [Tooltip("파일 브라우저 패널 (기본 비활성)")]
    [SerializeField] private GameObject fileBrowserPanel;

    [Tooltip("파일 목록이 생성될 ScrollView Content Transform")]
    [SerializeField] private Transform fileListContainer;

    [Tooltip("파일 목록 아이템 프리팹 (Button + TMP_Text)")]
    [SerializeField] private GameObject fileItemPrefab;

    [Tooltip("파일 브라우저 닫기 버튼")]
    [SerializeField] private Button fileBrowserCloseBtn;

    [Tooltip("선택된 파일 삭제 버튼")]
    [SerializeField] private Button fileDeleteBtn;

    [Header("── 확인 팝업 ────────────────────────")]

    [Tooltip("크기 변경 경고 팝업 패널")]
    [SerializeField] private GameObject confirmPanel;

    [Tooltip("팝업 메시지 텍스트")]
    [SerializeField] private TMP_Text confirmMessage;

    [Tooltip("팝업 확인 버튼")]
    [SerializeField] private Button confirmOkBtn;

    [Tooltip("팝업 취소 버튼")]
    [SerializeField] private Button confirmCancelBtn;

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    // 현재 편집 중인 데이터
    private MazeEditorData currentData;

    // 현재 편집 중인 matrix
    private char[,] matrix;
    private int     currentN;

    // 포탈 배치 상태: 쌍이 완성되지 않은 포탈의 첫 번째 위치
    // key: 포탈 문자, value: 첫 번째 위치
    private Dictionary<char, Vector2Int> pendingPortals = new Dictionary<char, Vector2Int>();

    // 현재 선택된 파일 브라우저 항목
    private SaveFileInfo selectedFileInfo = null;

    // 상태 메시지 자동 숨김 코루틴
    private Coroutine statusHideCoroutine;

    // ───────────────────────────────────────────────────────────
    // Unity 생명주기
    // ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (mazeRenderer == null || editorGrid == null || itemBar == null)
        {
            Debug.LogError("[MazeEditorUI] 필수 컴포넌트가 연결되지 않았습니다.");
            return;
        }

        // 기본 에디터 초기화 (5×5)
        currentData = new MazeEditorData();
        InitializeEditor(currentData.mazeSize);

        // 버튼 이벤트 등록
        applyBtn?.onClick.AddListener(OnApplySizeClicked);
        saveBtn?.onClick.AddListener(OnSaveClicked);
        loadBtn?.onClick.AddListener(OnLoadClicked);

        fileBrowserCloseBtn?.onClick.AddListener(() => fileBrowserPanel?.SetActive(false));
        fileDeleteBtn?.onClick.AddListener(OnDeleteFileClicked);

        confirmOkBtn?.onClick.AddListener(OnConfirmOk);
        confirmCancelBtn?.onClick.AddListener(() => confirmPanel?.SetActive(false));

        // 이름 입력 실시간 반영
        mazeNameInput?.onValueChanged.AddListener(v =>
        {
            if (currentData != null) currentData.mazeName = v;
        });

        // 아이템 바 콜백 등록
        itemBar.OnElementSelected      += OnElementSelected;
        itemBar.OnThemeSettingsChanged  += OnThemeSettingsChanged;
        itemBar.OnBgThemeChanged        += OnBgThemeChanged;

        // 그리드 콜백 등록
        editorGrid.OnCellChanged += OnGridCellChanged;

        // 파일 브라우저 초기 숨김
        fileBrowserPanel?.SetActive(false);
        confirmPanel?.SetActive(false);
    }

    // ───────────────────────────────────────────────────────────
    // 에디터 초기화
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// n×n 빈 미로로 에디터를 초기화합니다.
    /// 기존 matrix가 있으면 안 B(가능한 범위까지 유지)를 적용합니다.
    /// </summary>
    private void InitializeEditor(int n, char[,] existingMatrix = null)
    {
        n = Mathf.Clamp(n, 5, 30);
        currentN = n;

        char[,] newMatrix = new char[n, n];

        // 전체 '0' 초기화
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                newMatrix[r, c] = '0';

        // 기존 데이터 복사 (안 B: 가능한 범위까지 유지)
        if (existingMatrix != null)
        {
            int oldN = existingMatrix.GetLength(0);
            int copyN = Mathf.Min(oldN, n);
            for (int r = 0; r < copyN; r++)
                for (int c = 0; c < copyN; c++)
                    newMatrix[r, c] = existingMatrix[r, c];

            // 잘린 영역에 포탈 쌍 절반만 남은 경우 제거
            CleanOrphanPortals(newMatrix, n);
        }

        matrix = newMatrix;
        pendingPortals.Clear();

        // MazeRenderer로 그리드 렌더링
        mazeRenderer.RenderMaze(matrix);

        // EditorGrid 초기화 (셀 EventTrigger 등록)
        StartCoroutine(InitGridNextFrame());
    }

    /// <summary>
    /// RenderMaze 이후 한 프레임 대기 후 EditorGrid를 초기화합니다.
    /// (GridLayoutGroup 레이아웃 확정 대기)
    /// </summary>
    private IEnumerator InitGridNextFrame()
    {
        yield return new WaitForEndOfFrame();
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            editorGrid.GetComponent<RectTransform>() ?? mazeRenderer.GetComponent<RectTransform>()
        );
        editorGrid.InitGrid(matrix);
    }

    // ───────────────────────────────────────────────────────────
    // TopBar 이벤트
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 크기 적용 버튼 클릭: 기존 데이터가 있으면 확인 팝업을 표시합니다.
    /// </summary>
    private void OnApplySizeClicked()
    {
        if (!int.TryParse(mazeSizeInput?.text, out int n))
        {
            ShowStatus("올바른 숫자를 입력하세요 (5~30).", isError: true);
            return;
        }

        n = Mathf.Clamp(n, 5, 30);

        bool hasContent = HasAnyNonZeroContent();
        if (hasContent && n != currentN)
        {
            // 확인 팝업 표시
            ShowConfirm(
                $"미로 크기를 {currentN}×{currentN} → {n}×{n}으로 변경합니다.\n" +
                $"범위를 벗어난 요소는 삭제됩니다. 계속하시겠습니까?",
                () => ApplyNewSize(n)
            );
        }
        else
        {
            ApplyNewSize(n);
        }
    }

    private void ApplyNewSize(int n)
    {
        char[,] old = matrix;
        currentData.mazeSize = n;
        InitializeEditor(n, old);
        ShowStatus($"{n}×{n} 미로가 적용되었습니다.");
    }

    // ───────────────────────────────────────────────────────────
    // 저장 / 불러오기
    // ───────────────────────────────────────────────────────────

    private void OnSaveClicked()
    {
        SyncDataFromEditor();

        var (success, message) = MazeEditorSaveLoad.Save(currentData);
        ShowStatus(success ? $"저장 완료: {currentData.mazeName}" : message, !success);
    }

    private void OnLoadClicked()
    {
        // 인게임 파일 브라우저 열기
        RefreshFileBrowser();
        fileBrowserPanel?.SetActive(true);
        selectedFileInfo = null;
        fileDeleteBtn?.gameObject.SetActive(false);
    }

    /// <summary>
    /// 파일 브라우저를 저장 파일 목록으로 갱신합니다.
    /// </summary>
    private void RefreshFileBrowser()
    {
        if (fileListContainer == null) return;

        // 기존 항목 제거
        foreach (Transform child in fileListContainer)
            Destroy(child.gameObject);

        List<SaveFileInfo> files = MazeEditorSaveLoad.LoadFileList();

        if (files.Count == 0)
        {
            // 빈 목록 안내
            GameObject emptyGo = new GameObject("Empty", typeof(RectTransform));
            emptyGo.transform.SetParent(fileListContainer, false);
            TMP_Text emptyTxt = emptyGo.AddComponent<TextMeshProUGUI>();
            emptyTxt.text      = "저장된 미로가 없습니다.";
            emptyTxt.fontSize  = 16;
            emptyTxt.alignment = TextAlignmentOptions.Center;
            return;
        }

        foreach (SaveFileInfo info in files)
        {
            SaveFileInfo capturedInfo = info;

            GameObject itemGo = fileItemPrefab != null
                ? Instantiate(fileItemPrefab, fileListContainer)
                : CreateDefaultFileItem(info.ToDisplayString(), fileListContainer);

            Button btn = itemGo.GetComponent<Button>();
            TMP_Text lbl = itemGo.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = info.ToDisplayString();

            btn?.onClick.AddListener(() =>
            {
                selectedFileInfo = capturedInfo;
                fileDeleteBtn?.gameObject.SetActive(true);
                LoadFile(capturedInfo);
            });
        }
    }

    private void LoadFile(SaveFileInfo info)
    {
        var (success, data, message) = MazeEditorSaveLoad.LoadByFileName(info.fileName);

        if (!success)
        {
            ShowStatus(message, isError: true);
            return;
        }

        // 에디터에 데이터 적용
        currentData = data;

        if (mazeNameInput != null)
            mazeNameInput.text = data.mazeName;

        if (mazeSizeInput != null)
            mazeSizeInput.text = data.mazeSize.ToString();

        char[,] loadedMatrix = data.GetMatrix();
        InitializeEditor(data.mazeSize, loadedMatrix);

        // 아이템 바 UI 복원
        itemBar.LoadFromData(data);

        fileBrowserPanel?.SetActive(false);
        ShowStatus($"불러오기 완료: {data.mazeName}");
    }

    private void OnDeleteFileClicked()
    {
        if (selectedFileInfo == null) return;

        ShowConfirm(
            $"'{selectedFileInfo.displayName}'을 삭제하시겠습니까?",
            () =>
            {
                var (success, message) = MazeEditorSaveLoad.DeleteByFileName(selectedFileInfo.fileName);
                ShowStatus(success ? "삭제되었습니다." : message, !success);
                RefreshFileBrowser();
                selectedFileInfo = null;
                fileDeleteBtn?.gameObject.SetActive(false);
            }
        );
    }

    // ───────────────────────────────────────────────────────────
    // 그리드 셀 변경 콜백
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// MazeEditorGrid에서 셀이 변경될 때 호출됩니다.
    /// 포탈 쌍 관리 및 테마 플래그 갱신을 처리합니다.
    /// </summary>
    private void OnGridCellChanged(int row, int col, char newElement)
    {
        // 포탈 처리 (쌍 완성 로직)
        if (newElement >= '2' && newElement <= '9')
        {
            HandlePortalPlacement(row, col, newElement);
            return;
        }

        // 포탈 삭제 처리
        if (matrix[row, col] >= '2' && matrix[row, col] <= '9')
        {
            char oldPortal = matrix[row, col];
            RemovePortalPair(oldPortal, row, col);
        }

        // 테마 플래그 갱신
        UpdateThemeFlags();
    }

    // ───────────────────────────────────────────────────────────
    // 포탈 쌍 관리
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 포탈 배치 처리:
    ///   - 첫 번째 배치: pendingPortals에 기록
    ///   - 두 번째 배치: 쌍 완성 → matrix와 UI에 기록
    ///   - 같은 위치에 다시 찍으면: 취소
    /// </summary>
    private void HandlePortalPlacement(int row, int col, char portalChar)
    {
        if (pendingPortals.ContainsKey(portalChar))
        {
            Vector2Int firstPos = pendingPortals[portalChar];

            if (firstPos.x == row && firstPos.y == col)
            {
                // 같은 위치 재클릭 → 취소
                pendingPortals.Remove(portalChar);
                editorGrid.SetCell(row, col, '0');
                ShowStatus($"포탈 {portalChar - '1'} 배치가 취소되었습니다.");
                return;
            }

            // 쌍 완성
            pendingPortals.Remove(portalChar);
            editorGrid.SetPortalCell(firstPos.x, firstPos.y, portalChar);
            editorGrid.SetPortalCell(row, col, portalChar);

            // PortalPairs 갱신
            currentData.portalPairs.RemoveAll(p => p.portalChar == portalChar);
            currentData.portalPairs.Add(new PortalPair(
                portalChar,
                new Vector2Int(firstPos.x, firstPos.y),
                new Vector2Int(row, col)
            ));

            ShowStatus($"포탈 {portalChar - '1'} 쌍이 완성되었습니다.");
            UpdateThemeFlags();
        }
        else
        {
            // 첫 번째 포탈 배치
            // 기존에 같은 문자 포탈이 있으면 제거
            RemovePortalPairFromMatrix(portalChar);

            pendingPortals[portalChar] = new Vector2Int(row, col);
            editorGrid.SetPortalCell(row, col, portalChar);
            ShowStatus($"포탈 {portalChar - '1'} 첫 번째 위치를 찍었습니다. 두 번째 위치를 선택하세요.");
        }
    }

    /// <summary>
    /// 특정 포탈 문자의 쌍을 matrix에서 제거합니다.
    /// </summary>
    private void RemovePortalPairFromMatrix(char portalChar)
    {
        for (int r = 0; r < currentN; r++)
            for (int c = 0; c < currentN; c++)
                if (matrix[r, c] == portalChar)
                    editorGrid.SetCell(r, c, '0');

        currentData.portalPairs.RemoveAll(p => p.portalChar == portalChar);
        pendingPortals.Remove(portalChar);
    }

    /// <summary>
    /// 특정 위치의 포탈 삭제 시 쌍을 함께 제거합니다.
    /// </summary>
    private void RemovePortalPair(char portalChar, int row, int col)
    {
        // 반대쪽 포탈도 제거
        for (int r = 0; r < currentN; r++)
        {
            for (int c = 0; c < currentN; c++)
            {
                if (matrix[r, c] == portalChar && !(r == row && c == col))
                    editorGrid.SetCell(r, c, '0');
            }
        }
        currentData.portalPairs.RemoveAll(p => p.portalChar == portalChar);
        pendingPortals.Remove(portalChar);
    }

    // ───────────────────────────────────────────────────────────
    // 아이템 바 콜백
    // ───────────────────────────────────────────────────────────

    private void OnElementSelected(char element)
    {
        editorGrid.SetSelectedElement(element);
    }

    private void OnThemeSettingsChanged(bool minerOn, bool outlawOn, int minerCnt, int outlawCnt)
    {
        if (currentData == null) return;
        currentData.themeFlags[1] = minerOn;
        currentData.themeFlags[2] = outlawOn;
        currentData.minerSkillCount  = minerOn  ? minerCnt  : 0;
        currentData.outlawSkillCount = outlawOn ? outlawCnt : 0;
    }

    private void OnBgThemeChanged(string theme)
    {
        if (currentData != null) currentData.bgTheme = theme;
    }

    // ───────────────────────────────────────────────────────────
    // 테마 플래그 자동 갱신
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 matrix를 분석해 판타지/미래 테마 플래그를 자동 갱신합니다.
    /// - 판타지: 'e' 또는 'j'가 존재하면 활성
    /// - 미래: '2'~'9' 또는 'r'이 존재하면 활성
    /// </summary>
    private void UpdateThemeFlags()
    {
        if (currentData == null || matrix == null) return;

        bool hasFantasy = false;
        bool hasFuture  = false;

        for (int r = 0; r < currentN; r++)
        {
            for (int c = 0; c < currentN; c++)
            {
                char ch = matrix[r, c];
                if (ch == 'e' || ch == 'j') hasFantasy = true;
                if ((ch >= '2' && ch <= '9') || ch == 'r') hasFuture = true;
            }
        }

        currentData.themeFlags[3] = hasFantasy;
        currentData.themeFlags[4] = hasFuture;

        // 기본 테마: 다른 테마가 없을 때
        bool anyTheme = currentData.themeFlags[1] || currentData.themeFlags[2]
                     || currentData.themeFlags[3] || currentData.themeFlags[4];
        currentData.themeFlags[0] = !anyTheme;
    }

    // ───────────────────────────────────────────────────────────
    // 저장 전 데이터 동기화
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 저장 직전에 currentData를 최신 상태로 동기화합니다.
    /// </summary>
    private void SyncDataFromEditor()
    {
        if (currentData == null) return;

        currentData.mazeName = mazeNameInput != null ? mazeNameInput.text : currentData.mazeName;
        currentData.mazeSize = currentN;
        currentData.SetMatrix(matrix);
        currentData.bgTheme        = itemBar.GetBgTheme();
        currentData.themeFlags[1]  = itemBar.IsMinerActive();
        currentData.themeFlags[2]  = itemBar.IsOutlawActive();
        currentData.minerSkillCount  = itemBar.GetMinerCount();
        currentData.outlawSkillCount = itemBar.GetOutlawCount();
        UpdateThemeFlags(); // 판타지/미래 최종 반영
    }

    // ───────────────────────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────────────────────

    private bool HasAnyNonZeroContent()
    {
        if (matrix == null) return false;
        for (int r = 0; r < currentN; r++)
            for (int c = 0; c < currentN; c++)
                if (matrix[r, c] != '0') return true;
        return false;
    }

    /// <summary>
    /// 크기 축소 후 고아 포탈(쌍의 한쪽이 잘린 경우)을 제거합니다.
    /// </summary>
    private void CleanOrphanPortals(char[,] m, int n)
    {
        Dictionary<char, int> count = new Dictionary<char, int>();
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                char ch = m[r, c];
                if (ch >= '2' && ch <= '9')
                {
                    if (!count.ContainsKey(ch)) count[ch] = 0;
                    count[ch]++;
                }
            }
        }

        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                char ch = m[r, c];
                if (ch >= '2' && ch <= '9' && count.ContainsKey(ch) && count[ch] != 2)
                    m[r, c] = '0';
            }
    }

    // ───────────────────────────────────────────────────────────
    // 확인 팝업
    // ───────────────────────────────────────────────────────────

    private System.Action pendingConfirmAction;

    private void ShowConfirm(string message, System.Action onOk)
    {
        if (confirmPanel == null) { onOk?.Invoke(); return; }
        if (confirmMessage != null) confirmMessage.text = message;
        pendingConfirmAction = onOk;
        confirmPanel.SetActive(true);
    }

    private void OnConfirmOk()
    {
        confirmPanel?.SetActive(false);
        pendingConfirmAction?.Invoke();
        pendingConfirmAction = null;
    }

    // ───────────────────────────────────────────────────────────
    // 상태 메시지
    // ───────────────────────────────────────────────────────────

    private void ShowStatus(string message, bool isError = false)
    {
        if (statusText == null) return;
        statusText.text  = message;
        statusText.color = isError ? new Color(1f, 0.35f, 0.35f) : new Color(0.35f, 1f, 0.35f);
        statusText.gameObject.SetActive(true);

        if (statusHideCoroutine != null) StopCoroutine(statusHideCoroutine);
        statusHideCoroutine = StartCoroutine(HideStatusAfterDelay(3f));
    }

    private IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    // ───────────────────────────────────────────────────────────
    // 파일 브라우저 아이템 기본 생성
    // ───────────────────────────────────────────────────────────

    private GameObject CreateDefaultFileItem(string label, Transform parent)
    {
        GameObject go = new GameObject("FileItem", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.28f);

        Button btn = go.AddComponent<Button>();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 50f);

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.offsetMin = new Vector2(10f, 0f);

        TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 14;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return go;
    }
}