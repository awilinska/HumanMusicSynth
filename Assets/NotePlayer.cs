using UnityEngine;
using System.Collections.Generic;

public class NotePlayer : MonoBehaviour
{
    public PolySynth synth;

    [Header("C4 = 261.63 Hz")]
    public float baseFrequency = 261.63f;

    // C-dur w pó³tonach od C
    private readonly int[] majorScaleSemitones = { 0, 2, 4, 5, 7, 9, 11 };

    // aktywne nuty (po freq), ¿eby móc gasiæ
    private readonly List<float> activeNotes = new List<float>();

    void Start()
    {
        if (synth == null)
            synth = GetComponent<PolySynth>();
    }

    public float GetNoteFrequency(int scaleDegree, int octaveOffset = 0)
    {
        int degree = Mathf.FloorToInt(Mathf.Repeat(scaleDegree, majorScaleSemitones.Length));
        int semitone = majorScaleSemitones[degree] + 12 * octaveOffset;

        // f = f0 * 2^(n/12)
        return baseFrequency * Mathf.Pow(2f, semitone / 12f);
    }

    public void PlayNote(int scaleDegree, int octaveOffset = 0)
    {
        float freq = GetNoteFrequency(scaleDegree, octaveOffset);
        if (!activeNotes.Contains(freq))
            activeNotes.Add(freq);

        synth.NoteOn(freq);
    }

    public void StopNote(int scaleDegree, int octaveOffset = 0)
    {
        float freq = GetNoteFrequency(scaleDegree, octaveOffset);
        activeNotes.Remove(freq);
        synth.NoteOff(freq);
    }

    /// <summary>
    /// intervalsInScale: np. {0,2,4} = trójdŸwiêk w skali
    /// </summary>
    public void PlayChord(int rootDegree, int octaveOffset, int[] intervalsInScale)
    {
        foreach (var interval in intervalsInScale)
        {
            int degree = rootDegree + interval;
            PlayNote(degree, octaveOffset);
        }
    }

    public void StopChord(int rootDegree, int octaveOffset, int[] intervalsInScale)
    {
        foreach (var interval in intervalsInScale)
        {
            int degree = rootDegree + interval;
            StopNote(degree, octaveOffset);
        }
    }

    public void StopAll()
    {
        synth.AllNotesOff();
        activeNotes.Clear();
    }
}
