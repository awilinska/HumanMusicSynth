using UnityEngine;

public class BioToSynthParams : MonoBehaviour
{
    public BioDataReader reader;
    public PolySynth synth;

    [Header("Zakresy modulacji")]
    public float minGain = 0.05f;
    public float maxGain = 0.2f;

    public float maxVibratoDepth = 0.05f;

    void Update()
    {
        if (reader == null || synth == null)
            return;

        float breath = Mathf.Clamp01(reader.breathNormalized);

        // Im mocniejszy oddech, tym głośniej
        synth.masterGain = Mathf.Lerp(minGain, maxGain, breath);

        // I tym większe vibrato
        synth.vibratoDepth = Mathf.Lerp(0f, maxVibratoDepth, breath);
    }
}
