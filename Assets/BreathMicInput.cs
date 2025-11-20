using UnityEngine;

public class BreathMicInput : MonoBehaviour
{
    [Header("Ustawienia mikrofonu")]
    public string deviceName = "";      // puste = domyślny mikrofon
    public int sampleRate = 44100;
    public int bufferLengthSec = 1;

    [Header("Analiza sygnału")]
    public int sampleWindow = 1024;     // ile próbek analizujemy na raz
    [Range(0f, 1f)] public float breathMicLevel; // znormalizowany poziom 0..1

    [Tooltip("Wzmocnienie czułości – podbij, jeśli oddech jest 'za cichy'")]
    public float sensitivity = 10f;

    [Tooltip("Wygładzanie (0 = brak, 1 = ultra wolne)")]
    [Range(0f, 1f)] public float smoothFactor = 0.4f;

    private AudioClip _micClip;
    private float[] _sampleBuffer;
    private bool _micReady;

    void Start()
    {
        // Jeśli nie podano nazwy urządzenia, bierzemy domyślny mikrofon
        if (string.IsNullOrEmpty(deviceName))
        {
            if (Microphone.devices.Length > 0)
            {
                deviceName = Microphone.devices[0];
                Debug.Log("BreathMicInput: używam mikrofonu: " + deviceName);
            }
            else
            {
                Debug.LogError("BreathMicInput: brak dostępnych mikrofonów!");
                enabled = false;
                return;
            }
        }

        _micClip = Microphone.Start(deviceName, true, bufferLengthSec, sampleRate);
        _sampleBuffer = new float[sampleWindow];
        _micReady = true;
    }

    void Update()
    {
        if (!_micReady || _micClip == null)
            return;

        int micPos = Microphone.GetPosition(deviceName);
        if (micPos < sampleWindow)
            return; // jeszcze nie mamy wystarczająco próbek

        int startPos = micPos - sampleWindow;
        if (startPos < 0)
            startPos += _micClip.samples;

        _micClip.GetData(_sampleBuffer, startPos);

        // RMS (root mean square) – lepszy niż sam peak
        float sumSq = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            float s = _sampleBuffer[i];
            sumSq += s * s;
        }

        float rms = Mathf.Sqrt(sumSq / sampleWindow);

        // Wzmocnienie czułości + ograniczenie do 0..1
        float level = Mathf.Clamp01(rms * sensitivity);

        // Wygładzanie, żeby nie „trzaskało”
        breathMicLevel = Mathf.Lerp(breathMicLevel, level, 1f - smoothFactor);
    }

    void OnDestroy()
    {
        if (_micReady)
        {
            Microphone.End(deviceName);
            _micReady = false;
        }
    }
}
