using UnityEngine;

public class BioChorusMapper : MonoBehaviour
{
    public BioDataReaderV2 reader;
    public PolySynth synth;
    public NotePlayer notePlayer;

    [Header("Akordy (stopnie skali)")]
    public int[] triad = { 0, 2, 4 };
    public int[] seventh = { 0, 2, 4, 6 };

    [Header("Arp")]
    public float minStep = 0.07f;
    public float maxStep = 0.55f;

    [Header("Detune (chorus feel)")]
    [Tooltip("Ile pó³tonów max detune na dodatkowy g³os (np. 0.08 = delikatnie)")]
    public float detuneSemitones = 0.08f;

    float _t;
    int _arpIndex;

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

        bool ecgOK = !reader.leadOff;

        gsrStats.Push(reader.gsrRaw);
        ecgStats.Push(reader.ecgEnergy);

        float gZ = gsrStats.ZScore(reader.gsrRaw);
        float eZ = ecgOK ? ecgStats.ZScore(reader.ecgEnergy) : 0f;

        gZs = Mathf.Lerp(gZs, gZ, 0.06f);
        eZs = Mathf.Lerp(eZs, eZ, 0.10f);

        // Zamiast tanh: SoftSign (bardzo czu³e ko³o zera, miêkko limituje)
        float g = SoftSign(gZs * 1.1f); // -1..1
        float e = SoftSign(eZs * 1.3f); // -1..1

        // Harmonia z temperatury
        int root = TempToScaleDegree(reader.tempC);
        bool rich = reader.tempC >= 33.5f;

        // Barwa
        synth.waveType = PickWave(g);

        // Envelope: "charakter osoby"
        synth.attackTime = Mathf.Lerp(0.005f, 0.09f, (g + 1f) * 0.5f);
        synth.releaseTime = Mathf.Lerp(0.08f, 0.7f, (1f - (g + 1f) * 0.5f));

        // Gain: zale¿ny od ECG (pobudzenie)
        synth.masterGain = Mathf.Lerp(0.06f, 0.22f, (e + 1f) * 0.5f);

        float step = ComputeStepTime(reader.bpm, e);

        _t += Time.deltaTime;
        if (_t >= step)
        {
            _t = 0f;

            notePlayer.StopAll();

            int[] chord = rich ? seventh : triad;
            int degree = root + chord[_arpIndex % chord.Length];

            int octave = (e > 0.35f) ? 1 : 0;

            // G³ówny g³os
            notePlayer.PlayNote(degree, octave);

            // Dodatkowy „detune” g³os: robimy to bez modyfikacji NotePlayer:
            // -> bezpoœrednio NoteOn z PolySynth z lekko przesuniêt¹ czêstotliwoœci¹
            float baseFreq = notePlayer.GetNoteFrequency(degree, octave);
            float detune = Mathf.Lerp(-detuneSemitones, detuneSemitones, (g + 1f) * 0.5f);
            float detunedFreq = baseFreq * Mathf.Pow(2f, detune / 12f);
            synth.NoteOn(detunedFreq);

            _arpIndex++;
        }
    }

    static float SoftSign(float x) => x / (1f + Mathf.Abs(x));

    int TempToScaleDegree(float tempC)
    {
        float t = Mathf.InverseLerp(30.0f, 36.5f, tempC);
        return Mathf.Clamp(Mathf.RoundToInt(t * 6f), 0, 6);
    }

    PolySynth.WaveType PickWave(float g)
    {
        if (g < -0.4f) return PolySynth.WaveType.Sine;
        if (g < 0.1f) return PolySynth.WaveType.Triangle;
        if (g < 0.55f) return PolySynth.WaveType.Saw;
        return PolySynth.WaveType.Square;
    }

    float ComputeStepTime(int bpm, float e)
    {
        if (bpm >= 40 && bpm <= 180)
        {
            float beat = 60f / bpm;
            return Mathf.Clamp(beat * 0.5f, minStep, maxStep);
        }

        float t = (e + 1f) * 0.5f;
        return Mathf.Lerp(maxStep, minStep, t);
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

        public double StdDev => System.Math.Sqrt(System.Math.Max((n > 1) ? (m2 / (n - 1)) : 0.0, 1e-9));

        public float ZScore(double x)
        {
            if (n < 25) return 0f; // krótkie "uczenie osoby"
            return (float)((x - mean) / StdDev);
        }
    }
}
