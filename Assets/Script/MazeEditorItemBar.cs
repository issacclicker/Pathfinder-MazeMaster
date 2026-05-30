using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 에디터 우측 아이템 바를 관리합니다.
///
/// 담당 기능:
///   - 배치 요소 버튼 (길/벽/시작/도착/판타지/미래 요소들)
///   - 포탈 색 선택 UI (안 A: 색 먼저 선택 후 2개 찍으면 쌍 완성)
///   - 광부/무법자 테마 토글 + 횟수 슬라이더
///   - 배경 테마 드롭다운
///
/// [씬 설정 방법]
///   Inspector에서 각 버튼/슬라이더/토글을 직접 연결하거나,
///   GenerateButtonsAtRuntime()을 호출해 런타임에 자동 생성합니다.
/// </summary>
public class MazeEditorItemBar : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결
    // ───────────────────────────────────────────────────────────

    [Header("── 요소 버튼 컨테이너 ─────────────────")]

    [Tooltip("요소 버튼들이 배치될 부모 Transform (VerticalLayoutGroup 권장)")]
    [SerializeField] private Transform elementButtonContainer;

    [Tooltip("요소 버튼 프리팹 (Button + TMP_Text 포함)")]
    [SerializeField] private GameObject elementButtonPrefab;

    [Header("── 포탈 색 선택 ─────────────────────")]

    [Tooltip("포탈 색 버튼들이 배치될 부모 Transform")]
    [SerializeField] private Transform portalColorContainer;

    [Tooltip("포탈 색 버튼 프리팹")]
    [SerializeField] private GameObject portalColorButtonPrefab;

    [Header("── 광부 / 무법자 테마 ──────────────────")]

    [Tooltip("광부 테마 활성화 토글")]
    [SerializeField] private Toggle minerToggle;

    [Tooltip("무법자 테마 활성화 토글")]
    [SerializeField] private Toggle outlawToggle;

    [Tooltip("광부 곡괭이 횟수 슬라이더 (1~5)")]
    [SerializeField] private Slider minerCountSlider;

    [Tooltip("무법자 폭탄 횟수 슬라이더 (1~5)")]
    [SerializeField] private Slider outlawCountSlider;

    [Tooltip("광부 곡괭이 횟수 표시 텍스트")]
    [SerializeField] private TMP_Text minerCountLabel;

    [Tooltip("무법자 폭탄 횟수 표시 텍스트")]
    [SerializeField] private TMP_Text outlawCountLabel;

    [Header("── 배경 테마 ────────────────────────")]

    [Tooltip("배경 테마 선택 드롭다운")]
    [SerializeField] private TMP_Dropdown bgThemeDropdown;

    [Header("── 색상 설정 ─────────────────────────")]

    [Tooltip("MazeRenderer와 동일한 색상 배열 참조 (포탈 버튼 색상 표시용)")]
    [SerializeField] private Color[] portalColors = new Color[]
    {
        new Color(0.40f, 0.00f, 0.80f),
        new Color(0.00f, 0.80f, 0.80f),
        new Color(1.00f, 0.85f, 0.00f),
        new Color(0.00f, 0.50f, 1.00f),
        new Color(1.00f, 0.40f, 0.70f),
        new Color(0.60f, 0.90f, 0.20f),
        new Color(0.60f, 0.35f, 0.10f),
        new Color(0.70f, 0.70f, 0.70f),
    };

    // ───────────────────────────────────────────────────────────
    // 배치 요소 정의
    // ───────────────────────────────────────────────────────────

    /// <summary>배치 가능한 요소 목록 (버튼 자동 생성에 사용)</summary>
    private static readonly List<ElementDef> ElementDefs = new List<ElementDef>
    {
        new ElementDef('0', "길",       new Color(0.95f, 0.93f, 0.88f)),
        new ElementDef('1', "벽",       new Color(0.20f, 0.20f, 0.22f)),
        new ElementDef('s', "시작",     new Color(0.20f, 0.75f, 0.30f)),
        new ElementDef('d', "도착",     new Color(0.90f, 0.25f, 0.25f)),
        new ElementDef('e', "적",       new Color(0.85f, 0.15f, 0.55f)),
        new ElementDef('j', "점프대",   new Color(0.20f, 0.60f, 1.00f)),
        new ElementDef('r', "레이저",   new Color(1.00f, 0.55f, 0.00f)),
    };

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    // 현재 선택된 요소
    private char selectedElement = '1';

    // 포탈 선택 시 현재 선택된 포탈 문자 ('2'~'9')
    private char selectedPortalChar = '2';

    // 현재 선택된 버튼 (하이라이트용)
    private Button currentSelectedButton;

    // 콜백: 선택 요소 변경 시 MazeEditorUI에게 알림
    public System.Action<char> OnElementSelected;

    // 콜백: 테마 설정 변경 시
    public System.Action<bool, bool, int, int> OnThemeSettingsChanged;
    // (광부활성, 무법자활성, 광부횟수, 무법자횟수)

    // 콜백: 배경 테마 변경 시
    public System.Action<string> OnBgThemeChanged;

    // ───────────────────────────────────────────────────────────
    // 배경 테마 목록
    // ───────────────────────────────────────────────────────────

    private static readonly List<string> BgThemes = new List<string>
    {
        "Default", "Forest", "Dungeon", "Space", "Ice"
        // 추후 확장 가능
    };

    // ───────────────────────────────────────────────────────────
    // 초기화
    // ───────────────────────────────────────────────────────────

    private void Awake()
    {
        GenerateElementButtons();
        GeneratePortalColorButtons();
        SetupThemeControls();
        SetupBgThemeDropdown();
    }

    // ───────────────────────────────────────────────────────────
    // 요소 버튼 생성
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// ElementDefs를 기반으로 요소 버튼을 런타임에 자동 생성합니다.
    /// elementButtonPrefab이 없으면 기본 Button을 생성합니다.
    /// </summary>
    private void GenerateElementButtons()
    {
        if (elementButtonContainer == null)
        {
            Debug.LogWarning("[MazeEditorItemBar] elementButtonContainer가 연결되지 않았습니다.");
            return;
        }

        foreach (ElementDef def in ElementDefs)
        {
            GameObject btnGo = elementButtonPrefab != null
                ? Instantiate(elementButtonPrefab, elementButtonContainer)
                : CreateDefaultButton(def.label, elementButtonContainer);

            Button btn = btnGo.GetComponent<Button>();
            Image  img = btnGo.GetComponent<Image>();

            // 버튼 배경색을 요소 색상으로 설정
            if (img != null) img.color = def.color;

            // 레이블 설정
            TMP_Text label = btnGo.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text  = def.label;
                label.color = GetContrastColor(def.color);
            }

            char capturedElement = def.element;
            Button capturedBtn   = btn;
            btn.onClick.AddListener(() =>
            {
                SelectElement(capturedElement, capturedBtn);
            });
        }
    }

    /// <summary>
    /// 포탈 색 선택 버튼을 생성합니다. ('2'~'9', 최대 8개)
    /// </summary>
    private void GeneratePortalColorButtons()
    {
        if (portalColorContainer == null) return;

        for (int i = 0; i < 8; i++)
        {
            int    capturedIndex  = i;
            char   capturedPortal = (char)('2' + i);
            Color  color          = i < portalColors.Length ? portalColors[i] : Color.white;
            string label          = $"포탈 {i + 1}";

            GameObject btnGo = portalColorButtonPrefab != null
                ? Instantiate(portalColorButtonPrefab, portalColorContainer)
                : CreateDefaultButton(label, portalColorContainer);

            Image imgComp = btnGo.GetComponent<Image>();
            if (imgComp != null) imgComp.color = color;

            TMP_Text lbl = btnGo.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.text  = label;
                lbl.color = GetContrastColor(color);
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
    // 테마 컨트롤 설정
    // ───────────────────────────────────────────────────────────

    private void SetupThemeControls()
    {
        // 슬라이더 범위 설정
        if (minerCountSlider != null)
        {
            minerCountSlider.minValue = 1;
            minerCountSlider.maxValue = 5;
            minerCountSlider.wholeNumbers = true;
            minerCountSlider.value = 3;
            minerCountSlider.onValueChanged.AddListener(v =>
            {
                if (minerCountLabel != null) minerCountLabel.text = $"곡괭이: {(int)v}회";
                NotifyThemeChanged();
            });
        }

        if (outlawCountSlider != null)
        {
            outlawCountSlider.minValue = 1;
            outlawCountSlider.maxValue = 5;
            outlawCountSlider.wholeNumbers = true;
            outlawCountSlider.value = 3;
            outlawCountSlider.onValueChanged.AddListener(v =>
            {
                if (outlawCountLabel != null) outlawCountLabel.text = $"폭탄: {(int)v}회";
                NotifyThemeChanged();
            });
        }

        // 광부/무법자 상호 배타 처리
        if (minerToggle != null)
        {
            minerToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn && outlawToggle != null && outlawToggle.isOn)
                    outlawToggle.isOn = false; // 무법자 강제 해제
                SetMinerControlsInteractable(isOn);
                NotifyThemeChanged();
            });
        }

        if (outlawToggle != null)
        {
            outlawToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn && minerToggle != null && minerToggle.isOn)
                    minerToggle.isOn = false; // 광부 강제 해제
                SetOutlawControlsInteractable(isOn);
                NotifyThemeChanged();
            });
        }

        // 초기 비활성 처리
        SetMinerControlsInteractable(false);
        SetOutlawControlsInteractable(false);
    }

    private void SetMinerControlsInteractable(bool on)
    {
        if (minerCountSlider != null) minerCountSlider.interactable = on;
        if (minerCountLabel  != null) minerCountLabel.color = on ? Color.white : Color.gray;
    }

    private void SetOutlawControlsInteractable(bool on)
    {
        if (outlawCountSlider != null) outlawCountSlider.interactable = on;
        if (outlawCountLabel  != null) outlawCountLabel.color = on ? Color.white : Color.gray;
    }

    private void NotifyThemeChanged()
    {
        bool minerOn  = minerToggle  != null && minerToggle.isOn;
        bool outlawOn = outlawToggle != null && outlawToggle.isOn;
        int  minerCnt  = minerCountSlider  != null ? (int)minerCountSlider.value  : 0;
        int  outlawCnt = outlawCountSlider != null ? (int)outlawCountSlider.value : 0;
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

        // 이전 선택 버튼 하이라이트 제거
        if (currentSelectedButton != null)
        {
            ColorBlock cb = currentSelectedButton.colors;
            cb.normalColor = cb.normalColor; // 원래대로 (실제로는 Image.color로 관리)
            currentSelectedButton = null;
        }

        // 새 선택 버튼 테두리 하이라이트
        currentSelectedButton = btn;
        OnElementSelected?.Invoke(element);
    }

    // ───────────────────────────────────────────────────────────
    // 공개 접근자
    // ───────────────────────────────────────────────────────────

    public char GetSelectedElement()    => selectedElement;
    public char GetSelectedPortalChar() => selectedPortalChar;

    public bool IsMinerActive()  => minerToggle  != null && minerToggle.isOn;
    public bool IsOutlawActive() => outlawToggle != null && outlawToggle.isOn;
    public int  GetMinerCount()  => minerCountSlider  != null ? (int)minerCountSlider.value  : 1;
    public int  GetOutlawCount() => outlawCountSlider != null ? (int)outlawCountSlider.value : 1;
    public string GetBgTheme()   => bgThemeDropdown   != null ? BgThemes[bgThemeDropdown.value] : "Default";

    /// <summary>
    /// 불러온 데이터로 UI 상태를 복원합니다.
    /// </summary>
    public void LoadFromData(MazeEditorData data)
    {
        if (minerToggle  != null) minerToggle.isOn  = data.themeFlags.Length > 1 && data.themeFlags[1];
        if (outlawToggle != null) outlawToggle.isOn = data.themeFlags.Length > 2 && data.themeFlags[2];

        if (minerCountSlider  != null) minerCountSlider.value  = Mathf.Clamp(data.minerSkillCount,  1, 5);
        if (outlawCountSlider != null) outlawCountSlider.value = Mathf.Clamp(data.outlawSkillCount, 1, 5);

        if (bgThemeDropdown != null)
        {
            int idx = BgThemes.IndexOf(data.bgTheme);
            bgThemeDropdown.value = idx >= 0 ? idx : 0;
        }
    }

    // ───────────────────────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────────────────────

    /// <summary>프리팹 없이 기본 Button GameObject를 생성합니다.</summary>
    private GameObject CreateDefaultButton(string label, Transform parent)
    {
        GameObject go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = Color.gray;

        Button btn = go.AddComponent<Button>();

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 14;

        return go;
    }

    /// <summary>배경색에 대비되는 글자색(흰/검)을 반환합니다.</summary>
    private Color GetContrastColor(Color bg)
    {
        float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return luminance > 0.5f ? Color.black : Color.white;
    }
}

// ───────────────────────────────────────────────────────────
// 요소 정의 데이터
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