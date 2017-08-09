using System.Configuration;
using System.Text;
using HomeAssistant.Hub.Models;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace HomeAssistant.Hub.Mqtt
{
    public sealed class MqttPubSubClient
    {
        private static MqttPubSubClient _instance;
        private MqttClient mqttClient;

        private static readonly Encoding MessageEncoding = Encoding.UTF8;

        public static MqttPubSubClient Instance => _instance ?? (_instance = new MqttPubSubClient());

        public delegate void MessageReceivedEventHandler(object sender, Message message);
        public event MessageReceivedEventHandler MessageReceived;

        private MqttPubSubClient()
        {
            InitializeMqttClient();
        }

        private void InitializeMqttClient()
        {
            mqttClient = new MqttClient(
                brokerHostName: ConfigurationManager.AppSettings["mqtt_hostname"],
                brokerPort: int.Parse(ConfigurationManager.AppSettings["mqtt_port"]),
                secure: false,
                caCert: null,
                clientCert: null,
                sslProtocol: MqttSslProtocols.None)
            {
                ProtocolVersion = MqttProtocolVersion.Version_3_1_1
            };

            mqttClient.MqttMsgPublishReceived += OnMqttMsgPublishReceived;

            mqttClient.Connect(
                clientId: ConfigurationManager.AppSettings["mqtt_clientId"],
                username: ConfigurationManager.AppSettings["mqtt_username"],
                password: ConfigurationManager.AppSettings["mqtt_password"]);
        }

        public void Subscribe(string[] topicList)
        {
            byte[] qosLevels = new byte[topicList.Length];
            for (var i = 0; i < topicList.Length; i++)
            {
                qosLevels[i] = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE;
            }

            mqttClient.Subscribe(topicList, qosLevels);
        }

        public void PublishMessage(Message message)
        {
            string topic = MqttTopicResolver.ResolveTopicForMessage(message);
            if (string.IsNullOrWhiteSpace(topic))
            {
                return;
            }

            byte[] payload = MessageEncoding.GetBytes(message.StringData);
            mqttClient.Publish(topic, payload, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, true);
        }

        private void OnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs args)
        {
            string message = MessageEncoding.GetString(args.Message);
            Message parsed = MqttMessageParser.ParseMessage(args.Topic, message);

            if (parsed != null)
            {
                MessageReceived?.Invoke(this, parsed);
            }
        }
    }
}
