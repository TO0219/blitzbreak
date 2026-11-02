using UnityEngine;
using UnityEngine.UI;

public class LoadingWaveGraph : Graphic
{
    [SerializeField, Range(16, 192)] private int sampleCount = 84;
    [SerializeField, Min(1f)] private float lineWidth = 4f;
    [SerializeField, Min(0f)] private float glowWidth = 10f;
    [SerializeField] private Color glowColor = new Color(0.13f, 0.82f, 1f, 0.18f);
    [SerializeField, Min(16f)] private float graphHeight = 210f;
    [SerializeField, Min(0.02f)] private float pointIntervalSeconds = 0.085f;
    [SerializeField, Range(0.02f, 0.45f)] private float maxValueStep = 0.24f;
    [SerializeField, Range(0f, 0.35f)] private float centerPull = 0.08f;
    [SerializeField, Range(-0.5f, 0.5f)] private float verticalOffset = -0.08f;

    private float[] samples;
    private float currentValue = 0.5f;
    private float intervalTimer;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        InitializeSamples();
    }

    private void Update()
    {
        EnsureSamples();

        intervalTimer += Time.unscaledDeltaTime;
        float interval = Mathf.Max(0.02f, pointIntervalSeconds);
        while (intervalTimer >= interval)
        {
            intervalTimer -= interval;
            PushNextSample();
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        EnsureSamples();

        if (rect.width <= 0f || rect.height <= 0f || samples == null || samples.Length < 2)
            return;

        float interval = Mathf.Max(0.02f, pointIntervalSeconds);
        float scrollProgress = Mathf.Clamp01(intervalTimer / interval);
        Vector2 previousPoint = EvaluatePoint(rect, 0, scrollProgress);
        for (int i = 1; i < samples.Length; i++)
        {
            Vector2 point = EvaluatePoint(rect, i, scrollProgress);

            if (glowWidth > 0f && glowColor.a > 0f)
                AddLineSegment(vertexHelper, previousPoint, point, glowWidth, glowColor);

            AddLineSegment(vertexHelper, previousPoint, point, lineWidth, color);
            previousPoint = point;
        }
    }

    private Vector2 EvaluatePoint(Rect rect, int index, float scrollProgress)
    {
        float spacing = rect.width / Mathf.Max(1, sampleCount - 1);
        float x = rect.xMin + (index - scrollProgress) * spacing;
        float centerY = rect.center.y + rect.height * verticalOffset;
        float halfHeight = Mathf.Min(graphHeight, rect.height - lineWidth * 2f) * 0.5f;
        float y = centerY + (samples[index] - 0.5f) * halfHeight * 2f;

        return new Vector2(x, Mathf.Clamp(y, rect.yMin + lineWidth, rect.yMax - lineWidth));
    }

    private void InitializeSamples()
    {
        int requiredLength = Mathf.Max(2, sampleCount + 1);
        samples = new float[requiredLength];
        currentValue = 0.5f;
        intervalTimer = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            currentValue = GenerateNextValue();
            samples[i] = currentValue;
        }
    }

    private void EnsureSamples()
    {
        int requiredLength = Mathf.Max(2, sampleCount + 1);
        if (samples == null || samples.Length != requiredLength)
            InitializeSamples();
    }

    private void PushNextSample()
    {
        EnsureSamples();

        for (int i = 0; i < samples.Length - 1; i++)
            samples[i] = samples[i + 1];

        currentValue = GenerateNextValue();
        samples[^1] = currentValue;
    }

    private float GenerateNextValue()
    {
        float centerCorrection = (0.5f - currentValue) * centerPull;
        float randomStep = Random.Range(-maxValueStep, maxValueStep);
        return Mathf.Clamp01(currentValue + randomStep + centerCorrection);
    }

    private static void AddLineSegment(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color segmentColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int index = vertexHelper.currentVertCount;
        Color32 color32 = segmentColor;

        vertexHelper.AddVert(start - normal, color32, Vector2.zero);
        vertexHelper.AddVert(start + normal, color32, Vector2.zero);
        vertexHelper.AddVert(end + normal, color32, Vector2.zero);
        vertexHelper.AddVert(end - normal, color32, Vector2.zero);

        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index, index + 2, index + 3);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        sampleCount = Mathf.Max(2, sampleCount);
        lineWidth = Mathf.Max(1f, lineWidth);
        glowWidth = Mathf.Max(0f, glowWidth);
        graphHeight = Mathf.Max(16f, graphHeight);
        pointIntervalSeconds = Mathf.Max(0.02f, pointIntervalSeconds);
        SetVerticesDirty();
    }
#endif
}
