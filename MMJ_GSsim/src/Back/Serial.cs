using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

//install "System.IO.Ports"


namespace GARDENs_GS_Software.Library
{
    /// <summary>
    /// シリアル通信制御(1ストリーム1インスタンス) <br />
    /// </summary>
    class Customize_Serial : SerialPort
    {
        /// <summary>
        /// シリアル設定<br />
        /// </summary>x
        /// <param name="port">COMポート. 例: "COM13". [str]</param>
        /// <param name="baud">- baud : ボーレート. デフォルトは9600.(任意)　</param>
        /// <param name="timeout">タイムアウト. デフォルトはNone. ms単位 (任意)</param>
        /// <param name="stop_bit">ストップビット(0,1,2). デフォルトは1.  (任意)</param>
        public void SetSerial(string port, int baud, int timeout, int stop_bit)
        {
            PortName = port;
            BaudRate = baud;
            ReadTimeout = timeout;
            WriteTimeout = timeout;
            //DataBits = 8;
            //Parity = Parity.None;
            //Handshake = Handshake.None;

            if      (stop_bit == 1)
                StopBits = StopBits.One;
            else if (stop_bit == 2)
                StopBits = StopBits.Two;
            else
                StopBits = StopBits.None;

            Debug.WriteLine("port =" + PortName);
            Debug.WriteLine(" ,baud =" + BaudRate);
            Debug.WriteLine(" ,timeout =" + ReadTimeout);
            Debug.WriteLine(" ,stop_bit =" + stop_bit);
        }

        /// <summary>
        /// ストリームを開設
        /// </summary>
        public void OpenStream()
        {
            if (PortName == null)
            {
                Debug.WriteLine("COMポートが設定されていません");
                return;
            }

            try
            {
                Open();
                if (IsOpen)
                    Debug.WriteLine(PortName + " シリアルポートが正常に開かれました。");
                else
                    Debug.WriteLine(PortName + " シリアルポートの開くのに失敗しました。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(PortName + " シリアルポートの開くのに失敗しました。エラーメッセージ: " + ex.Message);
                throw new Exception(PortName + " " + ex.Message);
            }
        }

        /// <summary>
        /// ストリームを閉じる
        /// </summary>
        public void CloseStream()
        {
            if (!IsOpen)
            {
                Debug.WriteLine("Unknown stream. It cannot close stream. [{0}]", PortName);
                return;
            }

            Close();
            if (!IsOpen)
                Debug.WriteLine(PortName + " シリアルポートが正常に閉じられました。");
            else
                Debug.WriteLine(PortName + " シリアルトートが正常に閉じられませんでした。");
        }

        /// <summary>
        /// ストリームを削除
        /// </summary>
        public void ResetStream()
        {
            Debug.WriteLine("Attempting to reset stream");
            CloseStream();
            Debug.WriteLine("Stream reset successfully");
        }

        /// <summary>
        /// バイナリデータを送信
        /// </summary>
        /// <param name="txt"></param>
        public void WriteDataByte(byte[] bytes)
        {
            Thread.Sleep(100);
            if (PortName == null | !IsOpen)
            {
                Debug.WriteLine(PortName, "が開いていません");
                return;
            }
            else
            {
                //Debug.WriteLine(bytes.Length);
                Write(bytes, 0, bytes.Length);
                Debug.WriteLine("write byte data: " + BitConverter.ToString(bytes).Replace("-", " "));
                return;
            }
        }

        /// <summary>
        /// テキストデータを送信
        /// </summary>
        /// <param name="txt"></param>
        public void WriteDataString(string txt)
        {

            if (PortName == null | !IsOpen)
            {
                Debug.WriteLine("unknown stream. it cannot send. [Port: {0}]", PortName);
                return;
            }

            Write(txt);
            Debug.WriteLine("write string data: " + txt);
            Thread.Sleep(100);
            return;
        }

        /// <summary>
        /// 入力バッファー内の指定した value まで文字列を読み取り
        /// </summary>
        /// </<returns>Byte</returns>
        public string ReadData()
        {
            if (!IsOpen)
            {
                return "-1";
            }
            string txt = string.Empty;
            txt = ReadExisting();

            Debug.WriteLine("read data : " + txt);
            return txt;
        }
    }
}