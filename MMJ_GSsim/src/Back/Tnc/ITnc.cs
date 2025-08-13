namespace GARDENs_GS_Software.Library
{
    interface ITnc
    {
        (ComPortSearchType type, string value) AutoComMarker { get; }
        bool IsOpen { get; }
        string ModelName { get; }
        void SetPort(string port);
        bool Connect();
        void Disconnect();
        void SendPacket(string data);
        void SetKiss(bool state);
        string GetPacket();
    }
}