using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class DeleteElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // Operation outcome
        public bool IsSuccess { get; private set; }

        // Total number of elements Revit deleted — includes both the
        // explicitly requested elements AND any elements removed as a
        // cascade (doors hosted on a deleted wall, tags on deleted
        // elements, etc.). This is doc.Delete()'s native return value.
        public int DeletedCount { get; private set; }

        // The elements the caller explicitly asked to delete AND that
        // existed at the time of the call. DeletedCount - DirectDeletedCount
        // is the cascade count.
        public int DirectDeletedCount { get; private set; }

        // Cascade-deleted elements (dependencies that Revit removed
        // because their host was deleted). Exposed separately so the
        // caller can distinguish "14 as requested, 8 cascades" from
        // "22 as requested." Previously these were indistinguishable.
        public int CascadeDeletedCount { get; private set; }

        // Input IDs that could not be parsed or did not correspond to
        // an existing element. Reported to the caller so they know
        // which requested deletions were skipped.
        public List<string> InvalidIds { get; private set; } = new List<string>();

        // Error message if IsSuccess is false. Surfaced to the caller
        // instead of shown as a blocking Revit TaskDialog (previous
        // behavior wedged the UI).
        public string ErrorMessage { get; private set; }

        // State-synchronization
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // Element IDs to delete (input)
        public string[] ElementIds { get; set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                DeletedCount = 0;
                DirectDeletedCount = 0;
                CascadeDeletedCount = 0;
                InvalidIds.Clear();

                if (ElementIds == null || ElementIds.Length == 0)
                {
                    IsSuccess = false;
                    ErrorMessage = "No element IDs provided.";
                    return;
                }

                // Build the list of existing elements to delete.
                List<ElementId> elementIdsToDelete = new List<ElementId>();
                foreach (var idStr in ElementIds)
                {
                    if (int.TryParse(idStr, out int elementIdValue))
                    {
                        var elementId = new ElementId(elementIdValue);
                        if (doc.GetElement(elementId) != null)
                        {
                            elementIdsToDelete.Add(elementId);
                        }
                        else
                        {
                            InvalidIds.Add(idStr);
                        }
                    }
                    else
                    {
                        InvalidIds.Add(idStr);
                    }
                }

                if (elementIdsToDelete.Count > 0)
                {
                    DirectDeletedCount = elementIdsToDelete.Count;

                    using (var transaction = new Transaction(doc, "Delete Elements"))
                    {
                        transaction.Start();

                        // doc.Delete returns ALL elements removed, including
                        // cascade deletions. Subtract DirectDeletedCount to
                        // isolate the cascade count.
                        ICollection<ElementId> deletedIds = doc.Delete(elementIdsToDelete);
                        DeletedCount = deletedIds.Count;
                        CascadeDeletedCount = Math.Max(0, DeletedCount - DirectDeletedCount);

                        transaction.Commit();
                    }
                    IsSuccess = true;
                }
                else
                {
                    // No valid targets. Caller sees IsSuccess=false +
                    // ErrorMessage + InvalidIds — no blocking dialog.
                    IsSuccess = false;
                    ErrorMessage = InvalidIds.Count > 0
                        ? $"No valid elements to delete. Invalid or missing IDs: {string.Join(", ", InvalidIds)}"
                        : "No valid elements to delete.";
                }
            }
            catch (Exception ex)
            {
                // Previous behavior: TaskDialog.Show blocked the UI thread.
                // Now: return the error through the result object.
                IsSuccess = false;
                ErrorMessage = $"Delete failed: {ex.Message}";
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Delete Elements";
        }
    }
}
