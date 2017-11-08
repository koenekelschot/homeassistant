using HomeAssistant.Hub.Models;
using HomeWizard.Net;
using SimpleDI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.HomeWizard
{
    public sealed class HomeWizardService
    {
        private readonly HomeWizardClient _client;
        private readonly Dictionary<string, OnOff> _switchStateCache;

        public HomeWizardService(IOptions<HomeWizardConfig> config)
        {
            var settings = config.Value;
            _client = new HomeWizardClient();
            _client.Connect(settings.IpAddress, settings.Password);
            _switchStateCache = new Dictionary<string, OnOff>();
        }

        public async Task ToggleSwitch(SwitchStateMessage message)
        {
            OnOff? currentState = GetCurrentSwitchStateFromCache(message.DeviceId);
            OnOff newState = message.IsOn ? OnOff.On : OnOff.Off;

            if (newState != currentState)
            {
                await UpdateSwitchState(message);
            }
        }

        public async Task DimSwitch(DimLevelMessage message)
        {
            StoreSwitchStateInCache(message.DeviceId, message.Data == 0 ? OnOff.Off : OnOff.On);
            long switchId = long.Parse(message.DeviceId);
            await _client.DimSwitch(switchId, (int)message.Data);
        }

        public async Task AdjustTemperature(TemperatureMessage message)
        {
            await _client.SetTargetTemperature(0, message.Data);
        }

        public async Task<decimal?> GetRoomTemperature()
        {
            try
            {
                var response = await _client.GetSensors();
                var heatlink = response.HeatLinks.FirstOrDefault();
                return heatlink?.Rte;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task UpdateSwitchState(SwitchStateMessage message)
        {
            OnOff state = message.IsOn ? OnOff.On : OnOff.Off;
            StoreSwitchStateInCache(message.DeviceId, state);

            long switchId = long.Parse(message.DeviceId);
            if (state == OnOff.On)
            {
                await _client.SwitchOn(switchId);
            }
            else
            {
                await _client.SwitchOff(switchId);
            }
        }

        private void StoreSwitchStateInCache(string switchId, OnOff state)
        {
            if (_switchStateCache.ContainsKey(switchId))
            {
                _switchStateCache[switchId] = state;
            }
            else
            {
                _switchStateCache.Add(switchId, state);
            }
        }

        private OnOff? GetCurrentSwitchStateFromCache(string switchId)
        {
            if (_switchStateCache.ContainsKey(switchId))
            {
                return _switchStateCache[switchId];
            }
            return null;
        }

        private enum OnOff
        {
            On,
            Off
        }
    }
}
