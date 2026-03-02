using UnityEngine;


// 视野可视化组件，显示怪物的视野范围和角度
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FOVVisualizer : MonoBehaviour
{
    private MeshFilter viewMeshFilter;
    private Mesh viewMesh;
    private MonsterBase monster;

    // 网格精细度（越大越圆滑）
    [Header ("网格精细度")]
    public int meshResolution = 10;
    [Header("每次计算的边缘检测次数")]
    public int edgeResolveIterations = 4;
    [Header("边缘检测阈值")]
    public float edgeDstThreshold = 0.5f;

    // 可视化颜色
    [Header("可视化颜色")]
    public Color meshColor = new Color(1, 1, 0, 0.3f); // 半透明黄色

    void Start()
    {
        viewMeshFilter = GetComponent<MeshFilter>();
        viewMesh = new Mesh();
        viewMesh.name = "View Mesh";
        viewMeshFilter.mesh = viewMesh;

        // 设置材质（使用简单的透明材质）
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")); // 使用默认Sprite Shader支持透明
        renderer.material.color = meshColor;

        // 获取怪物引用
        monster = GetComponentInParent<MonsterBase>();
        if (monster == null)
        {
            Debug.LogError("FOVVisualizer needs to be a child of MonsterBase or on the same object.");
            enabled = false;
        }
        
        
        transform.localPosition = new Vector3(0,-0.1f, 0);
    }

    void LateUpdate() // 使用LateUpdate确保在怪物移动后更新
    {
        if (monster == null) return;
        DrawFieldOfView();
    }


    // 绘制视野扇形
    void DrawFieldOfView()
    {
        float viewAngle = monster.viewAngle;
        float viewRadius = monster.viewRange;

        int stepCount = Mathf.RoundToInt(viewAngle * meshResolution / 10f); // 根据角度计算步数
        if (stepCount < 2) stepCount = 2; // 至少是个三角形

        float stepAngleSize = viewAngle / stepCount;
        
        // 创建顶点列表
        // 顶点数 = 步数 + 1 (圆弧点) + 1 (圆心)
        Vector3[] vertices = new Vector3[stepCount + 1 + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[stepCount * 3];

        // 圆心 (本地坐标 0,0,0)
        vertices[0] = Vector3.zero;

        // 起始角度：怪物前方向左偏一般角度
        //本地空间计算，前方向是 0 度
        float currentAngle = -viewAngle / 2;

        for (int i = 0; i <= stepCount; i++)
        {
            // DirFromAngle 返回的是世界方向，转回本地，或者直接用三角函数算本地
            // sin/cos 算本地坐标
            float rad = Mathf.Deg2Rad * (currentAngle + 90); // +90是因为Unity Z轴向前，0度对应X轴
            float radAngle = Mathf.Deg2Rad * (currentAngle); // currentAngle是相对于前方的偏角
            Vector3 vertexPos = new Vector3(Mathf.Sin(radAngle) * viewRadius, 0, Mathf.Cos(radAngle) * viewRadius);

            vertices[i + 1] = vertexPos;

            if (i > 0)
            {
                triangles[(i - 1) * 3] = 0;        // 圆心
                triangles[(i - 1) * 3 + 1] = i + 1; // 当前点
                triangles[(i - 1) * 3 + 2] = i;     // 上一个点
            }

            currentAngle += stepAngleSize;
        }

        viewMesh.Clear();
        viewMesh.vertices = vertices;
        viewMesh.triangles = triangles;
        viewMesh.RecalculateNormals();
    }
}