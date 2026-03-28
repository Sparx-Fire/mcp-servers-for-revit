using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.UI
{
    public partial class MCPDockablePanel : Page
    {
        private static MCPDockablePanel _instance;
        private readonly DispatcherTimer _statusTimer;
        private bool _webViewInitialized;
        private bool _chatTabActive = true;

        private const string CLAUDE_URL = "https://claude.ai/new";
        private const int MCP_PORT = 8080;

        public static MCPDockablePanel Instance => _instance;

        public MCPDockablePanel()
        {
            InitializeComponent();
            _instance = this;

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (s, e) => UpdateStatus();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Start();
            UpdateStatus();
            await InitWebView();
        }

        private async Task InitWebView()
        {
            if (_webViewInitialized) return;
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RevitMCP", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await WebBrowser.EnsureCoreWebView2Async(env);
                _webViewInitialized = true;

                WebBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebBrowser.CoreWebView2.Navigate(CLAUDE_URL);

                WebBrowser.CoreWebView2.NavigationCompleted += (s, e2) =>
                {
                    Dispatcher.BeginInvoke(new Action(() => LoadingOverlay.Visibility = Visibility.Collapsed));
                };
            }
            catch (Exception ex)
            {
                var stack = LoadingOverlay.Child as StackPanel;
                if (stack?.Children.Count >= 2)
                {
                    (stack.Children[0] as TextBlock).Text = "WebView2 non disponibile";
                    (stack.Children[1] as TextBlock).Text = $"Installa WebView2 Runtime da Microsoft.\n{ex.Message}";
                }
            }
        }

        private void UpdateStatus()
        {
            try
            {
                bool isRunning = Core.SocketService.Instance.IsRunning;
                StatusIndicator.Fill = new SolidColorBrush(isRunning
                    ? Color.FromRgb(68, 204, 136)
                    : Color.FromRgb(255, 68, 68));
                StatusText.Text = isRunning ? "MCP On" : "MCP Off";
                StatusText.Foreground = StatusIndicator.Fill;
            }
            catch { }
        }

        // --- Tab switching ---

        private void TabChat_Click(object sender, RoutedEventArgs e)
        {
            _chatTabActive = true;
            ChatPanel.Visibility = Visibility.Visible;
            CommandsPanel.Visibility = Visibility.Collapsed;
            TabChat.Background = new SolidColorBrush(Color.FromRgb(91, 91, 214));
            TabChat.Foreground = Brushes.White;
            TabCommands.Background = new SolidColorBrush(Color.FromRgb(42, 42, 60));
            TabCommands.Foreground = new SolidColorBrush(Color.FromRgb(144, 144, 168));
        }

        private void TabCommands_Click(object sender, RoutedEventArgs e)
        {
            _chatTabActive = false;
            ChatPanel.Visibility = Visibility.Collapsed;
            CommandsPanel.Visibility = Visibility.Visible;
            TabCommands.Background = new SolidColorBrush(Color.FromRgb(91, 91, 214));
            TabCommands.Foreground = Brushes.White;
            TabChat.Background = new SolidColorBrush(Color.FromRgb(42, 42, 60));
            TabChat.Foreground = new SolidColorBrush(Color.FromRgb(144, 144, 168));
        }

        // --- Quick Commands ---

        private async void QuickCmd_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string tag = btn?.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            if (!Core.SocketService.Instance.IsRunning)
            {
                ResultText.Text = "Server MCP non attivo.\nClicca 'Revit MCP Switch' nel ribbon.";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                return;
            }

            ResultText.Text = $"Esecuzione: {tag}...";
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 200));
            btn.IsEnabled = false;

            try
            {
                string result = await ExecuteCommand(tag);
                ResultText.Text = result;
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(192, 192, 216));
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Errore: {ex.Message}";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private async Task<string> ExecuteCommand(string tag)
        {
            string method;
            JObject parameters = new JObject();

            switch (tag)
            {
                case "get_project_info":
                    method = "get_project_info";
                    break;
                case "analyze_model_statistics":
                    method = "analyze_model_statistics";
                    parameters["includeDetailedTypes"] = false;
                    break;
                case "get_warnings":
                    method = "get_warnings";
                    parameters["maxWarnings"] = 50;
                    break;
                case "get_materials":
                    method = "get_materials";
                    break;
                case "export_room_data":
                    method = "export_room_data";
                    break;
                case "get_material_quantities":
                    method = "get_material_quantities";
                    break;
                case "create_level_dialog":
                    method = "create_level";
                    parameters["data"] = JArray.Parse("[{\"name\": \"MCP New Level\", \"elevation\": 6000, \"createFloorPlan\": true}]");
                    break;
                case "create_3d":
                    method = "create_view";
                    parameters["viewType"] = "3D";
                    parameters["name"] = $"MCP 3D {DateTime.Now:HHmmss}";
                    parameters["detailLevel"] = "Fine";
                    break;
                case "create_section":
                    method = "create_view";
                    parameters["viewType"] = "Section";
                    parameters["name"] = $"MCP Section {DateTime.Now:HHmmss}";
                    parameters["direction"] = JObject.Parse("{\"x\": 0, \"y\": 1, \"z\": 0}");
                    parameters["scale"] = 50;
                    break;
                case "create_sheet_dialog":
                    method = "create_sheet";
                    parameters["sheetNumber"] = $"MCP-{DateTime.Now:HHmmss}";
                    parameters["sheetName"] = "MCP Generated Sheet";
                    break;
                case "create_schedule_columns":
                    method = "create_schedule";
                    parameters["categoryName"] = "OST_StructuralColumns";
                    parameters["name"] = $"MCP Columns {DateTime.Now:HHmmss}";
                    parameters["fields"] = JArray.Parse("[{\"parameterName\": \"Family and Type\"}, {\"parameterName\": \"Base Level\"}, {\"parameterName\": \"Length\"}]");
                    break;
                case "create_schedule_beams":
                    method = "create_schedule";
                    parameters["categoryName"] = "OST_StructuralFraming";
                    parameters["name"] = $"MCP Beams {DateTime.Now:HHmmss}";
                    parameters["fields"] = JArray.Parse("[{\"parameterName\": \"Family and Type\"}, {\"parameterName\": \"Length\"}, {\"parameterName\": \"Reference Level\"}]");
                    break;
                case "purge_preview":
                    method = "purge_unused";
                    parameters["dryRun"] = true;
                    break;
                case "purge_execute":
                    method = "purge_unused";
                    parameters["dryRun"] = false;
                    break;
                case "say_hello":
                    method = "say_hello";
                    parameters["message"] = "Ciao da MCP Panel!";
                    break;
                default:
                    return $"Comando sconosciuto: {tag}";
            }

            return await SendMcpCommand(method, parameters);
        }

        private async Task<string> SendMcpCommand(string method, JObject parameters)
        {
            var jsonRpc = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString(),
                ["method"] = method,
                ["params"] = parameters
            };

            using (var client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", MCP_PORT);
                var stream = client.GetStream();

                byte[] data = Encoding.UTF8.GetBytes(jsonRpc.ToString(Formatting.None));
                await stream.WriteAsync(data, 0, data.Length);

                byte[] buffer = new byte[65536];
                var sb = new StringBuilder();
                client.ReceiveTimeout = 30000;

                do
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    await Task.Delay(100); // small delay to let data arrive
                }
                while (stream.DataAvailable);

                string responseStr = sb.ToString();

                try
                {
                    var response = JObject.Parse(responseStr);
                    if (response["result"] != null)
                        return response["result"].ToString(Formatting.Indented);
                    if (response["error"] != null)
                        return $"Errore: {response["error"]?["message"]}";
                }
                catch { }

                return responseStr;
            }
        }

        // Called from SocketService
        public void LogCommand(string commandName, bool success, string message, double durationMs) { }
        public void OnToolExecuting(string toolName) { }
    }
}
