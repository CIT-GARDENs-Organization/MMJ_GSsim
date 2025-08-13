using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using GARDENs_GS_Software.Library;
using GARDENs_GS_Software.Back.Radio;
using System.Threading;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MMJ_GSsim.Front
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private IRadio _currentRadio;
        private ITnc _currentTnc;
        private bool _isRadioConnected = false;
        private bool _isTncConnected = false;
        
        // パケット監視用
        private CancellationTokenSource _packetMonitorCancellationTokenSource;
        private bool _isPacketMonitoringActive = false;

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Load available COM ports
            LoadComPorts();
            
            // Wire up event handlers
            RadioTypeComboBox.SelectionChanged += RadioTypeComboBox_SelectionChanged;
            TncTypeComboBox.SelectionChanged += TncTypeComboBox_SelectionChanged;
            ConnectButton.Click += ConnectButton_Click;
            
            // Set default values
            RadioTypeComboBox.SelectedIndex = 0;
            TncTypeComboBox.SelectedIndex = 0;
            
            UpdateConnectionStatus();
        }

        private void LoadComPorts()
        {
            try
            {
                var comPorts = CheckSerial.GetDeviceManagerStylePortsInfo(true);
                
                RadioComPortComboBox.ItemsSource = comPorts;
                RadioComPortComboBox.DisplayMemberPath = "ViewString";
                RadioComPortComboBox.SelectedValuePath = "Port";
                
                TncComPortComboBox.ItemsSource = comPorts;
                TncComPortComboBox.DisplayMemberPath = "ViewString";
                TncComPortComboBox.SelectedValuePath = "Port";
                
                if (comPorts.Count > 0)
                {
                    RadioComPortComboBox.SelectedIndex = 0;
                    if (comPorts.Count > 1)
                        TncComPortComboBox.SelectedIndex = 1;
                    else
                        TncComPortComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                AppendToHistory($"COM port loading error: {ex.Message}");
            }
        }

        private void RadioTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RadioTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string radioType = selectedItem.Content.ToString();
                
                // Update available COM ports based on radio type
                if (radioType == "YAESU" || radioType == "ICOM")
                {
                    LoadRadioSpecificPorts(radioType);
                }
            }
        }

        private void TncTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TNC selection changed - could add TNC-specific logic here
        }

        private void LoadRadioSpecificPorts(string radioType)
        {
            try
            {
                var allPorts = CheckSerial.GetDeviceManagerStylePortsInfo(true);
                var filteredPorts = new List<ComPortInfo>();

                foreach (var port in allPorts)
                {
                    if (radioType == "YAESU")
                    {
                        if (port.Name.Contains("Silicon Labs Dual CP2105") || 
                            port.Name.Contains("YAESU") ||
                            port.Name.StartsWith("DEBUG"))
                        {
                            filteredPorts.Add(port);
                        }
                    }
                    else if (radioType == "ICOM")
                    {
                        if (port.Name.Contains("Silicon Labs CP210x") || 
                            port.Name.Contains("ICOM") ||
                            port.Name.StartsWith("DEBUG"))
                        {
                            filteredPorts.Add(port);
                        }
                    }
                }

                // If no specific ports found, show all ports
                if (filteredPorts.Count == 0)
                {
                    filteredPorts = allPorts;
                }

                RadioComPortComboBox.ItemsSource = filteredPorts;
                if (filteredPorts.Count > 0)
                {
                    RadioComPortComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                AppendToHistory($"Radio port filtering error: {ex.Message}");
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isRadioConnected || _isTncConnected)
                {
                    // Disconnect
                    DisconnectDevices();
                }
                else
                {
                    // Connect
                    await ConnectDevices();
                }
            }
            catch (Exception ex)
            {
                AppendToHistory($"Connection error: {ex.Message}");
            }
        }

        private async Task ConnectDevices()
        {
            AppendToHistory("Starting connection process...");

            // Connect Radio
            if (RadioTypeComboBox.SelectedItem is ComboBoxItem radioItem && 
                RadioComPortComboBox.SelectedValue is string radioPort)
            {
                string radioType = radioItem.Content.ToString();
                AppendToHistory($"Connecting to {radioType} on {radioPort}...");

                try
                {
                    _currentRadio = CreateRadio(radioType);
                    if (_currentRadio != null)
                    {
                        _currentRadio.SetPort(radioPort);
                        _isRadioConnected = _currentRadio.Connect();
                        
                        
                        if (_isRadioConnected)
                        {
                            AppendToHistory($"Radio {radioType} connected successfully");
                        }
                        else
                        {
                            AppendToHistory($"Radio {radioType} connection failed");
                        }

                        _currentRadio.ChangeFrequency(436_850_000, 437_375_000);
                    }
                }
                catch (Exception ex)
                {
                    AppendToHistory($"Radio connection error: {ex.Message}");
                    _isRadioConnected = false;
                }
            }

            // Connect TNC
            if (TncTypeComboBox.SelectedItem is ComboBoxItem tncItem && 
                TncComPortComboBox.SelectedValue is string tncPort)
            {
                string tncType = tncItem.Content.ToString();
                AppendToHistory($"Connecting to {tncType} on {tncPort}...");

                try
                {
                    _currentTnc = CreateTnc(tncType);
                    if (_currentTnc != null)
                    {
                        _currentTnc.SetPort(tncPort);
                        _isTncConnected = _currentTnc.Connect();
                        
                        if (_isTncConnected)
                        {
                            AppendToHistory($"TNC {tncType} connected successfully");
                            // TNC接続成功時にパケット監視を開始
                            StartPacketMonitoring();
                        }
                        else
                        {
                            AppendToHistory($"TNC {tncType} connection failed");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendToHistory($"TNC connection error: {ex.Message}");
                    _isTncConnected = false;
                }
            }

            UpdateConnectionStatus();
        }

        private void DisconnectDevices()
        {
            AppendToHistory("Disconnecting devices...");

            // パケット監視を停止
            StopPacketMonitoring();

            if (_currentRadio != null)
            {
                try
                {
                    _currentRadio.Disconnect();
                    AppendToHistory("Radio disconnected");
                }
                catch (Exception ex)
                {
                    AppendToHistory($"Radio disconnect error: {ex.Message}");
                }
                _currentRadio = null;
                _isRadioConnected = false;
            }

            if (_currentTnc != null)
            {
                try
                {
                    _currentTnc.Disconnect();
                    AppendToHistory("TNC disconnected");
                }
                catch (Exception ex)
                {
                    AppendToHistory($"TNC disconnect error: {ex.Message}");
                }
                _currentTnc = null;
                _isTncConnected = false;
            }

            UpdateConnectionStatus();
        }

        private IRadio CreateRadio(string radioType)
        {
            return radioType switch
            {
                "YAESU" => new Yaesu(),
                "ICOM" => new Icom(),
                "DummyRadio" => new DummyRadio(),
                _ => null
            };
        }

        private ITnc CreateTnc(string tncType)
        {
            return tncType switch
            {
                "Kantronics" => new Kantronics(),
                "GS_Sim" => new GSsim(),
                _ => null
            };
        }

        private void UpdateConnectionStatus()
        {
            // Update status indicators
            RadioStatusIndicator.Fill = _isRadioConnected ? 
                new SolidColorBrush(Microsoft.UI.Colors.Green) : 
                new SolidColorBrush(Microsoft.UI.Colors.Gray);
                
            TncStatusIndicator.Fill = _isTncConnected ? 
                new SolidColorBrush(Microsoft.UI.Colors.Green) : 
                new SolidColorBrush(Microsoft.UI.Colors.Gray);

            // Update connect button
            bool anyConnected = _isRadioConnected || _isTncConnected;
            ConnectButton.Content = anyConnected ? "切断" : "接続";
            
            // Enable/disable controls
            RadioTypeComboBox.IsEnabled = !anyConnected;
            RadioComPortComboBox.IsEnabled = !anyConnected;
            TncTypeComboBox.IsEnabled = !anyConnected;
            TncComPortComboBox.IsEnabled = !anyConnected;

            // Update send button state based on TNC connection and hex input
            if (HexDataTextBox != null)
            {
                ValidateHexInput(HexDataTextBox);
            }
        }

        private void AppendToHistory(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            if (!string.IsNullOrEmpty(CommunicationHistoryTextBox.Text))
            {
                CommunicationHistoryTextBox.Text += Environment.NewLine;
            }
            
            CommunicationHistoryTextBox.Text += logEntry;
            
            // Auto-scroll to bottom
            if (CommunicationHistoryTextBox.Parent is ScrollViewer scrollViewer)
            {
                scrollViewer.ChangeView(null, scrollViewer.ExtentHeight, null);
            }
        }

        // 16進数データ入力の検証
        private void HexDataTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ValidateHexInput(textBox);
            }
        }

        private void ValidateHexInput(TextBox textBox)
        {
            try
            {
                string input = textBox.Text.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    SendDataButton.IsEnabled = false;
                    return;
                }

                // スペースで分割して16進数として解析
                string[] hexValues = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // 12バイトまでの制限をチェック
                if (hexValues.Length > 12)
                {
                    // 12バイトを超える場合、最初の12バイトのみを保持
                    string[] validHexValues = new string[12];
                    Array.Copy(hexValues, validHexValues, 12);
                    textBox.Text = string.Join(" ", validHexValues);
                    textBox.SelectionStart = textBox.Text.Length;
                    return;
                }

                // 各値が有効な16進数かチェック
                bool isValid = true;
                foreach (string hexValue in hexValues)
                {
                    if (hexValue.Length > 2 || !byte.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out _))
                    {
                        isValid = false;
                        break;
                    }
                }

                // TNCが接続されているかもチェック
                SendDataButton.IsEnabled = isValid && _isTncConnected && hexValues.Length > 0;
            }
            catch
            {
                SendDataButton.IsEnabled = false;
            }
        }

        private void SendDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentTnc == null || !_isTncConnected)
                {
                    AppendToHistory("Error: TNC is not connected");
                    return;
                }

                string input = HexDataTextBox.Text.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    AppendToHistory("Error: No data to send");
                    return;
                }

                // 入力された16進数を解析
                string[] hexValues = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                List<byte> dataBytes = new List<byte>();

                // 先頭に 0x42 0x4D 0x01 を追加
                dataBytes.Add(0x42);
                dataBytes.Add(0x4D);
                dataBytes.Add(0x01);

                // 入力されたデータを追加
                foreach (string hexValue in hexValues)
                {
                    if (byte.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                    {
                        dataBytes.Add(value);
                    }
                }

                // 16進数文字列として送信
                string hexString = BitConverter.ToString(dataBytes.ToArray()).Replace("-", " ");
                
                AppendToHistory($"Sending data: {hexString}");
                Thread.Sleep(700);
                _currentTnc.SendPacket(hexString);
                Thread.Sleep(1600);
                AppendToHistory("Data sent successfully");

                // 送信後、入力をクリア
                HexDataTextBox.Text = "";
            }
            catch (Exception ex)
            {
                AppendToHistory($"Send error: {ex.Message}");
            }
        }

        #region パケット監視機能

        /// <summary>
        /// パケット監視を開始
        /// stringが空でない限り、UIの操作にかかわらず常にGetPacketして受信データを監視
        /// </summary>
        private void StartPacketMonitoring()
        {
            if (_isPacketMonitoringActive || _currentTnc == null || !_isTncConnected)
                return;

            _isPacketMonitoringActive = true;
            _packetMonitorCancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () => await PacketMonitoringLoop(_packetMonitorCancellationTokenSource.Token));
            
            AppendToHistory("Packet monitoring started");
        }

        /// <summary>
        /// パケット監視を停止
        /// </summary>
        private void StopPacketMonitoring()
        {
            if (!_isPacketMonitoringActive)
                return;

            _isPacketMonitoringActive = false;
            _packetMonitorCancellationTokenSource?.Cancel();
            _packetMonitorCancellationTokenSource?.Dispose();
            _packetMonitorCancellationTokenSource = null;

            AppendToHistory("Packet monitoring stopped");
        }

        /// <summary>
        /// パケット監視ループ
        /// stringが空でない限り、UIの操作にかかわらず常にGetPacketして受信データを監視
        /// </summary>
        private async Task PacketMonitoringLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isTncConnected && _currentTnc != null)
            {
                try
                {
                    string packet = _currentTnc.GetPacket();
                    
                    if (!string.IsNullOrEmpty(packet))
                    {
                        // UIスレッドで履歴に追加
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            AppendToHistory($"Received packet: {packet}");
                        });

                        continue;
                    }

                    // パケットが空の場合、少し待機してから再試行
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // キャンセルされた場合は正常終了
                    break;
                }
                catch (Exception ex)
                {
                    // UIスレッドでエラーログを追加
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        AppendToHistory($"Packet monitoring error: {ex.Message}");
                    });

                    // エラーが発生した場合は少し待機してから再試行
                    await Task.Delay(1000, cancellationToken);
                }
            }

            // 監視終了
            _isPacketMonitoringActive = false;
        }

        #endregion
    }
}