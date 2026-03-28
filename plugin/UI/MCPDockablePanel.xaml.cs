using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace revit_mcp_plugin.UI
{
    public partial class MCPDockablePanel : Page
    {
        private static MCPDockablePanel _instance;
        private readonly ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private readonly DispatcherTimer _statusTimer;
        private readonly DispatcherTimer _typingTimer;
        private readonly ClaudeApiClient _apiClient;
        private bool _isProcessing;
        private int _dotCount;

        public static MCPDockablePanel Instance => _instance;

        public MCPDockablePanel()
        {
            InitializeComponent();
            _instance = this;
            ChatMessages.ItemsSource = _messages;

            _apiClient = new ClaudeApiClient();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (s, e) => UpdateStatus();

            _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _typingTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                TypingDots.Text = new string('.', _dotCount + 1);
            };

            // Welcome message
            AddMessage("assistant", "Ciao! Sono Claude, il tuo assistente AI per Revit. Posso creare livelli, viste, sheets, analizzare il modello, e molto altro.\n\nProva a scrivermi qualcosa come:\n- \"Crea un livello a 6000mm\"\n- \"Quanti elementi ci sono nel modello?\"\n- \"Mostra le warnings del progetto\"");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Start();
            UpdateStatus();
            ChatInput.Focus();
        }

        private void UpdateStatus()
        {
            try
            {
                bool isRunning = Core.SocketService.Instance.IsRunning;
                StatusIndicator.Fill = new SolidColorBrush(isRunning
                    ? Color.FromRgb(68, 204, 136)
                    : Color.FromRgb(255, 68, 68));
                StatusText.Text = isRunning ? "Online" : "Offline";
                StatusText.Foreground = StatusIndicator.Fill;
            }
            catch { }
        }

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isProcessing)
            {
                Send_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string input = ChatInput.Text?.Trim();
            if (string.IsNullOrEmpty(input) || _isProcessing) return;

            ChatInput.Text = "";
            AddMessage("user", input);

            _isProcessing = true;
            SendButton.IsEnabled = false;
            SendButton.Content = "...";
            TypingIndicator.Visibility = Visibility.Visible;
            _typingTimer.Start();

            try
            {
                // Check if MCP server is running - execute commands directly
                if (Core.SocketService.Instance.IsRunning)
                {
                    string response = await _apiClient.SendMessage(input);
                    AddMessage("assistant", response);
                }
                else
                {
                    AddMessage("assistant", "Il server MCP non e' attivo. Clicca 'Revit MCP Switch' nel ribbon per avviarlo.");
                }
            }
            catch (Exception ex)
            {
                AddMessage("assistant", $"Errore: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = true;
                SendButton.Content = "Send";
                TypingIndicator.Visibility = Visibility.Collapsed;
                _typingTimer.Stop();
            }
        }

        private void AddMessage(string role, string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _messages.Add(new ChatMessage(role, text));
                ChatScrollViewer.ScrollToEnd();
            }));
        }

        /// <summary>
        /// Log a command execution (called from SocketService)
        /// </summary>
        public void LogCommand(string commandName, bool success, string message, double durationMs)
        {
            string status = success ? "OK" : "FAIL";
            string logText = $"[{commandName}] {status} ({(int)durationMs}ms)";
            if (!success && !string.IsNullOrEmpty(message))
                logText += $"\n{message}";

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _messages.Add(new ChatMessage("tool", logText));
                ChatScrollViewer.ScrollToEnd();
            }));
        }

        private void ClearChat_Click(object sender, MouseButtonEventArgs e)
        {
            _messages.Clear();
        }
    }

    public class ChatMessage
    {
        public string Role { get; }
        public string Text { get; }
        public string RoleLabel { get; }
        public string LabelAlignment { get; }
        public Thickness LabelMargin { get; }
        public SolidColorBrush TextColor { get; }
        public Style BubbleStyle { get; }
        public FontFamily FontFamily { get; }

        public Thickness BubbleMargin { get; }
        public string BubbleAlignment { get; }

        public ChatMessage(string role, string text)
        {
            Role = role;
            Text = text;

            switch (role)
            {
                case "user":
                    RoleLabel = "You";
                    LabelAlignment = "Right";
                    LabelMargin = new Thickness(0, 4, 12, 0);
                    TextColor = new SolidColorBrush(Color.FromRgb(224, 224, 240));
                    FontFamily = new FontFamily("Segoe UI");
                    BubbleMargin = new Thickness(40, 2, 8, 2);
                    BubbleAlignment = "Right";
                    break;
                case "tool":
                    RoleLabel = "MCP";
                    LabelAlignment = "Left";
                    LabelMargin = new Thickness(12, 4, 0, 0);
                    TextColor = new SolidColorBrush(Color.FromRgb(100, 200, 120));
                    FontFamily = new FontFamily("Consolas");
                    BubbleMargin = new Thickness(8, 2, 40, 2);
                    BubbleAlignment = "Left";
                    break;
                default: // assistant
                    RoleLabel = "Claude";
                    LabelAlignment = "Left";
                    LabelMargin = new Thickness(12, 4, 0, 0);
                    TextColor = new SolidColorBrush(Color.FromRgb(200, 200, 220));
                    FontFamily = new FontFamily("Segoe UI");
                    BubbleMargin = new Thickness(8, 2, 40, 2);
                    BubbleAlignment = "Left";
                    break;
            }
        }

        public SolidColorBrush BubbleBackground
        {
            get
            {
                switch (Role)
                {
                    case "user": return new SolidColorBrush(Color.FromRgb(59, 59, 92));
                    case "tool": return new SolidColorBrush(Color.FromRgb(30, 42, 30));
                    default: return new SolidColorBrush(Color.FromRgb(42, 42, 60));
                }
            }
        }
    }
}
