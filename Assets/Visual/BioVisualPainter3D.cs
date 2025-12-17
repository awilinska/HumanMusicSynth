using UnityEngine;

public class BioVisualFlowPainter3D : MonoBehaviour
{
    [Header("Input")]
    public BioDataReaderV2 reader;
    public Camera cam;

    [Header("Prefabs")]
    public LineRenderer linePrefab;
    public GameObject spherePrefab;
    public GameObject cubePrefab;

    [Header("Canvas in front of camera")]
    public float planeDistance = 2.4f;

    [Tooltip("Jak bardzo dzie³o wychodzi poza ekran (1 = dok³adnie ekran, >1 = overscan)")]
    public Vector2 planeSize = new Vector2(3.8f, 2.4f);

    public float depthRange = 1.2f;

    [Header("Flow / spreading")]
    public float lateralSpread = 1.6f;
    public float flowStrength = 1.4f;
    public float noiseScale = 0.35f;

    [Header("Stroke")]
    public int maxPoints = 320;
    public float minPointSpacing = 0.015f;
    public float maxPointSpacing = 0.06f;
    public float minWidth = 0.004f;
    public float maxWidth = 0.07f;

    [Header("Stamping")]
    public float stampEvery = 0.1f;
    public float minStampScale = 0.04f;
    public float maxStampScale = 0.45f;

    [Header("Color")]
    public Gradient temperatureGradient;
    public float emissiveBoost = 1.6f;

    LineRenderer line;
    int pointCount;
    Vector3 lastPoint;
    float stampTimer;

    float seed;
    float colorPhase;

    RunningStats gsrStats = new RunningStats();
    RunningStats ecgStats = new RunningStats();
    RunningStats tempStats = new RunningStats();

    void Start()
    {
        if (cam == null) cam = Camera.main;
        seed = Random.value * 1000f;
        NewLine();
    }

    void Update()
    {
        if (reader == null || cam == null || line == null) return;

        // --- stats per osoba ---
        gsrStats.Push(reader.gsrRaw);
        ecgStats.Push(reader.ecgEnergy);
        tempStats.Push(reader.tempC);

        float g = SoftSign(gsrStats.ZScore(reader.gsrRaw) * 1.4f);
        float e = reader.leadOff ? 0f : SoftSign(ecgStats.ZScore(reader.ecgEnergy) * 1.6f);
        float t = SoftSign(tempStats.ZScore(reader.tempC) * 1.2f);

        float bpm01 = Mathf.InverseLerp(40f, 180f, reader.bpm);

        // --- centrum p³ótna ---
        Vector3 center =
            cam.transform.position +
            cam.transform.forward * planeDistance;

        // --- flow field (organiczne skrêcanie) ---
        float time = Time.time * 0.35f;
        float nx = Mathf.PerlinNoise(seed + time, g * noiseScale);
        float ny = Mathf.PerlinNoise(seed + 33.3f, time + e * noiseScale);

        Vector2 flow =
            new Vector2(nx - 0.5f, ny - 0.5f) *
            flowStrength;

        // --- pozycja ---
        float spreadX = (g + flow.x) * planeSize.x * lateralSpread;
        float spreadY = (e + flow.y) * planeSize.y * lateralSpread;

        float depth =
            Mathf.Sin(time + seed) * depthRange * t +
            Mathf.Cos(time * 1.3f) * 0.25f;

        Vector3 p =
            center +
            cam.transform.right * spreadX +
            cam.transform.up * spreadY +
            cam.transform.forward * depth;

        // --- linia ---
        float spacing = Mathf.Lerp(minPointSpacing, maxPointSpacing, (e + 1f) * 0.5f);
        float width = Mathf.Lerp(minWidth, maxWidth, bpm01);

        line.startWidth = width;
        line.endWidth = width;

        // --- kolor ---
        colorPhase += Time.deltaTime * (0.15f + bpm01);
        float colorT = Mathf.Repeat(
            Mathf.InverseLerp(-1f, 1f, t) +
            colorPhase +
            0.25f * g +
            0.1f * seed,
            1f);

        Color baseColor = temperatureGradient.Evaluate(colorT);

        float brightness = Mathf.Lerp(0.6f, emissiveBoost, (e + 1f) * 0.5f);
        Color finalColor = baseColor * brightness;

        line.startColor = finalColor;
        line.endColor = finalColor;

        // --- rysowanie ---
        if (pointCount == 0 || Vector3.Distance(lastPoint, p) >= spacing)
            AddPoint(p);

        // --- stemple 3D ---
        stampTimer += Time.deltaTime;
        if (stampTimer >= stampEvery)
        {
            stampTimer = 0f;
            Stamp(p, finalColor, g, e, bpm01);
        }

        if (pointCount >= maxPoints)
            NewLine();
    }

    void NewLine()
    {
        line = Instantiate(linePrefab);
        line.positionCount = 0;
        pointCount = 0;
        lastPoint = Vector3.zero;
    }

    void AddPoint(Vector3 p)
    {
        line.positionCount = pointCount + 1;
        line.SetPosition(pointCount, p);
        lastPoint = p;
        pointCount++;
    }

    void Stamp(Vector3 p, Color c, float g, float e, float bpm01)
    {
        GameObject prefab =
            (g + Mathf.Sin(seed)) < 0f ? spherePrefab : cubePrefab;

        if (prefab == null) return;

        float scale =
            Mathf.Lerp(minStampScale, maxStampScale,
                Mathf.Clamp01(0.55f * (e + 1f) * 0.5f + 0.45f * bpm01));

        var go = Instantiate(prefab, p, Random.rotation);
        go.transform.localScale = Vector3.one * scale;

        var r = go.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            r.material.color = c;
            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", c * emissiveBoost);
            }
        }

        Destroy(go, 14f);
    }

    static float SoftSign(float x) => x / (1f + Mathf.Abs(x));

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
