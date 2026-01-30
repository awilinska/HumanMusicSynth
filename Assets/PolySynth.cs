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
        public float amplitude;
        public float targetAmplitude;
    }

    [Header("Synth")]
    public WaveType waveType = WaveType.Sine;
    [Min(1)] public int maxVoices = 8;
    [Range(0f, 1f)] public float masterGain = 0.12f;
    public float maxAmplitude = 0.9f;

    [Header("Envelope")]
    public float attackTime = 0.01f;
    public float releaseTime = 0.25f;

    private Voice[] _voices;
    private double _sampleRate;

    void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _voices = new Voice[maxVoices];
        for (int i = 0; i < maxVoices; i++) _voices[i] = new Voice();
    }

    public void NoteOn(float frequency)
    {
        Voice v = null;
        for (int i = 0; i < maxVoices; i++)
            if (!_voices[i].active) { v = _voices[i]; break; }
        if (v == null) v = _voices[0];

        v.active = true;
        v.frequency = frequency;
        v.targetAmplitude = 1f;
    }

    public void NoteOff(float frequency)
    {
        for (int i = 0; i < maxVoices; i++)
            if (_voices[i].active && Mathf.Abs(_voices[i].frequency - frequency) < 0.01f)
                _voices[i].targetAmplitude = 0f;
    }

    public void AllNotesOff()
    {
        for (int i = 0; i < maxVoices; i++)
            _voices[i].targetAmplitude = 0f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        int sampleCount = data.Length / channels;
        double dt = 1.0 / _sampleRate;

        float aC = attackTime > 0f ? (float)(dt / attackTime) : 1f;
        float rC = releaseTime > 0f ? (float)(dt / releaseTime) : 1f;

        for (int n = 0; n < sampleCount; n++)
        {
            float mix = 0f;

            for (int i = 0; i < maxVoices; i++)
            {
                var v = _voices[i];
                if (!v.active && v.amplitude <= 0.0001f) continue;

                if (v.targetAmplitude > v.amplitude)
                {
                    v.amplitude += aC;
                    if (v.amplitude > 1f) v.amplitude = 1f;
                }
                else if (v.targetAmplitude < v.amplitude)
                {
                    v.amplitude -= rC;
                    if (v.amplitude <= 0f)
                    {
                        v.amplitude = 0f;
                        v.active = false;
                    }
                }

                double inc = 2.0 * Mathf.PI * v.frequency / _sampleRate;
                float s = GenerateWave((float)v.phase) * v.amplitude;

                mix += s;

                v.phase += inc;
                if (v.phase > 2.0 * Mathf.PI) v.phase -= 2.0 * Mathf.PI;
            }

            mix *= masterGain;
            mix = Mathf.Clamp(mix, -maxAmplitude, maxAmplitude);

            for (int c = 0; c < channels; c++)
                data[n * channels + c] = mix;
        }
    }

    float GenerateWave(float phase)
    {
        switch (waveType)
        {
            case WaveType.Sine: return Mathf.Sin(phase);
            case WaveType.Square: return phase < Mathf.PI ? 1f : -1f;
            case WaveType.Saw: return (phase / Mathf.PI) - 1f;
            case WaveType.Triangle:
                float saw = (phase / Mathf.PI) - 1f;
                return 2f * (Mathf.Abs(saw) - 0.5f);
        }
        return 0f;
    }
}
