using HomeAssistant.Hub.Models;
using HomeWizard.Net;
using SimpleDI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.HomeWizard
{
    public sealed class HomeWizardService
    {
        private static readonly IDictionary SwitchStateCache = new Dictionary<string, string>();

        private readonly HomeWizardClient _client;

        public HomeWizardService(IOptions<HomeWizardConfig> config)
        {
            var settings = config.Value;
            _client = new HomeWizardClient();
            _client.Connect(settings.IpAddress, settings.Password);
        }

        public async Task ToggleSwitch(SwitchStateMessage message)
        {
            if (!ShouldUpdate(message))
            {
                return;
            }

            await UpdateSwitchState(message);
        }

        public async Task DimSwitch(DimLevelMessage message)
        {
            throw new NotImplementedException("Dimming currently not supported");
        }

        public async Task AdjustTemperature(TemperatureMessage message)
        {
            await _client.SetTargetTemperature(0, message.Data);
        }

        public async Task<decimal?> GetRoomTemperature()
        {
            var response = await _client.GetSensors();
            try
            {
                var heatlink = response.HeatLinks.FirstOrDefault();
                return heatlink?.Rte;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool ShouldUpdate(Message message)
        {
            if (message.Type != MessageType.SwitchState)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(message.DeviceId) &&
                   !message.StringData.Equals(GetCurrentSwitchStateFromCache(message.DeviceId),
                       StringComparison.InvariantCultureIgnoreCase);
        }

        private async Task UpdateSwitchState(Message message)
        {
            if (message.Type != MessageType.SwitchState || string.IsNullOrWhiteSpace(message.DeviceId))
            {
                return;
            }

            StoreSwitchStateInCache(message.DeviceId, message.StringData);

            long switchId = long.Parse(message.DeviceId);
            if (message.StringData.Equals("on", StringComparison.InvariantCultureIgnoreCase))
            {
                await _client.SwitchOn(switchId);
            }
            else
            {
                await _client.SwitchOff(switchId);
            }
        }

        private static void StoreSwitchStateInCache(string switchId, string state)
        {
            if (SwitchStateCache.Contains(switchId))
            {
                SwitchStateCache[switchId] = state;
            }
            else
            {
                SwitchStateCache.Add(switchId, state);
            }
        }

        private static string GetCurrentSwitchStateFromCache(string switchId)
        {
            if (SwitchStateCache.Contains(switchId))
            {
                return SwitchStateCache[switchId] as string;
            }
            return null;
        }
    }
}
