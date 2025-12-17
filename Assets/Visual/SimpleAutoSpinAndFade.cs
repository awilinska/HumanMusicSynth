using UnityEngine;

public class SimpleAutoSpinAndFade : MonoBehaviour
{
    public float spin = 60f;
    public float bob = 0.05f;
    public float life = 12f;

    Renderer _r;
    Color _base;
    float _t;
    Vector3 _p0;

    void Awake()
    {
        _r = GetComponentInChildren<Renderer>();
        if (_r != null) _base = _r.material.color;
        _p0 = transform.position;
    }

    void Update()
    {
        _t += Time.deltaTime;

        transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
        transform.position = _p0 + Vector3.up * (bob * Mathf.Sin(_t * 2.1f));

        if (_r != null)
        {
            float a = Mathf.Clamp01(1f - (_t / life));
            _r.material.color = new Color(_base.r, _base.g, _base.b, a);
        }

        if (_t >= life) Destroy(gameObject);
    }
}
