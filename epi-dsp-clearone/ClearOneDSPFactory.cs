using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace ClearOneDSP
{
	/// <summary>
	/// Plugin device factory for CrestronConnected/RoomView devices
	/// </summary>
    public class ClearOneDSPFactory : EssentialsPluginDeviceFactory<ClearOneDSPDevice>
    {
		/// <summary>
		/// CCDisplay EPI Plugin device factory constructor
		/// </summary>
        public ClearOneDSPFactory()
        {
            // Set the minimum Essentials Framework Version
            MinimumEssentialsFrameworkVersion = "1.6.9";

            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "clearonedsp" };
        }
        
		/// <summary>
		/// Builds and returns an instance of CCDisplayDevice
		/// </summary>
		/// <param name="dc">device configuration</param>
		/// <returns>plugin device or null</returns>
		/// <remarks>		
		/// The example provided below takes the device key, name, properties config and the comms device created.
		/// Modify the EssetnialsPlugingDeviceTemplate constructor as needed to meet the requirements of the plugin device.
		/// </remarks>
		/// <seealso cref="PepperDash.Core.eControlMethod"/>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "[{0}] Factory Attempting to create new device from type: {1}", dc.Key, dc.Type);

            // get the plugin device properties configuration object & check for null 
            var propertiesConfig = dc.Properties.ToObject<ClearOneDSPConfig>();
            if (propertiesConfig == null)
            {
                Debug.Console(0, "[{0}] Factory: failed to read properties config for {1}", dc.Key, dc.Name);
                return null;
            }
            var comm = CommFactory.CreateCommForDevice(dc);

            return new ClearOneDSPDevice(dc.Key, dc.Name, propertiesConfig, comm);
        }
    }
}

          