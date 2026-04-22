using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Delete
{
    public class DeleteElementCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private DeleteElementEventHandler _handler => (DeleteElementEventHandler)Handler;

        public override string CommandName => "delete_element";

        public DeleteElementCommand(UIApplication uiApp)
            : base(new DeleteElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    // Parse the array parameter
                    var elementIds = parameters?["elementIds"]?.ToObject<string[]>();
                    if (elementIds == null || elementIds.Length == 0)
                    {
                        throw new ArgumentException("elementIds list must not be empty");
                    }

                    // Forward input to the handler
                    _handler.ElementIds = elementIds;

                    // Raise the external event and wait
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        if (_handler.IsSuccess)
                        {
                            // Return structured counts so the caller can
                            // distinguish direct deletions from cascades.
                            // Previously only the total was returned, which
                            // hid cascade side-effects from the user.
                            return new
                            {
                                deleted = true,
                                totalDeletedCount  = _handler.DeletedCount,
                                directDeletedCount = _handler.DirectDeletedCount,
                                cascadeDeletedCount = _handler.CascadeDeletedCount,
                                invalidIds = _handler.InvalidIds
                            };
                        }
                        else
                        {
                            // Handler already formatted the failure reason;
                            // propagate it rather than masking with a generic.
                            var msg = string.IsNullOrEmpty(_handler.ErrorMessage)
                                ? "Delete operation failed."
                                : _handler.ErrorMessage;
                            throw new Exception(msg);
                        }
                    }
                    else
                    {
                        throw new TimeoutException("Delete operation timed out");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Delete failed: {ex.Message}");
                }
            }
        }
    }
}
