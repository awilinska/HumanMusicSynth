using UnityEngine;

public class BreathMicToSynth : MonoBehaviour
{
    public BreathMicInput micInput;
    public PolySynth synth;

    [Header("Vibrato z mikrofonu")]
    public float extraVibratoDepth = 0.01f;   // ile vibrato dodać przy maksymalnym oddechu
    public float extraVibratoSpeed = 2f;      // dodatkowa prędkość

    void Update()
    {
        if (micInput == null || synth == null)
            return;

        float mic = micInput.breathMicLevel; // 0..1

        // dodatkowe vibrato w zależności od oddechu w mikrofon
        float addDepth = extraVibratoDepth * mic;
        float addSpeed = extraVibratoSpeed * mic;

        // zakładam, że w PolySynth masz ustawione jakieś bazowe vibratoDepth/Speed
        // np. w Inspectorze: vibratoDepth = 0.01, vibratoSpeed = 4
        // tutaj tylko lekko je modulujemy
        synth.vibratoDepth = Mathf.Clamp(synth.vibratoDepth + addDepth, 0f, 0.05f);
        synth.vibratoSpeed = Mathf.Clamp(synth.vibratoSpeed + addSpeed, 0f, 10f);

        // przy mocnym oddechu w mikrofonie – przełącz delikatnie na triangle
        if (mic > 0.7f)
        {
            synth.waveType = PolySynth.WaveType.Triangle;
        }
        else
        {
            synth.waveType = PolySynth.WaveType.Sine;
        }
    }
}
