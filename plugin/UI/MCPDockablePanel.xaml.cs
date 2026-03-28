using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace revit_mcp_plugin.UI
{
    public partial class MCPDockablePanel : Page
    {
        private static MCPDockablePanel _instance;
        private readonly ObservableCollection<CommandLogEntry> _logEntries = new ObservableCollection<CommandLogEntry>();
        private readonly DispatcherTimer _statusTimer;
        private DateTime _startTime;
        private int _successCount;
        private int _errorCount;
        private double _totalDuration;
        private int _commandCount;

        public static MCPDockablePanel Instance => _instance;

        public MCPDockablePanel()
        {
            InitializeComponent();
            _instance = this;
            LogItems.ItemsSource = _logEntries;

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += StatusTimer_Tick;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Start();
            UpdateStatus();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            try
            {
                bool isRunning = Core.SocketService.Instance.IsRunning;

                if (isRunning)
                {
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(68, 204, 136));
                    StatusText.Text = "Online";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(68, 204, 136));

                    if (_startTime == DateTime.MinValue)
                        _startTime = DateTime.Now;

                    var uptime = DateTime.Now - _startTime;
                    UptimeText.Text = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                }
                else
                {
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    StatusText.Text = "Offline";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 102));
                    UptimeText.Text = "--:--:--";
                    _startTime = DateTime.MinValue;
                }

                PortText.Text = Core.SocketService.Instance.Port.ToString();
                CommandCountText.Text = _commandCount.ToString();

                try
                {
                    var registry = Core.SocketService.Instance;
                    RegisteredCountText.Text = "62";
                }
                catch
                {
                    RegisteredCountText.Text = "--";
                }
            }
            catch { }
        }

        /// <summary>
        /// Log a command execution from SocketService
        /// </summary>
        public void LogCommand(string commandName, bool success, string message, double durationMs)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _commandCount++;

                if (success)
                    _successCount++;
                else
                    _errorCount++;

                _totalDuration += durationMs;

                SuccessCountText.Text = _successCount.ToString();
                ErrorCountText.Text = _errorCount.ToString();
                AvgTimeText.Text = _commandCount > 0
                    ? $"{(int)(_totalDuration / _commandCount)}"
                    : "--";
                CommandCountText.Text = _commandCount.ToString();

                var entry = new CommandLogEntry
                {
                    CommandName = commandName,
                    Success = success,
                    Message = message?.Length > 120 ? message.Substring(0, 120) + "..." : message ?? "",
                    TimeStamp = DateTime.Now.ToString("HH:mm:ss"),
                    Duration = $"{(int)durationMs}ms",
                    StatusColor = success
                        ? new SolidColorBrush(Color.FromRgb(68, 204, 136))
                        : new SolidColorBrush(Color.FromRgb(255, 68, 68))
                };

                _logEntries.Insert(0, entry);

                // Keep max 200 entries
                while (_logEntries.Count > 200)
                    _logEntries.RemoveAt(_logEntries.Count - 1);
            }));
        }

        private void ClearLog_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _logEntries.Clear();
            _commandCount = 0;
            _successCount = 0;
            _errorCount = 0;
            _totalDuration = 0;
            CommandCountText.Text = "0";
            SuccessCountText.Text = "0";
            ErrorCountText.Text = "0";
            AvgTimeText.Text = "--";
        }
    }

    public class CommandLogEntry
    {
        public string CommandName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TimeStamp { get; set; }
        public string Duration { get; set; }
        public SolidColorBrush StatusColor { get; set; }
    }
}
