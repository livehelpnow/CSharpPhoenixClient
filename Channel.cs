using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using System.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PhoenixChannels
{
    public class Channel
    {
        private ChannelState _state;

        public string Topic { get; set; }

        public Socket Socket { get; set; }
        
        public string JoinRef { get; private set; }
        

        private IDictionary<string, List<Action<JObject, string>>> _bindings;
        private bool _alreadyJoinedOnce;
        private Push _joinPush;
        private IList<Push> _pushBuffer;
        private Timer _rejoinTimer;

        public Channel(string topic, JObject params_, Socket socket)
        {
            _state = ChannelState.Closed;
            Topic = topic;

            Socket = socket;
            _bindings = new Dictionary<string, List<Action<JObject, string>>>();
            _alreadyJoinedOnce = false;
            _pushBuffer = new List<Push>();
            
            JoinRef = Socket.MakeRef();
            
            _joinPush = new Push(this, ChannelEvents.Join, params_);
            _joinPush.Receive("ok", (x) =>
            {
                _state = ChannelState.Joined;
            });

            OnClose((o, reference) =>
            {
                _state = ChannelState.Closed;
                Socket.Remove(this);
            });

            OnError((reason, reference) => //reason is not used
            {
                _state = ChannelState.Errored;
                _rejoinTimer.Start();

            });

            On(ChannelEvents.Reply, (payload, reference) =>
            {
                Trigger(ReplyEventName(reference), payload, reference);
            });


            _rejoinTimer = new Timer(Socket.ReconnectAfterMs);
            _rejoinTimer.AutoReset = false;
            _rejoinTimer.Elapsed += (o, e) => RejoinUntilConnected();
            //_rejoinTimer.Enabled = true;
        }
        private void RejoinUntilConnected()
        {
            if (_state != ChannelState.Errored) return;

            if (Socket.IsConnected())
            {
                Rejoin();
            }
            else
            {
                _rejoinTimer.Start();
            }
        }          

        public Push Join()
        {
            if (_alreadyJoinedOnce)
            {
                throw new Exception("tried to join mulitple times. 'join' can only be called a singe time per channel instance");
            }
            else
            {
                _alreadyJoinedOnce = true;
            }

            SendJoin();

            return _joinPush;
        }

        public void OnClose(Action<object, string> callback)
        {
            On(ChannelEvents.Close, callback);
        }

        public void OnError(Action<object, string> callback)
        {
            On(ChannelEvents.Error, callback);
        }

        public void On(string evt, Action<JObject, string> callback)
        {
            if (!_bindings.ContainsKey(evt))
                _bindings[evt] = new List<Action<JObject, string>>();
            _bindings[evt].Add(callback);
        }

        public void Off(string evt)
        {
            _bindings.Remove(evt);
        }

        private bool CanPush()
        {
            return Socket.IsConnected() && _state == ChannelState.Joined;
        }

        public Push Push(string event_, JObject payload = null)
        {
            if (!_alreadyJoinedOnce)
            {
                throw new Exception(string.Format("tried to push {0} to {1} before joining. Use Channel.Join() before pushing events", event_, payload));
            }

            var pushEvent = new Push(this, event_, payload);

            if (CanPush())
            {
                pushEvent.Send();
            }
            else
            {
                _pushBuffer.Add(pushEvent);
            }

            return pushEvent;
        }

        public Push Leave()
        {
            return Push(ChannelEvents.Leave).Receive("ok", (x) =>
            {
                this.Trigger(ChannelEvents.Close);//, "leave");
            });
        }

        public bool IsMember(string topic)
        {
            return Topic == topic;
        }

        private void SendJoin()
        {
            _state = ChannelState.Joining;
            _joinPush.Send();
        }

        private void Rejoin()
        {
            SendJoin();
            foreach (var p in _pushBuffer)
            {
                p.Send();
            }
            _pushBuffer.Clear();
        }

        internal void Trigger(string event_, JObject msg = null, string reference = null)
        {
            if (_bindings.ContainsKey(event_))
            {
                foreach (var callback in _bindings[event_])
                {
                    callback(msg, reference);
                }
            }
        }
        public string ReplyEventName(string ref_)
        {
            return string.Format("chan_reply_{0}", ref_);
        }
    }
}