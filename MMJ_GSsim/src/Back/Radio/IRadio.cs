namespace GARDENs_GS_Software.Library
{
    interface IRadio
    {
        (ComPortSearchType type, string value) AutoComMarker { get; }
        bool IsOpen { get; }
        string ModelName { get; }
        void SetPort(string port);
        bool Connect();
        void Disconnect();
        void ChangeFrequency(uint uplinkFrequency, uint downlinkFrequency);
        void ChangeReceiveMode(string mode);
    }
}