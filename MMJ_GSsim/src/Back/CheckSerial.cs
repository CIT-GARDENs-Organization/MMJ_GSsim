using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace GARDENs_GS_Software.Library
{
    /// <summary>
    /// COMポートの情報を表すクラス
    /// </summary>
    /// <remarks>
    /// WMIを利用して取得したCOMポートの情報を格納するためのクラスです。
    /// </remarks>
    public class ComPortInfo
    {
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public string Description { get; set; }
        public string DeviceManufacturer { get; set; }
        private string[] _hardwareId;
        public string[] hardwareId
        {
            get => _hardwareId;
            set
            {
                _hardwareId = value;
                ParseHardwareIds(); // hardwareId がセットされたら自動で解析する
            }
        }

        private string _port;
        public string Port
        {
            get
            {
                if (!string.IsNullOrEmpty(_port))
                    return _port;
                if (hardwareId == null)
                    return "";

                var regex = new Regex(@"COM\d+", RegexOptions.IgnoreCase);
                _port = Name.Contains("(COM") ? regex.Match(Name).Value : "";
                if (string.IsNullOrEmpty(_port))
                {
                    foreach (var id in hardwareId)
                    {
                        if (id.Contains("COM"))
                        {
                            _port = regex.Match(id).Value;
                            break;
                        }
                    }
                }
                return _port;
            }
        }

        /// <summary>
        /// UI表示用（例： "COM5    USB Serial Port (COM5)" ）
        /// </summary>
        public string ViewString => $"{Port}      {Name}";

        public string Vid { get; private set; }
        public string Pid { get; private set; }
        public string Rev { get; private set; }
        public string SerialNumber { get; private set; }

        /// <summary>
        /// hardwareId または DeviceId から VID, PID, REV, SerialNumber を解析してプロパティにセットする。
        /// hardwareId に値があればそちらを優先します。
        /// </summary>
        private void ParseHardwareIds()
        {
            // 初期化
            Vid = Pid = Rev = SerialNumber = "";

            // hardwareId 配列があれば解析
            if (hardwareId != null && hardwareId.Length > 0)
            {
                string hw = hardwareId[0];
                // 例: "USB\\VID_0403&PID_6001&REV_0600"
                var vidMatch = Regex.Match(hw, @"VID_([0-9A-Fa-f]{4})");
                var pidMatch = Regex.Match(hw, @"PID_([0-9A-Fa-f]{4})");
                var revMatch = Regex.Match(hw, @"REV_([0-9A-Fa-f]{4})");

                if (vidMatch.Success) Vid = vidMatch.Groups[1].Value;
                if (pidMatch.Success) Pid = pidMatch.Groups[1].Value;
                if (revMatch.Success) Rev = revMatch.Groups[1].Value;

                // シリアル番号の抽出例
                // 例: "FTDIBUS\\COMPORT&VID_0403&PID_6001+8&2F12DD6&0&3\\0000"
                var snMatch = Regex.Match(hw, @"\+(.+?)\\");
                if (snMatch.Success)
                    SerialNumber = snMatch.Groups[1].Value;
            }
            // hardwareId がない場合は DeviceId から試す
            else if (!string.IsNullOrEmpty(DeviceId))
            {
                var vidMatch = Regex.Match(DeviceId, @"VID_([0-9A-Fa-f]{4})");
                var pidMatch = Regex.Match(DeviceId, @"PID_([0-9A-Fa-f]{4})");
                var revMatch = Regex.Match(DeviceId, @"REV_([0-9A-Fa-f]{4})");
                if (vidMatch.Success) Vid = vidMatch.Groups[1].Value;
                if (pidMatch.Success) Pid = pidMatch.Groups[1].Value;
                if (revMatch.Success) Rev = revMatch.Groups[1].Value;

                var snMatch = Regex.Match(DeviceId, @"\+(.+?)\\");
                if (snMatch.Success)
                    SerialNumber = snMatch.Groups[1].Value;
            }
        }
    }

    /// <summary>
    /// COMポートの検索条件を表す列挙型
    /// </summary>
    public enum ComPortSearchType
    {
        Name,
        Value,
        HardwareId,
        FriendlyName,
        Dummy
    }

    /// <summary>
    /// COMポートの検索や情報取得を行うクラス
    /// </summary>
    public class CheckSerial
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CheckSerial()
        {
            // ManagementScopeの作成は現在使用していないため、用途が明確になった場合に実装を追加する
            // ManagementScope _ = new ManagementScope("\\\\.\\ROOT\\cimv2");
        }

        /// <summary>
        /// 利用可能なCOMポートを昇順に取得する静的メソッド（すべてのポート）
        /// </summary>
        /// <returns>COMポート名の配列</returns>
        public static string[] GetAvailableComPorts()
        {
            var ports = SerialPort.GetPortNames()
                .OrderBy(port => ExtractNumber(port))
                .ToArray();

            if (ports.Length > 0)
                return ports;
            else
                return ["DEBUG0", "DEBUG1", "DEBUG2"]; // デバッグ用のダミーポート
        }

        /// <summary>
        /// 利用可能なCOMポートを取得する静的メソッド
        /// </summary>
        /// <param name="onlyAvailable">trueの場合、実際にポートを開けるポートのみを返す</param>
        /// <returns>COMポート名の配列</returns>
        public static string[] GetAvailableComPorts(bool onlyAvailable)
        {
            var ports = GetAvailableComPorts();

            if (ports.Length > 0 && ports[0] == "DEBUG0")
                return ports;
            else if (onlyAvailable)
            {
                var result = new List<string>();
                foreach (var port in ports)
                {
                    if (IsPortAvailable(port))
                    {
                        result.Add(port);
                    }
                }
                return result.ToArray();
            }
            else
            {
                return ports;
            }
        }

        /// <summary>
        /// 指定したCOMポートが利用可能かどうかをチェックするメソッド
        /// </summary>
        /// <param name="portName">チェックするCOMポート名</param>
        /// <returns>利用可能ならtrue、利用不可ならfalse</returns>
        private static bool IsPortAvailable(string portName)
        {
            try
            {
                using (var port = new SerialPort(portName))
                {
                    port.Open();
                    port.Close();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// COMポート名から含まれる数値部分を抽出するヘルパーメソッド
        /// </summary>
        /// <param name="portName">COMポート名</param>
        /// <returns>抽出された数値。見つからなければ0</returns>
        private static int ExtractNumber(string portName)
        {
            var match = Regex.Match(portName, @"\d+");
            return match.Success ? int.Parse(match.Value) : 0;
        }

        /// <summary>
        /// COMポートを検索するメソッド（enumを利用した安全な実装）
        /// </summary>
        /// <param name="searchType">検索条件の種類</param>
        /// <param name="key">検索に使用するキーワード</param>
        /// <returns>検索にヒットしたCOMポート名のリスト</returns>
        public List<string> SearchPort(ComPortSearchType searchType, string key)
        {
            switch (searchType)
            {
                case ComPortSearchType.Name:
                    return SearchByName(key);
                case ComPortSearchType.Value:
                    return SearchBySerialNumber(key);
                case ComPortSearchType.HardwareId:
                    return SearchByHardwareId(key);
                case ComPortSearchType.FriendlyName:
                    return SearchByFriendlyName(key);
                case ComPortSearchType.Dummy:
                    return SearchByDummy(key);
                default:
                    return new List<string>();
            }
        }

        /// <summary>
        /// 名前（Description）に基づいてCOMポートを検索するメソッド
        /// </summary>
        /// <param name="name">検索に使用するキーワード</param>
        /// <returns>該当するCOMポート名のリスト</returns>
        private List<string> SearchByName(string name)
        {
            var result = new List<string>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                if (queryObj["Description"].ToString().Contains(name))
                {
                    result.Add(queryObj["DeviceID"].ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// シリアル番号（PNPDeviceID）に基づいてCOMポートを検索するメソッド（現状未使用）
        /// </summary>
        /// <param name="serialNumber">検索に使用するシリアル番号</param>
        /// <returns>該当するCOMポート名のリスト</returns>
        private List<string> SearchBySerialNumber(string serialNumber)
        {
            var result = new List<string>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                if (queryObj["PNPDeviceID"].ToString().Contains(serialNumber))
                {
                    result.Add(queryObj["DeviceID"].ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// インスタンスIDに基づいてCOMポートを検索するメソッド
        /// </summary>
        /// <param name="hardwareId">検索に使用するインスタンスID</param>
        /// <returns>該当するCOMポート名のリスト</returns>
        private List<string> SearchByHardwareId(string hardwareId)
        {
            var result = new List<string>();
            var entities = GetDeviceManagerFromWin32PnPEntity();

            foreach (var entity in entities)
            {
                if (entity.hardwareId != null && entity.hardwareId.Any(id => id.Contains(hardwareId.Replace("\\\\", "\\"))))
                    result.Add(entity.Port);
            }
            return result;
        }

        /// <summary>
        /// フレンドリーネーム（NameやCaption）に基づいてCOMポートを検索する新しい方法
        /// </summary>
        /// <param name="friendlyName">検索に使用するフレンドリーネームのキーワード</param>
        /// <returns>該当するCOMポート名のリスト</returns>
        private List<string> SearchByFriendlyName(string friendlyName)
        {
            var result = new List<string>();
            string query = "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    string nameProperty = queryObj["Name"]?.ToString() ?? "";
                    if (nameProperty.Contains(friendlyName))
                    {
                        string deviceId = queryObj["DeviceID"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(deviceId))
                        {
                            result.Add(deviceId);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// ダミー向けの検索メソッド
        /// </summary>
        /// <param name="dummy"></param>
        /// <returns></returns>
        private List<string> SearchByDummy(string dummy)
        {
            return new List<string> { dummy };
        }

        /// <summary>
        /// デバイスマネージャーのように、COMポート番号とフレンドリーネーム（例："USB Serial Port (COM3)"）を返すメソッド
        /// </summary>
        /// <param name="onlyAvailable">trueの場合、実際にポートを開けるポートのみを返す</param>
        /// <returns>
        /// COMポートの情報を格納した<see cref="ComPortInfo"/>のリスト
        /// </returns>
        public static List<ComPortInfo> GetDeviceManagerStylePortsInfo(bool onlyAvailable)
        {
            // var ports = GetDeviceManagerFromWin32SerialPort();
            var ports = GetDeviceManagerFromWin32PnPEntity();
            if (onlyAvailable)
            {
                var result = new List<ComPortInfo>();
                foreach (var port in ports)
                {
                    if (IsPortAvailable(port.Port))
                        result.Add(port);
                }
                if (result.Count > 0)
                    return result;
                else
                {
                    return new List<ComPortInfo>
                        {
                            new ComPortInfo
                            {
                                Name = "DEBUG0",
                                DeviceId = "DEBUG0",
                                Description = "ダミーポート0",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG0" }
                            },
                            new ComPortInfo
                            {
                                Name = "DEBUG1",
                                DeviceId = "DEBUG1",
                                Description = "ダミーポート1",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG1" }
                            },
                            new ComPortInfo
                            {
                                Name = "DEBUG2",
                                DeviceId = "DEBUG2",
                                Description = "ダミーポート2",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG2" }
                            }
                        };
                }
            }
            else
                if (ports.Count > 0)
                return ports;
            else
            {
                return new List<ComPortInfo>
                        {
                            new ComPortInfo
                            {
                                Name = "DEBUG0",
                                DeviceId = "DEBUG0",
                                Description = "ダミーポート0",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG0" }
                            },
                            new ComPortInfo
                            {
                                Name = "DEBUG1",
                                DeviceId = "DEBUG1",
                                Description = "ダミーポート1",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG1" }
                            },
                            new ComPortInfo
                            {
                                Name = "DEBUG2",
                                DeviceId = "DEBUG2",
                                Description = "ダミーポート2",
                                DeviceManufacturer = "DEBUG",
                                hardwareId = new string[] { "DEBUG2" }
                            }
                        };
            }
        }


        private static List<ComPortInfo> GetDeviceManagerFromWin32SerialPort()
        {
            var result = new List<ComPortInfo>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                result.Add(new ComPortInfo
                {
                    Name = queryObj["Name"]?.ToString() ?? "",
                    DeviceId = queryObj["DeviceID"]?.ToString() ?? "",
                    Description = queryObj["Description"]?.ToString() ?? "",
                    DeviceManufacturer = queryObj["Manufacturer"]?.ToString() ?? "",
                    hardwareId = queryObj["hardwareId"] as string[],
                });
            }
            return result;
        }

        private static List<ComPortInfo> GetDeviceManagerFromWin32PnPEntity()
        {
            var result = new List<ComPortInfo>();
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                result.Add(new ComPortInfo
                {
                    Name = queryObj["Name"]?.ToString() ?? "",
                    DeviceId = queryObj["DeviceID"]?.ToString() ?? "",
                    Description = queryObj["Description"]?.ToString() ?? "",
                    DeviceManufacturer = queryObj["Manufacturer"]?.ToString() ?? "",
                    hardwareId = queryObj["hardwareId"] as string[],
                });
            }
            return result;
        }
    }
}