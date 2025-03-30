using System;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        // _webSocket を null 許容にしておく
        private ClientWebSocket? _webSocket;

        public MainWindow()
        {
            InitializeComponent();
            LoadLastUrl();
            this.Closing += MainWindow_Closing;
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

        // 40個のボタンを UniformGrid に追加
        private void InitializeButtons()
        {
            for (int i = 0; i < 40; i++)
            {
                Button btn = new Button();
                string labelSuffix = GetLabelSuffix(i); // 例："a", "b", …, "an"
                btn.Content = $"CN{i+1}: {labelSuffix}";
                btn.Margin = new Thickness(5);
                btn.Tag = i; // ボタン番号を Tag に保存
                btn.Click += async (s, e) => await ButtonClicked((int)btn.Tag, labelSuffix);
                buttonGrid.Children.Add(btn);
            }
        }

        // Excel の列名のような文字列を生成するメソッド
        private string GetLabelSuffix(int index)
        {
            const string letters = "abcdefghijklmnopqrstuvwxyz";
            string result = "";
            do
            {
                result = letters[index % 26] + result;
                index = index / 26 - 1;
            } while (index >= 0);
            return result;
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
        private async Task ButtonClicked(int buttonIndex, string labelSuffix)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                LogMessage("エラー", "WebSocketが接続されていません。");
                return;
            }

            // ボタン 0 は "a:10000:1"、それ以降は [ラベル]:10000:1 の形式のメッセージを送信
            string sendMessage = (buttonIndex == 0) ? "a:10000:1" : $"{labelSuffix}:10000:1";

            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(sendMessage)),
                                           WebSocketMessageType.Text, true, CancellationToken.None);
                // ログ出力: 「Cha[番号]:[ラベル]が押されました。送信: [送信メッセージ]」
                LogMessage("情報", $"CN{buttonIndex+1}:{labelSuffix}が押されました。送信: {sendMessage}");
            }
            catch (Exception ex)
            {
                LogMessage("エラー", $"送信エラー: {ex.Message}");
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
