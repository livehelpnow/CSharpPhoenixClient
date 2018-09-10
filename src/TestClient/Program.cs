using System;
using Newtonsoft.Json.Linq;

namespace TestClient
{
    class Program
    {
        static void Main(string[] _)
        {
            var socket = new PhoenixChannels.Socket("ws://localhost:4000/socket");
            socket.OnOpen(() =>
            {
                Console.WriteLine("Socket open");
                var @params = new JObject();
                @params["user_id"] = "Milovan";
                
                var chan = socket.Chan("room:lobby", @params);
                
                chan.On("new_msg", (o, s) =>
                {
                    Console.WriteLine($"GOT EVENT {s} with payload {o}");
                });
                
                chan.Join()
                    .Receive("ok", o => { Console.WriteLine("Joined to topic [room:lobby]"); });
            });
            socket.OnError(a => { Console.WriteLine($"Socket error {a.Reason}"); });
            socket.OnClose(eventArgs => { Console.WriteLine("Socket closed"); });
            
            socket.Connect();
            
            Console.WriteLine("Application Started");
            var key = Console.ReadKey();
        }
    }
}