using NLog;
using SimpleDI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

#pragma warning disable S3242 // Method parameters should be declared with base types
//IBluetoothLEDevice and IGattCharacteristic are internal, so disable warning
namespace HomeAssistant.Hub.Soma
{
    public sealed class ShadesService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ShadesConfig _settings;
        private readonly List<DeviceHandle> _deviceList;
        private static readonly Object _deviceListLock = new Object();

        public ShadesService(IOptions<ShadesConfig> settings) {
            _settings = settings.Value;

            ConfigureShades();

            lock (_deviceListLock)
            {
                _deviceList = new List<DeviceHandle>();
            }
        }

        public IEnumerable<Shade> GetConfiguredShades()
        {
            return _settings.Shades;
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
                uint batteryLevelValue = await handle.GetCharacteristicValue<uint>(Constants.ShadeBatteryCharacteristic, 0);
                return NormalizeBatteryLevel(batteryLevelValue);
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't get battery level");
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
                var position = await handle.GetCharacteristicValue<uint>(Constants.ShadeMotorStateCharacteristic, 0);
                return DescalePosition(shadeName, position);
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't get position");
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
            logger.Debug($"Setting position for {shadeName} to {position}");
            if (position < 0 || position > 100)
            {
                return false;
            }

            position = ScalePosition(shadeName, position);
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
            if (handle == null)
            {
                return false;
            }
            return await handle.SetCharacteristicValue(characteristicId, actionValues);
        }

        private void ConfigureShades()
        {
            for (var i = 0; i < _settings.Shades.Length; i++)
            {
                var limitUp = Math.Max(0, _settings.Shades[i].PositionUp);
                var limitDown = Math.Min(100, _settings.Shades[i].PositionDown);
                _settings.Shades[i].PositionUp = Math.Min(limitUp, limitDown);
                _settings.Shades[i].PositionDown = Math.Max(limitUp, limitDown);
                logger.Debug($"Configured {_settings.Shades[i].Name}: [{_settings.Shades[i].PositionUp}-{_settings.Shades[i].PositionDown}]");
            }
        }

        //Convert position to take upper and lower limits into account
        //Should be used internally
        private uint ScalePosition(string shadeName, uint normalPosition)
        {
            logger.Debug($"External position {shadeName}: {normalPosition}");
            Shade shade = GetShade(shadeName);
            if (shade == null)
            {
                return normalPosition;
            }

            uint scaledPosition = (uint)((shade.Range / 100.0) * normalPosition);
            scaledPosition += shade.PositionUp;
            logger.Debug($"Internal position {shadeName}: {scaledPosition}");
            return scaledPosition;
        }

        //Convert position to ignore upper and lower limits
        //Should be used to communicate position to outside world
        private uint DescalePosition(string shadeName, uint scaledPosition)
        {
            logger.Debug($"Internal position {shadeName}: {scaledPosition}");
            Shade shade = GetShade(shadeName);
            if (shade == null)
            {
                return scaledPosition;
            }

            if (shade.Range == 0)
            {
                return shade.PositionDown;
            }

            uint normalPosition = scaledPosition - shade.PositionUp;
            normalPosition = (uint)(normalPosition * (100.0 / shade.Range));
            logger.Debug($"External position {shadeName}: {normalPosition}");
            return normalPosition;
        }

        private Shade GetShade(string shadeName)
        {
            return _settings.Shades.FirstOrDefault(shade => shade.Name.Equals(shadeName, StringComparison.InvariantCultureIgnoreCase));
        }

        private async Task<DeviceHandle> FindDeviceHandleWithName(string name)
        {
            if (GetShade(name) == null)
            {
                logger.Error($"Device with name {name} hass not been configured.");
                return null;
            }

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
                    logger.Debug($"Found device with name {name}.");
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
                logger.Debug($"Read value: {resultString}");
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
            private readonly BluetoothLEDevice Device;

            public DeviceHandle(string name, BluetoothLEDevice device)
            {
                Name = name;
                Device = device;
            }
            
            public async Task<T> GetCharacteristicValue<T>(Guid characteristicId, int retry) where T : IConvertible
            {
                try
                {
                    logger.Debug($"Reading characteristic: {characteristicId}");
                    foreach (var service in Device.GattServices)
                    {
                        var characteristic = service.GetCharacteristics(characteristicId).FirstOrDefault();
                        if (characteristic != null)
                        {
                            return await ReadCharacteristicValue<T>(characteristic);
                        }
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is ObjectDisposedException)
                {
                    if (retry < 4)
                    {
                        retry++;
                        await Task.Delay(2000);
                        return await GetCharacteristicValue<T>(characteristicId, retry);
                    }
                }
                return default(T);
            }

            public async Task<bool> SetCharacteristicValue(Guid characteristicId, byte[] value)
            {
                foreach (var service in Device.GattServices)
                {
                    var characteristic = service.GetCharacteristics(characteristicId).FirstOrDefault();
                    if (characteristic != null)
                    {
                        GattCommunicationStatus result = await characteristic.WriteValueAsync(value.AsBuffer(), GattWriteOption.WriteWithResponse);
                        return result == GattCommunicationStatus.Success;
                    }
                }
                return false;
            }

            private async Task<T> ReadCharacteristicValue<T>(GattCharacteristic characteristic) where T : IConvertible
            {
                GattReadResult readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (readResult.Status == GattCommunicationStatus.Success)
                {
                    return ReadValueFromBuffer<T>(readResult.Value);
                }

                logger.Warn("Could not read characteristic value (unreachable)");
                return default(T);
            }
        }
    }
}
#pragma warning restore S3242 // Method parameters should be declared with base types
