using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 에디터 그리드의 클릭/드래그 입력을 처리합니다.
/// MazeRenderer가 생성한 셀에 EventTrigger를 동적으로 부착해 입력을 감지합니다.
///
/// [동작 방식]
/// - 좌클릭 드래그: 선택된 요소를 셀에 그림
/// - 우클릭: 해당 셀을 '0'(빈 칸)으로 삭제
///
/// [의존 관계]
///   MazeRenderer  — 셀 Image 컴포넌트 접근
///   MazeEditorUI  — 선택된 요소, matrix 데이터 접근
/// </summary>
public class MazeEditorGrid : MonoBehaviour
{
    // ───────────────────────────────────────────────────────────
    // Inspector 연결
    // ───────────────────────────────────────────────────────────

    [Tooltip("MazeRenderer 컴포넌트 참조")]
    [SerializeField] private MazeRenderer mazeRenderer;

    [Tooltip("GridLayoutGroup이 있는 컨테이너")]
    [SerializeField] private RectTransform gridContainer;

    // ───────────────────────────────────────────────────────────
    // 내부 상태
    // ───────────────────────────────────────────────────────────

    // 현재 편집 중인 matrix (MazeEditorUI에서 참조를 공유)
    private char[,] matrix;
    private int mazeN;

    // 드래그 중 여부
    private bool isDragging = false;

    // 현재 선택된 배치 요소 (MazeEditorItemBar에서 설정)
    private char selectedElement = '1';

    // 셀별 EventTrigger 등록 여부 (재등록 방지)
    private bool[,] triggerRegistered;

    // 외부 콜백: 셀이 변경될 때 MazeEditorUI에게 알림
    public System.Action<int, int, char> OnCellChanged;

    // ───────────────────────────────────────────────────────────
    // 공개 API
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 새 matrix로 그리드 입력 시스템을 초기화합니다.
    /// RenderMaze 호출 이후에 실행해야 셀 오브젝트가 존재합니다.
    /// </summary>
    public void InitGrid(char[,] m)
    {
        matrix = m;
        mazeN  = m.GetLength(0);
        triggerRegistered = new bool[mazeN, mazeN];
        RegisterCellTriggers();
    }

    /// <summary>
    /// 현재 선택된 배치 요소를 설정합니다. (MazeEditorItemBar에서 호출)
    /// </summary>
    public void SetSelectedElement(char element)
    {
        selectedElement = element;
    }

    /// <summary>
    /// 현재 편집 중인 matrix 참조를 반환합니다.
    /// </summary>
    public char[,] GetMatrix() => matrix;

    // ───────────────────────────────────────────────────────────
    // 셀 EventTrigger 등록
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 모든 셀에 PointerDown / PointerEnter / PointerUp / PointerClick(우클릭)
    /// 이벤트를 등록합니다.
    /// </summary>
    private void RegisterCellTriggers()
    {
        for (int row = 0; row < mazeN; row++)
        {
            for (int col = 0; col < mazeN; col++)
            {
                if (triggerRegistered[row, col]) continue;

                RectTransform cellRt = mazeRenderer.GetCellRectTransform(row, col);
                if (cellRt == null) continue;

                GameObject cellGo = cellRt.gameObject;

                // Raycast 활성화 (이벤트 수신을 위해 필수)
                Image img = cellGo.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;

                // EventTrigger 부착
                EventTrigger trigger = cellGo.GetComponent<EventTrigger>();
                if (trigger == null) trigger = cellGo.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                int capturedRow = row;
                int capturedCol = col;

                // PointerDown: 드래그 시작 + 첫 셀 적용
                AddTriggerEntry(trigger, EventTriggerType.PointerDown, (data) =>
                {
                    PointerEventData ped = data as PointerEventData;
                    if (ped == null) return;

                    if (ped.button == PointerEventData.InputButton.Left)
                    {
                        isDragging = true;
                        ApplyElement(capturedRow, capturedCol);
                    }
                    else if (ped.button == PointerEventData.InputButton.Right)
                    {
                        EraseElement(capturedRow, capturedCol);
                    }
                });

                // PointerEnter: 드래그 중 연속 적용
                AddTriggerEntry(trigger, EventTriggerType.PointerEnter, (data) =>
                {
                    if (isDragging)
                        ApplyElement(capturedRow, capturedCol);
                });

                // PointerUp: 드래그 종료
                AddTriggerEntry(trigger, EventTriggerType.PointerUp, (data) =>
                {
                    isDragging = false;
                });

                triggerRegistered[row, col] = true;
            }
        }
    }

    /// <summary>
    /// EventTrigger에 이벤트 항목을 추가하는 헬퍼입니다.
    /// </summary>
    private void AddTriggerEntry(EventTrigger trigger, EventTriggerType type,
                                  UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    // ───────────────────────────────────────────────────────────
    // 요소 배치 / 삭제
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// 선택된 요소를 해당 셀에 적용합니다.
    /// 시작점(s)/도착점(d)은 덮어쓰기 불가입니다.
    /// </summary>
    private void ApplyElement(int row, int col)
    {
        if (matrix == null) return;

        char current = matrix[row, col];

        // 시작점/도착점은 덮어쓰기 불가 (단, 선택된 요소가 s/d 본인인 경우는 허용)
        if ((current == 's' || current == 'd') && selectedElement != 's' && selectedElement != 'd')
            return;

        // 시작점을 새로 설정하면 기존 시작점을 '0'으로 초기화
        if (selectedElement == 's')
            ClearExisting('s');

        // 도착점을 새로 설정하면 기존 도착점을 '0'으로 초기화
        if (selectedElement == 'd')
            ClearExisting('d');

        // 포탈 배치 처리 (MazeEditorUI에서 포탈 상태 관리)
        if (selectedElement >= '2' && selectedElement <= '9')
        {
            OnCellChanged?.Invoke(row, col, selectedElement);
            return; // 포탈은 MazeEditorUI에서 쌍 완성 여부를 판단 후 직접 기록
        }

        SetCell(row, col, selectedElement);
    }

    /// <summary>
    /// 해당 셀을 '0'(빈 칸)으로 삭제합니다.
    /// 시작점/도착점도 우클릭으로 삭제 가능합니다.
    /// </summary>
    private void EraseElement(int row, int col)
    {
        if (matrix == null) return;

        char current = matrix[row, col];

        // 포탈 삭제 시 쌍 제거 알림
        if (current >= '2' && current <= '9')
            OnCellChanged?.Invoke(row, col, '0');
        else
            SetCell(row, col, '0');
    }

    /// <summary>
    /// matrix와 UI를 동시에 갱신합니다.
    /// </summary>
    public void SetCell(int row, int col, char element)
    {
        if (matrix == null) return;
        matrix[row, col] = element;
        mazeRenderer.UpdateCell(row, col, element);
        OnCellChanged?.Invoke(row, col, element);
    }

    /// <summary>
    /// matrix 전체에서 특정 문자를 찾아 '0'으로 초기화합니다.
    /// 시작점/도착점 유일성 보장에 사용됩니다.
    /// </summary>
    private void ClearExisting(char target)
    {
        for (int r = 0; r < mazeN; r++)
        {
            for (int c = 0; c < mazeN; c++)
            {
                if (matrix[r, c] == target)
                    SetCell(r, c, '0');
            }
        }
    }

    // ───────────────────────────────────────────────────────────
    // 포탈 외부 직접 쓰기 (MazeEditorUI에서 쌍 확정 후 호출)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// MazeEditorUI가 포탈 쌍 확정 후 matrix와 UI에 직접 기록합니다.
    /// </summary>
    public void SetPortalCell(int row, int col, char portalChar)
    {
        if (matrix == null) return;
        matrix[row, col] = portalChar;
        mazeRenderer.UpdateCell(row, col, portalChar);
    }

    // ───────────────────────────────────────────────────────────
    // Update: 드래그 중 마우스 버튼 뗌 감지 (셀 밖에서 뗀 경우 처리)
    // ───────────────────────────────────────────────────────────

    private void Update()
    {
        // 마우스 버튼을 셀 밖에서 뗀 경우에도 드래그 종료
        if (isDragging && !Input.GetMouseButton(0))
            isDragging = false;
    }
}