namespace HomeAssistant.Hub.Mqtt
{
    public class MqttTopicConfig
    {
        public string SubscribeSet { get; set; }
        public string PublishState { get; set; }
        public string SubscribeDim { get; set; }   
        public string PublishDim { get; set; }
        public string SubscribeTemp { get; set; }
        public string PublishTemp { get; set; }
        public string RoomTemp { get; set; }
        public string SubscribeShade { get; set; }  
        public string PublishShade { get; set; }
        public string ShadeBattery { get; set; }  
    }
}
