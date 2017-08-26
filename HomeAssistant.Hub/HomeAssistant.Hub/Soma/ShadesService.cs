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
namespace HomeAssistant.Hub.Soma
{
    public class ShadesService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly List<DeviceHandle> _deviceList;
        private static readonly Object _deviceListLock = new Object();
        //private Timer _deviceReconnectTimer;
        //private readonly TimeSpan _deviceReconnectInterval = TimeSpan.FromHours(4);
        /*
        private readonly IDictionary<Guid, IList<DeviceHandle>> _subscriptions = new Dictionary<Guid, IList<DeviceHandle>>();
        private static readonly Object _subscriptionsLock = new Object();
        */
        private static ShadesService _instance;

        public static ShadesService Instance => _instance ?? (_instance = new ShadesService());
        /*
        public delegate void BatteryLevelChangedEventHandler(object sender, string shadeName, uint value);
        public delegate void PositionChangedEventHandler(object sender, string shadeName, uint value);
        public event BatteryLevelChangedEventHandler BatteryLevelChanged;
        public event PositionChangedEventHandler PositionChanged;
        */

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

        /*
        public async Task SubscribeToBatteryLevel(string shadeName)
        {
            await SubscribeDeviceHandle(shadeName, Constants.ShadeBatteryService, Constants.ShadeBatteryCharacteristic);
        }

        public async Task SubscribeToPosition(string shadeName)
        {
            logger.Debug($"SubscribeToPosition({shadeName})");
            await SubscribeDeviceHandle(shadeName, Constants.ShadeMotorService, Constants.ShadeMotorStateCharacteristic);
        }
        */

        private async Task<bool> ExecuteAction(string name, Guid characteristicId, byte[] actionValues)
        {
            DeviceHandle handle = await FindDeviceHandleWithName(name);
            if (handle == null)
            {
                return false;
            }
            return await handle.SetCharacteristicValue(characteristicId, actionValues);
        }

        /*
        private async Task SubscribeDeviceHandle(string shadeName, Guid serviceId, Guid characteristicId)
        {
            logger.Debug($"SubscribeDeviceHandle({shadeName}, {serviceId}, {characteristicId})");
            DeviceHandle handle = await FindDeviceHandleWithName(shadeName);
            if (handle == null)
            {
                return;
            }

            SaveCharacteristicSubscription(handle, characteristicId);

            await handle.SubscribeToCharacteristic(serviceId, characteristicId);
        }
        */

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

        /*
        private void SaveCharacteristicSubscription(DeviceHandle handle, Guid characteristicId)
        {
            logger.Debug($"SaveCharacteristicSubscription({handle}, {characteristicId})");
            lock (_subscriptionsLock)
            {
                bool alreadySubscribedHandler = false;
                foreach (var key in _subscriptions.Keys)
                {
                    alreadySubscribedHandler |= _subscriptions[key].Any(dh => handle.Name.Equals(dh.Name, StringComparison.InvariantCultureIgnoreCase));
                }

                if (!alreadySubscribedHandler)
                {
                    logger.Debug("Subscribed to handler");
                    handle.CharacteristicValueChanged += Handle_CharacteristicValueChanged;
                }

                if (!_subscriptions.ContainsKey(characteristicId))
                {
                    _subscriptions.Add(characteristicId, new List<DeviceHandle>());
                }

                if (!_subscriptions[characteristicId].Any(dh => handle.Name.Equals(dh.Name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    logger.Debug("Saved handler subscription");
                    _subscriptions[characteristicId].Add(handle);
                }
            }
        }

        private void Handle_CharacteristicValueChanged(DeviceHandle sender, Guid characteristicId, IBuffer buffer)
        {
            logger.Debug("handle.CharacteristicValueChanged");
            lock (_subscriptionsLock)
            {
                if (_subscriptions.ContainsKey(characteristicId) &&
                    _subscriptions[characteristicId].Any(dh => sender.Name.Equals(dh.Name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (characteristicId.Equals(Constants.ShadeBatteryCharacteristic))
                    {
                        uint batteryLevelValue = ReadValueFromBuffer<uint>(buffer);
                        BatteryLevelChanged?.Invoke(this, sender.Name, NormalizeBatteryLevel(batteryLevelValue));
                    }
                    if (characteristicId.Equals(Constants.ShadeMotorStateCharacteristic))
                    {
                        PositionChanged?.Invoke(this, sender.Name, ReadValueFromBuffer<uint>(buffer));
                    }
                }
            }
        }
        */

        private async Task<DeviceHandle> FindDeviceHandleWithName(string name)
        {
            DeviceHandle handle;
            lock (_deviceListLock)
            {
                handle = _deviceList.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
                if (handle != null)
                {
                    logger.Debug($"Got device from cache.");
                }
            }

            if (handle == null)
            {
                logger.Debug($"Discovering device with name {name}.");
                
                string deviceSelector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(name);
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(deviceSelector);
                logger.Debug($"Found {devices.Count} devices");
                DeviceInformation deviceInfo = devices.FirstOrDefault(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
                BluetoothLEDevice device = null;

                if (deviceInfo != null)
                {
                    device = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
                }

                if (device != null)
                {
                    handle = new DeviceHandle
                    {
                        Name = name,
                        Characteristics = GetCharacteristicsForDevice(device)
                    };

                    lock (_deviceListLock)
                    {
                        if (!_deviceList.Any(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            _deviceList.Add(handle);
                            logger.Debug($"Added device to list");
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

        private List<GattCharacteristic> GetCharacteristicsForDevice(BluetoothLEDevice device)
        {
            logger.Debug("Getting characteristics");
            List<GattCharacteristic> characteristics = new List<GattCharacteristic>();
            var getServicesResult = device.GattServices;
            
            foreach (var service in getServicesResult)
            {
                var getCharacteristicsResult = service.GetAllCharacteristics();
                characteristics.AddRange(getCharacteristicsResult);
            }

            logger.Debug($"Got {characteristics.Count} characteristics");
            return characteristics;
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
            public string Name { get; set; }
            public List<GattCharacteristic> Characteristics { set; get; } = new List<GattCharacteristic>();

            /*
            private readonly IList<GattCharacteristic> _characteristicSubscriptions = new List<GattCharacteristic>();
            private static readonly Object _characteristicSubscriptionsLock = new Object();

            public delegate void ValueChangedEventHandler(DeviceHandle sender, Guid characteristicId, IBuffer buffer);
            public event ValueChangedEventHandler CharacteristicValueChanged;
            */

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
                if (characteristic != null)
                {
                    GattCommunicationStatus result = await characteristic.WriteValueAsync(value.AsBuffer(), GattWriteOption.WriteWithResponse);
                    return result == GattCommunicationStatus.Success;
                }
                return false;
            }

            /*
            public async Task SubscribeToCharacteristic(Guid serviceId, Guid characteristicId)
            {
                logger.Debug($"SubscribeToCharacteristic({serviceId}, {characteristicId})");
                lock (_characteristicSubscriptionsLock)
                {
                    if (_characteristicSubscriptions.Select(c => c.Uuid).Any(uuid => uuid.Equals(characteristicId)))
                    {
                        return;
                    }
                }

                logger.Debug("Not yet in _characteristicSubscriptions");
                GattCharacteristic characteristic = GetCharacteristic(serviceId, characteristicId);
                await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                lock (_characteristicSubscriptionsLock)
                {
                    if (!_characteristicSubscriptions.Select(c => c.Uuid).Any(uuid => uuid.Equals(characteristicId)))
                    {
                        _characteristicSubscriptions.Add(characteristic);
                        logger.Debug("Subscribed to characteristic");
                        characteristic.ValueChanged += Characteristic_ValueChanged;
                    }
                }
            }

            private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
            {
                logger.Debug("characteristic.ValueChanged");
                CharacteristicValueChanged?.Invoke(this, sender.Uuid, args.CharacteristicValue);
            }
            */

            private async Task<T> ReadCharacteristicValue<T>(GattCharacteristic characteristic) where T : IConvertible
            {
                GattReadResult readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (readResult.Status == GattCommunicationStatus.Success)
                {
                    return ReadValueFromBuffer<T>(readResult.Value);
                }
                return default(T);
            }
        }
    }
}
#pragma warning restore S3242 // Method parameters should be declared with base types
