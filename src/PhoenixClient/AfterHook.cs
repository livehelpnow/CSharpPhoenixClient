using System;
using System.Timers;

namespace PhoenixChannels
{
    /// <summary>
    /// Object used to register hooks which should be executed after message is pushed.
    ///
    /// Only single callback action can be registered in <see cref="Push"/> instance.
    /// </summary>
    internal class AfterHook
    {
        public int Ms { get; set; }
        public Action Callback { get; set; }
        public Timer Timer { get; set; }
    }
}