using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.UI
{
    public partial class MCPDockablePanel : Page
    {
        private static MCPDockablePanel _instance;
        private readonly ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private readonly DispatcherTimer _statusTimer;
        private ClaudeRevitClient _client;
        private bool _isProcessing;
        private bool _planningMode;
        private int _selectedModelIndex;

        public static MCPDockablePanel Instance => _instance;

        private static readonly List<ModelOption> AvailableModels = new List<ModelOption>
        {
            new ModelOption("Opus 4.6", "claude-opus-4-6", "Il più capace per progetti ambiziosi"),
            new ModelOption("Sonnet 4.6", "claude-sonnet-4-6", "Il più efficiente per uso quotidiano"),
            new ModelOption("Haiku 4.5", "claude-haiku-4-5-20251001", "Il più veloce per risposte rapide"),
        };

        public MCPDockablePanel()
        {
            InitializeComponent();
            _instance = this;
            ChatMessages.ItemsSource = _messages;

            // Populate model selector
            _selectedModelIndex = LoadSavedModelIndex();
            ModelLabel.Text = AvailableModels[_selectedModelIndex].DisplayName;
            BuildModelPopup();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (s, e) => UpdateStatus();

            ChatInput.TextChanged += (s, e) =>
            {
                Placeholder.Visibility = string.IsNullOrEmpty(ChatInput.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            };

            AddMessage("assistant",
                "Ciao! Sono Claude, il tuo assistente per Revit.\n\n" +
                "Ho accesso diretto al modello aperto e posso eseguire operazioni in tempo reale. " +
                "Chiedimi qualsiasi cosa sul progetto o dimmi cosa creare.");
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
                bool running = Core.SocketService.Instance.IsRunning;
                StatusIndicator.Fill = new SolidColorBrush(running
                    ? Color.FromRgb(76, 175, 80) : Color.FromRgb(244, 67, 54));
                StatusText.Text = running ? "MCP Online" : "MCP Offline";
                StatusText.Foreground = new SolidColorBrush(running
                    ? Color.FromRgb(76, 175, 80) : Color.FromRgb(136, 136, 136));
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
            TypingIndicator.Visibility = Visibility.Visible;
            TypingText.Text = _planningMode ? "Claude sta pianificando..." : "Claude sta pensando...";

            try
            {
                if (!Core.SocketService.Instance.IsRunning)
                {
                    AddMessage("assistant",
                        "Il server MCP non e' attivo. Clicca \"Revit MCP Switch\" nel ribbon per avviarlo.");
                    return;
                }

                if (_client == null)
                {
                    _client = new ClaudeRevitClient();
                    _client.Model = AvailableModels[_selectedModelIndex].ModelId;
                    _client.ThinkingEnabled = _planningMode;
                }
                string response = await _client.SendMessage(input);
                AddMessage("assistant", response);
            }
            catch (Exception ex)
            {
                AddMessage("assistant", $"Si e' verificato un errore: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = true;
                TypingIndicator.Visibility = Visibility.Collapsed;
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

        public void OnToolExecuting(string toolName)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TypingText.Text = $"Eseguo {toolName}...";
                _messages.Add(new ChatMessage("tool", toolName));
                ChatScrollViewer.ScrollToEnd();
            }));
        }

        public void LogCommand(string commandName, bool success, string message, double durationMs) { }

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "revit_chat_settings.json");

        private void ModelToggle_Click(object sender, MouseButtonEventArgs e)
        {
            ModelPopup.IsOpen = !ModelPopup.IsOpen;
        }

        private void BuildModelPopup()
        {
            ModelPopupItems.Children.Clear();
            for (int i = 0; i < AvailableModels.Count; i++)
            {
                int index = i;
                var model = AvailableModels[i];

                var item = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Colors.Transparent)
                };

                item.MouseEnter += (s, ev) =>
                    ((Border)s).Background = new SolidColorBrush(Color.FromRgb(245, 244, 242));
                item.MouseLeave += (s, ev) =>
                    ((Border)s).Background = new SolidColorBrush(Colors.Transparent);

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock
                {
                    Text = model.DisplayName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = model.Description,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(textStack, 0);
                grid.Children.Add(textStack);

                var check = new TextBlock
                {
                    Text = "✓",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(88, 130, 207)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                    Visibility = i == _selectedModelIndex ? Visibility.Visible : Visibility.Collapsed,
                    Tag = $"check_{i}"
                };
                Grid.SetColumn(check, 1);
                grid.Children.Add(check);

                item.Child = grid;
                item.MouseLeftButtonDown += (s, ev) => SelectModel(index);

                ModelPopupItems.Children.Add(item);
            }
        }

        private void SelectModel(int index)
        {
            _selectedModelIndex = index;
            var selected = AvailableModels[index];
            ModelLabel.Text = selected.DisplayName;
            if (_client != null)
                _client.Model = selected.ModelId;
            SaveModelIndex(index);
            ModelPopup.IsOpen = false;
            BuildModelPopup(); // Refresh checkmarks
        }

        private static int LoadSavedModelIndex()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = JObject.Parse(File.ReadAllText(SettingsPath));
                    int idx = json["modelIndex"]?.Value<int>() ?? 1;
                    return idx >= 0 && idx < AvailableModels.Count ? idx : 1;
                }
            }
            catch { }
            return 1; // Sonnet 4.6 default
        }

        private static void SaveModelIndex(int index)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath,
                    new JObject { ["modelIndex"] = index }.ToString());
            }
            catch { }
        }

        private void PlanningToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _planningMode = !_planningMode;
            var white = new SolidColorBrush(Colors.White);
            var gray = new SolidColorBrush(Color.FromRgb(153, 153, 153));

            if (_planningMode)
            {
                PlanningToggle.Background = new SolidColorBrush(Color.FromRgb(217, 119, 87));
                PlanningToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 87));
                PlanningLabel.Foreground = white;
                PlanningArrow.Foreground = white;
            }
            else
            {
                PlanningToggle.Background = new SolidColorBrush(Color.FromRgb(245, 244, 242));
                PlanningToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 221, 217));
                PlanningLabel.Foreground = gray;
                PlanningArrow.Foreground = gray;
            }

            if (_client != null)
                _client.ThinkingEnabled = _planningMode;
        }

        public void OnRetrying(int seconds)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TypingText.Text = $"Rate limit — riprovo tra {seconds}s...";
            }));
        }

        public void OnThinkingReceived(string thinkingText)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Show a compact summary instead of the full thinking text
                int charCount = thinkingText.Length;
                string firstLine = thinkingText.Split('\n')[0];
                if (firstLine.Length > 120)
                    firstLine = firstLine.Substring(0, 120) + "...";
                string summary = $"{firstLine}\n[{charCount:N0} caratteri di ragionamento]";
                _messages.Add(new ChatMessage("thinking", summary));
                ChatScrollViewer.ScrollToEnd();
            }));
        }

        private void StopButton_Click(object sender, MouseButtonEventArgs e)
        {
            _client?.Cancel();
            TypingText.Text = "Annullamento...";
        }

        private void ClearChat_Click(object sender, MouseButtonEventArgs e)
        {
            _messages.Clear();
            _client?.ClearHistory();
            AddMessage("assistant", "Chat azzerata. Come posso aiutarti?");
        }
    }

    public class ModelOption
    {
        public string DisplayName { get; }
        public string ModelId { get; }
        public string Description { get; }

        public ModelOption(string displayName, string modelId, string description = "")
        {
            DisplayName = displayName;
            ModelId = modelId;
            Description = description;
        }
    }

    public class ChatMessage
    {
        public string Role { get; }
        public string Text { get; }
        public string RoleLabel { get; }
        public string AvatarLetter { get; }
        public SolidColorBrush AvatarBackground { get; }
        public SolidColorBrush RoleLabelColor { get; }
        public SolidColorBrush TextColor { get; }
        public SolidColorBrush RowBackground { get; }
        public FontFamily FontFamily { get; }

        private static readonly SolidColorBrush ClaudeOrange = new SolidColorBrush(Color.FromRgb(217, 119, 87));
        private static readonly SolidColorBrush UserBlue = new SolidColorBrush(Color.FromRgb(88, 130, 207));
        private static readonly SolidColorBrush ToolGreen = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush ThinkingPurple = new SolidColorBrush(Color.FromRgb(147, 112, 219));

        public ChatMessage(string role, string text)
        {
            Role = role;
            Text = text;

            switch (role)
            {
                case "user":
                    RoleLabel = "Tu";
                    AvatarLetter = "L";
                    AvatarBackground = UserBlue;
                    RoleLabelColor = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                    TextColor = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                    RowBackground = new SolidColorBrush(Colors.White);
                    FontFamily = new FontFamily("Segoe UI");
                    break;
                case "thinking":
                    RoleLabel = "Planning";
                    AvatarLetter = "💡";
                    AvatarBackground = ThinkingPurple;
                    RoleLabelColor = ThinkingPurple;
                    TextColor = new SolidColorBrush(Color.FromRgb(120, 100, 160));
                    RowBackground = new SolidColorBrush(Color.FromRgb(248, 246, 252));
                    FontFamily = new FontFamily("Segoe UI");
                    break;
                case "tool":
                    RoleLabel = "";
                    AvatarLetter = "⚡";
                    AvatarBackground = ToolGreen;
                    RoleLabelColor = ToolGreen;
                    TextColor = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    RowBackground = new SolidColorBrush(Color.FromRgb(250, 249, 247));
                    FontFamily = new FontFamily("Consolas");
                    break;
                default:
                    RoleLabel = "Claude";
                    AvatarLetter = "C";
                    AvatarBackground = ClaudeOrange;
                    RoleLabelColor = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                    TextColor = new SolidColorBrush(Color.FromRgb(55, 53, 47));
                    RowBackground = new SolidColorBrush(Color.FromRgb(250, 249, 247));
                    FontFamily = new FontFamily("Segoe UI");
                    break;
            }
        }
    }
}
