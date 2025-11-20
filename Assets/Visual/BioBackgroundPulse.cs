using UnityEngine;

public class BioBackgroundPulse : MonoBehaviour
{
    public BioDataReader bio;
    public BreathMicInput mic;

    [Tooltip("Główna kamera – jeśli puste, użyje Camera.main")]
    public Camera targetCamera;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void Update()
    {
        if (targetCamera == null || bio == null)
            return;

        float gsr = Mathf.Clamp01(bio.gsrNormalized);
        float breath = Mathf.Clamp01(bio.breathNormalized);
        float micLevel = mic != null ? Mathf.Clamp01(mic.breathMicLevel) : 0f;

        // Hue z GSR: spokojnie = turkus/niebieski, pobudzenie = bardziej pomarańcz/czerwony
        float hue = Mathf.Lerp(0.55f, 0.05f, gsr);
        float sat = Mathf.Lerp(0.2f, 0.8f, gsr);

        // Jasność z oddechu + delikatne pulsowanie z mikrofonu
        float baseValue = Mathf.Lerp(0.1f, 0.6f, breath);
        float pulse = Mathf.Sin(Time.time * Mathf.Lerp(0.5f, 2f, micLevel)) * 0.1f * micLevel;
        float val = Mathf.Clamp01(baseValue + pulse);

        Color bg = Color.HSVToRGB(hue, sat, val);
        targetCamera.backgroundColor = bg;
    }
}
