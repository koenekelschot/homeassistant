namespace HomeAssistant.Hub.Mqtt
{
    public class MqttClientConfig
    {
        public string Hostname { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; }
    }
}
