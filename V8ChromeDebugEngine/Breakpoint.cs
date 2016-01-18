namespace V8ChromeDebugEngine
{
    using Newtonsoft.Json;

    public class Breakpoint
    {
        public Breakpoint()
        {
            this.Enabled = true;
        }

        public Breakpoint(int lineNumber)
            : this()
        {
            this.LineNumber = lineNumber;
        }

        [JsonProperty("breakPointNumber")]
        public int BreakPointNumber
        {
            get;
            set;
        }

        [JsonProperty("scriptId")]
        public int? ScriptId
        {
            get;
            set;
        }

        [JsonProperty("scriptName")]
        public string ScriptName
        {
            get;
            set;
        }

        [JsonProperty("lineNumber")]
        public int LineNumber
        {
            get;
            set;
        }

        [JsonProperty("column")]
        public int? Column
        {
            get;
            set;
        }

        [JsonProperty("groupId")]
        public int? GroupId
        {
            get;
            set;
        }

        [JsonProperty("enabled")]
        public bool Enabled
        {
            get;
            set;
        }

        [JsonProperty("condition")]
        public string Condition
        {
            get;
            set;
        }

        [JsonProperty("ignoreCount")]
        public int? IgnoreCount
        {
            get;
            set;
        }

        [JsonProperty("hitCount")]
        public int? HitCount
        {
            get;
            set;
        }
    }
}
