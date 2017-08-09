using System.Globalization;
using HomeAssistant.Hub.Models;

namespace HomeAssistant.Hub.HomeWizard
{
    public static class HomeWizardResourceResolver
    {
        private static Message _message;
        private static string _resourceData;

        public static string ResolveResourceForMessage(Message message)
        {
            _message = message;
            _resourceData = ParseData();

            return (_resourceData == null) ? null : GetResource();
        }

        private static string GetResource()
        {
            switch (_message.Type)
            {
                case MessageType.SwitchState:
                    return GetSwitchStateResource();
                case MessageType.DimLevel:
                    return GetDimLevelResource();
                case MessageType.Temperature:
                    return GetTemperatureResource();
                default:
                    return null;
            }
        }
        
        private static string GetSwitchStateResource()
        {
            return _message.DeviceId == null ? null : $"sw/{_message.DeviceId}/{_resourceData}";
        }

        private static string GetDimLevelResource()
        {
            return _message.DeviceId == null ? null : $"sw/dim/{_message.DeviceId}/{_resourceData}";
        }

        private static string GetTemperatureResource()
        {
            return $"hl/0/settarget/{_resourceData}";
        }

        private static string ParseData()
        {
            return _message?.StringData?.ToLower(CultureInfo.InvariantCulture);
        }
    }
}
