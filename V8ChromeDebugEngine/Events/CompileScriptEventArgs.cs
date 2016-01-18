namespace V8ChromeDebugEngine.Events
{
    using System;
    using V8ChromeDebugEngine.Messages;

    public sealed class CompileScriptEventArgs : EventArgs
    {
        public CompileScriptEventArgs(EventResponse compileScriptEvent)
        {
            CompileScriptEvent = compileScriptEvent;
        }

        public EventResponse CompileScriptEvent { get; private set; }
    }
}