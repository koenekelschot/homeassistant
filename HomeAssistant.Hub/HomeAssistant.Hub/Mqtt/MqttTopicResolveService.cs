using HomeAssistant.Hub.Models;
using SimpleDI;

namespace HomeAssistant.Hub.Mqtt
{
    public sealed class MqttTopicResolveService
    {
        private readonly MqttTopicConfig _settings;

        public MqttTopicResolveService(IOptions<MqttTopicConfig> settings)
        {
            _settings = settings.Value;
        }

        public string ResolveTopicForMessage(Message message)
        {
            switch (message.Type)
            {
                case MessageType.SwitchState:
                    return GetSwitchTopic(_settings.PublishState, message);
                case MessageType.DimLevel:
                    return GetSwitchTopic(_settings.PublishDim, message);
                case MessageType.Temperature:
                    return GetTemperatureTopic(message);
                case MessageType.Shade:
                    return GetSwitchTopic(_settings.PublishShade, message);
                case MessageType.Download:
                    return GetSwitchTopic(_settings.PublishDownload, message);
                default:
                    return null;
            }
        }

        private string GetSwitchTopic(string mainTopic, Message message)
        {
            return message.DeviceId == null ? null : mainTopic.Replace("+", message.DeviceId.ToLower());
        }

        private string GetTemperatureTopic(Message message)
        {
            switch (GetTemperatureType(message))
            {
                case TemperatureType.Target:
                    return _settings.PublishTemp;
                case TemperatureType.Room:
                    return _settings.RoomTemp;
                default:
                    return null;
            }
        }

        private TemperatureType GetTemperatureType(Message message)
        {
            TemperatureType type = TemperatureType.Unknown;
            if (message is TemperatureMessage)
            {
                type = ((TemperatureMessage)message).TemperatureType;
            }
            return type;
        }
    }
}
