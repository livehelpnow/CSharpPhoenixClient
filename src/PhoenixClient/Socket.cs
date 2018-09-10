using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Timers;
using Newtonsoft.Json.Linq;
using PureWebSockets;

namespace PhoenixChannels
{
    public struct CloseEventArgs
    {
        public WebSocketCloseStatus Reason { get; }

        public CloseEventArgs(WebSocketCloseStatus reason)
        {
            Reason = reason;
        }
    }

    public struct ErrorEventArgs
    {
        public Exception Reason { get; }

        public ErrorEventArgs(Exception reason)
        {
            Reason = reason;
        }
    }

    public class Socket
    {
        private IList<Action> _openCallbacks;
        private IList<Action<CloseEventArgs>> _closeCallbacks;
        private IList<Action<ErrorEventArgs>> _errorCallbacks;
        private IList<Action<string, string, JObject>> _messageCallbacks;
        private IList<Channel> _channels;

        private IList<Action> _sendBuffer;
        private int _ref = 0;
        private int _heartbeatIntervalMs;
        private string _endPoint;

        private PureWebSocket _conn;
        private Timer _reconnectTimer;
        private Timer _heartbeatTimer;

        public int ReconnectAfterMs { get; set; }

        public Socket(string endPoint, int heartbeatIntervalMs = 30000, int reconnectAfterMs = 5000)
        {
            _openCallbacks = new List<Action>();
            _closeCallbacks = new List<Action<CloseEventArgs>>();
            _errorCallbacks = new List<Action<ErrorEventArgs>>();
            _messageCallbacks = new List<Action<string, string, JObject>>();

            _channels = new List<Channel>();
            _sendBuffer = new List<Action>();
            _ref = 0;

            _heartbeatIntervalMs = heartbeatIntervalMs;
            ReconnectAfterMs = reconnectAfterMs;

            var uri = new Uri(endPoint);
            if (uri.Scheme != "ws" && uri.Scheme != "wss")
            {
                throw new ArgumentException($"Uri scheme is invalid, should be wss or ws instead got '{uri.Scheme}'",
                    nameof(endPoint));
            }

            if (!uri.Query.Contains("vsn=2.0.0"))
            {
                
                var builder = new UriBuilder(
                    uri.Scheme,
                    uri.Host,
                    uri.Port,
                    uri.AbsolutePath.EndsWith("/websocket") ? uri.AbsolutePath : uri.AbsolutePath + "/websocket",
                    uri.Query.Length == 0 ? "?vsn=2.0.0" : "&vsn=2.0.0"
                );
                
                uri = builder.Uri;
            }

            _endPoint = uri.ToString();


            _reconnectTimer = new Timer(ReconnectAfterMs);
            _reconnectTimer.AutoReset = true;
            //_reconnectTimer.Enabled = false;
            _reconnectTimer.Elapsed += (o, e) => Connect();

            _heartbeatTimer = new Timer(_heartbeatIntervalMs);
            _heartbeatTimer.AutoReset = true;
            //_heartbeatTimer.Enabled = true;
            _heartbeatTimer.Elapsed += (o, e) => SendHeartbeat();
        }

        public void Disconnect(Action callback)
        {
            if (_conn != null)
            {
                // _conn.OnClose(); //TODO how to clear event handler?
                if (_conn.State == WebSocketState.Open)
                {
                    _conn.Disconnect();
                }

                _conn.Dispose();
                _conn = null;
            }

            callback?.Invoke();
        }

        public void Connect()
        {
            if (IsConnected())
            {
                return;
            }

            Disconnect(() =>
            {
                var opts = new PureWebSocketOptions
                {
                    MyReconnectStrategy = new ReconnectStrategy(1000, 30000)
                };
                _conn = new PureWebSocket(_endPoint, opts);
                _conn.OnOpened += OnConnOpen;
                _conn.OnError += OnConnError;
                _conn.OnMessage += OnConnMessage;
                _conn.OnClosed += OnConnClose;
                try
                {
                    _conn.Connect();
                    _reconnectTimer.Stop();
                }
                catch (Exception ex)
                {
                    _reconnectTimer.Start();
                }
            });
        }


        public Socket OnOpen(Action callback)
        {
            _openCallbacks.Add(callback);
            return this;
        }

        public Socket OnClose(Action<CloseEventArgs> callback)
        {
            _closeCallbacks.Add(callback);
            return this;
        }

        public Socket OnError(Action<ErrorEventArgs> callback)
        {
            _errorCallbacks.Add(callback);
            return this;
        }

        public Socket OnMessage(Action<string, string, JObject> callback)
        {
            _messageCallbacks.Add(callback);
            return this;
        }


        private void OnConnOpen()
        {
            FlushSendBuffer();
            _reconnectTimer.Stop();
            _heartbeatTimer.Stop();
            _heartbeatTimer.Start();

            foreach (var callback in _openCallbacks)
            {
                callback();
            }
        }


        private void OnConnClose(WebSocketCloseStatus reason)
        {
            TriggerChanError();
            _reconnectTimer.Stop();
            _heartbeatTimer.Stop();

            _reconnectTimer.Start();
            var args = new CloseEventArgs(reason);
            foreach (var callback in _closeCallbacks) callback(args);
        }

        private void OnConnError(Exception exception)
        {
            TriggerChanError();
            var args = new ErrorEventArgs(exception);
            foreach (var callback in _errorCallbacks) callback(args);
        }

        private void TriggerChanError()
        {
            foreach (var c in _channels)
            {
                c.Trigger(ChannelEvents.Error);
            }
        }

        private WebSocketState ConnectionState()
        {
            return _conn?.State ?? WebSocketState.Closed;
        }

        public bool IsConnected()
        {
            return ConnectionState() == WebSocketState.Open;
        }

        public void Remove(Channel chan)
        {
            _channels = _channels.Where(c => !c.IsMember(chan.Topic)).ToList();
        }

        public Channel Chan(string topic, JObject payload)
        {
            var chan = new Channel(topic, payload, this);
            _channels.Add(chan);
            return chan;
        }

        public void Push(Envelope envelope)
        {
            var push = new JArray(
                envelope.JoinRef,
                envelope.Ref,
                envelope.Topic,
                envelope.Event,
                envelope.Payload);
            void Callback() => _conn.Send(push.ToString());

            if (IsConnected())
            {
                Callback();
            }
            else
            {
                _sendBuffer.Add(Callback);
            }
        }

        public string MakeRef()
        {
            var newRef = _ref + 1;
            _ref = (newRef < Int32.MaxValue) ? _ref = newRef : _ref = 0;

            return _ref.ToString();
        }

        private void SendHeartbeat()
        {
            var env = new Envelope()
            {
                Topic = "phoenix",
                Event = "heartbeat",
                Payload = new JObject(),
                Ref = MakeRef()
            };

            Push(env);
        }

        private void FlushSendBuffer()
        {
            if (IsConnected() && _sendBuffer.Count > 0)
            {
                foreach (var c in _sendBuffer)
                {
                    c();
                }

                _sendBuffer.Clear();
            }
        }

        private void OnConnMessage(string message)
        {
            var push = JArray.Parse(message);
            var env = new Envelope
            {
                JoinRef = push.Value<string>(0),
                Ref = push.Value<string>(1),
                Topic = push.Value<string>(2),
                Event = push.Value<string>(3),
                Payload = push.Value<JObject>(4)
            };

            foreach (var chan in _channels.Where(c => c.IsMember(env.Topic)).ToList())
            {
                chan.Trigger(env.Event, env.Payload, env.Ref);
            }

            foreach (var callback in _messageCallbacks) callback(env.Topic, env.Event, env.Payload);
        }
    }
}