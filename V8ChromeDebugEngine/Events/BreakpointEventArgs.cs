namespace V8ChromeDebugEngine.Events
{
    using System;
    using V8ChromeDebugEngine.Messages;

    public sealed class BreakpointEventArgs : EventArgs
    {
        public BreakpointEventArgs(EventResponse breakpointEvent)
        {
            BreakpointEvent = breakpointEvent;
        }

        public EventResponse BreakpointEvent { get; private set; }
    }
}