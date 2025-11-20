using UnityEngine;

public class BreathMicNoteTrigger : MonoBehaviour
{
    [Header("Źródła")]
    public BreathMicInput micInput;   // z BreathMicInput.cs
    public NotePlayer notePlayer;     // z NotePlayer.cs
    [Tooltip("Opcjonalne – jeśli podasz, GSR będzie wpływało na wybór stopnia skali")]
    public BioDataReader bioReader;   // można zostawić null

    [Header("Progi wyzwalania")]
    [Tooltip("Poziom oddechu w mikrofonie, powyżej którego wyzwalamy nutę/akord")]
    public float triggerThreshold = 0.35f;
    [Tooltip("Poziom, poniżej którego układ się 'uzbraja' na kolejne dmuchnięcie")]
    public float releaseThreshold = 0.2f;
    [Tooltip("Minimalny czas między kolejnymi wyzwoleniami (sekundy)")]
    public float minRetriggerTime = 0.4f;

    [Header("Co gramy")]
    public bool playChord = true;
    [Tooltip("Interwały w skali dla akordu (jak w NotePlayerze)")]
    public int[] chordShape = { 0, 2, 4 }; // triada
    [Tooltip("Bazowa oktawa (0 = C4 jeśli baseFrequency=C4, -1 = niżej)")]
    public int baseOctave = 0;
    [Tooltip("Maksymalna dodatkowa oktawa (0 = zawsze ta sama, 1 = czasem wyżej)")]
    public int maxExtraOctave = 1;

    [Header("Stopnie skali do wyboru (opcjonalnie ograniczone)")]
    [Tooltip("Jeśli pusta – użyjemy wszystkich stopni 0..6. Jeśli nie – losujemy spośród tych wartości.")]
    public int[] allowedDegrees;  // np. {0, 3, 4, 5} dla I, IV, V, VI

    private bool _armed = true;
    private float _timeSinceLastTrigger;

    void Update()
    {
        if (micInput == null || notePlayer == null)
            return;

        _timeSinceLastTrigger += Time.deltaTime;

        float level = Mathf.Clamp01(micInput.breathMicLevel);

        // jeśli uzbrojony i przekroczono próg – wyzwól
        if (_armed && level >= triggerThreshold && _timeSinceLastTrigger >= minRetriggerTime)
        {
            FireTrigger();
            _armed = false;
            _timeSinceLastTrigger = 0f;
        }

        // „rozbrojenie” dopiero gdy poziom spadnie poniżej progu release
        if (!_armed && level <= releaseThreshold)
        {
            _armed = true;
        }
    }

    void FireTrigger()
    {
        // 1) Wybór stopnia skali
        int degree = ChooseDegree();

        // 2) Wybór oktawy
        int octave = ChooseOctave();

        // 3) Gramy
        notePlayer.StopAll(); // podmieniamy akord/nutę na nową

        if (playChord && chordShape != null && chordShape.Length > 0)
        {
            notePlayer.PlayChord(degree, octave, chordShape);
        }
        else
        {
            notePlayer.PlayNote(degree, octave);
        }
    }

    int ChooseDegree()
    {
        // Jeśli mamy ograniczoną listę stopni – losuj z niej
        if (allowedDegrees != null && allowedDegrees.Length > 0)
        {
            // GSR (jeśli jest) może wpływać na wybór, zamiast czystego losu
            float selector = bioReader != null ? Mathf.Clamp01(bioReader.gsrNormalized) : Random.value;
            int idx = Mathf.FloorToInt(selector * allowedDegrees.Length);
            if (idx >= allowedDegrees.Length) idx = allowedDegrees.Length - 1;
            return allowedDegrees[idx];
        }

        // W przeciwnym razie – standardowe 0..6
        if (bioReader != null)
        {
            float g = Mathf.Clamp01(bioReader.gsrNormalized);
            return Mathf.RoundToInt(Mathf.Lerp(0, 6, g));
        }

        // fallback – losowo 0..6
        return Random.Range(0, 7);
    }

    int ChooseOctave()
    {
        int extra = 0;

        if (bioReader != null && maxExtraOctave > 0)
        {
            float b = Mathf.Clamp01(bioReader.breathNormalized);
            extra = Mathf.RoundToInt(Mathf.Lerp(0, maxExtraOctave, b));
        }
        else if (maxExtraOctave > 0)
        {
            extra = Random.Range(0, maxExtraOctave + 1);
        }

        return baseOctave + extra;
    }
}
