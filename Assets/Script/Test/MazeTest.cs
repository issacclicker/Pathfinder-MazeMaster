using UnityEngine;

public class MazeTest : MonoBehaviour
{
    // 미래 + 판타지 + 광부 복합 테마 적용 테스트 (사이즈 15 * 15)

    [SerializeField]
    private GameObject mazeRenderer;

    [SerializeField] [Tooltip("미로 크기")]
    int mazeSize = 3;

    // 인덱스: 0:기본, 1:광부, 2:무법자, 3:판타지, 4:미래
    [SerializeField] [Tooltip("인덱스: 0:기본, 1:광부, 2:무법자, 3:판타지, 4:미래")]
    bool[] myThemes = new bool[] { false, true, false, true, true };
    void Start()
    {
        MazeGenerator generator = gameObject.AddComponent<MazeGenerator>();

        // 미래 + 판타지 + 광부 복합 테마 적용 테스트 (사이즈 15 * 15)
        // 인덱스: 0:기본, 1:광부, 2:무법자, 3:판타지, 4:미래
        // bool[] myThemes = new bool[] { false, true, false, true, true }; 

        Maze resultMaze = generator.GenerateMaze(mazeSize, myThemes);

        // 결과 출력 로그
        Debug.Log($"[미로 생성 완료] 난이도: {resultMaze.difficulty}");
        if (myThemes[1]) Debug.Log($"광부 곡괭이 횟수: {resultMaze.minerSkillCount}");
        if (myThemes[2]) Debug.Log($"무법자 폭탄 횟수: {resultMaze.outlawSkillCount}");

        // 미로 맵 눈으로 확인하기
        string mazeGridText = "";
        int length = resultMaze.matrix.GetLength(0);
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < length; j++)
            {
                mazeGridText += resultMaze.matrix[i, j] + " ";
            }
            mazeGridText += "\n";
        }
        Debug.Log(mazeGridText);

        mazeRenderer.GetComponent<MazeRenderer>().RenderMaze(resultMaze);
    }
}