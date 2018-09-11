using System;
using Newtonsoft.Json.Linq;
using PhoenixChannels;

namespace TestClient
{
    class Program
    {

        private Socket _socket;
        private Channel _chan;
        public Program()
        {
            _socket = new Socket("ws://localhost:4000/socket");
            _socket.OnOpen(OnConnect);
            _socket.OnClose(OnDisconnect);
            _socket.OnError(OnError);
        }

        public void Start()
        {
            _socket.Connect();
            Info("Application Started");
        }

        private void OnConnect()
        {
            Info("Connected...");
            var @params = new JObject
            {
                ["user_id"] = "TestClient"
            };
            var topic = "room:lobby";
            _chan = _socket.Chan(topic, @params);
            _chan.On("new_msg", OnLobbyMessage);
            _chan.Join()
                .Receive("ok", OnLobbyJoinSuccess)
                .Receive("error", OnLobbyJoinError);
        }

        private void OnLobbyMessage(JObject arg1, string _)
        {
            Info($"Message from Lobby: {arg1["body"]}");
        }

        private void OnLobbyJoinError(JObject obj)
        {
            Error($"Failed to join to lobby channel due {obj}");
        }

        private void OnLobbyJoinSuccess(JObject obj)
        {
            Info($"Successfuly joined to lobby got message: {obj}");
            var hiMessage = new JObject
            {
                ["body"] = "Hello phoenix!"
            };
            _chan.Push("new_msg", hiMessage)
                 .Receive("ok", o =>
                 {
                     Info("Succesfully pushed message to lobby.");
                 }).Receive("error", e =>
                 {
                     Error($"Failed to push message to pheoenix channel lobby, due {e}");
                 });
        }

        private void OnError(ErrorEventArgs args)
        {
            Error($"Socket error {args.Reason}");
        }

        private void OnDisconnect(CloseEventArgs args)
        {
            Warn($"Socket disconnected {args.Reason}");
        }

        private void Info(string message)
        {
            Log($"[INFO] {message}", ConsoleColor.Green);
        }

        private void Error(string message)
        {
            Log($"[ERROR] {message}", ConsoleColor.Red);
        }

        private void Warn(string message)
        {
            Log($"[WARNING] {message}", ConsoleColor.Yellow);
        }

        private void Log(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss zzz")}] {message}");
            Console.ResetColor();
        }

        static void Main(string[] _)
        {

            var chatApp = new Program();

            chatApp.Start();
            var key = Console.ReadKey();
        }
    }
}