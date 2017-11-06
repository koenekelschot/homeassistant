using System.Text;
using HomeAssistant.Hub.Models;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using SimpleDI;

namespace HomeAssistant.Hub.Mqtt
{
    public sealed class MqttService
    {
        private readonly MqttTopicResolveService _topicResolver;
        private readonly MqttMessageParseService _messageParser;
        private readonly MqttClient _mqttClient;
        private readonly MqttClientConfig _settings;

        private static readonly Encoding MessageEncoding = Encoding.UTF8;
        private string[] _topics;

        public delegate void MessageReceivedEventHandler(object sender, Message message);
        public event MessageReceivedEventHandler MessageReceived;

        public MqttService(MqttTopicResolveService topicResolver, MqttMessageParseService messageParser, IOptions<MqttClientConfig> settings)
        {
            _topicResolver = topicResolver;
            _messageParser = messageParser;
            _settings = settings.Value;

            _mqttClient = new MqttClient(
                brokerHostName: _settings.Hostname,
                brokerPort: _settings.Port,
                secure: false,
                caCert: null,
                clientCert: null,
                sslProtocol: MqttSslProtocols.None)
            {
                ProtocolVersion = MqttProtocolVersion.Version_3_1_1
            };
        }

        public void Start(string[] topicList)
        {
            _mqttClient.MqttMsgPublishReceived += OnMqttMsgPublishReceived;
            _mqttClient.Connect(
                clientId: _settings.ClientId,
                username: _settings.Username,
                password: _settings.Password);

            _topics = topicList;
            Subscribe();
        }

        public void Stop()
        {
            _mqttClient.Unsubscribe(_topics);
            _mqttClient.Disconnect();
            _mqttClient.MqttMsgPublishReceived -= OnMqttMsgPublishReceived;
        }

        private void Subscribe()
        {
            if (_topics != null)
            {
                byte[] qosLevels = new byte[_topics.Length];
                for (var i = 0; i < _topics.Length; i++)
                {
                    qosLevels[i] = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE;
                }

                _mqttClient.Subscribe(_topics, qosLevels);
            }
        }

        public void PublishMessage(Message message)
        {
            string topic = _topicResolver.ResolveTopicForMessage(message);
            if (string.IsNullOrWhiteSpace(topic))
            {
                return;
            }

            byte[] payload = MessageEncoding.GetBytes(message.StringData);
            _mqttClient.Publish(topic, payload, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, true);
        }

        private void OnMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs args)
        {
            string message = MessageEncoding.GetString(args.Message);
            Message parsed = _messageParser.ParseMessage(args.Topic, message);

            if (parsed != null)
            {
                MessageReceived?.Invoke(this, parsed);
            }
        }
    }
}
