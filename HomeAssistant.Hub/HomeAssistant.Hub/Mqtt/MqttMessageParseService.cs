using System;
using System.Linq;
using HomeAssistant.Hub.Models;

namespace HomeAssistant.Hub.Mqtt
{
    public sealed class MqttMessageParseService
    {
        public Message ParseMessage(string topic, string payload)
        {
            var topicNameParts = topic.Split('/');

            if (topicNameParts.Length <= 2 || string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            switch (GetMessageType(topicNameParts))
            {
                case MessageType.SwitchState:
                    return ParseSwitchStateMessage(topicNameParts, payload);
                case MessageType.DimLevel:
                    return ParseDimLevelMessage(topicNameParts, payload);
                case MessageType.Temperature:
                    return ParseTemperatureMessage(topicNameParts, payload);
                case MessageType.Shade:
                    return ParseShadeMessage(topicNameParts, payload);
                default:
                    return null;
            }
        }

        private MessageType GetMessageType(string[] topicNameParts)
        {
            switch (topicNameParts[1])
            {
                case "sw":
                    return GetSwitchMessageType(topicNameParts);
                case "temp":
                    return MessageType.Temperature;
                case "shade":
                    return MessageType.Shade;
                default:
                    return MessageType.Unknown;
            }
        }

        private MessageType GetSwitchMessageType(string[] topicNameParts)
        {
            if (topicNameParts.Length < 4)
            {
                return MessageType.Unknown;
            }
            switch (topicNameParts[3])
            {
                case "set":
                    return MessageType.SwitchState;
                case "dim":
                    return MessageType.DimLevel;
                default:
                    return MessageType.Unknown;
            }
        }

        private SwitchStateMessage ParseSwitchStateMessage(string[] topicNameParts, string payload)
        {
            var states = new[] { "on", "off" };
            string deviceId = GetSwitchId(topicNameParts);

            if (string.IsNullOrWhiteSpace(deviceId) ||
                !states.Any(state => state.Equals(payload, StringComparison.InvariantCultureIgnoreCase)))
            {
                return null;
            }

            return new SwitchStateMessage
            {
                DeviceId = deviceId,
                Data = payload
            };
        }

        private DimLevelMessage ParseDimLevelMessage(string[] topicNameParts, string payload)
        {
            string deviceId = GetSwitchId(topicNameParts);

            if (string.IsNullOrWhiteSpace(deviceId) || !uint.TryParse(payload, out uint dimLevel))
            {
                return null;
            }

            return new DimLevelMessage
            {
                DeviceId = deviceId,
                Data = dimLevel > 100 ? 100 : dimLevel
            };
        }

        private TemperatureMessage ParseTemperatureMessage(string[] topicNameParts, string payload)
        {
            if (topicNameParts.Length < 3 || !decimal.TryParse(payload, out decimal temperature))
            {
                return null;
            }

            return new TemperatureMessage
            {
                Data = temperature < 5.0m ? 5.0m : temperature > 35.0m ? 35.0m : temperature,
                TemperatureType = TemperatureType.Target
            };
        }

        private ShadeMessage ParseShadeMessage(string[] topicNameParts, string payload)
        {
			string deviceId = GetSwitchId(topicNameParts);

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            return new ShadeMessage
            {
                DeviceId = deviceId,
                Data = payload
            };
        }

        private string GetSwitchId(string[] topicNameParts)
        {
            if (topicNameParts.Length < 3)
            {
                return null;
            }
            return topicNameParts[2];
        }
    }
}
