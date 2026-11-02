using UnityEngine;
namespace MyGame
{

}
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadQuad : MonoBehaviour
{
    [Header("サイズ設定")]
    public float width = 1.0f;
    public float height = 1.0f;

    void Awake() => GenerateMesh();

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) GenerateMesh();
        };
    }
#endif

    public void GenerateMesh()
    {
        float w = width * 0.5f;
        float h = height * 0.5f;

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-w, 0,  h),
            new Vector3( w, 0,  h),
            new Vector3( w, 0, -h),
            new Vector3(-w, 0, -h),
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
            new Vector2(0, 0),
        };

        // 表＋裏（両面）
        int[] triangles = new int[]
        {
            0, 1, 2,  0, 2, 3,  // 表
            0, 2, 1,  0, 3, 2,  // 裏
        };

        Mesh mesh = new Mesh();
        mesh.name = "RoadQuad";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}