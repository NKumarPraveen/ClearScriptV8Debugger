using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using V8ChromeDebugEngine.Messages;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using V8ChromeDebugEngine.Events;
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript;
using System.Reflection;
using System.Xml;
using System.Net;

namespace V8ChromeDebugEngine
{
    public class V8EngineSession: IDisposable
    {
        private readonly Regex m_contentLength = new Regex(@"Content-Length:\s*(\d+)", RegexOptions.Compiled);

        private ConcurrentDictionary<long, ManualResetEvent> _requestWaitHandles = new ConcurrentDictionary<long, ManualResetEvent>();
        private ConcurrentDictionary<long, Response> _responses = new ConcurrentDictionary<long, Response>();       

        private ManualResetEvent _openEvent = new ManualResetEvent(false);

        private readonly TcpClient m_tcpClient;        
        private readonly object m_networkClientLock = new object();
        private int m_currentSequence = 1;

        private string m_currentScriptName;
        private readonly List<Breakpoint> m_breakpoints = new List<Breakpoint>();

        private readonly object logFileLock = new object();

        private V8ScriptEngine m_scriptEngine;

        string logFilePath = string.Empty;
        Uri portUri = null;

        /// <summary>
        /// Break point event handler.
        /// </summary>
        public event EventHandler<BreakpointEventArgs> BreakpointEvent;

        /// <summary>
        /// Compile script event handler.
        /// </summary>
        public event EventHandler<CompileScriptEventArgs> CompileScriptEvent;

        /// <summary>
        /// Exception event handler.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExceptionEvent;

        public bool Connected
        {
            get
            {
                lock (m_networkClientLock)
                {
                    return m_tcpClient != null && m_tcpClient.Connected;
                }
            }
        }

        private TcpClient V8TcpClient 
        {
            get 
            {
                lock (m_networkClientLock)
                {
                    return m_tcpClient;
                }
            }
        }

        public string CurrentScriptName
        {
            get
            {
                return m_currentScriptName + " [temp]";
            }
        }

        public V8EngineSession() 
        {
            logFilePath = Path.Combine(Path.GetTempPath(), "V8Enginelog.log");
            
            m_currentScriptName = GetRandomScriptTargetName();
          
            var debuggingPort = PortUtilities.FindFreePort(IPAddress.Loopback);

            m_scriptEngine = new V8ScriptEngine(V8ScriptEngineFlags.DisableGlobalMembers | V8ScriptEngineFlags.EnableDebugging, debuggingPort)
            {
                AllowReflection = false,
            };
                        
            m_scriptEngine.AddHostObject("host", new HostFunctions());
            m_scriptEngine.AddHostObject("lib", new HostTypeCollection("mscorlib", "System.Core"));
            m_scriptEngine.AddHostObject("winform", new HostTypeCollection("System.Windows.Forms"));

            portUri = new Uri("tcp://127.0.0.1:"+debuggingPort);
            m_tcpClient = new TcpClient(portUri.Host, portUri.Port);

            Connect();
        }

        private void Connect()
        {           
            Task.Factory.StartNew(ReadStreamAsync);                        
        }

        private async void ReadStreamAsync()
        {
            try
            {
                TcpClient networkClient;
                
                lock (m_networkClientLock)
                {
                    networkClient = m_tcpClient;
                }

                var networkStream=networkClient.GetStream();
                //networkStream.ReadTimeout = 2000;

                using (var streamReader = new StreamReader(networkStream, Encoding.Default))                              
                {                    
                   while (Connected) 
                   {
                      try
                           {
                               WriteToLogFile("Before Response: ");

                               var result = await streamReader.ReadLineAsync().ConfigureAwait(false);
                               
                               WriteToLogFile("Response from engine. Response: " + result);

                               if (result == null)
                               {
                                   continue;
                               }

                               // Check whether result is content length header
                               var match = m_contentLength.Match(result);

                               if (!match.Success)
                               {
                                   //DebugWriteLine(string.Format("Debugger info: {0}", result));
                                   continue;
                               }

                               await streamReader.ReadLineAsync().ConfigureAwait(false);

                               // Retrieve content length
                               var length = int.Parse(match.Groups[1].Value);
                               if (length == 0)
                               {
                                   continue;
                               }

                               // Read content
                               string message = await streamReader.ReadLineBlockAsync(length).ConfigureAwait(false);

                               WriteToLogFile("Response from engine. Response: " + message);

                               HandleOutput(message);
                           }
                           catch (Exception exx)
                           {
                               WriteToLogFile("Exception reading from stream. Err Mesg:" + exx.Message + Environment.NewLine);
                           }                       
                    }
                }
            }
            catch (Exception e)
            {
                WriteToLogFile("Exception reading from stream. Err Mesg:" + e.Message + Environment.NewLine);
                Debug.Write("Exception reading from stream. Err Mesg:" + e.Message + Environment.NewLine);
            }
        }

        private void HandleOutput(string engineMessage)
        {
            var message = JObject.Parse(engineMessage);
            var messageType = (string)message["type"];

            switch (messageType)
            {   
                case "event":
                    var eventResponse = JsonConvert.DeserializeObject<EventResponse>(engineMessage);
                    HandleEventMessage(eventResponse);
                    break;
                
                case "response":
                    var response = JsonConvert.DeserializeObject<Response>(engineMessage);
                    HandleResponseMessage(response);
                    break;
                
                default:
                    WriteToLogFile(string.Format("Unrecognized type '{0}' in message: {1}", messageType, message));
                    Debug.Fail(string.Format("Unrecognized type '{0}' in message: {1}", messageType, message));
                    break;
            }
        }
        
        private void HandleEventMessage(EventResponse eventResponse)
        {
            var eventType = eventResponse.Event;
            switch (eventType)
            {
                case "afterCompile":
                    //var compileScriptHandler = CompileScriptEvent;
                    //if (compileScriptHandler != null)
                    //{                       
                    //    compileScriptHandler(this, new CompileScriptEventArgs(eventResponse));
                    //}
                    //break;

                case "break":
                    var breakpointHandler = BreakpointEvent;
                    if (breakpointHandler != null)
                    {
                        breakpointHandler(this, new BreakpointEventArgs(eventResponse));
                    }
                    break;

                case "exception":
                    var exceptionHandler = ExceptionEvent;
                    if (exceptionHandler != null)
                    {
                        //var exceptionEvent = new ExceptionEvent(message);
                        //exceptionHandler(this, new ExceptionEventArgs(exceptionEvent));
                    }
                    break;

                case "beforeCompile":
                case "breakForCommand":
                case "newFunction":
                case "scriptCollected":
                    break;

                default:
                    Debug.Fail(string.Format("Unrecognized type '{0}' in event message: {1}", eventType, eventResponse.Body));
                    break;
            }
        }

        private void HandleResponseMessage(Response response)
        {
            TaskCompletionSource<Response> promise;
            var messageId = response.RequestSequence;

            if (null == response) return;
            ManualResetEvent requestMre;

            if (_requestWaitHandles.TryGetValue(messageId, out requestMre))
            {
                _responses.AddOrUpdate(messageId, id => response, (key, value) => response);
                requestMre.Set();               
            }
            else
            {
                // in the case of an error, we don't always get the request Id back :(
                // if there is only one pending requests, we know what to do ... otherwise
                if (1 == _requestWaitHandles.Count)
                {
                    var requestId = _requestWaitHandles.Keys.First();
                    _requestWaitHandles.TryGetValue(requestId, out requestMre);
                    _responses.AddOrUpdate(requestId, id => response, (key, value) => response);
                    requestMre.Set();
                }
            }
        }

        private Task<Response> SendRequestAsync(Request request, CancellationToken cancellationToken = new CancellationToken()) 
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.Sequence.HasValue == false)
                request.Sequence = m_currentSequence++;
            else
                m_currentSequence = request.Sequence.Value + 1;

             var requestResetEvent = new ManualResetEvent(false);
             _requestWaitHandles.AddOrUpdate(request.Sequence.Value, requestResetEvent, (id, r) => requestResetEvent);

             var serializedRequest = JsonConvert.SerializeObject(request);

             return Task.Run(() => {

                 SendMessage(serializedRequest);

                 requestResetEvent.WaitOne();

                 Response response = null;
                 _responses.TryRemove(request.Sequence.Value, out response);

                 _requestWaitHandles.TryRemove(request.Sequence.Value, out requestResetEvent);

                 return response;
             });             
        }

        private async void SendMessage(string message) 
        {
            try
            {
               
                TcpClient networkClient;

                lock (m_networkClientLock)
                {
                    networkClient = m_tcpClient;
                }

                if (!networkClient.Connected)
                {
                    networkClient.Connect(portUri.Host, portUri.Port);
                }

                var networkStream = m_tcpClient.GetStream();

                var streamWriter = new StreamWriter(networkStream);
                
                int byteCount = Encoding.UTF8.GetByteCount(message);
                string request = string.Format("Content-Length: {0}{1}{1}{2}", byteCount, Environment.NewLine, message);

                if(Connected)
                {
                    await streamWriter.WriteAsync(request).ConfigureAwait(false);
                    await streamWriter.FlushAsync().ConfigureAwait(false);
                                       
                    WriteToLogFile("Written to stream. Message:" + request);                    
                }
                
            }
            catch (Exception e)
            {
                WriteToLogFile("Exception writing to stream. Err Mesg:" + e.Message + Environment.NewLine);                    
                Debug.Write("Exception writing to stream. Err Mesg:" + e.Message+Environment.NewLine);
            }
        }
                        
        #region Commands to engine

        public async Task<dynamic> SetBreakpoint(Breakpoint breakpoint)
        {
            var response = await SetBreakpointInternal(breakpoint);

            if (response.Success)
                m_breakpoints.Add(breakpoint);

            return response.Body.breakpoint;
        }

        private async Task<Response> SetBreakpointInternal(Breakpoint breakpoint)
        {
            var breakPointRequest = new Request("setbreakpoint");

            breakPointRequest.Arguments.type = "script";
            breakPointRequest.Arguments.target = CurrentScriptName;

            breakPointRequest.Arguments.line = breakpoint.LineNumber;

            if (breakpoint.Column.HasValue && breakpoint.Column > 0)
                breakPointRequest.Arguments.column = breakpoint.Column.Value;

            if (breakpoint.Enabled == false)
                breakPointRequest.Arguments.enabled = false;

            if (String.IsNullOrWhiteSpace(breakpoint.Condition) == false)
                breakPointRequest.Arguments.condition = breakpoint.Condition;

            if (breakpoint.IgnoreCount.HasValue && breakpoint.IgnoreCount > 0)
                breakPointRequest.Arguments.ignoreCount = breakpoint.IgnoreCount.Value;

            var breakPointResponse = await SendRequestAsync(breakPointRequest);
            return breakPointResponse;
            
        }
        
        private string GetRandomScriptTargetName()
        {
            return Guid.NewGuid().ToString() + "_" + TokenGenerator.GetUniqueKey(10) + ".js";
        }

        private async Task ResetScriptEngine()
        {
            //clear existing breakpoints.
            var currentBreakpoints = await ListAllBreakpoints();

            foreach (var breakpoint in currentBreakpoints.GetBreakpoints().Where(bp => bp.ScriptName == CurrentScriptName))
            {
                await ClearBreakpoint(breakpoint.BreakPointNumber);
            }

            await GarbageCollect();

            m_currentScriptName = GetRandomScriptTargetName();

            //Set breakpoints on new "instance"
            foreach (var breakpoint in m_breakpoints)
            {
                await SetBreakpointInternal(breakpoint);
            }
        }

        public async Task<Response> ListAllBreakpoints()
        {
            var listBreakpointsRequest = new Request("listbreakpoints");

            var listBreakpointsResponse = await SendRequestAsync(listBreakpointsRequest);
            return listBreakpointsResponse;
        }

        /// <summary>
        /// The request backtrace returns a backtrace (or stacktrace) from the current execution state.
        /// </summary>
        /// <remarks>
        /// When issuing a request a range of frames can be supplied. The top frame is frame number 0. If no frame range is supplied data for 10 frames will be returned.
        /// </remarks>
        /// <param name="fromFrame"></param>
        /// <param name="toFrame"></param>
        /// <param name="bottom"></param>
        /// <returns></returns>
        public async Task<Response> Backtrace(int? fromFrame = null, int? toFrame = null, bool? bottom = null)
        {
            var backtrace = new Request("backtrace");

            if (fromFrame.HasValue && toFrame.HasValue)
            {
                backtrace.Arguments.fromFrame = fromFrame.Value;
                backtrace.Arguments.toFrame = toFrame.Value;
            }

            if (bottom.HasValue)
            {
                backtrace.Arguments.bottom = bottom.Value;
            }

            var backtraceResponse = await SendRequestAsync(backtrace);
            return backtraceResponse;
        }

        /// <summary>
        /// The request changebreakpoint changes the status of a break point.
        /// </summary>
        /// <param name="breakpointNumber">number of the break point to clear</param>
        /// <param name="enabled">initial enabled state. True or false, default is true</param>
        /// <param name="condition">string with break point condition</param>
        /// <param name="ignoreCount">number specifying the number of break point hits</param>
        /// <returns></returns>
        public async Task<Response> ChangeBreakpoint(int breakpointNumber, bool enabled = true, string condition = null, int ignoreCount = 0)
        {
            var changeBreakpointRequest = new Request("changebreakpoint");
            changeBreakpointRequest.Arguments.breakpoint = breakpointNumber;
            if (enabled != true)
                changeBreakpointRequest.Arguments.enabled = false;

            if (String.IsNullOrWhiteSpace(condition) == false)
                changeBreakpointRequest.Arguments.condition = condition;

            if (ignoreCount > 0)
                changeBreakpointRequest.Arguments.ignoreCount = ignoreCount;

            var changeBreakpointResponse = await SendRequestAsync(changeBreakpointRequest);
            return changeBreakpointResponse;
        }

        /// <summary>
        /// The request clearbreakpoint clears a break point.
        /// </summary>
        /// <param name="breakpointNumber">number of the break point to clear</param>
        /// <returns></returns>
        public async Task<Response> ClearBreakpoint(int breakpointNumber)
        {
            var clearBreakpointRequest = new Request("clearbreakpoint");
            clearBreakpointRequest.Arguments.breakpoint = breakpointNumber;

            var clearBreakpointResponse = await SendRequestAsync(clearBreakpointRequest);
            return clearBreakpointResponse;
        }

        /// <summary>
        /// The request continue is a request from the debugger to start the VM running again. As part of the continue request the debugger can specify if it wants the VM to perform a single step action.
        /// </summary>
        /// <param name="stepAction"></param>
        /// <param name="stepCount"></param>
        /// <returns></returns>
        public async Task<Response> Continue(StepAction stepAction = StepAction.Next, int? stepCount = null)
        {
            var continueRequest = new Request("continue");

            continueRequest.Arguments.stepaction = stepAction.ToString().ToLowerInvariant();

            if (stepCount.HasValue && stepCount.Value > 1)
                continueRequest.Arguments.stepCount = stepCount.Value;

            var continueResponse = await SendRequestAsync(continueRequest);

            return continueResponse;
        }
                
        /// <summary>
        /// The request disconnect is used to detach the remote debugger from the debuggee.
        /// </summary>
        /// <remarks>
        /// This will trigger the debuggee to disable all active breakpoints and resumes execution if the debuggee was previously stopped at a break.
        /// </remarks>
        /// <returns></returns>
        public async Task<Response> Disconnect()
        {
            var disconnectRequest = new Request("disconnect");

            var disconnectResponse = await SendRequestAsync(disconnectRequest);
            return disconnectResponse;
        }

        /// <summary>
        /// Evaluates the specified code.
        /// </summary>
        /// <remarks>
        /// Subsequent calls to eval will use a different script name.
        /// </remarks>
        /// <param name="code"></param>
        /// <returns></returns>
        public async Task<object> Evaluate(string code)
        {
            var result = m_scriptEngine.Evaluate(m_currentScriptName, true, code);

            await ResetScriptEngine();
            return result;
        }

        public void EvaluateScript(string code)
        {
            m_scriptEngine.Evaluate(m_currentScriptName, true, code);
        }

        /// <summary>
        /// The request evaluate is used to evaluate an expression. The body of the result is as described in response object serialization below.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="disableBreak"></param>
        /// <returns></returns>
        public async Task<Response> EvalImmediate(string expression, bool disableBreak = false)
        {
            var evaluateRequest = new Request("evaluate");
            evaluateRequest.Arguments.expression = expression;
            evaluateRequest.Arguments.frame = 0;
            //evaluateRequest.Arguments.global = true;
            evaluateRequest.Arguments.disable_break = disableBreak;
            evaluateRequest.Arguments.additional_context = new JArray();

            var evalResponse = await SendRequestAsync(evaluateRequest);
            return evalResponse;
        }

        public async Task<Response> EvaluateExpression(string expression, int? frameNumber = null, bool global = false, bool disableBreak = false)
        {
            var evaluateRequest = new Request("evaluate");

            evaluateRequest.Arguments.expression = expression;

            if (frameNumber.HasValue)
                evaluateRequest.Arguments.frame = frameNumber.Value;

            evaluateRequest.Arguments.global = global;
            evaluateRequest.Arguments.disableBreak = disableBreak;

            var evaluateResponse = await SendRequestAsync(evaluateRequest);
            return evaluateResponse;
        }

        public V8Script CompileScript(string codeScript)
        {
            V8Script result = m_scriptEngine.Compile(m_currentScriptName, codeScript);
            return result;
        }

        /// <summary>
        /// The request frame selects a new selected frame and returns information for that. If no frame number is specified the selected frame is returned.
        /// </summary>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Frame(int? frameNumber)
        {
            var frameRequest = new Request("frame");

            if (frameNumber.HasValue)
                frameRequest.Arguments.number = frameNumber.Value;

            var frameResponse = await SendRequestAsync(frameRequest);
            return frameResponse;
        }

        /// <summary>
        /// The request gc is a request to run the garbage collector in the debuggee.
        /// </summary>
        /// <returns></returns>
        public async Task<Response> GarbageCollect()
        {
            var gcRequest = new Request("gc");

            var gcResponse = await SendRequestAsync(gcRequest);
            return gcResponse;
        }

        /// <summary>
        /// Stops the script engine at the current point.
        /// </summary>
        /// <returns></returns>
        public async Task Interrupt()
        {
            try
            {
                m_scriptEngine.Interrupt();
            }
            catch (Exception)
            {
                throw;
            }

            await ResetScriptEngine();
        }

        /// <summary>
        /// The request lookup is used to lookup objects based on their handle.
        /// </summary>
        /// <remarks>
        /// The individual array elements of the body of the result is as described in response object serialization below.
        /// </remarks>
        /// <param name="includeSource"></param>
        /// <param name="handles"></param>
        /// <returns></returns>
        public async Task<Response> Lookup(bool includeSource = false, params int[] handles)
        {
            var lookupRequest = new Request("lookup");
            lookupRequest.Arguments.handles = handles;
            lookupRequest.Arguments.includeSource = includeSource;

            var response = await SendRequestAsync(lookupRequest);
            return response;
        }

        /// <summary>
        /// The request scope returns information on a given scope for a given frame. If no frame number is specified the selected frame is used.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Scope(int number, int? frameNumber = null)
        {
            var scopeRequest = new Request("scope");
            scopeRequest.Arguments.number = number;

            if (frameNumber.HasValue)
                scopeRequest.Arguments.frameNumber = frameNumber;

            var response = await SendRequestAsync(scopeRequest);
            return response;
        }

        /// <summary>
        /// The request scopes returns all the scopes for a given frame. If no frame number is specified the selected frame is returned.
        /// </summary>
        /// <param name="frameNumber"></param>
        /// <returns></returns>
        public async Task<Response> Scopes(int? frameNumber = null)
        {
            var scopesRequest = new Request("scopes");

            if (frameNumber.HasValue)
                scopesRequest.Arguments.frameNumber = frameNumber.Value;

            var response = await SendRequestAsync(scopesRequest);
            return response;
        }

        /// <summary>
        /// The request scripts retrieves active scripts from the VM.
        /// </summary>
        /// <remarks>
        /// An active script is source code from which there is still live objects in the VM. This request will always force a full garbage collection in the VM.
        /// </remarks>
        /// <param name="types">types of scripts to retrieve</param>
        /// <param name="ids">array of id's of scripts to return. If this is not specified all scripts are requrned</param>
        /// <param name="includeSource">boolean indicating whether the source code should be included for the scripts returned</param>
        /// <param name="filter">filter string or script id.</param>
        /// <returns></returns>
        public async Task<Response> Scripts(ScriptType? types = null, int[] ids = null, bool includeSource = false, string filter = null)
        {
            var scriptsRequest = new Request("scripts");

            if (types.HasValue)
                scriptsRequest.Arguments.types = (byte)types.Value;

            if (ids != null && ids.Length > 0)
                scriptsRequest.Arguments.ids = ids;

            if (includeSource)
                scriptsRequest.Arguments.includeSource = true;

            if (String.IsNullOrWhiteSpace(filter) == false)
            {
                int scriptNumber;
                if (int.TryParse(filter, out scriptNumber))
                    scriptsRequest.Arguments.filter = scriptNumber;
                else
                    scriptsRequest.Arguments.filter = filter;
            }
            
            var response = await SendRequestAsync(scriptsRequest);
            return response;
        }

        /// <summary>
        /// The request source retrieves source code for a frame.
        /// </summary>
        /// <remarks>
        /// It returns a number of source lines running from the fromLine to but not including the toLine,
        /// that is the interval is open on the "to" end. For example,
        /// requesting source from line 2 to 4 returns two lines (2 and 3).
        /// Also note that the line numbers are 0 based: the first line is line 0.
        /// </remarks>
        /// <param name="frameNumber">frame number (default selected frame)</param>
        /// <param name="fromLine">from line within the source default is line 0</param>
        /// <param name="toLine">to line within the source this line is not included in the result default is the number of lines in the script</param>
        /// <returns></returns>
        public async Task<Response> Source(int? frameNumber = null, int? fromLine = null, int? toLine = null)
        {
            var sourceRequest = new Request("source");

            if (frameNumber.HasValue)
                sourceRequest.Arguments.frame = frameNumber.Value;

            if (fromLine.HasValue)
                sourceRequest.Arguments.fromLine = fromLine.Value;

            if (toLine.HasValue)
                sourceRequest.Arguments.toLine = toLine.Value;

            var response = await SendRequestAsync(sourceRequest);
            return response;
        }
        
        #endregion

        public void Dispose()
        {
            if (m_tcpClient != null)
            {
                m_tcpClient.Close();
            }

            if (m_scriptEngine != null)
            {
                m_scriptEngine.Interrupt();
                //m_scriptEngine.CollectGarbage(true);
                m_scriptEngine.Dispose();
                m_scriptEngine = null;
            }
        }

        #region Debug

        private void WriteToLogFile(string log)
        {
            lock (logFileLock)
            {
                try
                {
                    File.AppendAllText(logFilePath, Environment.NewLine);
                    File.AppendAllText(logFilePath, log);
                    File.AppendAllText(logFilePath, Environment.NewLine);
                }
                catch (Exception)
                {

                }
            }
        }

        #endregion


    }
}
