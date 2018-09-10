using System;
using Newtonsoft.Json.Linq;

namespace PhoenixChannels
{
    /// <summary>
    /// Receive Hook used to register delegates/callbacks which should be executed when message is received from server.
    /// </summary>
    internal class RecHook
    {
        public string Status { get; set; }
        public Action<JObject> Callback { get; set; }
    }
}