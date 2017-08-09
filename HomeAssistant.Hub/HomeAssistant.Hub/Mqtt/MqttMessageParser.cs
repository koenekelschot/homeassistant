using System;
using System.Linq;
using HomeAssistant.Hub.Models;

namespace HomeAssistant.Hub.Mqtt
{
    public static class MqttMessageParser
    {
        private static string[] _topicNameParts;
        private static string _payload;

        public static Message ParseMessage(string topic, string payload)
        {
            _topicNameParts = topic.Split('/');
            _payload = payload;

            if (_topicNameParts.Length <= 2 || string.IsNullOrWhiteSpace(_payload))
            {
                return null;
            }

            switch (GetMessageType())
            {
                case MessageType.SwitchState:
                    return ParseSwitchStateMessage();
                case MessageType.DimLevel:
                    return ParseDimLevelMessage();
                case MessageType.Temperature:
                    return ParseTemperatureMessage();
                case MessageType.Shade:
                    return ParseShadeMessage();
                default:
                    return null;
            }
        }

        private static MessageType GetMessageType()
        {
            switch (_topicNameParts[1])
            {
                case "sw":
                    return GetSwitchMessageType();
                case "temp":
                    return MessageType.Temperature;
                case "shade":
                    return MessageType.Shade;
                default:
                    return MessageType.Unknown;
            }
        }

        private static MessageType GetSwitchMessageType()
        {
            if (_topicNameParts.Length < 4)
            {
                return MessageType.Unknown;
            }
            switch (_topicNameParts[3])
            {
                case "set":
                    return MessageType.SwitchState;
                case "dim":
                    return MessageType.DimLevel;
                default:
                    return MessageType.Unknown;
            }
        }

        private static SwitchStateMessage ParseSwitchStateMessage()
        {
            var states = new[] { "on", "off" };
            string deviceId = GetSwitchId();

            if (string.IsNullOrWhiteSpace(deviceId) ||
                !states.Any(state => state.Equals(_payload, StringComparison.InvariantCultureIgnoreCase)))
            {
                return null;
            }

            return new SwitchStateMessage
            {
                DeviceId = deviceId,
                Data = _payload
            };
        }

        private static DimLevelMessage ParseDimLevelMessage()
        {
            string deviceId = GetSwitchId();

            if (string.IsNullOrWhiteSpace(deviceId) || !uint.TryParse(_payload, out uint dimLevel))
            {
                return null;
            }

            return new DimLevelMessage
            {
                DeviceId = deviceId,
                Data = dimLevel > 100 ? 100 : dimLevel
            };
        }

        private static TemperatureMessage ParseTemperatureMessage()
        {
            if (_topicNameParts.Length < 3 || !double.TryParse(_payload, out double temperature))
            {
                return null;
            }

            return new TemperatureMessage
            {
                Data = temperature < 5.0d ? 5.0d : temperature > 35.0d ? 35.0d : temperature,
                TemperatureType = TemperatureType.Target
            };
        }

        private static ShadeMessage ParseShadeMessage()
        {
			string deviceId = GetSwitchId();

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            return new ShadeMessage
            {
                DeviceId = deviceId,
                Data = _payload
            };
        }

        private static string GetSwitchId()
        {
            if (_topicNameParts.Length < 3)
            {
                return null;
            }
            return _topicNameParts[2];
        }
    }
}
