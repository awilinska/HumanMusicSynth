using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PolySynth : MonoBehaviour
{
    public enum WaveType { Sine, Triangle }

    [System.Serializable]
    public class Voice
    {
        public bool active;
        public float frequency;
        public double phase;
        public float amplitude;
        public float targetAmplitude;
    }

    [Header("Główne parametry")]
    public WaveType waveType = WaveType.Sine;
    public int maxVoices = 8;
    [Range(0f, 1f)] public float masterGain = 0.15f;
    public float maxAmplitude = 0.9f;

    [Header("Envelope (łagodny pad)")]
    public float attackTime = 0.4f;   // wolny narost
    public float releaseTime = 1.8f;  // długie wybrzmiewanie

    [Header("Delikatne vibrato")]
    [Range(0f, 0.05f)] public float vibratoDepth = 0.01f; 
    public float vibratoSpeed = 4f; // raczej wolne

    private Voice[] voices;
    private double sampleRate;
    private double vibratoPhase;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        voices = new Voice[maxVoices];
        for (int i = 0; i < maxVoices; i++)
            voices[i] = new Voice();
    }

    public void NoteOn(float frequency)
    {
        Voice v = null;
        for (int i = 0; i < maxVoices; i++)
        {
            if (!voices[i].active)
            {
                v = voices[i];
                break;
            }
        }

        if (v == null)
            v = voices[0]; // prosty voice stealing

        v.active = true;
        v.frequency = frequency;
        v.targetAmplitude = 1f;
    }

    public void NoteOff(float frequency)
    {
        for (int i = 0; i < maxVoices; i++)
        {
            if (voices[i].active && Mathf.Abs(voices[i].frequency - frequency) < 0.01f)
            {
                voices[i].targetAmplitude = 0f;
            }
        }
    }

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

        float attackCoeff = attackTime > 0f ? (float)(dt / attackTime) : 1f;
        float releaseCoeff = releaseTime > 0f ? (float)(dt / releaseTime) : 1f;

        for (int n = 0; n < sampleCount; n++)
        {
            // delikatne, globalne vibrato
            float vibrato = 0f;
            if (vibratoDepth > 0f)
            {
                vibrato = vibratoDepth * Mathf.Sin((float)vibratoPhase);
                vibratoPhase += 2.0 * Mathf.PI * vibratoSpeed / sampleRate;
                if (vibratoPhase > 2.0 * Mathf.PI)
                    vibratoPhase -= 2.0 * Mathf.PI;
            }

            float mix = 0f;

            for (int v = 0; v < maxVoices; v++)
            {
                Voice voice = voices[v];
                if (!voice.active && voice.amplitude <= 0.0001f)
                    continue;

                double freqWithVibrato = voice.frequency * (1.0 + vibrato);
                double phaseIncrement = 2.0 * Mathf.PI * freqWithVibrato / sampleRate;

                // łagodny envelope
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

            for (int c = 0; c < channels; c++)
                data[n * channels + c] = mix;
        }
    }

    float GenerateWaveSample(float phase)
    {
        switch (waveType)
        {
            case WaveType.Sine:
                return Mathf.Sin(phase);

            case WaveType.Triangle:
                float saw = (phase / Mathf.PI) - 1f;
                return 2f * (Mathf.Abs(saw) - 0.5f);

            default:
                return Mathf.Sin(phase);
        }
    }
}
