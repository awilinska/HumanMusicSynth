using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PolySynth : MonoBehaviour
{
    public enum WaveType { Sine, Square, Saw, Triangle }

    [System.Serializable]
    public class Voice
    {
        public bool active;
        public float frequency;
        public double phase;

        // Prosty envelope
        public float amplitude;       // volume (0..1)
        public float targetAmplitude; // target value (attack/release)
    }

    [Header("Główne parametry")]
    public WaveType waveType = WaveType.Sine;
    public int maxVoices = 8;
    [Range(0f, 1f)] public float masterGain = 0.1f;
    public float maxAmplitude = 0.9f;

    [Header("Envelope (pseudo-ADSR)")]
    public float attackTime = 0.01f;   // seconds
    public float releaseTime = 0.2f;   // seconds

    private Voice[] voices;
    private double sampleRate;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        voices = new Voice[maxVoices];
        for (int i = 0; i < maxVoices; i++)
            voices[i] = new Voice();
    }

    /// <summary>
    /// Turns off note with exact frequency (Hz).
    /// </summary>
    public void NoteOn(float frequency)
    {
        // Find unused voice
        Voice v = null;
        for (int i = 0; i < maxVoices; i++)
        {
            if (!voices[i].active)
            {
                v = voices[i];
                break;
            }
        }

        // If all are used - override first
        if (v == null)
            v = voices[0];

        v.active = true;
        v.frequency = frequency;
        v.targetAmplitude = 1f; // start
    }

    /// <summary>
    /// Turns off note with same frequency
    /// </summary>
    public void NoteOff(float frequency)
    {
        for (int i = 0; i < maxVoices; i++)
        {
            if (voices[i].active && Mathf.Abs(voices[i].frequency - frequency) < 0.01f)
            {
                voices[i].targetAmplitude = 0f; // go into release
            }
        }
    }

    /// <summary>
    /// Turns off all notes.
    /// </summary>
    public void AllNotesOff()
    {
        for (int i = 0; i < maxVoices; i++)
        {
            voices[i].targetAmplitude = 0f;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        int sampleCount = data.Length / channels;
        double dt = 1.0 / sampleRate;

        // Calculating envelope smoothing factor
        float attackCoeff = attackTime > 0f ? (float)(dt / attackTime) : 1f;
        float releaseCoeff = releaseTime > 0f ? (float)(dt / releaseTime) : 1f;

        for (int n = 0; n < sampleCount; n++)
        {
            float mix = 0f;

            // Every voice generates sample
            for (int v = 0; v < maxVoices; v++)
            {
                Voice voice = voices[v];
                if (!voice.active && voice.amplitude <= 0.0001f)
                    continue;

                double phaseIncrement = 2.0 * Mathf.PI * voice.frequency / sampleRate;

                // Envelope
                if (voice.targetAmplitude > voice.amplitude)
                {
                    voice.amplitude += attackCoeff;
                    if (voice.amplitude > 1f) voice.amplitude = 1f;
                }
                else if (voice.targetAmplitude < voice.amplitude)
                {
                    voice.amplitude -= releaseCoeff;
                    if (voice.amplitude < 0f)
                    {
                        voice.amplitude = 0f;
                        voice.active = false;
                    }
                }

                float sample = GenerateWaveSample((float)voice.phase) * voice.amplitude;

                mix += sample;

                voice.phase += phaseIncrement;
                if (voice.phase > 2.0 * Mathf.PI)
                    voice.phase -= 2.0 * Mathf.PI;
            }

            mix *= masterGain;
            mix = Mathf.Clamp(mix, -maxAmplitude, maxAmplitude);

            // save to all channels
            for (int c = 0; c < channels; c++)
            {
                data[n * channels + c] = mix;
            }
        }
    }

    float GenerateWaveSample(float phase)
    {
        switch (waveType)
        {
            case WaveType.Sine:
                return Mathf.Sin(phase);

            case WaveType.Square:
                return phase < Mathf.PI ? 1f : -1f;

            case WaveType.Saw:
                return (phase / Mathf.PI) - 1f; // -1..1

            case WaveType.Triangle:
                float saw = (phase / Mathf.PI) - 1f;  // -1..1
                return 2f * (Mathf.Abs(saw) - 0.5f);  // -1..1

            default:
                return 0f;
        }
    }
}
