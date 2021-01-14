using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Devices.Common.DSP;

namespace ClearOneDSP
{
	/// <summary>
	/// Plugin device template for third party devices that use IBasicCommunication
	/// </summary>
    [Description("ClearOne PRO DSP Device")]
    public class ClearOneDSPDevice : DspBase, ICommunicationMonitor, IBridgeAdvanced, IDisposable
    {
        public class QueuedCommand
        {
            public string Command { get; set; }
            public ClearOneDSPVolumeControl Control { get; set; }
        }

        #region Private Fields
        
        private bool disposed = false;
        private ClearOneDSPConfig _config;
        private CommunicationGather _portGather { get; set; }
        private CrestronQueue _commandQueue;
        private QueuedCommand _commandInProgress;
        private CTimer _commandInProgressTimer;
        private CrestronQueue<string> _responseQueue;
        private Thread _responseParseThread;

        private Dictionary<string, ClearOneDSPDeviceInfo> _devices;

        #endregion

        #region Public Properties

        public IBasicCommunication Communication { get; private set; }

        public new Dictionary<string, ClearOneDSPVolumeControl> LevelControlPoints { get; private set; }
        
        #endregion

        #region ICommunicationMonitor Members

        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #endregion

        /// <summary>
        /// CCDDisplay Plugin device constructor for ISerialComport transport
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <param name="display">Loaded and initialized instance of CCD Display driver instance</param>
        public ClearOneDSPDevice(string key, string name, ClearOneDSPConfig config, IBasicCommunication comm)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);

            _config = config;
            _commandQueue = new CrestronQueue(100);
            _responseQueue = new CrestronQueue<string>();
            _responseParseThread = new Thread(parseResponse, null, Thread.eThreadStartOptions.Running);
            _commandInProgressTimer = new CTimer((o) => { _commandInProgress = null; }, Timeout.Infinite);

            _devices = new Dictionary<string, ClearOneDSPDeviceInfo>();

            Communication = comm;
            _portGather = new CommunicationGather(Communication, "\x0D\x0A");
            _portGather.LineReceived += this.lineReceived;

            if (config.CommunicationMonitorProperties != null)
            {
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, config.CommunicationMonitorProperties);
            }
            else
            {
                //#warning Need to deal with this poll string
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 30000, 60000, new Action(() =>
                {
                    if (_devices.Count == 0)
                        _commandQueue.Enqueue("** VER");
                        //sendLine("** VER");

                    foreach (var controlPoint in LevelControlPoints.Values)
                        controlPoint.Poll();
                }));
            }

            LevelControlPoints = new Dictionary<string, ClearOneDSPVolumeControl>();
            foreach (KeyValuePair<string, ClearOneLevelControlBlockConfig> kvp in _config.LevelControlBlocks)
            {
                this.LevelControlPoints.Add(kvp.Key, new ClearOneDSPVolumeControl(kvp.Key, kvp.Value, this));
            }

            CrestronConsole.AddNewConsoleCommand((s) => 
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Devices:");
                    foreach (var kvp in _devices)
                    {
                        sb.AppendFormat("\tDevice: {0}\r\n", kvp.Key);
                        sb.AppendFormat("\t\tModel:     {0}\r\n", kvp.Value.DeviceType.ToString());
                        sb.AppendFormat("\t\tId:        {0}\r\n", kvp.Value.DeviceId);
                        sb.AppendFormat("\t\tFirmware:  {0}\r\n", kvp.Value.Version);
                    }
                    CrestronConsole.ConsoleCommandResponse("{0}", sb.ToString());
                },
                Key + "INFO", "Print Driver Info", ConsoleAccessLevelEnum.AccessOperator);

        }

        ~ClearOneDSPDevice()
        {
            // Do not re-create Dispose clean-up code here. 
            // Calling Dispose(false) is optimal in terms of 
            // readability and maintainability.
            Dispose(false);
        }

        /// <summary>
        /// Registers the Crestron device, connects up to the base events, starts communication monitor
        /// </summary>
        public override bool CustomActivate()
        {
            Debug.Console(0, this, "Activating");
            if (!base.CustomActivate())
                return false;

            Communication.Connect();
            CommunicationMonitor.StatusChange +=
                (o, a) => { Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);};
            CommunicationMonitor.Start();

            CrestronConsole.AddNewConsoleCommand(sendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);

            return true;
        }

        /// <summary>
        /// Adds a command from a child module to the queue
        /// </summary>
        /// <param name="command">Command object from child module</param>
        internal void enqueueCommand(QueuedCommand commandToEnqueue)
        {
            /// check if command already in the queue
            bool commandInQueue = false;
            foreach (var command in _commandQueue)
            {
                string commandText;
                if (command is QueuedCommand)
                {
                    commandText = (command as QueuedCommand).Command;
                }
                else
                {
                    commandText = (string)command;
                }
                if (commandText == commandToEnqueue.Command)
                {
                    commandInQueue = true;
                    break;
                }
            }

            if(commandInQueue)
                Debug.Console(1, this, "Enqueueing command '{0}' is duplicate, skipping. CommandQueue Size: '{1}'", commandToEnqueue.Command, _commandQueue.Count);
            else
                _commandQueue.Enqueue(commandToEnqueue);
            //Debug.Console(1, this, "Command (QueuedCommand) Enqueued '{0}'. CommandQueue Size: '{1}'", commandToEnqueue.Command, CommandQueue.Count);

            if (_commandInProgress == null && _responseQueue.IsEmpty)
                sendNextQueuedCommand();
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        private void sendNextQueuedCommand()
        {
            if (Communication.IsConnected && !_commandQueue.IsEmpty)
            {
                if (_commandQueue.Peek() is QueuedCommand)
                {
                    _commandInProgress = (QueuedCommand)_commandQueue.Dequeue();
                    Debug.Console(1, this, "Command '{0}' Dequeued. CommandQueue Size: {1}", _commandInProgress.Command, _commandQueue.Count);

                    _commandInProgressTimer.Reset(2000);
                    sendLine(_commandInProgress.Command);
                }
                else
                {
                    string nextCommand = (string)_commandQueue.Dequeue();
                    Debug.Console(1, this, "Command '{0}' Dequeued. CommandQueue Size: {1}", nextCommand, _commandQueue.Count);

                    sendLine(nextCommand);
                }
            }
        }

        /// <summary>
        /// Sends a command to the DSP (with delimiter appended)
        /// </summary>
        /// <param name="s">Command to send</param>
        private void sendLine(string s)
        {
            Debug.Console(1, this, "TX: '{0}'", s);
            Communication.SendText('#' + s + "\x0D\x0A");
        }

        /// <summary>
        /// Recieve response message from DSP and queue for processing
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="args"></param>
        private void lineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {

            Debug.Console(1, this, "RX: '{0}'", args.Text);

            try
            {
                if (String.IsNullOrEmpty(args.Text) || (args.Text == @"> ") || (args.Text == @"OK> "))
                {
                    //skip empty messages
                }
                else if (args.Text.IndexOf("ERROR", StringComparison.Ordinal) > -1)
                {
                    // Error response
                    Debug.Console(0, this, "Error From DSP: '{0}'", args.Text);
                }
                else
                    _responseQueue.Enqueue(args.Text);
            }
            catch (Exception e)
            {
                if (Debug.Level == 2)
                    Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
            }

        }

        /// <summary>
        /// Handles a response message from the DSP
        /// </summary>
        /// <param name="obj"></param>
        private object parseResponse(object obj)
        {
            while (true)
            {
                try
                {
                    string respnose = _responseQueue.Dequeue();
                    Debug.Console(1, this, "Response '{0}' Dequeued. ResponseQueue Size: {1}", respnose, _responseQueue.Count);

                    if (respnose == null)
                    {
                        Debug.Console(2, this, "Exception in parseResponse thread, deque string is empty");
                        return null;
                    }

                    if (respnose.StartsWith("OK> #", StringComparison.Ordinal) || respnose.StartsWith("> #", StringComparison.Ordinal))
                    {
                        if (_commandInProgress == null)
                        {
                            /// response is not associated with any particular command, iterate through controls
                            parseAll(respnose);
                        }
                        else
                        {
                            _commandInProgressTimer.Stop();
                            if (!_commandInProgress.Control.Parse(respnose))
                            {
                                /// current command owner could not parse response, iterating through all others
                                parseAll(respnose);
                            }
                            _commandInProgress = null;
                        }
                    }
                }
                catch(Exception e)
                {
                    Debug.Console(2, this, "Exception in parseResponse thread: '{0}'\n{1}", e.Message, e.StackTrace);
                }

                if (!_commandQueue.IsEmpty && _responseQueue.IsEmpty)
                    sendNextQueuedCommand();
            } // while(true)
        }

        /// <summary>
        /// Go through all controls to parse response
        /// </summary>
        /// <param name="obj"></param>
        private bool parseAll(string message)
        {
            bool parsed = false;

            foreach (var controlPoint in LevelControlPoints.Values)
            {
                if (controlPoint.Parse(message))
                {
                    parsed = true;
                    break;
                }
            }

            /// check if it was global version command
            if(!parsed)
                parsed = parseVersion(message);

            return parsed;
        }

        /// <summary>
        /// Parses version response
        /// </summary>
        /// <param name="message">The message to parse</param>
        /// <returns> true if message associated with this parser</returns>
        private bool parseVersion(string message)
        {
            if (message.IndexOf(" VER ") == -1)
                return false;

            try
            {
                string[] parts = message.Substring(message.IndexOf('#')+1).Split(' ');

                char type = parts[0][0];
                DeviceType typeEnum = (DeviceType)Convert.ToByte(type); 
                char id = parts[0][1];
                string ver = parts[2];

                string deviceKey = parts[0];

                _devices[deviceKey] = new ClearOneDSPDeviceInfo() { DeviceType = typeEnum, DeviceId = id, Version = ver };
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Error parsing versions: '{0}'\n{1}", message, e.Message);
            }
            return true;
        }

        #region IBridgeAdvanced Members

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback StatusFeedback { get; private set; }

        /// <summary>
        /// Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new ClearOneDSPBridgeJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

            // links to bridge

            /// eJoinCapabilities.ToFromSIMPL - ToSIMPL subscription
            StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);

            UpdateFeedbacks();

            /// Propagate String/Serial values through eisc when it becomes online 
            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;

                //trilist.SetString(joinMap.Driver.JoinNumber, _display.GetType().AssemblyQualifiedName);
                UpdateFeedbacks();
            };
        }

        private void UpdateFeedbacks()
        {
            StatusFeedback.FireUpdate();
        }

        #endregion


        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method. 
            // Therefore, you should call GC.SupressFinalize to 
            // take this object off the finalization queue 
            // and prevent finalization code for this object 
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios. 
        // If disposing equals true, the method has been called directly 
        // or indirectly by a user's code. Managed and unmanaged resources 
        // can be disposed. 
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed. 
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if(!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if(disposing)
                {
                    // Dispose managed resources.
                    _commandInProgressTimer.Dispose();
                }

                // Note disposing has been done.
                disposed = true;
            }
        }


        #endregion
    }
}

