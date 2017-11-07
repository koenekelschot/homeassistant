using HomeAssistant.Hub.Models;
using Newtonsoft.Json;
using NLog;
using SimpleDI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.Webhooks
{
    public sealed class CouchPotatoWebhookService : WebhookListener
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly CouchPotatoWebhookConfig _settings;

        public override event MessageReceivedEventHandler MessageReceived;

        public CouchPotatoWebhookService(WebhookService webhookService, IOptions<CouchPotatoWebhookConfig> settings) : base(webhookService)
        {
            _settings = settings.Value;
        }

        public override async Task ProcessMessage(WebhookMessage message)
        {
            if (!message.UrlSegments.Any(segment => segment.Equals(_settings.Path, StringComparison.InvariantCultureIgnoreCase))) {
                return;
            }

            logger.Info("Got message for CouchPotato");

            MessageReceived?.Invoke(this, message.Data);
        }
    }
}
