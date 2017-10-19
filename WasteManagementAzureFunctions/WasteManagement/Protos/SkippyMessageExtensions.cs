using System;
using System.Linq;
using System.Text;
using Google.ProtocolBuffers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkippyCore;

namespace Lx.Zangor.SkippyEventProcessor.Protos
{
    public static class SkippyMessageExtensions
    {
        public static string GetEventType(this SkippyMessage message)
        {
            switch (message.Reason)
            {
                case SkippyMessage.Types.ReasonForMessage.ALARM:
                    return "alarm";

                case SkippyMessage.Types.ReasonForMessage.SIGN_ON:
                    return "startup";

                case SkippyMessage.Types.ReasonForMessage.SAMPLE_PERIOD:
                default:
                    return "heartbeat";
            }
        }

        public static string GetEventJson(this SkippyMessage message, string deviceId)
        {
            // There is a bug in some FW versions which results in the deviceId being reported by the
            // device not being reliable.  So we just overwrite it here with what the device registerred
            // against the server with.
            var jsonObject = JObject.Parse(message.ToJson());

            jsonObject["device"]["deviceId"] = deviceId;

            return jsonObject.ToString(Formatting.None);
        }
    }
}