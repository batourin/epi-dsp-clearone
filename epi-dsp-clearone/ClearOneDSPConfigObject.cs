using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace ClearOneDSP
{
	/// <summary>
	/// Plugin device configuration object
	/// </summary>
	[ConfigSnippet("\"properties\":{\"control\":{}")]
	public class ClearOneDSPConfig
	{
		/// <summary>
		/// JSON control object
		/// </summary>
		/// <remarks>
		/// Required for CCD Transports: ISerialComport, ICecDevice, IIr.
		/// </remarks>
		/// <example>
		/// <code>
		/// "control": {
        ///		"method": "com",
		///		"controlPortDevKey": "processor",
		///		"controlPortNumber": 1,
		///		"comParams": {
		///			"baudRate": 57600,
		///			"dataBits": 8,
		///			"stopBits": 1,
		///			"parity": "None",
		///			"protocol": "RS232",
		///			"hardwareHandshake": "None",
		///			"softwareHandshake": "None"
		///		}
		///	}
		/// </code>
		/// </example>
		[JsonProperty("control", Required=Required.Default)]
		public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("communicationMonitorProperties")]
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        /// <summary>
		/// These are key-value pairs, string id, string type.  
		/// Valid types are level and mute.
		/// </summary>
        [JsonProperty("levels", Required = Required.Always)]
        public Dictionary<string, ClearOneLevelControlBlockConfig> LevelControlBlocks { get; set; }

		/// <summary>
		/// Constuctor
		/// </summary>
		/// <remarks>
		/// If using a collection you must instantiate the collection in the constructor
		/// to avoid exceptions when reading the configuration file 
		/// </remarks>
		public ClearOneDSPConfig()
		{
            LevelControlBlocks = new Dictionary<string, ClearOneLevelControlBlockConfig>();
		}
	}
    public class ClearOneLevelControlBlockConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }
        [JsonProperty("DeviceType", Required = Required.Always)]
        public DeviceType DeviceType { get; set; }
        [JsonProperty("DeviceId", Required = Required.Always)] // allowed avlues are 0-B or *
        public char DeviceId { get; set; }
        [JsonProperty("Group", Required = Required.Always)]
        public Group Group { get; set; }
        [JsonProperty("Channel", Required = Required.Always)]
        public string Channel { get; set; }
    }

}