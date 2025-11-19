using UnityEngine;

public class BioChordController : MonoBehaviour
{
    public BioDataReader reader;
    public NotePlayer notePlayer;

    private int[] majorTriad = { 0, 2, 4 };
    private float timer;
    public float minInterval = 0.3f;
    public float maxInterval = 2.0f;

    void Update()
    {
        if (reader == null || notePlayer == null) return;

        // breath indicates pause between acords
        float t = Mathf.Clamp01(reader.breathNormalized);
        float interval = Mathf.Lerp(minInterval, maxInterval, 1f - t); // deeper breath - faster

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;

            int degree = Mathf.RoundToInt(Mathf.Lerp(0, 6, Mathf.Clamp01(reader.gsrNormalized)));

            notePlayer.StopAll();
            notePlayer.PlayChord(degree, 0, majorTriad);
        }
    }
}
