using UnityEngine;
using System.Collections;

public class BioChorusMapper : MonoBehaviour
{
    public BioDataReaderV2 reader;
    public PolySynth synth;
    public NotePlayer notePlayer;

    public int[] triad = { 0, 2, 4 };
    public int[] seventh = { 0, 2, 4, 6 };

    public float minStep = 0.07f;
    public float maxStep = 0.55f;
    public float detuneSemitones = 0.08f;

    [Header("Stage=None (monotone but alive)")]
    public bool monotoneWhenNone = true;
    public PolySynth.WaveType noneWave = PolySynth.WaveType.Sine;
    public float noneGain = 0.10f;

    public int noneDroneDegree = 0;
    public int noneDroneOctave = 0;

    public float nonePulseEvery = 0.8f;
    public int nonePulseInterval = 4;
    public int nonePulseOctave = 1;

    [Tooltip("Pulse duration in seconds (short).")]
    public float nonePulseDuration = 0.08f;

    float _t;
    int _arpIndex;

    bool _noneInitialized;
    float _nonePulseTimer;
    float _noneDroneFreq;

    RunningStats gsrStats = new RunningStats();
    RunningStats ecgStats = new RunningStats();
    float gZs, eZs;

    void Start()
    {
        if (synth == null) synth = GetComponent<PolySynth>();
    }

    void Update()
    {
        if (reader == null || synth == null || notePlayer == null) return;

        if (monotoneWhenNone && reader.stage == BioDataReaderV2.SensorStage.None)
        {
            if (!_noneInitialized)
            {
                notePlayer.StopAll();

                synth.waveType = noneWave;
                synth.attackTime = 0.01f;
                synth.releaseTime = 0.18f;
                synth.masterGain = noneGain;

                _noneDroneFreq = notePlayer.GetNoteFrequency(noneDroneDegree, noneDroneOctave);
                synth.NoteOn(_noneDroneFreq); // steady drone (single voice)

                _nonePulseTimer = 0f;
                _noneInitialized = true;
            }

            _nonePulseTimer += Time.deltaTime;
            if (_nonePulseTimer >= Mathf.Max(0.15f, nonePulseEvery))
            {
                _nonePulseTimer = 0f;

                float pulseFreq = notePlayer.GetNoteFrequency(noneDroneDegree + nonePulseInterval, noneDroneOctave + nonePulseOctave);
                StartCoroutine(OneShot(pulseFreq, nonePulseDuration));
            }

            return;
        }

        // leaving NONE: stop drone voice
        if (_noneInitialized)
        {
            synth.NoteOff(_noneDroneFreq);
            notePlayer.StopAll();
            _noneInitialized = false;
        }

        // --- Normal sensor mapping ---
        bool gsrOn = reader.EnableGSR;
        bool tempOn = reader.EnableTemp;
        bool ecgOn = reader.EnableECG && !reader.leadOff;

        float g = 0f, e = 0f, temp = 37f, bpm01 = 0.25f;

        if (gsrOn)
        {
            gsrStats.Push(reader.gsrRaw);
            gZs = Mathf.Lerp(gZs, gsrStats.ZScore(reader.gsrRaw), 0.06f);
            g = SoftSign(gZs * 1.1f);
        }

        if (tempOn) temp = reader.tempC;

        if (ecgOn)
        {
            ecgStats.Push(reader.ecgEnergy);
            eZs = Mathf.Lerp(eZs, ecgStats.ZScore(reader.ecgEnergy), 0.10f);
            e = SoftSign(eZs * 1.3f);
            bpm01 = Mathf.InverseLerp(40f, 180f, reader.bpm);
        }

        int root = TempToScaleDegree(temp);
        bool rich = temp >= 37.4f;

        synth.waveType = PickWave(g);
        synth.attackTime = Mathf.Lerp(0.008f, 0.09f, (g + 1f) * 0.5f);
        synth.releaseTime = Mathf.Lerp(0.10f, 0.75f, (1f - (g + 1f) * 0.5f));
        synth.masterGain = Mathf.Lerp(0.06f, 0.22f, (e + 1f) * 0.5f);

        float step = Mathf.Lerp(maxStep, minStep, Mathf.Clamp01(bpm01));

        _t += Time.deltaTime;
        if (_t >= step)
        {
            _t = 0f;
            notePlayer.StopAll();

            int[] chord = rich ? seventh : triad;
            int degree = root + chord[_arpIndex % chord.Length];
            int octave = (e > 0.35f) ? 1 : 0;

            notePlayer.PlayNote(degree, octave);

            float baseFreq = notePlayer.GetNoteFrequency(degree, octave);
            float det = Mathf.Lerp(-detuneSemitones, detuneSemitones, (g + 1f) * 0.5f);
            float detuned = baseFreq * Mathf.Pow(2f, det / 12f);

            // one-shot detune voice (so it never accumulates)
            StartCoroutine(OneShot(detuned, 0.10f));

            _arpIndex++;
        }
    }

    IEnumerator OneShot(float freq, float duration)
    {
        synth.NoteOn(freq);
        yield return new WaitForSeconds(duration);
        synth.NoteOff(freq);
    }

    static float SoftSign(float x) => x / (1f + Mathf.Abs(x));

    int TempToScaleDegree(float tempC)
    {
        float t = Mathf.InverseLerp(36.3f, 38.8f, tempC);
        return Mathf.Clamp(Mathf.RoundToInt(t * 6f), 0, 6);
    }

    PolySynth.WaveType PickWave(float g)
    {
        if (g < -0.4f) return PolySynth.WaveType.Sine;
        if (g < 0.1f) return PolySynth.WaveType.Triangle;
        if (g < 0.55f) return PolySynth.WaveType.Saw;
        return PolySynth.WaveType.Square;
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
