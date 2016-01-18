namespace V8ChromeDebugEngine.Messages
{
    using Newtonsoft.Json;

    public class Response
    {
        /// <summary>
        /// Sequence number of the response.
        /// </summary>
        [JsonProperty("seq")]
        public int Sequence
        {
            get;
            set;
        }

        /// <summary>
        /// The type of response.
        /// </summary>
        [JsonProperty("type")]
        public string Type
        {
            get;
            set;
        }

        /// <summary>
        /// Sequence number of the request.
        /// </summary>
        [JsonProperty("request_seq")]
        public int RequestSequence
        {
            get;
            set;
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
        /// Gets the body associated with the response.
        /// </summary>
        [JsonProperty("body")]
        public dynamic Body
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a value that indicates if the VM is running after this response.
        /// </summary>
        [JsonProperty("running")]
        public bool Running
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a value that indicates if the request was successful.
        /// </summary>
        [JsonProperty("success")]
        public bool Success
        {
            get;
            set;
        }

        /// <summary>
        /// If the request was not successful, returns a message.
        /// </summary>
        [JsonProperty("message")]
        public string Message
        {
            get;
            set;
        }
    }
}
