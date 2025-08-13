using System;
using System.Diagnostics;
using GARDENs_GS_Software.Library;


namespace GARDENs_GS_Software.Back.Radio
{
    public class DummyRadio : IRadio
    {
        public string ModelName => "DummyRadio";
        public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.Dummy, "DEBUG0");
        public bool IsOpen { get; private set; }
        private string port = "COM0";

        public void SetPort(string _port)
        {
            port = _port;
            Debug.WriteLine($"{ModelName} port is {port}");
        }

        public bool Connect()
        {
            Debug.WriteLine($"{ModelName} connected.");
            IsOpen = true;
            return true;
        }

        public void Disconnect()
        {
            Debug.WriteLine($"{ModelName} disconnected.");
            IsOpen = false;
        }

        public void ChangeReceiveMode(string mode)
        {
            Debug.WriteLine($"{ModelName} disconnected.");
        }

        public void ChangeFrequency(uint uplinkFrequency, uint downlinkFrequency)
        {
            Debug.WriteLine($"{ModelName} changed uplink frequency to {uplinkFrequency}");
            Debug.WriteLine($"{ModelName} changed downlink frequency to {downlinkFrequency}");
        }
    }
}