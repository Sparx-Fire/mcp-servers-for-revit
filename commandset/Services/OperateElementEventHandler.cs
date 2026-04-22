using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class OperateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Operation data (input)
        /// </summary>
        public OperationSetting OperationData { get; private set; }
        /// <summary>
        /// Operation result (output). The Message field includes a
        /// "view switched from X to Y" note when the action forced
        /// a view change (SelectionBox with a non-3D active view).
        /// </summary>
        public AIResult<string> Result { get; private set; }

        /// <summary>
        /// Set operation parameters
        /// </summary>
        public void SetParameters(OperationSetting data)
        {
            OperationData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            // Capture the active view BEFORE the operation so we can
            // detect any silent view switch that the action caused
            // (SelectionBox in particular may switch to a default 3D
            // view if the current view isn't already 3D).
            ElementId viewIdBefore = uiDoc.ActiveView?.Id;
            string viewNameBefore = uiDoc.ActiveView?.Name;

            try
            {
                bool _ = ExecuteElementOperation(uiDoc, OperationData);

                // Compare view after completion
                ElementId viewIdAfter = uiDoc.ActiveView?.Id;
                string viewNameAfter = uiDoc.ActiveView?.Name;
                bool viewSwitched = viewIdBefore != null
                    && viewIdAfter != null
                    && viewIdBefore.GetValue() != viewIdAfter.GetValue();

                string message = "Operation completed successfully.";
                if (viewSwitched)
                {
                    message += $" NOTE: the active view switched from '{viewNameBefore}' to '{viewNameAfter}' during the operation. Switch back manually if that was not intended.";
                }

                Result = new AIResult<string>
                {
                    Success = true,
                    Message = message,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<string>
                {
                    Success = false,
                    Message = $"operate_element failed: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // Signal that operation completed
            }
        }

        /// <summary>
        /// Wait for completion
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds</param>
        /// <returns>True if the operation completed before the timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Operate Element";
        }

        /// <summary>
        /// Execute the specified operation on the target elements
        /// </summary>
        /// <param name="uidoc">Current UI document</param>
        /// <param name="setting">Operation settings</param>
        /// <returns>True if the operation succeeded</returns>
        public static bool ExecuteElementOperation(UIDocument uidoc, OperationSetting setting)
        {
            // Validate input
            if (uidoc == null || uidoc.Document == null || setting == null || setting.ElementIds == null ||
                (setting.ElementIds.Count == 0 && setting.Action.ToLower() != "resetisolate"))
                throw new Exception("Invalid input: document is null or no element IDs were provided.");

            Document doc = uidoc.Document;

            // Convert int IDs to ElementId instances
            ICollection<ElementId> elementIds = setting.ElementIds.Select(id => new ElementId(id)).ToList();

            // Parse action type
            ElementOperationType action;
            if (!Enum.TryParse(setting.Action, true, out action))
            {
                throw new Exception($"Unsupported action: {setting.Action}");
            }

            // Dispatch by action
            switch (action)
            {
                case ElementOperationType.Select:
                    // Select elements
                    uidoc.Selection.SetElementIds(elementIds);
                    return true;

                case ElementOperationType.SelectionBox:
                    // Create a section box in a 3D view.

                    // Is the current view a 3D view?
                    View3D targetView;

                    if (doc.ActiveView is View3D)
                    {
                        // Current view is 3D — create the box here
                        targetView = doc.ActiveView as View3D;
                    }
                    else
                    {
                        // Find a default 3D view to switch to.
                        // Note: this causes a silent view switch. The
                        // handler detects this in Execute() and reports
                        // it to the caller in Result.Message.
                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        collector.OfClass(typeof(View3D));

                        // Prefer {3D} or "Default 3D" by name
                        targetView = collector
                            .Cast<View3D>()
                            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked && (v.Name.Contains("{3D}") || v.Name.Contains("Default 3D")));

                        if (targetView == null)
                        {
                            // No suitable 3D view available
                            throw new Exception("No suitable 3D view found for creating the section box.");
                        }

                        // Activate the chosen 3D view
                        uidoc.ActiveView = targetView;
                    }

                    // Compute the combined bounding box of the selected elements
                    BoundingBoxXYZ boundingBox = null;

                    foreach (ElementId id in elementIds)
                    {
                        Element elem = doc.GetElement(id);
                        BoundingBoxXYZ elemBox = elem.get_BoundingBox(null);

                        if (elemBox != null)
                        {
                            if (boundingBox == null)
                            {
                                boundingBox = new BoundingBoxXYZ
                                {
                                    Min = new XYZ(elemBox.Min.X, elemBox.Min.Y, elemBox.Min.Z),
                                    Max = new XYZ(elemBox.Max.X, elemBox.Max.Y, elemBox.Max.Z)
                                };
                            }
                            else
                            {
                                // Expand the box to include the current element
                                boundingBox.Min = new XYZ(
                                    Math.Min(boundingBox.Min.X, elemBox.Min.X),
                                    Math.Min(boundingBox.Min.Y, elemBox.Min.Y),
                                    Math.Min(boundingBox.Min.Z, elemBox.Min.Z));

                                boundingBox.Max = new XYZ(
                                    Math.Max(boundingBox.Max.X, elemBox.Max.X),
                                    Math.Max(boundingBox.Max.Y, elemBox.Max.Y),
                                    Math.Max(boundingBox.Max.Z, elemBox.Max.Z));
                            }
                        }
                    }

                    if (boundingBox == null)
                    {
                        throw new Exception("Could not build a bounding box for the selected elements.");
                    }

                    // Pad the box slightly so it doesn't hug the geometry
                    double offset = 1.0; // 1 foot of padding
                    boundingBox.Min = new XYZ(boundingBox.Min.X - offset, boundingBox.Min.Y - offset, boundingBox.Min.Z - offset);
                    boundingBox.Max = new XYZ(boundingBox.Max.X + offset, boundingBox.Max.Y + offset, boundingBox.Max.Z + offset);

                    // Enable and set the section box
                    using (Transaction trans = new Transaction(doc, "Create Section Box"))
                    {
                        trans.Start();
                        targetView.IsSectionBoxActive = true;
                        targetView.SetSectionBox(boundingBox);
                        trans.Commit();
                    }

                    // Bring the elements into view
                    uidoc.ShowElements(elementIds);
                    return true;

                case ElementOperationType.SetColor:
                    // Apply color override to elements in the active view
                    using (Transaction trans = new Transaction(doc, "Set Element Color"))
                    {
                        trans.Start();
                        SetElementsColor(doc, elementIds, setting.ColorValue);
                        trans.Commit();
                    }
                    // Scroll the view to make the elements visible
                    uidoc.ShowElements(elementIds);
                    return true;


                case ElementOperationType.SetTransparency:
                    // Set element transparency in the active view
                    using (Transaction trans = new Transaction(doc, "Set Element Transparency"))
                    {
                        trans.Start();

                        // Build override graphic settings
                        OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();

                        // Clamp transparency to 0-100
                        int transparencyValue = Math.Max(0, Math.Min(100, setting.TransparencyValue));

                        // Apply surface transparency
                        overrideSettings.SetSurfaceTransparency(transparencyValue);

                        // Apply to each element
                        foreach (ElementId id in elementIds)
                        {
                            doc.ActiveView.SetElementOverrides(id, overrideSettings);
                        }

                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Delete:
                    // Delete elements (transaction required)
                    using (Transaction trans = new Transaction(doc, "Delete Elements"))
                    {
                        trans.Start();
                        doc.Delete(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Hide:
                    // Hide elements (requires active view + transaction)
                    using (Transaction trans = new Transaction(doc, "Hide Elements"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.TempHide:
                    // Temporary-hide elements (requires active view + transaction)
                    using (Transaction trans = new Transaction(doc, "Temp-Hide Elements"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Isolate:
                    // Isolate elements (requires active view + transaction)
                    using (Transaction trans = new Transaction(doc, "Isolate Elements"))
                    {
                        trans.Start();
                        doc.ActiveView.IsolateElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Unhide:
                    // Unhide elements (requires active view + transaction)
                    using (Transaction trans = new Transaction(doc, "Unhide Elements"))
                    {
                        trans.Start();
                        doc.ActiveView.UnhideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.ResetIsolate:
                    // Reset temporary isolate (requires active view + transaction)
                    using (Transaction trans = new Transaction(doc, "Reset Isolate"))
                    {
                        trans.Start();
                        doc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        trans.Commit();
                    }
                    return true;

                default:
                    throw new Exception($"Unsupported action: {setting.Action}");
            }
        }

        /// <summary>
        /// Apply an RGB color override to the given elements in the active view.
        /// </summary>
        /// <param name="doc">Document</param>
        /// <param name="elementIds">Elements to color</param>
        /// <param name="elementColor">RGB color [r, g, b]</param>
        private static void SetElementsColor(Document doc, ICollection<ElementId> elementIds, int[] elementColor)
        {
            // Validate color array
            if (elementColor == null || elementColor.Length < 3)
            {
                elementColor = new int[] { 255, 0, 0 }; // default red
            }
            // Clamp RGB to 0-255
            int r = Math.Max(0, Math.Min(255, elementColor[0]));
            int g = Math.Max(0, Math.Min(255, elementColor[1]));
            int b = Math.Max(0, Math.Min(255, elementColor[2]));
            // Build the Revit color (byte cast required)
            Color color = new Color((byte)r, (byte)g, (byte)b);
            // Build override graphic settings
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
            // Apply color to projection, cut, and surface (foreground + background)
            overrideSettings.SetProjectionLineColor(color);
            overrideSettings.SetCutLineColor(color);
            overrideSettings.SetSurfaceForegroundPatternColor(color);
            overrideSettings.SetSurfaceBackgroundPatternColor(color);

            // Try to apply a solid fill pattern
            try
            {
                // Look up available fill patterns
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                // Prefer solid fill
                FillPatternElement solidPattern = patternCollector
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                if (solidPattern != null)
                {
                    overrideSettings.SetSurfaceForegroundPatternId(solidPattern.Id);
                    overrideSettings.SetSurfaceForegroundPatternVisible(true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to apply fill pattern: {ex.Message}");
            }

            // Apply the overrides to each element
            foreach (ElementId id in elementIds)
            {
                doc.ActiveView.SetElementOverrides(id, overrideSettings);
            }
        }

    }
}
