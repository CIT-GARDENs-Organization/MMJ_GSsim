using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using GARDENs_GS_Software.Library;

namespace GARDENs_GS_Software.Library
{
    // 接続できない場合は
    // 033:"CAT TRS"をDISABLEにする

    /// <summary>
    /// 八重洲無線機
    /// </summary>
    internal class Yaesu : Customize_Serial, IRadio
    {
        //------------------------------------------------------------------------------------------------

        public string ModelName => "YAESU";
        public (ComPortSearchType type, string value) AutoComMarker => (ComPortSearchType.Name, "Silicon Labs Dual CP2105 USB to UART Bridge: Enhanced COM Port");

        //------------------------------------------------------------------------------------------------

        public void SetPort(string _port)
        {
            SetSerial(_port, 9600, 100, 2);
            RtsEnable = true;
        }

        /// <summary>
        /// 八重洲無線機とのパケット通信を確立<br />
        /// 初期設定込み<br />
        /// </summary>
        public bool Connect()
        {
            Debug.WriteLine("八重洲無線機 接続処理");
            try
            {
                OpenStream();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            WriteDataByte(GenerateCommand(ID));                         // 無線機IDの取得
            Thread.Sleep(10);
            string radio_id = GetData();

            Console.WriteLine(radio_id);

            if (radio_id == null)
            {
                Debug.WriteLine("無線機からデータが取得できません");
                return false;
            }

            //-------------------------------------------初期設定------------------------------------------------

            Debug.WriteLine("八重洲無線機 初期設定");

            WriteDataByte(GenerateCommand(FUNC_TX, "3"));               // VFO-Bを送信周波数に設定(スプリット運用)

            WriteDataByte(GenerateCommand(MENU, "062", "1"));           // DATA MODEをOTHERSに設定
            WriteDataByte(GenerateCommand(MENU, "070", "1"));           // DATA IN  をREARに設定
            WriteDataByte(GenerateCommand(MENU, "071", "0"));           // DATA PTT をDAKYに設定
            WriteDataByte(GenerateCommand(MENU, "072", "1"));           // DATA PORTをDATAに設定
            WriteDataByte(GenerateCommand(MENU, "073", "100"));         // DATA OUT LEVELを100に設定

            WriteDataByte(GenerateCommand(MENU, "074", "1"));           // FM MIC をREARに設定
            WriteDataByte(GenerateCommand(MENU, "075", "100"));         // FM OUT LEVELを100に設定


            WriteDataByte(GenerateCommand(MENU, "117", "0"));           // ディスプレイにスペクトラムを表示

            //---------------------------------------------------------------------------------------------------
            SetUpGMSK();

            Debug.WriteLine("八重洲無線機 接続処理終了");
            return true;

        }

        /// <summary>
        /// GMSK用設定
        /// </summary>
        public void SetUpGMSK()
        {
            ChangeTxMode("FM-D");                                       // 送信モードをFM-D
            ChangeRxMode("FM-D");                                       // 受信モードをFM-D
            WriteDataByte(GenerateCommand(MENU, "079", "1"));           // FM PKT MODEを9600に設定
            WriteDataByte(GenerateCommand(BAND, "16"));
            ChangeFrequency(43685000, 43685000);                        // 周波数を設定
            WriteDataByte(GenerateCommand("AB"));
            ChangeFrequency(43685000, 43685000);
            WriteDataByte(GenerateCommand(MENU, "076", "0"));           // FM PTT をDAKYに設定
            WriteDataByte(GenerateCommand(MENU, "077", "1"));           // FM PORTをDATAに設定
        }

        /// <summary>
        /// AFSK用設定
        /// </summary>
        public void SetUpAFSK()
        {
            ChangeTxMode("FM-D");                                       // 送信モードをFM-D
            ChangeRxMode("FM-D");                                       // 受信モードをFM-D
            WriteDataByte(GenerateCommand(MENU, "079", "9"));           // FM PKT MODEを1200に設定
            WriteDataByte(GenerateCommand(BAND, "15"));
            ChangeFrequency(145825000, 145825000);                      // 周波数を設定
            WriteDataByte(GenerateCommand(MENU, "076", "1"));           // FM PTT をRTSに設定
            WriteDataByte(GenerateCommand(MENU, "077", "2"));           // FM PORTをUSBに設定
        }

        /// <summary>
        /// Yaesu無線機とのシリアル通信を切断
        /// </summary>
        public void Disconnect()
        {
            Debug.WriteLine("八重洲無線機 切断処理");
            CloseStream();
        }

        /// <summary>
        /// FT-991AM データ受信
        /// </summary>
        /// <returns></returns>
        public string GetData()
        {
            string txt = string.Empty;

            txt = ReadData();

            return txt;
        }

        /// <summary>
        /// 送信周波数の変更
        /// </summary>
        /// <param name="uplinkFrequency">Transmit Frequency</param>
        /// <param name="downlinkFrequency">Receive Frequency</param>
        public void ChangeFrequency(uint uplinkFrequency, uint downlinkFrequency)
        {
            Debug.WriteLine("八重洲無線機 周波数変更");
            Debug.WriteLine($"uplink = {uplinkFrequency}, downlink = {downlinkFrequency}");
            WriteDataByte(GenerateCommand(VFO_A, Convert.ToString(downlinkFrequency)));
            WriteDataByte(GenerateCommand(VFO_B, Convert.ToString(uplinkFrequency)));
        }


        /// <summary>
        /// FT-991AM送信モード変更
        /// </summary>
        /// <param name="mode">"CW-U" or "FM-D" or "FM"</param>
        public void ChangeTxMode(string mode)
        {
            if (mode == "CW-U")
            {
                WriteDataByte(GenerateCommand(SWAP_VFO));
                WriteDataByte(GenerateCommand(MODE, "0", "3"));
                WriteDataByte(GenerateCommand(SWAP_VFO));
                Debug.WriteLine("八重洲無線機　送信モード : CW-U");
            }
            else if (mode == "FM-D")
            {
                WriteDataByte(GenerateCommand(SWAP_VFO));
                WriteDataByte(GenerateCommand(MODE, "0", "A"));
                WriteDataByte(GenerateCommand(SWAP_VFO));
                Debug.WriteLine("八重洲無線機 送信モード : FM-D");
            }
            else if (mode == "FM")
            {
                WriteDataByte(GenerateCommand(SWAP_VFO));
                WriteDataByte(GenerateCommand(MODE, "0", "4"));
                WriteDataByte(GenerateCommand(SWAP_VFO));
                Debug.WriteLine("八重洲無線機 送信モード : FM-D");
            }
        }

        public bool isEnableTransmit()
        {
            string _asnswer;
            WriteDataByte(GenerateCommand("TX"));
            Thread.Sleep(10);
            _asnswer = GetData();
            Debug.WriteLine(_asnswer);
            char thirdChar = _asnswer[2];

            if (thirdChar == 2)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        // interface統一用
        public void ChangeReceiveMode(string _mode)
        {
            ChangeRxMode(_mode);
        }

        /// <summary>
        /// FT-991AM受信モード変更
        /// </summary>
        /// <param name="mode">"CW-U" or "FM-D"</param>
        public void ChangeRxMode(string mode)
        {
            if (mode == "CW-U")
            {
                WriteDataByte(GenerateCommand(MODE, "0", "3"));
                Debug.WriteLine("受信モード : CW-U");
            }
            else if (mode == "FM-D")
            {
                WriteDataByte(GenerateCommand(MODE, "0", "A"));
                Debug.WriteLine("受信モード : FM-D");
            }

        }

        /// <summary>
        /// 八重洲無線機のシリアル通信制御コマンド生成<br /><br />
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="args"></param>
        private byte[] GenerateCommand(string cmd, params string[] args)
        {
            string cmd_txt = System.String.Empty;

            cmd_txt += cmd;

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    cmd_txt += args[i];
                }
            }
            cmd_txt += TERMINATOR;
            Debug.WriteLine($"command = {cmd_txt}");
            return Encoding.UTF8.GetBytes(cmd_txt);
        }


        //-------------------------------------Command----------------------------------------

        /// <summary>
        /// 周波数帯を選択
        /// Args(1):<br />
        /// Band : "15":144 MHz, "16":430MHz<br />
        /// </summary>
        private const string BAND_SELECT = "BS";

        /// <summary>
        /// メニュー設定<br />
        /// Args(2):<br />
        /// 1.MENU_Num : 3 digits. e.g. 075.<br />
        /// 2.Setting value :<br />
        /// </summary>
        private const string MENU = "EX";


        private const string BAND = "BS";

        /// <summary>
        /// VFO A周波数設定<br />
        /// Args(1):<br />
        /// Frequency : 9 digits. e.g. 437375000.<br />
        /// </summary>
        private const string VFO_A = "FA";

        /// <summary>
        /// VFO B周波数設定<br />
        /// Args(1):<br />
        /// Frequency : 9 digits. e.g. 437375000.<br />
        /// </summary>
        private const string VFO_B = "FB";

        /// <summary>
        /// 送信VFOの設定<br />
        /// Args(1):<br />
        /// VFO : VFO-A:2 / VFO-B:3<br />
        /// </summary>
        private const string FUNC_TX = "FT";

        /// <summary>
        /// 無線機ID読み出し<br />
        /// Returns:<br />
        /// ID0570;<br />
        /// </summary>
        private const string ID = "ID";

        /// <summary>
        /// モード切替<br />
        /// Args(2):<br />
        /// 1. MAIN_RX : 0<br />
        /// 2. MODE :CW-USB:3 / DATA-FM:A<br />
        /// </summary>
        private const string MODE = "MD";

        /// <summary>
        /// 送信出力設定<br />
        /// Args(1):<br />
        /// 1.Power : 3 digits : 005-050<br />
        /// </summary>
        private const string RF_POWER = "PC";

        /// <summary>
        /// 電源スイッチ操作<br />
        /// Args(1):<br />
        /// 1.Switch : OFF:0 / ON:1<br />
        /// </summary>
        private const string POWER_SW = "PS";

        /// <summary>
        /// A/Bボタン操作(VFO切り替え)<br />
        /// </summary>
        private const string SWAP_VFO = "SV";

        //------------------------------------------------------------------------------------

        /// <summary>
        /// 制御コマンドの終端
        /// </summary>
        private const string TERMINATOR = ";";

        //-------------------------------------------------------------------------------------

    }
}


