using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 에디터 우측 아이템 바를 관리합니다.
///
/// 담당 기능:
///   - 배치 요소 버튼 (길/벽/시작/도착/판타지/미래 요소들)
///   - 포탈 색 선택 UI (색 먼저 선택 후 2개 찍으면 쌍 완성)
///   - 광부/무법자 테마 통합 섹션 (없음/광부/무법자 토글 + 슬라이더 1개)
///   - 배경 테마 드롭다운
///
/// [씬 변경 사항]
///   기존 minerToggle, outlawToggle, minerCountSlider, outlawCountSlider,
///   minerCountLabel, outlawCountLabel 필드가 제거되었습니다.
///   아래 새 필드로 교체하세요:
///     noneToggle, minerToggle, outlawToggle  (Toggle 각 1개)
///     skillCountSlider                        (Slider 1개)
///     skillCountLabel                         (TMP_Text 1개)
/// </summary>
public class MazeEditorItemBar : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결
    // ───────────────────────────────────────────────────────────

    [Header("── 요소 버튼 컨테이너 ─────────────────")]

    [Tooltip("요소 버튼들이 배치될 부모 Transform\n" +
             "GridLayoutGroup + ContentSizeFitter(Vertical: Preferred Size) 권장")]
    [SerializeField] private Transform elementButtonContainer;

    [Tooltip("요소 버튼 프리팹 (Button + TMP_Text 포함).\n" +
             "비워두면 자동 생성합니다.")]
    [SerializeField] private GameObject elementButtonPrefab;

    [Header("── 포탈 색 선택 ─────────────────────")]

    [Tooltip("포탈 색 버튼들이 배치될 부모 Transform\n" +
             "GridLayoutGroup + ContentSizeFitter(Vertical: Preferred Size) 권장")]
    [SerializeField] private Transform portalColorContainer;

    [Tooltip("포탈 색 버튼 프리팹.\n" +
             "비워두면 자동 생성합니다.")]
    [SerializeField] private GameObject portalColorButtonPrefab;

    [Header("── 광부 / 무법자 테마 (통합) ────────────")]

    [Tooltip("'없음' 토글: 광부/무법자 테마를 사용하지 않습니다.\n" +
             "이 토글이 켜지면 슬라이더가 비활성화됩니다.")]
    [SerializeField] private Toggle noneToggle;

    [Tooltip("'광부' 토글: 광부 테마를 활성화합니다.\n" +
             "슬라이더가 '곡괭이 횟수'를 나타냅니다.")]
    [SerializeField] private Toggle minerToggle;

    [Tooltip("'무법자' 토글: 무법자 테마를 활성화합니다.\n" +
             "슬라이더가 '폭탄 횟수'를 나타냅니다.")]
    [SerializeField] private Toggle outlawToggle;

    [Tooltip("스킬 사용 횟수 슬라이더 (1~5).\n" +
             "'없음' 선택 시 비활성화됩니다.")]
    [SerializeField] private Slider skillCountSlider;

    [Tooltip("슬라이더 값 표시 텍스트.\n" +
             "선택된 토글에 따라 '곡괭이 횟수: N회' 또는 '폭탄 횟수: N회'로 자동 변경됩니다.")]
    [SerializeField] private TMP_Text skillCountLabel;

    [Header("── 배경 테마 ────────────────────────")]

    [Tooltip("배경 테마 선택 드롭다운.\n" +
             "옵션 목록은 런타임에 자동으로 채워집니다.")]
    [SerializeField] private TMP_Dropdown bgThemeDropdown;

    [Header("── 버튼 크기 설정 ────────────────────")]

    [Tooltip("요소 버튼 셀 크기 (픽셀).\n" +
             "GridLayoutGroup의 Cell Size에 반영됩니다.\n" +
             "패널 너비에 맞게 조정하세요.")]
    [SerializeField] private Vector2 elementCellSize = new Vector2(90f, 44f);

    [Tooltip("포탈 버튼 셀 크기 (픽셀).")]
    [SerializeField] private Vector2 portalCellSize = new Vector2(62f, 44f);

    [Tooltip("버튼 텍스트 폰트 크기")]
    [SerializeField] private float buttonFontSize = 16f;

    [Header("── 폰트 설정 ─────────────────────────")]

    [Tooltip("버튼 텍스트에 사용할 TMP Font Asset.\n" +
             "비워두면 TMP 기본 폰트를 사용합니다.\n" +
             "한글 사용 시 한글 문자셋이 포함된 Font Asset을 지정하세요.")]
    [SerializeField] private TMP_FontAsset buttonFont;

    [Header("── 색상 설정 ─────────────────────────")]

    [Tooltip("포탈 버튼 색상 목록 (인덱스 0='2', 인덱스 7='9').\n" +
             "MazeRenderer의 colorPortals 배열과 동일하게 맞추세요.")]
    [SerializeField] private Color[] portalColors = new Color[]
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
    // 배치 요소 정의
    // ───────────────────────────────────────────────────────────

    private static readonly List<ElementDef> ElementDefs = new List<ElementDef>
    {
        new ElementDef('0', "길",     new Color(0.95f, 0.93f, 0.88f)),
        new ElementDef('1', "벽",     new Color(0.20f, 0.20f, 0.22f)),
        new ElementDef('s', "시작",   new Color(0.20f, 0.75f, 0.30f)),
        new ElementDef('d', "도착",   new Color(0.90f, 0.25f, 0.25f)),
        new ElementDef('e', "적",     new Color(0.85f, 0.15f, 0.55f)),
        new ElementDef('j', "점프대", new Color(0.20f, 0.60f, 1.00f)),
        new ElementDef('r', "레이저", new Color(1.00f, 0.55f, 0.00f)),
    };

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    private char   selectedElement  = '1';
    private char   selectedPortalChar = '2';
    private Button currentSelectedButton;

    // 현재 선택된 테마 상태
    // 0: 없음, 1: 광부, 2: 무법자
    private int currentThemeMode = 0;

    // ───────────────────────────────────────────────────────────
    // 콜백
    // ───────────────────────────────────────────────────────────

    /// <summary>배치 요소가 변경될 때 호출됩니다. (char: 선택된 요소 문자)</summary>
    public System.Action<char> OnElementSelected;

    /// <summary>
    /// 테마 설정이 변경될 때 호출됩니다.
    /// (광부활성, 무법자활성, 광부횟수, 무법자횟수)
    /// </summary>
    public System.Action<bool, bool, int, int> OnThemeSettingsChanged;

    /// <summary>배경 테마가 변경될 때 호출됩니다. (string: 테마 이름)</summary>
    public System.Action<string> OnBgThemeChanged;

    // ───────────────────────────────────────────────────────────
    // 배경 테마 목록
    // ───────────────────────────────────────────────────────────

    private static readonly List<string> BgThemes = new List<string>
    {
        "Default", "Forest", "Dungeon", "Space", "Ice"
    };

    // ───────────────────────────────────────────────────────────
    // 초기화
    // ───────────────────────────────────────────────────────────

    private void Awake()
    {
        GenerateElementButtons();
        GeneratePortalColorButtons();
        SetupSkillThemeControls();
        SetupBgThemeDropdown();

        // 런타임 버튼 생성 후 레이아웃 강제 재계산
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    // ───────────────────────────────────────────────────────────
    // 요소 버튼 생성
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// ElementDefs 목록을 기반으로 요소 선택 버튼을 런타임에 자동 생성합니다.
    /// elementButtonContainer의 GridLayoutGroup에 cellSize를 적용합니다.
    /// </summary>
    private void GenerateElementButtons()
    {
        if (elementButtonContainer == null)
        {
            Debug.LogWarning("[MazeEditorItemBar] elementButtonContainer가 연결되지 않았습니다.");
            return;
        }

        // GridLayoutGroup 셀 크기 적용
        GridLayoutGroup glg = elementButtonContainer.GetComponent<GridLayoutGroup>();
        if (glg != null) glg.cellSize = elementCellSize;

        foreach (ElementDef def in ElementDefs)
        {
            GameObject btnGo = elementButtonPrefab != null
                ? Instantiate(elementButtonPrefab, elementButtonContainer)
                : CreateDefaultButton(def.label, elementButtonContainer);

            Button  btn = btnGo.GetComponent<Button>();
            Image   img = btnGo.GetComponent<Image>();
            if (img != null) img.color = def.color;

            TMP_Text lbl = btnGo.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.text     = def.label;
                lbl.color    = GetContrastColor(def.color);
                lbl.fontSize = buttonFontSize;
                if (buttonFont != null) lbl.font = buttonFont;
            }

            char   capturedElement = def.element;
            Button capturedBtn     = btn;
            btn.onClick.AddListener(() => SelectElement(capturedElement, capturedBtn));
        }
    }

    /// <summary>
    /// 포탈 색 선택 버튼을 런타임에 자동 생성합니다. ('2'~'9', 최대 8개)
    /// </summary>
    private void GeneratePortalColorButtons()
    {
        if (portalColorContainer == null) return;

        // GridLayoutGroup 셀 크기 적용
        GridLayoutGroup glg = portalColorContainer.GetComponent<GridLayoutGroup>();
        if (glg != null) glg.cellSize = portalCellSize;

        for (int i = 0; i < 8; i++)
        {
            char   capturedPortal = (char)('2' + i);
            Color  color          = i < portalColors.Length ? portalColors[i] : Color.white;
            string labelText      = $"포탈 {i + 1}";

            GameObject btnGo = portalColorButtonPrefab != null
                ? Instantiate(portalColorButtonPrefab, portalColorContainer)
                : CreateDefaultButton(labelText, portalColorContainer);

            Image imgComp = btnGo.GetComponent<Image>();
            if (imgComp != null) imgComp.color = color;

            TMP_Text lbl = btnGo.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.text     = labelText;
                lbl.color    = GetContrastColor(color);
                lbl.fontSize = buttonFontSize;
                if (buttonFont != null) lbl.font = buttonFont;
            }

            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                selectedPortalChar = capturedPortal;
                SelectElement(capturedPortal, btn);
            });
        }
    }

    // ───────────────────────────────────────────────────────────
    // 광부/무법자 통합 테마 컨트롤
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 통합 테마 섹션을 설정합니다.
    ///
    /// 토글 3개(없음/광부/무법자)는 라디오 버튼처럼 동작합니다.
    /// 하나를 켜면 나머지 둘은 강제로 꺼집니다.
    /// '없음' 선택 시 슬라이더가 잠깁니다.
    /// </summary>
    private void SetupSkillThemeControls()
    {
        // 슬라이더 초기 설정
        if (skillCountSlider != null)
        {
            skillCountSlider.minValue     = 1;
            skillCountSlider.maxValue     = 5;
            skillCountSlider.wholeNumbers = true;
            skillCountSlider.value        = 3;
            skillCountSlider.onValueChanged.AddListener(_ => RefreshSkillLabel());
        }

        // ── 없음 토글 ──
        if (noneToggle != null)
        {
            noneToggle.isOn = true; // 초기값: 없음
            noneToggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return; // 다른 토글이 켜지면서 발생하는 false 이벤트 무시
                currentThemeMode = 0;
                // 나머지 토글 강제 해제 (이벤트 루프 방지를 위해 isOn 체크 후 해제)
                if (minerToggle  != null && minerToggle.isOn)  minerToggle.isOn  = false;
                if (outlawToggle != null && outlawToggle.isOn) outlawToggle.isOn = false;
                RefreshSkillControls();
                NotifyThemeChanged();
            });
        }

        // ── 광부 토글 ──
        if (minerToggle != null)
        {
            minerToggle.isOn = false;
            minerToggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                currentThemeMode = 1;
                if (noneToggle   != null && noneToggle.isOn)   noneToggle.isOn   = false;
                if (outlawToggle != null && outlawToggle.isOn) outlawToggle.isOn = false;
                RefreshSkillControls();
                NotifyThemeChanged();
            });
        }

        // ── 무법자 토글 ──
        if (outlawToggle != null)
        {
            outlawToggle.isOn = false;
            outlawToggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                currentThemeMode = 2;
                if (noneToggle  != null && noneToggle.isOn)  noneToggle.isOn  = false;
                if (minerToggle != null && minerToggle.isOn) minerToggle.isOn = false;
                RefreshSkillControls();
                NotifyThemeChanged();
            });
        }

        // 초기 상태 적용
        RefreshSkillControls();
    }

    /// <summary>
    /// 현재 테마 모드에 따라 슬라이더 활성화 및 레이블 텍스트를 갱신합니다.
    /// </summary>
    private void RefreshSkillControls()
    {
        bool sliderActive = (currentThemeMode != 0); // 없음이 아닐 때만 활성

        if (skillCountSlider != null)
            skillCountSlider.interactable = sliderActive;

        RefreshSkillLabel();
    }

    /// <summary>
    /// 선택된 토글에 따라 슬라이더 레이블 텍스트를 갱신합니다.
    /// - 없음:    "(테마 없음)"
    /// - 광부:    "곡괭이 횟수 : N회"
    /// - 무법자:  "폭탄 횟수 : N회"
    /// </summary>
    private void RefreshSkillLabel()
    {
        if (skillCountLabel == null) return;

        int val = skillCountSlider != null ? (int)skillCountSlider.value : 3;

        skillCountLabel.text = currentThemeMode switch
        {
            1 => $"곡괭이 횟수 : {val}회",
            2 => $"폭탄 횟수 : {val}회",
            _ => "(테마 없음)"
        };

        // 없음 상태일 때 레이블을 회색으로 표시
        skillCountLabel.color = currentThemeMode == 0
            ? new Color(0.5f, 0.5f, 0.5f)
            : Color.white;
    }

    private void NotifyThemeChanged()
    {
        int val       = skillCountSlider != null ? (int)skillCountSlider.value : 3;
        bool minerOn  = currentThemeMode == 1;
        bool outlawOn = currentThemeMode == 2;
        int  minerCnt  = minerOn  ? val : 0;
        int  outlawCnt = outlawOn ? val : 0;
        OnThemeSettingsChanged?.Invoke(minerOn, outlawOn, minerCnt, outlawCnt);
    }

    // ───────────────────────────────────────────────────────────
    // 배경 테마 드롭다운
    // ───────────────────────────────────────────────────────────

    private void SetupBgThemeDropdown()
    {
        if (bgThemeDropdown == null) return;

        bgThemeDropdown.ClearOptions();
        bgThemeDropdown.AddOptions(BgThemes);
        bgThemeDropdown.onValueChanged.AddListener(idx =>
        {
            string theme = idx < BgThemes.Count ? BgThemes[idx] : "Default";
            OnBgThemeChanged?.Invoke(theme);
        });
    }

    // ───────────────────────────────────────────────────────────
    // 요소 선택
    // ───────────────────────────────────────────────────────────

    private void SelectElement(char element, Button btn)
    {
        selectedElement = element;
        currentSelectedButton = btn;
        OnElementSelected?.Invoke(element);
    }

    // ───────────────────────────────────────────────────────────
    // 공개 접근자
    // ───────────────────────────────────────────────────────────

    public char   GetSelectedElement()    => selectedElement;
    public char   GetSelectedPortalChar() => selectedPortalChar;
    public bool   IsMinerActive()         => currentThemeMode == 1;
    public bool   IsOutlawActive()        => currentThemeMode == 2;
    public int    GetMinerCount()         => IsMinerActive()  && skillCountSlider != null ? (int)skillCountSlider.value : 0;
    public int    GetOutlawCount()        => IsOutlawActive() && skillCountSlider != null ? (int)skillCountSlider.value : 0;
    public string GetBgTheme()            => bgThemeDropdown  != null ? BgThemes[bgThemeDropdown.value] : "Default";

    /// <summary>
    /// 저장 파일을 불러온 뒤 UI 상태를 복원합니다.
    /// </summary>
    public void LoadFromData(MazeEditorData data)
    {
        bool isMiner  = data.themeFlags.Length > 1 && data.themeFlags[1];
        bool isOutlaw = data.themeFlags.Length > 2 && data.themeFlags[2];

        // 라디오 토글 복원: 이벤트 발생 순서 때문에 noneToggle을 먼저 끄고 설정
        if (isMiner)
        {
            if (minerToggle  != null) minerToggle.isOn  = true;
        }
        else if (isOutlaw)
        {
            if (outlawToggle != null) outlawToggle.isOn = true;
        }
        else
        {
            if (noneToggle   != null) noneToggle.isOn   = true;
        }

        // 슬라이더 복원 (광부/무법자 중 활성화된 쪽의 횟수 사용)
        int savedCount = isMiner ? data.minerSkillCount : (isOutlaw ? data.outlawSkillCount : 3);
        if (skillCountSlider != null)
            skillCountSlider.value = Mathf.Clamp(savedCount, 1, 5);

        // 배경 테마 복원
        if (bgThemeDropdown != null)
        {
            int idx = BgThemes.IndexOf(data.bgTheme);
            bgThemeDropdown.value = idx >= 0 ? idx : 0;
        }

        RefreshSkillControls();
    }

    // ───────────────────────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 프리팹 없이 기본 Button + TMP_Text GameObject를 생성합니다.
    /// buttonFont와 buttonFontSize가 자동 적용됩니다.
    /// </summary>
    private GameObject CreateDefaultButton(string labelText, Transform parent)
    {
        GameObject go = new GameObject(labelText, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = Color.gray;
        go.AddComponent<Button>();

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.offsetMin = new Vector2(4f, 2f);
        textRt.offsetMax = new Vector2(-4f, -2f);

        TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text                   = labelText;
        tmp.alignment              = TextAlignmentOptions.Center;
        tmp.fontSize               = buttonFontSize;
        tmp.enableAutoSizing       = true;
        tmp.fontSizeMin            = 8f;
        tmp.fontSizeMax            = buttonFontSize;
        tmp.overflowMode           = TextOverflowModes.Ellipsis;
        if (buttonFont != null) tmp.font = buttonFont;

        return go;
    }

    /// <summary>배경색 밝기에 따라 대비되는 글자색(흰/검)을 반환합니다.</summary>
    private Color GetContrastColor(Color bg)
    {
        float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return luminance > 0.5f ? Color.black : Color.white;
    }
}

// ───────────────────────────────────────────────────────────
// 요소 정의 데이터 클래스
// ───────────────────────────────────────────────────────────

public class ElementDef
{
    public char   element;
    public string label;
    public Color  color;

    public ElementDef(char element, string label, Color color)
    {
        this.element = element;
        this.label   = label;
        this.color   = color;
    }
}