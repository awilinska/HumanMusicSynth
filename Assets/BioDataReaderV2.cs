using UnityEngine;
using System.IO.Ports;

public class BioDataReaderV2 : MonoBehaviour
{
    public enum SensorStage
    {
        None,
        GSR,
        GSR_Temp,
        GSR_Temp_ECG
    }

    [Header("Sensor enable (set from Inspector)")]
    public SensorStage stage = SensorStage.None;

    public bool EnableGSR => stage >= SensorStage.GSR;
    public bool EnableTemp => stage >= SensorStage.GSR_Temp;
    public bool EnableECG => stage >= SensorStage.GSR_Temp_ECG;

    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Raw (from Arduino)")]
    public int gsrRaw;
    public int ecgRaw;
    public int ecgFiltered;
    public int ecgEnergy;
    public int bpm;
    public bool leadOff;

    [Header("Temperature (simulated)")]
    public Vector2 simulatedTempRange = new Vector2(36.3f, 38.8f);

    [Tooltip("How much GSR can raise/lower temp (C)")]
    public float gsrToTempStrengthC = 0.9f;

    [Tooltip("Slow drift (C)")]
    public float tempDriftAmplitudeC = 0.12f;

    [Tooltip("Small wave (C)")]
    public float tempWaveAmplitudeC = 0.05f;

    [Range(0.001f, 1f)]
    public float tempResponse = 0.04f;

    [Header("Outputs")]
    public float tempC;

    // --- internal ---
    SerialPort _port;

    float _baseTemp;
    float _tempGsrOffset;
    float _seed;

    RunningStats _gsrStats = new RunningStats();

    void Start()
    {
        _seed = Random.value * 1000f;
        _baseTemp = Random.Range(simulatedTempRange.x, simulatedTempRange.y);
        tempC = _baseTemp;

        _port = new SerialPort(portName, baudRate);
        _port.ReadTimeout = 30;

        try { _port.Open(); }
        catch (System.Exception e) { Debug.LogError("Serial open failed: " + e.Message); }
    }

    void Update()
    {
        // 1) Read serial (always read if available, but later we gate what we expose)
        if (_port != null && _port.IsOpen)
        {
            try
            {
                string line = _port.ReadLine(); // gsr,ecgRaw,filt,energy,temp,bpm,lead
                Parse(line);
            }
            catch (System.TimeoutException) { }
            catch (System.Exception e) { Debug.LogWarning("Serial read error: " + e.Message); }
        }

        // 2) Gate outputs by stage
        // GSR
        if (!EnableGSR)
        {
            // keep gsrRaw but treat as "inactive" downstream by exposing neutral behavior:
            // easiest: set to baseline-ish value (constant) so it doesn't drive mappings.
            gsrRaw = 512;
        }
        else
        {
            _gsrStats.Push(gsrRaw);
        }

        // TEMP (simulated, but only if stage enables it)
        if (EnableTemp)
        {
            float gZ = _gsrStats.ZScore(gsrRaw);
            float g = SoftSign(gZ * 1.1f); // -1..1

            float targetOffset = g * gsrToTempStrengthC;
            _tempGsrOffset = Mathf.Lerp(_tempGsrOffset, targetOffset, tempResponse);

            float drift = tempDriftAmplitudeC * Mathf.Sin(Time.time * 0.06f + _seed);
            float wave = tempWaveAmplitudeC * Mathf.Sin(Time.time * 0.22f + _seed * 0.7f);

            float t = _baseTemp + _tempGsrOffset + drift + wave;
            tempC = Mathf.Clamp(t, simulatedTempRange.x, simulatedTempRange.y);
        }
        else
        {
            // neutral temp (fixed) so it does not change harmony/colors
            tempC = 37.0f;
        }

        // ECG
        if (!EnableECG)
        {
            ecgRaw = 0;
            ecgFiltered = 0;
            ecgEnergy = 0;
            bpm = 0;
            leadOff = true;
        }
        // else: use whatever came from Arduino (leadOff stays real)
    }

    void Parse(string line)
    {
        var p = line.Split(',');
        if (p.Length < 7) return;

        int.TryParse(p[0], out gsrRaw);
        int.TryParse(p[1], out ecgRaw);
        int.TryParse(p[2], out ecgFiltered);
        int.TryParse(p[3], out ecgEnergy);
        // p[4] temp from Arduino ignored (sensor dead)
        int.TryParse(p[5], out bpm);

        int lead = 0;
        int.TryParse(p[6], out lead);
        leadOff = (lead == 1);
    }

    void OnDestroy()
    {
        if (_port != null && _port.IsOpen) _port.Close();
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
