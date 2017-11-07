using HomeAssistant.Hub.Models;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.Webhooks
{
    public abstract class WebhookListener
    {
        public delegate void MessageReceivedEventHandler(object sender, string message);
        public abstract event MessageReceivedEventHandler MessageReceived;

        public abstract Task ProcessMessage(WebhookMessage message);

        private readonly WebhookService _webhookService;

        public WebhookListener(WebhookService webhookService)
        {
            _webhookService = webhookService;
            _webhookService.MessageReceived += async (object sender, WebhookMessage message) =>
            {
                await ProcessMessage(message);
            };
        }
    }
}
