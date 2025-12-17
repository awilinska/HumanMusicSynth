using UnityEngine;
using System.IO.Ports;
using System.Globalization;

public class BioDataReaderV2 : MonoBehaviour
{
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Raw")]
    public int gsrRaw;
    public int ecgRaw;
    public int ecgFiltered;
    public int ecgEnergy;
    public float tempC;
    public int bpm;
    public bool leadOff;

    SerialPort _port;

    void Start()
    {
        _port = new SerialPort(portName, baudRate);
        _port.ReadTimeout = 30;

        try { _port.Open(); }
        catch (System.Exception e) { Debug.LogError("Serial open failed: " + e.Message); }
    }

    void Update()
    {
        if (_port == null || !_port.IsOpen) return;

        try
        {
            string line = _port.ReadLine(); // gsr,ecgRaw,filt,energy,temp,bpm,lead
            Parse(line);
        }
        catch (System.TimeoutException) { }
        catch (System.Exception e) { Debug.LogWarning("Serial read error: " + e.Message); }
    }

    void Parse(string line)
    {
        var p = line.Split(',');
        if (p.Length < 7) return;

        int.TryParse(p[0], out gsrRaw);
        int.TryParse(p[1], out ecgRaw);
        int.TryParse(p[2], out ecgFiltered);
        int.TryParse(p[3], out ecgEnergy);

        float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out tempC);

        int.TryParse(p[5], out bpm);

        int lead = 0;
        int.TryParse(p[6], out lead);
        leadOff = lead == 1;
    }

    void OnDestroy()
    {
        if (_port != null && _port.IsOpen) _port.Close();
    }
}
