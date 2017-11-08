using System;
using System.Globalization;

namespace HomeAssistant.Hub.Models
{
    public abstract class Message
    {
        public virtual MessageType Type => MessageType.Unknown;
        public string DeviceId { get; set; }
        public abstract string StringData { get; }
    }

    public class SwitchStateMessage : Message
    {
        public override MessageType Type => MessageType.SwitchState;
        public string Data { get; set; }
        public override string StringData => Data;
    }

    public class DimLevelMessage : Message
    {
        public override MessageType Type => MessageType.DimLevel;
        public uint Data { get; set; }
        public override string StringData => Convert.ToString(Data, CultureInfo.InvariantCulture);
    }

    public class TemperatureMessage : Message
    {
        public override MessageType Type => MessageType.Temperature;
        public TemperatureType TemperatureType { get; set; }
        public decimal Data { get; set; }
        public override string StringData => Convert.ToString(Data, CultureInfo.InvariantCulture);
    }

    public class ShadeMessage : Message
    {
        public override MessageType Type => MessageType.Shade;
        public string Data { get; set; }
        public override string StringData => Data;
    }

    public class WebhookMessage : Message
    {
        public override MessageType Type => MessageType.Webhook;
        public string[] UrlSegments { get; set; }
        public string Method { get; set; }
        public string Data { get; set; }
        public override string StringData => Data;
    }

    public class DownloadMessage : Message
    {
        public override MessageType Type => MessageType.Download;
        public string Data { get; set; }
        public override string StringData => Data;
    }
}
