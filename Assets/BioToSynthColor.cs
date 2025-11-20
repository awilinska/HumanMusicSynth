using UnityEngine;

public class BioToSynthColor : MonoBehaviour
{
    public BioDataReader reader;
    public PolySynth synth;

    [Header("Vibrato")]
    public float minVibratoDepth = 0.005f;
    public float maxVibratoDepth = 0.02f;
    public float minVibratoSpeed = 3f;
    public float maxVibratoSpeed = 6f;

    void Update()
    {
        if (reader == null || synth == null)
            return;

        float breath = Mathf.Clamp01(reader.breathNormalized);
        float gsr    = Mathf.Clamp01(reader.gsrNormalized);

        // fala: głównie sinus, od czasu do czasu triangle
        if (breath < 0.7f)
            synth.waveType = PolySynth.WaveType.Sine;
        else
            synth.waveType = PolySynth.WaveType.Triangle;

        // bardzo delikatne vibrato z GSR (emocje)
        synth.vibratoDepth = Mathf.Lerp(minVibratoDepth, maxVibratoDepth, gsr);

        // lekka zmiana prędkości vibrato z dynamiki oddechu
        float speedMod = Mathf.Clamp01(Mathf.Abs(reader.breathDelta) * 10f);
        synth.vibratoSpeed = Mathf.Lerp(minVibratoSpeed, maxVibratoSpeed, speedMod);
    }
}
