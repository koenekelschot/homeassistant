using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using HomeAssistant.Hub.HomeWizard.Models;
using HomeAssistant.Hub.Models;
using RestSharp;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.HomeWizard
{
    public sealed class HomeWizardClient
    {
        private static HomeWizardClient _instance;
        private static RestClient _restClient;

        private static readonly IDictionary SwitchStateCache = new Dictionary<string, string>();

        public static HomeWizardClient Instance => _instance ?? (_instance = new HomeWizardClient());

        private HomeWizardClient()
        {
            InitializeRestClient();
        }
        
        private static void InitializeRestClient()
        {
            var baseUrl =
                $"{ConfigurationManager.AppSettings["hw_hostname"]}/{ConfigurationManager.AppSettings["hw_password"]}";
            _restClient = new RestClient(baseUrl);
        }

        public async Task Update(Message message)
        {
            string resource = HomeWizardResourceResolver.ResolveResourceForMessage(message);
            if (resource == null || !ShouldUpdate(message))
            {
                return;
            }

            UpdateSwitchState(message);
            await _restClient.ExecuteTaskAsync(new RestRequest(resource));
        }

        public async Task<double?> GetRoomTemperature()
        {
            var response = await _restClient.ExecuteTaskAsync<ApiResponse<Sensors>>(new RestRequest("get-sensors"));
            try
            {
                var heatlink = response.Data.Response.HeatLinks.FirstOrDefault();
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

        private static void UpdateSwitchState(Message message)
        {
            if (message.Type != MessageType.SwitchState || string.IsNullOrWhiteSpace(message.DeviceId))
            {
                return;
            }

            StoreSwitchStateInCache(message.DeviceId, message.StringData);
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
