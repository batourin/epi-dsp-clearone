using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace ClearOneDSP
{
    public class ClearOneDSPVolumeControl : IBasicVolumeWithFeedback, IKeyName
    {
        ClearOneLevelControlBlockConfig _config;
        private ClearOneDSPDevice _parent;
        bool _isMuted;
        ushort _volumeLevel;

        private readonly string _muteCmd;
        private readonly string _gainCmd;
        private readonly string _muteCmdFeedbackKey;
        private readonly string _gainCmdFeedbackKey;

        public string Key { get; protected set; }
        public string Name { get; protected set; }

        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }


        public ClearOneDSPVolumeControl(string key, ClearOneLevelControlBlockConfig config, ClearOneDSPDevice parent)
        {
            _config = config;
            _parent = parent;
            Key = string.Format("{0}--{1}", _parent.Key, key);
            Name = config.Label;

            DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding LevelControl '{0}':{1}", Key, Name);

            _muteCmd = String.Format("{0}{1} {2} {3} {4}", _config.DeviceType.ToChar(), _config.DeviceId, @"MUTE", _config.Channel, _config.Group.ToChar());
            _gainCmd = String.Format("{0}{1} {2} {3} {4}", _config.DeviceType.ToChar(), _config.DeviceId, @"GAIN", _config.Channel, _config.Group.ToChar());

            _muteCmdFeedbackKey = "OK> #" + _muteCmd + ' ';
            _gainCmdFeedbackKey = "OK> #" + _gainCmd + ' ';

            MuteFeedback = new BoolFeedback(() => _isMuted);
            VolumeLevelFeedback = new IntFeedback(() => _volumeLevel);
        }

        internal void Poll()
        {
            prepareCommand(_muteCmd, null);
            prepareCommand(_gainCmd, null);
        }

        /// <summary>
        /// Parses response
        /// </summary>
        /// <param name="message">The message to parse</param>
        /// <returns> true if message associated with this parser</returns>
        public bool Parse(string message)
        {
            if (message.StartsWith(_muteCmdFeedbackKey, StringComparison.Ordinal))
            {
                switch (message.Substring(_muteCmdFeedbackKey.Length))
                {
                    case OnOffToggle.Off:
                        if (_isMuted)
                        {
                            _isMuted = false;
                            MuteFeedback.FireUpdate();
                        }
                        break;
                    case OnOffToggle.On:
                        if (!_isMuted)
                        {
                            _isMuted = true;
                            MuteFeedback.FireUpdate();
                        }
                        break;
                    default:
                        Debug.Console(1, this, "Can't parse MUTE feedback: \'{0}\'", message);
                        break;
                }
                return true;
            }
            else if (message.StartsWith(_gainCmdFeedbackKey, StringComparison.Ordinal))
            {
                string val = message.Substring(_gainCmdFeedbackKey.Length, message.IndexOf(' ',_gainCmdFeedbackKey.Length) - _gainCmdFeedbackKey.Length);
                if (!String.IsNullOrEmpty(val))
                {
                    var db = Double.Parse(val);
                    ushort newVolumeLevel = (ushort)scale(db, -65.00, 20.00, 0, 65535);
                    if (_volumeLevel != newVolumeLevel)
                    {
                        _volumeLevel = newVolumeLevel;
                        VolumeLevelFeedback.FireUpdate();
                    }
                    Debug.Console(1, this, "Volume feedback: \'{0}\'", _volumeLevel);
                }
                return true;
            }
            return false;
        }

        private void prepareCommand(string command, string value)
        {
            // This command will generate a return value response so it needs to be queued
            if (String.IsNullOrEmpty(value))
                _parent.enqueueCommand(new ClearOneDSPDevice.QueuedCommand { Command = command, Control = this });
            else
                _parent.enqueueCommand(new ClearOneDSPDevice.QueuedCommand { Command = command + " " + _config.Group.ToChar(), Control = this });
        }

        /// <summary>
        /// Scales the input from the input range to the output range
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inMin"></param>
        /// <param name="inMax"></param>
        /// <param name="outMin"></param>
        /// <param name="outMax"></param>
        /// <returns></returns>
        private double scale(double input, double inMin, double inMax, double outMin, double outMax)
        {
            Debug.Console(2, this, "Scaling (double) input '{0}' with min '{1}'/max '{2}' to output range min '{3}'/max '{4}'", input, inMin, inMax, outMin, outMax);

            double inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
            }

            double outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            Debug.Console(2, this, "Scaled output '{0}'", output);

            return output;
        }

        #region IBasicVolumeWithFeedback Members


        public void MuteOff()
        {
            prepareCommand(_muteCmd, OnOffToggle.Off);
        }

        public void MuteOn()
        {
            prepareCommand(_muteCmd, OnOffToggle.On);
        }

        public void SetVolume(ushort level)
        {
            Debug.Console(1, this, "volume: {0}", level);

            if (level > _volumeLevel)
                if (_isMuted)
                    MuteOff();

            double volumeLevel = scale(level, 0, 65535, -65, 20);

            prepareCommand(_gainCmd, string.Format("{0:#0.00} A", volumeLevel)); 
        }

        #endregion

        #region IBasicVolumeControls Members

        public void MuteToggle()
        {
            prepareCommand(_muteCmd, OnOffToggle.Toggle);
        }

        public void VolumeDown(bool pressRelease)
        {
            prepareCommand(_gainCmd, string.Format("{0:#0.00} R", -1.00));
        }

        public void VolumeUp(bool pressRelease)
        {
            prepareCommand(_gainCmd, string.Format("{0:#0.00} R", 1.00)); 

            if (!_isMuted)
                MuteOff();
        }

        #endregion
    }
}