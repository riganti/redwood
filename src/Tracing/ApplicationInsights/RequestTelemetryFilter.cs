using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace DotVVM.Tracing.ApplicationInsights
{
    public class RequestTelemetryFilter : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; set; }

        public RequestTelemetryFilter(ITelemetryProcessor next)
        {
            this.Next = next;
        }

        public void Process(ITelemetry item)
        {
            if (!CheckIfSend(item)) { return; }

            this.Next.Process(item);
        }

        private bool CheckIfSend(ITelemetry item)
        {
            var request = item as RequestTelemetry;
            if (request == null)
            {
                return true;
            }
            if (request.Url.AbsolutePath.StartsWith("/_dotvvm/resource") || request.ResponseCode.Equals("404"))
            {
                return false;
            }

            return true;
        }
    }
}
