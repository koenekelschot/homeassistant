using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

#pragma warning disable S3242 // Method parameters should be declared with base types
//IBluetoothLEDevice and IGattCharacteristic are internal, so disable warning
namespace HomeAssistant.Hub.Soma
{
    public class ShadesService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly List<DeviceHandle> _deviceList;
        private static readonly Object _deviceListLock = new Object();
        //private Timer _deviceReconnectTimer;
        //private readonly TimeSpan _deviceReconnectInterval = TimeSpan.FromHours(4);

        private static ShadesService _instance;
        public static ShadesService Instance => _instance ?? (_instance = new ShadesService());
        
        private ShadesService() {
            lock (_deviceListLock)
            {
                _deviceList = new List<DeviceHandle>();
            }
            //SetupReconnectTimer();
        }

        public async Task<uint?> GetBatteryLevel(string shadeName)
        {
            DeviceHandle handle = await FindDeviceHandleWithName(shadeName);
            if (handle == null)
            {
                return null;
            }
            try
            {
                uint batteryLevelValue = await handle.GetCharacteristicValue<uint>(Constants.ShadeBatteryCharacteristic);
                return NormalizeBatteryLevel(batteryLevelValue);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<uint?> GetPosition(string shadeName)
        {
            DeviceHandle handle = await FindDeviceHandleWithName(shadeName);
            if (handle == null)
            {
                return null;
            }
            try
            {
                return await handle.GetCharacteristicValue<uint>(Constants.ShadeMotorStateCharacteristic);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> Notify(string shadeName)
        {
            return await ExecuteAction(shadeName, Constants.NotifyCharacteristic, new byte[1]
            {
                Convert.ToByte("0", 16)
            });
        }

        public async Task<bool> SetPosition(string shadeName, uint position)
        {
            if (position < 0 || position > 100)
            {
                return false;
            }

            return await ExecuteAction(shadeName, Constants.ShadeMotorTargetCharacteristic, new byte[1]
            {
                Convert.ToByte(position.ToString("X"), 16)
            });
        }

        public async Task<bool> MoveUp(string shadeName)
        {
            return await ExecuteAction(shadeName, Constants.ShadeMotorControlCharacteristic, new byte[1]
            {
                Convert.ToByte("69", 16)
            });
        }

        public async Task<bool> MoveDown(string shadeName)
        {
            return await ExecuteAction(shadeName, Constants.ShadeMotorControlCharacteristic, new byte[1]
            {
                Convert.ToByte("96", 16)
            });
        }

        public async Task<bool> Stop(string shadeName)
        {
            return await ExecuteAction(shadeName, Constants.ShadeMotorControlCharacteristic, new byte[1]
            {
                Convert.ToByte("0", 16)
            });
        }

        private async Task<bool> ExecuteAction(string name, Guid characteristicId, byte[] actionValues)
        {
            DeviceHandle handle = await FindDeviceHandleWithName(name);
            logger.Debug($"Handle: {handle?.Name ?? "null"}");
            if (handle == null)
            {
                return false;
            }
            return await handle.SetCharacteristicValue(characteristicId, actionValues);
        }

        /*
        private void SetupReconnectTimer()
        {
            _deviceReconnectTimer = new Timer(async (object target) =>
            {
                await ReconnectDeviceHandles();
            }, null, _deviceReconnectInterval, _deviceReconnectInterval);
        }

        private async Task ReconnectDeviceHandles()
        {
			logger.Info("Reconnecting all devices.");
			string[] names;
            lock (_deviceListLock)
            {
                names = _deviceList.Select(dh => dh.Name).ToArray();
                _deviceList.Clear();
            }

            foreach(string name in names)
            {
                await FindDeviceHandleWithName(name);
            }
        }
        */

        private async Task<DeviceHandle> FindDeviceHandleWithName(string name)
        {
            DeviceHandle handle;
            lock (_deviceListLock)
            {
                handle = _deviceList.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            }

            if (handle == null)
            {
                logger.Debug($"Discovering device with name {name}.");
                
                string deviceSelector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(name);
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(deviceSelector);
                DeviceInformation deviceInfo = devices.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
                BluetoothLEDevice device = null;

                if (deviceInfo != null)
                {
                    device = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
                }

                if (device != null)
                {
                    handle = new DeviceHandle(name, device);

                    lock (_deviceListLock)
                    {
                        if (!_deviceList.Any(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            _deviceList.Add(handle);
                        }
                    }
                }
            }

            if (handle == null)
            {
                logger.Warn($"Could not discover device with name {name}. Has it been paired?");
            }

            return handle;
        }

        private static T ReadValueFromBuffer<T>(IBuffer buffer) where T : IConvertible
        {
            using (var reader = DataReader.FromBuffer(buffer))
            {
                byte[] result = new byte[buffer.Length];
                reader.ReadBytes(result);
                string resultString = Convert.ToString(result[0]);
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
                return (T)converter.ConvertFromInvariantString(resultString);
            }
        }

        private uint NormalizeBatteryLevel(uint reportedLevel)
        {
            //The Android app displays another battery level than is communicated by the device
            //This code is taken from decompiled sources
            return reportedLevel == 0 ? 1 : (uint)Math.Min(100f, reportedLevel * 1.333333f);
        }

        private class DeviceHandle
        {
            public readonly string Name;
            private readonly List<GattCharacteristic> Characteristics;
            private readonly BluetoothLEDevice Device;

            public DeviceHandle(string name, BluetoothLEDevice device)
            {
                Name = name;
                Device = device;
                Characteristics = GetCharacteristics();
            }
            
            public async Task<T> GetCharacteristicValue<T>(Guid characteristicId) where T : IConvertible
            {
                var characteristic = Characteristics.FirstOrDefault(c => c.Uuid.Equals(characteristicId));
                if (characteristic != null)
                {
                    return await ReadCharacteristicValue<T>(characteristic);
                }
                return default(T);
            }

            public async Task<bool> SetCharacteristicValue(Guid characteristicId, byte[] value)
            {
                var characteristic = Characteristics.FirstOrDefault(c => c.Uuid.Equals(characteristicId));
                logger.Debug($"Characteristic: {characteristic?.Uuid.ToString() ?? "null"}");
                if (characteristic != null)
                {
                    GattCommunicationStatus result = await characteristic.WriteValueAsync(value.AsBuffer(), GattWriteOption.WriteWithResponse);
                    return result == GattCommunicationStatus.Success;
                }
                return false;
            }

            private List<GattCharacteristic> GetCharacteristics()
            {
                var characteristics = new List<GattCharacteristic>();
                var getServicesResult = Device.GattServices;

                foreach (var service in getServicesResult)
                {
                    var getCharacteristicsResult = service.GetAllCharacteristics();
                    characteristics.AddRange(getCharacteristicsResult);
                }

                return characteristics;
            }

            private async Task<T> ReadCharacteristicValue<T>(GattCharacteristic characteristic) where T : IConvertible
            {
                GattReadResult readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (readResult.Status == GattCommunicationStatus.Success)
                {
                    return ReadValueFromBuffer<T>(readResult.Value);
                }
                else
                {
                    logger.Warn("Could not read characteristic value (unreachable)");
                }
                return default(T);
            }
        }
    }
}
#pragma warning restore S3242 // Method parameters should be declared with base types
