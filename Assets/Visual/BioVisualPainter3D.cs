using UnityEngine;

public class BioVisualFlowPainter3D : MonoBehaviour
{
    [Header("Input")]
    public BioDataReaderV2 reader;
    public Camera cam;

    [Header("Optional 3D stamps (can be null)")]
    public GameObject spherePrefab;
    public GameObject cubePrefab;

    [Header("Stage=None (monotone but moving)")]
    public bool monotoneWhenNone = true;
    public Vector2 noneCenterViewport = new Vector2(0.5f, 0.5f);
    public float noneRadius = 0.08f;
    public float noneSpeed = 0.35f;
    public Color noneColor = new Color(0.85f, 0.85f, 0.9f, 1f);
    public bool noneDisableStamps = true;

    [Header("Where to draw (in front of camera)")]
    public float drawDistance = 2.2f;
    public float depthWobble = 0.18f;

    [Header("Viewport clamp (never leaves camera view)")]
    [Range(0f, 0.25f)] public float viewportMargin = 0.08f;
    [Range(0.2f, 1f)] public float fill = 0.98f;

    [Header("Frame mode (more often near edges)")]
    public bool frameMode = true;
    [Range(0f, 1f)] public float edgeBias = 0.75f;

    [Header("Flow / noise")]
    [Range(0f, 1f)] public float flowStrength = 0.18f;
    [Range(0.01f, 2f)] public float noiseSpeed = 0.22f;

    [Header("Line (single renderer, no spawning)")]
    public bool drawLines = true;
    public int maxPoints = 320;
    public float breakLineDistance = 0.30f;

    [Tooltip("Global cap on point rate (prevents too many points per second).")]
    public float maxPointsPerSecond = 120f;

    public float minWidth = 0.0025f;
    public float maxWidth = 0.018f;
    [Range(0f, 1f)] public float lineAlpha = 0.75f;

    [Header("Stamps (optional)")]
    public bool enableStamps = true;
    public float stampEvery = 0.14f;
    public float minStampScale = 0.06f;
    public float maxStampScale = 0.55f;
    public float stampLifetimeSeconds = 8f;

    [Header("Color")]
    public Gradient palette;
    public float colorSpeed = 0.32f;
    public float brightnessBoost = 1.6f;

    // internals
    LineRenderer line;
    int pointCount;
    Vector3 lastPoint;
    float stampTimer;
    float seed;
    float colorPhase;
    float pointCooldown;

    Material _lineMat;

    RunningStats gsrStats = new RunningStats();
    RunningStats ecgStats = new RunningStats();
    RunningStats tempStats = new RunningStats();

    static MaterialPropertyBlock _mpb;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        seed = Random.value * 1000f;

        if (palette == null) palette = DefaultPalette();

        var shader = Shader.Find("Sprites/Default");
        if (shader != null) _lineMat = new Material(shader);

        EnsureLine();
    }

    void EnsureLine()
    {
        if (!drawLines) return;

        // Create or reuse a single LineRenderer on this object
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        if (_lineMat != null) line.sharedMaterial = _lineMat;

        ClearLine();
    }

    void ClearLine()
    {
        if (line == null) return;
        pointCount = 0;
        lastPoint = Vector3.zero;
        line.positionCount = 0;
    }

    void Update()
    {
        if (reader == null || cam == null) return;

        if (drawLines && line == null) EnsureLine();

        // limit point rate globally
        if (maxPointsPerSecond > 1f)
            pointCooldown = Mathf.Max(0f, pointCooldown - Time.deltaTime);

        // --- NONE: monotone movement ---
        if (monotoneWhenNone && reader.stage == BioDataReaderV2.SensorStage.None)
        {
            DrawNoneMonotone();
            return;
        }

        // --- normal mapping ---
        gsrStats.Push(reader.gsrRaw);
        ecgStats.Push(reader.ecgEnergy);
        tempStats.Push(reader.tempC);

        bool ecgOK = reader.EnableECG && !reader.leadOff;

        float g = reader.EnableGSR ? SoftSign(gsrStats.ZScore(reader.gsrRaw) * 1.3f) : 0f;
        float e = ecgOK ? SoftSign(ecgStats.ZScore(reader.ecgEnergy) * 1.5f) : 0f;
        float t = reader.EnableTemp ? SoftSign(tempStats.ZScore(reader.tempC) * 1.2f) : 0f;

        float bpm01 = ecgOK ? Mathf.InverseLerp(40f, 180f, reader.bpm) : 0.25f;

        if (frameMode)
        {
            float power = Mathf.Lerp(1f, 0.35f, edgeBias);
            g = SignedPow(g, power);
            e = SignedPow(e, power);
        }

        float safeMin = viewportMargin;
        float safeMax = 1f - viewportMargin;

        float half = 0.5f * Mathf.Clamp01(fill);
        float minFill = 0.5f - half;
        float maxFill = 0.5f + half;

        float vx = Mathf.Lerp(minFill, maxFill, (g + 1f) * 0.5f);
        float vy = Mathf.Lerp(minFill, maxFill, (e + 1f) * 0.5f);

        float time = Time.time * noiseSpeed;
        float nx = Mathf.PerlinNoise(seed + time, 12.3f + seed) - 0.5f;
        float ny = Mathf.PerlinNoise(45.6f + seed, seed + time) - 0.5f;

        float flow = flowStrength * (0.25f + 0.75f * (0.35f + bpm01));
        vx += nx * flow;
        vy += ny * flow;

        vx += 0.04f * t * Mathf.Sin(time + seed);
        vy += 0.03f * t * Mathf.Cos(time * 0.9f + seed);

        vx = Mathf.Clamp(vx, safeMin, safeMax);
        vy = Mathf.Clamp(vy, safeMin, safeMax);

        float depth = drawDistance + depthWobble * (t * 0.8f + 0.2f * Mathf.Sin(Time.time * 0.6f + seed));
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        colorPhase += Time.deltaTime * (colorSpeed + bpm01 * 0.35f);
        float palT = Mathf.Repeat(colorPhase + 0.18f * ((t + 1f) * 0.5f) + 0.07f * seed, 1f);

        Color baseC = palette.Evaluate(palT);
        float bright = Mathf.Lerp(0.75f, brightnessBoost, (e + 1f) * 0.5f);

        Color lineColor = baseC * bright; lineColor.a = lineAlpha;
        Color shapeColor = new Color((baseC * bright).r, (baseC * bright).g, (baseC * bright).b, 1f);

        DrawStroke(world, lineColor, bpm01, e);

        if (enableStamps)
        {
            stampTimer += Time.deltaTime;
            if (stampTimer >= stampEvery)
            {
                stampTimer = 0f;
                Stamp(world, shapeColor, g, e, bpm01);
            }
        }
    }

    void DrawNoneMonotone()
    {
        float safeMin = viewportMargin;
        float safeMax = 1f - viewportMargin;

        float angle = Time.time * noneSpeed * 2f * Mathf.PI;
        float vx = noneCenterViewport.x + Mathf.Cos(angle) * noneRadius;
        float vy = noneCenterViewport.y + Mathf.Sin(angle) * noneRadius;

        vx = Mathf.Clamp(vx, safeMin, safeMax);
        vy = Mathf.Clamp(vy, safeMin, safeMax);

        Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, drawDistance));

        Color lc = noneColor;
        lc.a = lineAlpha;

        DrawStroke(world, lc, 0.25f, 0f);

        if (!noneDisableStamps && enableStamps)
        {
            stampTimer += Time.deltaTime;
            if (stampTimer >= stampEvery)
            {
                stampTimer = 0f;
                Stamp(world, new Color(noneColor.r, noneColor.g, noneColor.b, 1f), 0f, 0f, 0.25f);
            }
        }
    }

    void DrawStroke(Vector3 world, Color lineColor, float bpm01, float e)
    {
        if (!drawLines || line == null) return;

        // constant-ish width, minimal changes
        float widthT = Mathf.Clamp01(0.55f * bpm01 + 0.45f * (e + 1f) * 0.5f);
        float width = Mathf.Lerp(minWidth, maxWidth, widthT);

        line.startWidth = width;
        line.endWidth = width;
        line.startColor = lineColor;
        line.endColor = lineColor;

        // break huge segment: just clear the single line (no spawning)
        if (pointCount > 0 && Vector3.Distance(lastPoint, world) > breakLineDistance)
        {
            ClearLine();
        }

        // rate limit points
        if (maxPointsPerSecond > 1f && pointCooldown > 0f) return;
        if (maxPointsPerSecond > 1f) pointCooldown = 1f / maxPointsPerSecond;

        // add point
        if (pointCount == 0 || Vector3.Distance(lastPoint, world) >= 0.001f)
        {
            if (pointCount >= maxPoints)
            {
                // reset instead of creating new objects
                ClearLine();
            }

            line.positionCount = pointCount + 1;
            line.SetPosition(pointCount, world);
            lastPoint = world;
            pointCount++;
        }
    }

    void Stamp(Vector3 p, Color c, float g, float e, float bpm01)
    {
        GameObject prefab = (g < 0f) ? spherePrefab : cubePrefab;
        if (prefab == null) return;

        float tt = Mathf.Clamp01(0.5f * bpm01 + 0.5f * (e + 1f) * 0.5f);
        float s = Mathf.Lerp(minStampScale, maxStampScale, tt);

        var go = Instantiate(prefab, p, Random.rotation);
        go.transform.localScale = Vector3.one * s;

        var r = go.GetComponentInChildren<Renderer>();
        if (r != null) ApplyRendererColor(r, c);

        if (stampLifetimeSeconds > 0f)
            Destroy(go, stampLifetimeSeconds);
    }

    void ApplyRendererColor(Renderer r, Color c)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        _mpb.SetColor(EmissionColorId, c * 1.8f);
        r.SetPropertyBlock(_mpb);
    }

    static float SoftSign(float x) => x / (1f + Mathf.Abs(x));
    static float SignedPow(float x, float p) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), p);

    Gradient DefaultPalette()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.15f, 0.75f, 1f), 0f),
                new GradientColorKey(new Color(0.65f, 0.25f, 1f), 0.33f),
                new GradientColorKey(new Color(1f, 0.25f, 0.65f), 0.66f),
                new GradientColorKey(new Color(1f, 0.92f, 0.25f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
        return g;
    }

    class RunningStats
    {
        int n;
        double mean;
        double m2;

        public void Push(double x)
        {
            n++;
            double d = x - mean;
            mean += d / n;
            double d2 = x - mean;
            m2 += d * d2;
        }

        public float ZScore(double x)
        {
            if (n < 25) return 0f;
            double v = (n > 1) ? (m2 / (n - 1)) : 0.0;
            double sd = System.Math.Sqrt(System.Math.Max(v, 1e-9));
            return (float)((x - mean) / sd);
        }
    }
}
