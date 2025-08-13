using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace GARDENs_GS_Software.Library
{
    internal class Icom : Customize_Serial, IRadio
    {
        //------------------------------------------------------------------------------------------------

        public string ModelName => "ICOM";
        public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.Name, "Silicon Labs CP210x USB to UART Bridge");
        public string radioName => "IC-9100";
        private byte rxAdr;
        private byte txAdr;

        // private uint uplinkFrequency, downlinkFrequency;

        //------------------------------------------------------------------------------------------------
        public Icom() { }

        public void SetPort(string _port)
        {
            SetSerial(_port, 9600, 100, 1);
        }

        /// <summary>
        /// ICOM製無線機とのパケット通信を確立<br />
        /// 初期設定込み<br />
        /// </summary>
        public bool Connect()
        {
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
                Debug.WriteLine("ICOM無線機接続失敗");
                return false;
            }
            else
            {
                // 無線機操作アドレス設定
                if (radioName == "IC-9100")
                {
                    txAdr = 0x00;
                    rxAdr = 0x00;
                }
                else if (radioName == "IC-7100")
                {
                    txAdr = 0x00;
                    rxAdr = 0x00;
                }
                else
                {
                    txAdr = 0x00;
                    rxAdr = 0x00;
                }


                WriteDataByte(GenerateCommand(0x19, 0x00));     //本体のIDコード読み込み
                                                                //byte[] readData = GetData();
                                                                // if (false) //readData[3] == 0x00
                                                                // {
                                                                // Debug.WriteLine("ICOM無線機 データが読み取れません");
                                                                // return false;
                                                                // }
                                                                // else
                                                                // {
                Debug.WriteLine("ICOM無線機 接続成功");
                //------------------初期設定--------------

                WriteDataByte(GenerateCommand(VFO, 0x01));      // スプリット運用有効化
                WriteDataByte(GenerateCommand(VFO, 0xFF));      // VFOモードを有効化
                WriteDataByte(GenerateCommand(QSPLIT, 0x1A, 0x05, 0x00, 0x14, 0x01));
                ChangeFrequency(43685000, 437375000);           // 送信周波数を436.850MHz, 受信周波数を437.375MHz
                ChangeRxMode("FM-D");                           // 受信モードを"FM-D"
                ChangeTxMode("FM-D");                           // 送信モードを"FM-D"
                ChangeTxLowPower();                             // 送信出力を5W
                                                                //WriteDataByte((GenerateCommand(0x0F, 0x01)));
                WriteDataByte((GenerateCommand(0x0F, 0x11)));
                WriteDataByte((GenerateCommand(0x0F, 0x10)));
                Debug.WriteLine("ICOM無線機 初期設定終了");

                return true;
                // }
            }

        }

        /// <summary>
        /// ICOM無線機とのシリアル通信を解除
        /// </summary>
        /// <returns></returns>
        public void Disconnect()
        {
            Debug.WriteLine("ICOM無線機 切断処理");
            WriteDataByte((GenerateCommand(0x0F, 0x00)));   // スプリットをOFFにする
            CloseStream();
            if (!IsOpen)
            {
                Debug.WriteLine("ICOM無線機 切断成功");
            }
            else
            {
                Debug.WriteLine("ICOM無線機 切断失敗");
            }
        }

        /// <summary>
        /// ICOM無線機データ受信
        /// </summary>
        /// <returns>byte[]</returns>
        internal byte[] GetData()
        {
            byte[] readData = { 0xFD }; //修正
            return readData;
        }

        /// <summary>
        /// 周波数の変更
        /// </summary>
        /// <param name="_uplinkFrequency">送信周波数</param>
        /// <param name="_downlinkFrequency">受信周波数</param>
        public void ChangeFrequency(uint _uplinkFrequency, uint _downlinkFrequency)
        {
            Debug.WriteLine($"uplink = {_uplinkFrequency}, downlink = {_downlinkFrequency}");

            uint ofsetfreq = _downlinkFrequency - _uplinkFrequency;
            byte[] splitoffset = new byte[13];
            splitoffset[0] = 0xFE;
            splitoffset[1] = 0xFE;
            splitoffset[2] = 0x7C;
            splitoffset[3] = 0x00;
            splitoffset[4] = 0x1A;
            splitoffset[5] = 0x05;
            splitoffset[6] = 0x00;
            splitoffset[7] = 0x17;
            byte hexValue;
            uint digit1 = ofsetfreq / 100 % 10;          // 1kHz
            uint digit2 = ofsetfreq / 1000 % 10;          // 100Hz
            hexValue = Convert.ToByte(digit2 * 16 + digit1);
            splitoffset[8] = hexValue;
            digit1 = ofsetfreq / 10000 % 10;        // 100kHz
            digit2 = ofsetfreq / 100000 % 10;        // 10kHzw
            hexValue = Convert.ToByte(digit2 * 16 + digit1);
            splitoffset[9] = hexValue;
            digit1 = ofsetfreq / 1000000 % 10;        // 10MHz
            digit2 = ofsetfreq / 10000000 % 10;        // 1MHzw
            hexValue = Convert.ToByte(digit2 * 16 + digit1);
            splitoffset[10] = hexValue;
            splitoffset[12] = 0xFD;
            WriteDataByte(GenerateCommand(FREQ, 0xFF, _downlinkFrequency));   // downfreq周波数を設定
            Debug.WriteLine("OFFSET " + ofsetfreq);
            WriteDataByte(splitoffset);
            //WriteDataByte((GenerateCommand(0x0F, 0x01)));
            WriteDataByte((GenerateCommand(0x0F, 0x11)));   //DUP--
        }

        public void ChangeReceiveMode(string _mode)
        {
            ChangeRxMode(_mode, [0, 0]);
        }

        /// <summary>
        /// 受信モード変更
        /// </summary>
        /// <param name="mode">"CW" or "FM-D"</param>
        public void ChangeRxMode(string mode, params int[] filter)
        {
            WriteDataByte(GenerateCommand(VFO, 0x00));                  // VFO-Aを選択

            if (mode == "CW-U")
            {
                WriteDataByte(GenerateCommand(MODE, 0xFF, 0x03));       // 受信モードをCWに設定
                WriteDataByte(GenerateCommand(0x1A, 0x06, 0x00, 0x00)); // データモードを無効
                //WriteDataByte((GenerateCommand(0x0F, 0x01)));
                WriteDataByte((GenerateCommand(0x0F, 0x11)));
            }
            else if (mode == "FM-D")
            {
                WriteDataByte(GenerateCommand(MODE, 0xFF, 0x05));       // 受信モードをFMに設定
                WriteDataByte(GenerateCommand(0x1A, 0x06, 0x01, 0x01)); // データモードを有効
                //WriteDataByte((GenerateCommand(0x0F, 0x01)));
                WriteDataByte((GenerateCommand(0x0F, 0x11)));
            }
        }

        /// <summary>
        /// 受信モード変更
        /// </summary>
        /// <param name="mode">"CW" or "FM-D"</param>
        public void ChangeTxMode(string mode, params int[] filter)
        {
            WriteDataByte(GenerateCommand(VFO, 0x00));                  // VFO-Aを選択

            if (mode == "CW")
            {
                WriteDataByte(GenerateCommand(MODE, 0xFF, 0x03));       // 受信モードをCWに設定
                WriteDataByte(GenerateCommand(0x1A, 0x06, 0x00, 0x00)); // データモードを無効
            }
            else if (mode == "FM-D")
            {
                WriteDataByte(GenerateCommand(MODE, 0xFF, 0x05));       // 受信モードをFMに設定
                WriteDataByte(GenerateCommand(0x1A, 0x06, 0x01, 0x01)); // データモードを有効
            }
        }

        /// <summary>
        /// 出力を5Wにする
        /// </summary>
        public void ChangeTxLowPower()
        {

        }

        /// <summary>
        /// ICOM無線機のシリアル通信制御コマンド生成<br />
        /// </summary>
        /// <param name="cmd">コマンド</param>
        /// <param name="subcmd">subcmd無し:"0xFF"</param>
        /// <param name="data">パラメータ</param>
        /// 
        private byte[] GenerateCommand(byte cmd, byte subcmd, params uint[] data)
        {
            List<byte> cmdList = new List<byte>();
            List<byte> dataArea = new List<byte>();

            // データエリアコマンド
            switch (cmd)
            {
                case FREQ:
                    //周波数設定
                    uint _freq = data[0];
                    uint digit1;
                    uint digit2;

                    digit1 = _freq % 10;                // 1Hz
                    digit2 = _freq / 10 % 10;           // 10Hz
                    byte hexValue = Convert.ToByte(digit2 * 16 + digit1);
                    dataArea.Add(hexValue);

                    digit1 = _freq / 100 % 10;          // 1kHz
                    digit2 = _freq / 1000 % 10;          // 100Hz
                    hexValue = Convert.ToByte(digit2 * 16 + digit1);
                    dataArea.Add(hexValue);

                    digit1 = _freq / 10000 % 10;        // 100kHz
                    digit2 = _freq / 100000 % 10;        // 10kHzw
                    hexValue = Convert.ToByte(digit2 * 16 + digit1);
                    dataArea.Add(hexValue);

                    digit1 = _freq / 1000000 % 10;      // 10MHz
                    digit2 = _freq / 10000000 % 10;      // 1MHz
                    hexValue = Convert.ToByte(digit2 * 16 + digit1);
                    dataArea.Add(hexValue);

                    digit1 = _freq / 100000000 % 10;    // 1GHz
                    digit2 = _freq / 1000000000 % 10;   // 100MHz
                    hexValue = Convert.ToByte(digit2 * 16 + digit1);
                    dataArea.Add(hexValue);

                    byte[] dataAreaByte = dataArea.ToArray();
                    Debug.WriteLine(BitConverter.ToString(dataAreaByte).Replace("-", " "));
                    break;

                default:
                    for (int i = 0; i < data.Length; i++)
                    {
                        dataArea.Add(Convert.ToByte(data[i]));
                    }
                    break;
            }

            //コマンド作成
            cmdList.Add(0xFE);              // プリアンブル
            cmdList.Add(0xFE);              // プリアンブル
            cmdList.Add(txAdr);             // 受信アドレス
            cmdList.Add(rxAdr);             // 送信アドレス
            cmdList.Add(cmd);               // コマンド
            if (subcmd != 0xFF)
            {
                cmdList.Add(subcmd);        // サブコマンド
            }
            cmdList.AddRange(dataArea);     // データエリア
            cmdList.Add(0xFD);              // ポストアンブル 

            byte[] cmdByte = cmdList.ToArray();
            Debug.WriteLine(BitConverter.ToString(cmdByte).Replace("-", " "));
            return cmdByte;
        }


        //-------------------------------------Command----------------------------------------

        /// <summary>
        /// 周波数を設定
        /// 437375000 Hz
        /// </summary>
        private const byte FREQ = 0x00;

        /// <summary>
        /// 運用モードの設定
        /// FM : 0x05, CW: 0x03
        /// </summary>
        private const byte MODE = 0x01;

        /// <summary>
        /// VFOモード設定<br />
        /// subcmd/ VFO有効化:null,VFO-A:0x00, VFO-B:0x01<br />
        /// MAINバンドの選択 : 0xD0, SUBバンドの選択 : 0xD1<br />
        /// </summary>
        private const byte VFO = 0x07;

        /// <summary>
        /// スプリット運用設定<br />
        /// 0x0014 / 0x00=OFF, 0x01=ON
        /// </summary>
        private const byte SPLIT = 0x0F;

        /// <summary>
        /// クイックスプリット運用設定<br />
        /// 0x0014 / 0x00=OFF, 0x01=ON
        /// </summary>
        private const byte QSPLIT = 0x1A;

        /// <summary>
        /// RF POWERの設定<br />
        /// subcmd  / RF POWERの設定:0x0A <br />
        /// param   / 0~255<br />
        /// </summary>
        private const byte POWER = 0x14;
    }
}
