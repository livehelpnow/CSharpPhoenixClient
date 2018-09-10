using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PhoenixChannels
{
    public class Envelope
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }
        [JsonProperty("event")]
        public string Event { get; set; }
        [JsonProperty("payload")]
        public JObject Payload { get; set; }
        [JsonProperty("ref")]
        public string Ref { get; set; }
        [JsonProperty("join_ref")]
        public string JoinRef { get; set; }
    }

}