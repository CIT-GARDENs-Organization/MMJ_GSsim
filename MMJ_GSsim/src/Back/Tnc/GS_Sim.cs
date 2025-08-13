using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GARDENs_GS_Software.Library
{
    class GSsim : Customize_Serial, ITnc
    {
        public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.Dummy, "");
        public string ModelName => "GS-Sim";

        // private string port = "COM0";
        private bool kissFlg = false;
        private bool receiveFlg = false;

        private Thread receiveThread;
        //private bool stopReceiveThread = false;
        private ConcurrentQueue<string> receivePacketData = new();

        public void SetPort(string _port)
        {
            SetSerial(_port, 115200, 100, 1);
        }

        /// <summary>
        /// 衛星にパケットデータを送信
        /// </summary>
        /// <param name="packetData">送信データ:str</param>
        public void SendPacket(string packetData)
        {
            List<byte> txData =
            [
                0x42    // Birds Header(0x42="B")
            ];

            string[] _data = packetData.Split(' ');

            for (int i = 0; i < _data.Length; i++)
                txData.Add(Convert.ToByte(_data[i], 16));

            UInt32 crc = CalculateCRC(txData);

            txData.Add(Convert.ToByte(crc & 0xFF));
            txData.Add(Convert.ToByte(crc >> 8));

            Debug.WriteLine("Send to TNC");
            WriteDataByte([.. txData]);

        }

        private static UInt32 CalculateCRC(List<byte> data)
        {
            UInt32 crcReg = 0xFFFF;
            UInt32 calc = 0x8408;
            Byte w;
            Byte[] cal_data = new Byte[data.Count];

            // fprintf(PORT1,"\r\n CRC start\r\n ");
            for (int k = 0; k < cal_data.Length; k++)
            {
                cal_data[k] = data[k];
                //fprintf(PORT1,"data = %x\r\n ",data[k]);
                for (int i = 0; i < 8; i++)
                {
                    w = (Byte)((crcReg ^ cal_data[k]) & 0x0001);
                    crcReg = crcReg >> 1;

                    if (w == 1)
                        crcReg = crcReg ^ calc;

                    cal_data[k] = (byte)(cal_data[k] >> 1);

                }

            }

            crcReg = crcReg ^ 0xFFFF;

            return crcReg;
        }

        public bool Connect()
        {
            Debug.WriteLine("GS-Sim接続処理");
            try
            {
                OpenStream();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            if (!IsOpen)
            {
                Debug.WriteLine("Error");
                return false;
            }

            ReceiveStart();
            return true;
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

        private void ReceiveStart()
        {

            receiveFlg = true;
            receiveThread = new Thread(ReadPacket);
            receiveThread.Start();
            Debug.WriteLine("Start Recieve Process");
        }

        /// <summary>
        /// TNCからの受信パケットデータ
        /// </summary>
        /// <returns></returns>
        private void ReadPacket()
        {
            while (IsOpen && receiveFlg)
            {
                try
                {
                    // 初期バッファの設定
                    byte[] buffer = new byte[1];
                    List<byte> packet = [];

                    // データ読み取りループ
                    while (IsOpen && receiveFlg)
                    {
                        if (BytesToRead > 0)
                        {
                            //Thread.Sleep(50);
                            while(BytesToRead > 0)
                            {
                                int bytesRead = Read(buffer, 0, 1); // 1バイトずつ読み取る
                                if (bytesRead > 0)
                                    packet.Add(buffer[0]); // フレーム内のデータを追加
                                                           //Debug.WriteLine($"packet = {buffer[0]}");
                                //Thread.Sleep(1);
                            }

                            if (packet.Count > 2)
                            {
                                UInt32 receive_crc = (UInt32)(packet[^1] * 0x100 + packet[^2]);
                                packet.RemoveAt(packet.Count - 1);
                                packet.RemoveAt(packet.Count - 1);
                                UInt32 calc_crc = CalculateCRC(packet);

                                if (calc_crc != receive_crc)
                                {
                                    Debug.WriteLine("CRC ERROR");
                                    packet.Clear();
                                }
                                else
                                    break;
                            }
                            else
                            {
                                packet.Clear();
                            }
                        }
                        else
                        {
                            // 最初のC0が入るまでは待機時間あり 0.1s
                            Thread.Sleep(100); // 少し待つ
                        }
                    }

                    if (packet.Count > 0)
                    {
                        byte[] actualData = [.. packet];
                        string tncData = BitConverter.ToString(actualData).Replace("-", " ");

                        /*if (tncData.Length > 6)
                        {
                            tncData = tncData.Substring(3);
                            // tncData = tncData.Substring(0, tncData.Length - 3);
                        }*/

                        Debug.WriteLine("Receive Data: " + tncData);
                        receivePacketData.Enqueue(tncData.ToLower());

                        string packetData = BitConverter.ToString([.. EncodeKiss(tncData)]).Replace("-", " ");
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
                    receiveFlg = false;
                }
            }
            Debug.WriteLine("TNC ReceiveThreadFin");
        }

        /// <summary>
        /// TNCにパケットデータを送信
        /// </summary>
        /// <param name="packetData">送信データ:str</param>
        private static List<byte> EncodeKiss(string packetData)
        {
            List<byte> kissFrame =
            [
                0xC0, // Frame start delimiter
                0x00, // Command byte (default to 0x00)
            ];

            string[] _data = packetData.Split(' ');

            for (int i = 0; i < _data.Length; i++)
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

            return kissFrame;
        }

        private void ReceiveStop()
        {
            receiveFlg = false;
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

        public void SetKiss(bool _state)
        {
            // 実装方法不明
            kissFlg = _state;
        }

        public string GetPacket()
        {
            string result = "";
            if (!receivePacketData.IsEmpty)
            {
                receivePacketData.TryDequeue(out result);
            }

            return result;
        }
    }
}
