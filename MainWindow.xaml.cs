using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace DirectNetViveTester
{
    // エラー一覧表示用のデータモデル
    public class ErrorEntry
    {
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private class TagSetting
        {
            public int ChannelNumber { get; set; }
            public string? TagName { get; set; }
            public string SendValue { get; set; } = "1.0";  // デフォルト値を設定
            
            public TagSetting(int channelNumber, string tagName)
            {
                ChannelNumber = channelNumber;
                TagName = tagName;
            }
        }

        private readonly List<TagSetting> _tagSettings = new();
        private readonly string[] _buttonTags = new string[]
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j",
            "k", "l", "m", "n", "o", "p", "q", "r", "s", "t",
            "u", "v", "w", "x", "y", "z", "aa", "ab", "ac", "ad",
            "ae", "af", "ag", "ah", "ai", "aj", "ak", "al", "am", "an"
        };

        // _webSocket を null 許容にしておく
        private ClientWebSocket? _webSocket;

        public MainWindow()
        {
            InitializeComponent();
            LoadLastUrl();
            this.Closing += MainWindow_Closing;
            InitializeTagSettings();
            InitializeButtons();
        }

        // 起動時に保存された URL を読み込む
        private void LoadLastUrl()
        {
            string lastUrl = Properties.Settings.Default.LastUrl;
            if (!string.IsNullOrEmpty(lastUrl))
            {
                txtUrl.Text = lastUrl;
            }
        }

        // アプリ終了時に現在の URL を保存
        private void SaveLastUrl()
        {
            Properties.Settings.Default.LastUrl = txtUrl.Text;
            Properties.Settings.Default.Save();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveLastUrl();
        }

        private void InitializeTagSettings()
        {
            _tagSettings.Clear();
            // デフォルトのタグ名を設定
            for (int i = 0; i < _buttonTags.Length; i++)
            {
                _tagSettings.Add(new TagSetting(i + 1, _buttonTags[i]));
            }
            tagListView.ItemsSource = null;  // 一度nullを設定して確実に更新
            tagListView.ItemsSource = _tagSettings;
        }

        private void btnUpdateTags_Click(object sender, RoutedEventArgs e)
        {
            InitializeButtons();
        }

        private void btnResetTags_Click(object sender, RoutedEventArgs e)
        {
            _tagSettings.Clear();
            // デフォルトのタグ名を設定
            for (int i = 0; i < _buttonTags.Length; i++)
            {
                _tagSettings.Add(new TagSetting(i + 1, _buttonTags[i]));
            }
            tagListView.ItemsSource = null;  // 一度nullを設定して確実に更新
            tagListView.ItemsSource = _tagSettings;
        }

        // 40個のボタンを UniformGrid に追加
        private void InitializeButtons()
        {
            buttonGrid.Children.Clear();
            
            // タグ設定の数を確認
            int tagCount = _tagSettings.Count;
            
            for (int index = 0; index < 40; index++)
            {
                Button btn = new Button();
                string labelSuffix;
                
                // タグ設定が存在する場合
                if (index < tagCount && _tagSettings[index].TagName != null)
                {
                    labelSuffix = _tagSettings[index].TagName;
                }
                // タグ設定が存在しない場合
                else if (index < _buttonTags.Length)
                {
                    labelSuffix = _buttonTags[index];
                }
                // どちらも存在しない場合
                else
                {
                    labelSuffix = "N/A";
                }
                
                btn.Content = $"CN{index+1}: {labelSuffix}";
                btn.Margin = new Thickness(5);
                btn.Tag = index; // ボタン番号を Tag に保存
                btn.Click += async (s, e) => await ButtonClicked((int)btn.Tag, labelSuffix);
                buttonGrid.Children.Add(btn);
            }
        }

        // WebSocket の生存確認を行うメソッド（ping を送信）
        private async Task<bool> IsWebSocketAlive()
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                return false;

            try
            {
                // 2秒のタイムアウトを設定して ping を送信（"ping" メッセージは任意）
                using (var cts = new CancellationTokenSource(2000))
                {
                    var buffer = Encoding.UTF8.GetBytes("ping");
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Connect ボタン押下時の処理（生存確認を追加）
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                // 既存接続が生きているか確認
                if (await IsWebSocketAlive())
                {
                    LogMessage("情報", "既に接続済みです。");
                    return;
                }
                else
                {
                    // ping 送信で例外が発生した場合は接続切れと判断
                    _webSocket.Dispose();
                    _webSocket = null;
                    LogMessage("情報", "接続が切断状態のため再接続します。");
                }
            }

            try
            {
                _webSocket = new ClientWebSocket();
                Uri uri = new Uri(txtUrl.Text);
                await _webSocket.ConnectAsync(uri, CancellationToken.None);
                LogMessage("情報", "接続に成功しました！");
            }
            catch (Exception ex)
            {
                LogMessage("エラー", $"接続エラー: {ex.Message}");
            }
        }

        // Disconnect ボタン押下時の処理
        private async void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                LogMessage("情報", "接続されていません。");
                return;
            }

            try
            {
                // 正常な切断を要求
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                LogMessage("情報", "切断に成功しました！");
                _webSocket.Dispose();
                _webSocket = null;
            }
            catch (Exception ex)
            {
                LogMessage("エラー", $"切断エラー: {ex.Message}");
            }
        }

        // 各ボタン押下時の処理
        private async Task ButtonClicked(int buttonIndex, string? labelSuffix)
        {
            if (labelSuffix == null)
            {
                LogMessage("Error", $"Button {buttonIndex + 1} has no tag name");
                return;
            }

            try
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    LogMessage("Error", "WebSocket is not connected");
                    return;
                }

                // タグ設定から送信値を取得
                var sendValue = "1.0";  // デフォルト値
                if (buttonIndex < _tagSettings.Count)
                {
                    sendValue = _tagSettings[buttonIndex].SendValue;
                }

                var message = $"{labelSuffix}:{sendValue}";
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                LogMessage("Info", $"Sent: {message}");
            }
            catch (Exception ex)
            {
                LogMessage("Error", $"Failed to send message: {ex.Message}");
            }
        }

        // エラー一覧ウィンドウへメッセージを追加するメソッド
        private void LogMessage(string type, string message)
        {
            var errorEntry = new ErrorEntry { Type = type, Message = message };
            errorListView.Items.Add(errorEntry);
            // 追加した項目を表示するようスクロールする
            errorListView.ScrollIntoView(errorEntry);
        }
    }
}
