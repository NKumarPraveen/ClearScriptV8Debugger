namespace V8ChromeDebugEngine.Events
{
    using System;
    using V8ChromeDebugEngine.Messages;

    public sealed class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(EventResponse exceptionEvent)
        {
            ExceptionEvent = exceptionEvent;
        }

        public EventResponse ExceptionEvent { get; private set; }
    }
}