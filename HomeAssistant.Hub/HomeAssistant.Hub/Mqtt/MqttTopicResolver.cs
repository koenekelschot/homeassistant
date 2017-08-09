using System.Configuration;
using HomeAssistant.Hub.Models;

namespace HomeAssistant.Hub.Mqtt
{
    public static class MqttTopicResolver
    {
        private static Message _message;

        public static string ResolveTopicForMessage(Message message)
        {
            _message = message;
            return GetTopic();
        }

        private static string GetTopic()
        {
            switch (_message.Type)
            {
                case MessageType.SwitchState:
                    return GetSwitchTopic(ConfigurationManager.AppSettings["mqtt_publish_state"]);
                case MessageType.DimLevel:
                    return GetSwitchTopic(ConfigurationManager.AppSettings["mqtt_publish_dim"]);
                case MessageType.Temperature:
                    return GetTemperatureTopic();
                case MessageType.Shade:
                    return GetSwitchTopic(ConfigurationManager.AppSettings["mqtt_publish_shade"]);
                default:
                    return null;
            }
        }

        private static string GetSwitchTopic(string mainTopic)
        {
            return _message.DeviceId == null ? null : mainTopic.Replace("+", _message.DeviceId);
        }

        private static string GetTemperatureTopic()
        {
            switch (GetTemperatureType())
            {
                case TemperatureType.Target:
                    return ConfigurationManager.AppSettings["mqtt_publish_temp"];
                case TemperatureType.Room:
                    return ConfigurationManager.AppSettings["mqtt_room_temp"];
                default:
                    return null;
            }
        }

        private static TemperatureType GetTemperatureType()
        {
            TemperatureType type = TemperatureType.Unknown;
            if (_message is TemperatureMessage message)
            {
                type = message.TemperatureType;
            }
            return type;
        }
    }
}
