using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Lightweight health-check handler. Previously this showed a
    /// blocking TaskDialog inside Revit, which wedged the UI until the
    /// user clicked OK — meaning the handler never completed in
    /// headless / background MCP sessions. It now returns immediately
    /// with a non-blocking handshake; the command surface still returns
    /// the Message through the IPC so callers get a confirmation.
    /// </summary>
    public class SayHelloEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string Message { get; set; } = "Hello MCP!";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            // Intentionally no TaskDialog here. Previous behavior:
            //   TaskDialog.Show("Revit MCP", Message);
            // blocked the Revit UI thread until manual dismissal and
            // made the handler unusable for automated health checks.
            // The Message is already surfaced via SayHelloCommand's
            // return value, so no in-Revit popup is needed.
            _resetEvent.Set();
        }

        public string GetName()
        {
            return "Say Hello";
        }
    }
}
