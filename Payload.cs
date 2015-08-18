using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using System.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PhoenixChannels
{
    public class Payload //Todo make this JOBJECT
    {
        public string Status {get; set;}
        public object Response {get; set;}
        public string Ref { get; set; }
    }

}