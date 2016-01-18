namespace V8ChromeDebugEngine.Messages
{
    using System.Dynamic;
    using Newtonsoft.Json;

    public class Request
    {
        public Request(string command)
        {
            this.Type = "request";
            this.Command = command;
            this.Arguments = new ExpandoObject();
        }

        public Request(int sequence, string command)
            : this(command)
        {
            this.Sequence = sequence;
        }

        /// <summary>
        /// Sequence number of the request.
        /// </summary>
        [JsonProperty("seq")]
        public int? Sequence
        {
            get;
            set;
        }

        /// <summary>
        /// The type of request (will always be "request");
        /// </summary>
        [JsonProperty("type")]
        public string Type
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the command associated with the request.
        /// </summary>
        [JsonProperty("command")]
        public string Command
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the arguments associated with the request.
        /// </summary>
        [JsonProperty("arguments")]
        public dynamic Arguments
        {
            get;
            set;
        }
    }
}