using UnityEngine;
using System.IO.Ports;

public class BioDataReader : MonoBehaviour
{
    public string portName = "COM3";
    public int baudRate = 115200;

    private SerialPort _port;

    [Range(0, 1)] public float gsrNormalized;
    [Range(0, 1)] public float breathNormalized;

    void Start()
    {
        _port = new SerialPort(portName, baudRate);
        _port.ReadTimeout = 50;

        try
        {
            _port.Open();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Nie mogę otworzyć portu: " + e.Message);
        }
    }

    void Update()
    {
        if (_port == null || !_port.IsOpen) return;

        try
        {
            string line = _port.ReadLine(); //reads data from Arduino
            ParseLine(line);
        }
        catch (System.TimeoutException)
        {
            // No new data after this frame
        }
    }

    void ParseLine(string line)
    {
        // Simple parser
        string[] parts = line.Split(',');
        int gsrRaw = 0;
        int distRaw = 0;

        foreach (var p in parts)
        {
            if (p.StartsWith("GSR:"))
                int.TryParse(p.Substring(4), out gsrRaw);
            else if (p.StartsWith("DIST:"))
                int.TryParse(p.Substring(5), out distRaw);
        }

        // Normalization - values experimental
        gsrNormalized = Mathf.InverseLerp(300, 800, gsrRaw);   // example???
        breathNormalized = Mathf.InverseLerp(20, 60, distRaw); // 20-60 centimeters
    }

    void OnDestroy()
    {
        if (_port != null && _port.IsOpen)
            _port.Close();
    }
}
