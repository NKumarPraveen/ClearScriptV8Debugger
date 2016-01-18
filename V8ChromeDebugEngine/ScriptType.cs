namespace V8ChromeDebugEngine
{
    using System;

    [Flags]
    public enum ScriptType
    {
        Native = 0,
        Extension = 1,
        Normal = 2,
    }
}
