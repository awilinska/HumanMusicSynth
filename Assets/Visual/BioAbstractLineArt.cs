using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BioAbstractLineArt : MonoBehaviour
{
    [Header("Źródła danych")]
    public BioDataReader bio;        // od Arduino (GSR + oddech z czujnika odległości)
    public BreathMicInput mic;       // od mikrofonu przy nosie (oddech akustyczny)

    [Header("Geometria")]
    [Tooltip("Liczba punktów linii (im więcej, tym gładsza, ale cięższa)")]
    public int pointCount = 256;
    [Tooltip("Średni promień figury")]
    public float baseRadius = 5f;
    [Tooltip("Maksymalna ilość 'chaosu' (jak bardzo linia może się wyginać)")]
    public float maxChaos = 2.5f;

    [Header("Ruch")]
    [Tooltip("Bazowa prędkość obrotu (stopnie na sekundę)")]
    public float baseRotationSpeed = 5f;
    [Tooltip("Dodatkowa prędkość obrotu z mikrofonu")]
    public float extraRotationSpeed = 30f;
    [Tooltip("Skala szumu (im większa, tym bardziej fraktalnie)")]
    public float noiseScale = 2f;

    [Header("Linia")]
    [Tooltip("Grubość linii")]
    public float lineWidth = 0.08f;

    private LineRenderer _lr;
    private Vector3[] _points;
    private float _angleOffset; // aktualny obrót całości

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        if (_lr == null)
            _lr = gameObject.AddComponent<LineRenderer>();

        _lr.positionCount = pointCount;
        _lr.loop = true;
        _lr.useWorldSpace = false;
        _lr.widthMultiplier = lineWidth;

        // prosty materiał (jednolity kolor, bez lighting)
        if (_lr.sharedMaterial == null)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            _lr.sharedMaterial = mat;
        }

        _points = new Vector3[pointCount];
    }

    void Update()
    {
        float t = Time.time;

        float gsr = bio != null ? Mathf.Clamp01(bio.gsrNormalized) : 0f;
        float breath = bio != null ? Mathf.Clamp01(bio.breathNormalized) : 0f;
        float micLevel = mic != null ? Mathf.Clamp01(mic.breathMicLevel) : 0f;

        // 1) Obrót całości: baza + wpływ mikrofonu
        float rotSpeed = baseRotationSpeed + extraRotationSpeed * micLevel;
        _angleOffset += rotSpeed * Mathf.Deg2Rad * Time.deltaTime;

        // 2) Ilość chaosu: im wyższy GSR, tym bardziej linia 'pęka'
        float chaos = maxChaos * gsr;

        // 3) Promień: lekko pulsuje z oddechem
        float radiusBase = baseRadius * Mathf.Lerp(0.8f, 1.2f, breath);

        for (int i = 0; i < pointCount; i++)
        {
            float u = (float)i / (pointCount - 1); // 0..1
            float angle = u * Mathf.PI * 2f + _angleOffset;

            // Perlin noise – organiczne falowanie
            float n = Mathf.PerlinNoise(
                u * noiseScale + t * 0.1f,
                t * 0.15f + gsr * 5f
            );

            // odchylenie promienia z szumu i sygnałów
            float radialOffset = (n - 0.5f) * 2f * chaos;

            float r = radiusBase + radialOffset;

            float x = Mathf.Cos(angle) * r;
            float y = Mathf.Sin(angle) * r;

            // lekki „oddech” w osi Z
            float z = Mathf.Sin(t * 0.5f + u * Mathf.PI * 4f) * 0.3f * breath;

            _points[i] = new Vector3(x, y, z);
        }

        _lr.SetPositions(_points);

        UpdateColors(gsr, breath, micLevel);
    }

    void UpdateColors(float gsr, float breath, float micLevel)
    {
        // Kolory z HSV, żeby łatwo sterować barwą
        // hue1: zależny od GSR (spokój -> niebieski, pobudzenie -> czerwony)
        float hue1 = Mathf.Lerp(0.6f, 0.0f, gsr); // 0.6 ~ niebieski, 0 ~ czerwony
        // hue2: przesunięty trochę, zależny od oddechu
        float hue2 = Mathf.Repeat(hue1 + Mathf.Lerp(0.1f, 0.3f, breath), 1f);

        float sat = Mathf.Lerp(0.4f, 0.9f, gsr);     // bardziej nasycone przy emocjach
        float val1 = Mathf.Lerp(0.3f, 1f, breath);   // jaśniej przy głębszym oddechu
        float val2 = Mathf.Lerp(0.2f, 0.8f, micLevel);

        Color c1 = Color.HSVToRGB(hue1, sat, val1);
        Color c2 = Color.HSVToRGB(hue2, sat, val2);

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(c1, 0f),
                new GradientColorKey(c2, 0.5f),
                new GradientColorKey(c1, 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(1.0f, 0.2f),
                new GradientAlphaKey(1.0f, 0.8f),
                new GradientAlphaKey(0.0f, 1f),
            }
        );

        _lr.colorGradient = gradient;
    }
}
