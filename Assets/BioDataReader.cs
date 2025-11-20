using UnityEngine;
using System.IO.Ports;

public class BioDataReader : MonoBehaviour
{
    [Header("Port szeregowy")]
    public string portName = "COM5";
    public int baudRate = 115200;

    private SerialPort _port;

    [Header("Dane surowe")]
    public int gsrRaw;
    public int distRaw;

    [Header("Znormalizowane")]
    [Range(0f, 1f)] public float gsrNormalized;
    [Range(0f, 1f)] public float breathNormalized;
    [Range(-1f, 1f)] public float breathDelta;
    public bool isInhale;

    // wewnętrzne
    private float _lastBreathNorm;
    private float _smoothBreath;

    void Start()
    {
        _port = new SerialPort(portName, baudRate);
        _port.ReadTimeout = 50;

        try
        {
            _port.Open();
            Debug.Log("BioDataReader: Otwarty port " + portName);
        }
        catch (System.Exception e)
        {
            Debug.LogError("BioDataReader: Nie mogę otworzyć portu " + portName + " - " + e.Message);
        }
    }

    void Update()
    {
        if (_port == null || !_port.IsOpen)
            return;

        try
        {
            string line = _port.ReadLine(); // np. "GSR:523,DIST:37"
            ParseLine(line);
        }
        catch (System.TimeoutException)
        {
            // brak nowych danych w tej klatce – ignorujemy
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("BioDataReader: błąd odczytu: " + e.Message);
        }
    }

    void ParseLine(string line)
    {
        // Prosty parser CSV: "GSR:xxx,DIST:yyy"
        string[] parts = line.Split(',');
        int gsr = gsrRaw;
        int dist = distRaw;

        foreach (string p in parts)
        {
            if (p.StartsWith("GSR:"))
            {
                int.TryParse(p.Substring(4), out gsr);
            }
            else if (p.StartsWith("DIST:"))
            {
                int.TryParse(p.Substring(5), out dist);
            }
        }

        gsrRaw = gsr;
        distRaw = dist;

        UpdateGsr(gsrRaw);
        UpdateBreath(distRaw);
    }

    void UpdateGsr(int raw)
    {
        // Zakresy do kalibracji – podejrzyj gsrRaw w Play Mode
        float norm = Mathf.InverseLerp(300f, 800f, raw);
        gsrNormalized = Mathf.Clamp01(norm);
    }

    void UpdateBreath(int rawDist)
    {
        if (rawDist <= 0)
            return;

        // 1. Zawężamy zakres do typowego ruchu klatki (dopasuj do swoich wartości)
        float raw = Mathf.Clamp(rawDist, 25f, 40f);

        // 2. Mapujemy na 0..1 (0 = bliżej, 1 = dalej)
        float norm = Mathf.InverseLerp(25f, 40f, raw);

        // 3. Nieliniowa krzywa – zwiększa czułość na końcu zakresu
        norm = Mathf.Pow(norm, 2.0f);

        // 4. Wygładzanie, żeby nie skakało
        _smoothBreath = Mathf.Lerp(_smoothBreath, norm, 0.2f);

        // 5. Delta – prędkość zmiany
        breathDelta = _smoothBreath - _lastBreathNorm;

        // 6. Kierunek oddechu
        isInhale = breathDelta > 0.001f;

        _lastBreathNorm = _smoothBreath;
        breathNormalized = Mathf.Clamp01(_smoothBreath);
    }

    void OnDestroy()
    {
        if (_port != null && _port.IsOpen)
        {
            _port.Close();
            Debug.Log("BioDataReader: Zamknięto port " + portName);
        }
    }
}
