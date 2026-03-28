using System;
using Autodesk.Revit.UI;

namespace revit_mcp_plugin.UI
{
    public class MCPDockablePaneProvider : IDockablePaneProvider
    {
        private MCPDockablePanel _panel;

        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("A1B2C3D4-E5F6-7890-ABCD-123456789ABC"));

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            _panel = new MCPDockablePanel();
            data.FrameworkElement = _panel;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                MinimumWidth = 320,
                MinimumHeight = 400
            };
            data.VisibleByDefault = false;
        }
    }
}
