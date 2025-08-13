using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace GARDENs_GS_Software.Library
{
    class Kantronics : Customize_Serial, ITnc
    {
        //--------------------------------------------------------
        private ConcurrentQueue<string> ReceivePacketData = new ConcurrentQueue<string>();   //受信データ保存先
        private Thread receiveThread;
        private volatile static bool receiveFlag;
        private static bool kissFlag;
        public string ModelName => "9612XE";
        // public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.HardwareId, @"FTDIBUS\VID_0403+PID_6001+7&2176249&0&2\0000");
        public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.HardwareId, @"FTDIBUS\\COMPORT&VID_0403&PID_6001");
        //--------------------------------------------------------

        public void SetPort(string _port)
        {
            SetSerial(_port, 9600, 100, 1);
        }

        /// <summary>
        /// TNCにパケットデータを送信
        /// </summary>
        /// <param name="packetData">送信データ:str</param>
        public void SendPacket(string packetData)
        {
            List<byte> kissFrame = new List<byte>
            {
                0xC0, // Frame start delimiter
                0x00, // Command byte (default to 0x00)
                0x42    // Birds Header(0x42)
            };
            
            string[] _data = packetData.Split(' ');
            
            for (int i = 0 ; i < _data.Length; i++)
            {
                switch (_data[i])
                {
                    case "C0":
                        kissFrame.Add(0xDB);
                        kissFrame.Add(0xDC);
                        break;
                    case "DB":
                        kissFrame.Add(0xDB);
                        kissFrame.Add(0xDD);
                        break;
                    default:
                        
                        kissFrame.Add(Convert.ToByte(_data[i], 16));
                        break;
                }
            }
            /*
            //KISSフレームに変換
            foreach (byte b in packetData)
            {
                switch (b)
                {
                    case 0xC0:
                        kissFrame.Add(0xDB);
                        kissFrame.Add(0xDC);
                        break;
                    case 0xDB:
                        kissFrame.Add(0xDB);
                        kissFrame.Add(0xDD);
                        break;
                    default:
                        kissFrame.Add(b);
                        break;
                }
            }
            */

            kissFrame.Add(0xC0); // Frame end delimiter

            Debug.WriteLine("Send to TNC");
            WriteDataByte(kissFrame.ToArray());
        }

        /// <summary>
        /// TNCとの接続
        /// </summary>
        /// <returns>false<br/>シリアルポートが開いていない<br/>TNCの情報が読み取れない</returns>
        public bool Connect()
        {
            const string radioName = "9612";
            byte[] EXIT_KISS = { 0xC0, 0xFF, 0xC0 };
            string data = null;

            Debug.WriteLine("\rKantronics接続処理");
            try
            {
            OpenStream();
            }
            catch (Exception e)
            {
                throw new Exception( e.Message);
            }

            if (!IsOpen)
            {
                Debug.WriteLine("Error");
                return false;
            }

            // エラー防止のための入力
            Thread.Sleep(500);
            WriteDataString("\r");
            WriteDataString("\r");
            data = ReadData();
            if (data.Length < 1)
            {
                // TERMINALモードに変更
                Debug.WriteLine("CHANGE TERMINAL MODE");
                WriteDataByte(EXIT_KISS);
                Thread.Sleep(500);
                WriteDataString("\r");
                DiscardInBuffer();
            }

            DiscardInBuffer();
            WriteDataString("VERSION\r"); // TNCのバージョンを取得
            data = ReadData();
            if (data.Contains(radioName))
            {
                WriteDataString("HBAUD 4800\r");        // 無線機へのボーレートを4800bpsに設定
                WriteDataString("ABAUD 9600\r");        // シリアルのボーレートを9600bpsに設定
                WriteDataString("XMITLVL 100/27\r");
                WriteDataString("MYDROP 1/0\r");
                WriteDataString("PORT 2\r");            // 無線機との通信をPORT2に設定
                WriteDataString("TXDELAY 100/100\r");
                WriteDataString("AXDELAY 0/0\r");
                WriteDataString("MAXUSERS 0/1\r");
                WriteDataString("INTF KISS\r");      // KISSモードに変更
                WriteDataString("RESET\r");           // TNCの再起動
                Debug.WriteLine(ReadExisting());

                DiscardInBuffer();
                Thread.Sleep(100);
                Debug.WriteLine("Finish TNC Setup");

                ReceiveStart();
                return true;
            }
            else
            {
                Debug.WriteLine("Kantronicsからデータを読み取れません");

                return false;
            }
        }

        public void Disconnect()
        {
            Debug.WriteLine("Kantronics切断処理");
            ReceiveStop();
            Debug.WriteLine("受信スレッド終了");
            Thread.Sleep(100);
            Debug.WriteLine("切断処理開始");
            CloseStream();
            Debug.WriteLine("切断処理終了");
        }

        /// <summary>
        /// TNCからの受信パケットデータ
        /// </summary>
        /// <returns></returns>
        private void ReadPacket()
        {
            while (IsOpen && receiveFlag)
            {
                try
                {
                    // 初期バッファの設定
                    byte[] buffer = new byte[1];
                    List<byte> packet = new List<byte>();
                    bool inFrame = false;

                    // データ読み取りループ
                    while (IsOpen && receiveFlag)
                    {
                        if (BytesToRead > 0)
                        {
                            int bytesRead = Read(buffer, 0, 1); // 1バイトずつ読み取る

                            if (bytesRead > 0)
                            {
                                if (buffer[0] == 0xC0)
                                {
                                    if (inFrame)
                                    {
                                        // フレームの終了を見つけた
                                        break;
                                    }
                                    else
                                    {
                                        // フレームの開始を見つけた
                                        inFrame = true;
                                    }
                                }
                                else if (inFrame)
                                {
                                    packet.Add(buffer[0]); // フレーム内のデータを追加
                                    Debug.WriteLine($"packet = {buffer[0]}");
                                }
                            }
                        }
                        if (!inFrame)
                        {
                            // 最初のC0が入るまでは待機時間あり 0.1s
                            Thread.Sleep(100); // 少し待つ
                        }
                    }

                    if (packet.Count > 0)
                    {
                        byte[] actualData = packet.ToArray();
                        string tncData = BitConverter.ToString(actualData).Replace("-", " ");
                        
                        // C0の置き換え
                        tncData = tncData.Replace("DB DC", "C0");
                        tncData = tncData.Replace("DB DD", "DB");

                        // プリアンブル削除
                        // if (tncData.Length > 6)
                        // {
                        //     tncData = tncData.Substring(6);
                        // }
                        // if (tncData.Length > 3)
                        if (tncData.Length > 6)
                        {
                            tncData = tncData.Substring(3);
                            // tncData = tncData.Substring(0, tncData.Length - 3);
                        }

                        Debug.WriteLine("Receive Data: " + tncData);
                        ReceivePacketData.Enqueue(tncData.ToLower());

                        string packetData = ReadKissPacket(actualData); // 同一クラス内のメソッド使用においてクラス名は不要
                        if (!string.IsNullOrEmpty(packetData))
                        {
                            Debug.WriteLine("Add Data\n" + packetData);
                        }
                    }
                    // Thread.Sleep(100);
                }
                catch (TimeoutException)
                {
                    // タイムアウト例外を適切に処理
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error: {ex.Message}");
                    receiveFlag = false;
                }
            }
            Debug.WriteLine("TNC ReceiveThreadFin");
        }




        static private string ReadKissPacket(byte[] data) // 静的メソッドの宣言
        {
            
            string packetData = BitConverter.ToString(data).Replace("-", " ");;
            /*string tncData = ReadByte().ToString("x2"); // 0x00表記に変更
            if (tncData == "c0")
            {
                packetData += tncData + " ";
                tncData = ReadByte().ToString("x2").ToUpper();
                packetData += tncData + " ";
                while (tncData != "c0")
                {
                    tncData = ReadByte().ToString("x2");
                    packetData += tncData + " ";
                }

                // プリアンブル削除シーケンス
                if (packetData.Length > 6)
                {
                    packetData = packetData.Substring(6);
                }
                if (packetData.Length > 3)
                {
                    packetData = packetData.Substring(0, packetData.Length - 3);
                }

                // C0の置き換え
                packetData = packetData.Replace("DB DC", "C0");
                packetData = packetData.Replace("DB DD", "DB");
            }*/
            return packetData;
        }

        /// <summary>
        /// 受信停止
        /// </summary>
        public void ReceiveStop()
        {
            receiveFlag = false;
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join();
                Debug.WriteLine("Stop Recieve Thread");
            }
            else
            {
                Debug.WriteLine("Stop Recieve Thread Warning");
            }
        }

        /// <summary>
        /// 受信再開
        /// </summary>
        public void ReceiveStart()
        {
            receiveFlag = true;
            receiveThread = new Thread(ReadPacket);
            receiveThread.Start();
            Debug.WriteLine("Start Recieve Process");
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Debug.WriteLine("Data Received:");
            Console.Write(indata);

        }

        public void SetKiss(bool _state)
        {
            // 実装方法不明
            kissFlag = _state;
        }

        public string GetPacket()
        {
            string result = "";
            if (!ReceivePacketData.IsEmpty)
            {
                ReceivePacketData.TryDequeue(out result);
            }
            
            return result;
        }
    }
}
