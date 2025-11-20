using UnityEngine;

public class BioChordController : MonoBehaviour
{
    [Header("Źródła danych")]
    public BioDataReader reader;
    public NotePlayer notePlayer;

    [Header("Akordy (interwały w skali)")]
    public int[] majorTriad = { 0, 2, 4 };
    public int[] minorTriad = { 0, 2, 4 };
    public int[] add9Chord  = { 0, 2, 4, 6 };

    [Header("Tempo akordów (sekundy)")]
    public float minInterval = 2.5f;
    public float maxInterval = 6.0f;

    private float timer;

    // tylko te stopnie skali (C, F, G, a) – bardzo stabilnie i „muzycznie”
    private int[] pleasantDegrees = { 0, 3, 4, 5 };

    void Update()
    {
        if (reader == null || notePlayer == null)
            return;

        float breath = Mathf.Clamp01(reader.breathNormalized);
        float gsr    = Mathf.Clamp01(reader.gsrNormalized);

        // wolniejsze zmiany: intensywniejszy oddech = trochę częściej
        float interval = Mathf.Lerp(maxInterval, minInterval, breath);
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;

            // wybór stopnia tylko z przyjemniejszego zbioru
            int idx = Mathf.FloorToInt(gsr * (pleasantDegrees.Length - 1));
            int degree = pleasantDegrees[idx];

            // oktawa: tylko niższe / środkowe
            int octave = (breath < 0.6f) ? -1 : 0;

            // typ akordu: spokojniejszy wybór
            int[] chord;
            if (breath < 0.33f)
                chord = majorTriad;
            else if (breath < 0.66f)
                chord = minorTriad;
            else
                chord = add9Chord;

            notePlayer.StopAll();
            notePlayer.PlayChord(degree, octave, chord);
        }
    }
}
