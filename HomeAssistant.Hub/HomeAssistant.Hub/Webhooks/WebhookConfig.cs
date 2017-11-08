namespace HomeAssistant.Hub.Webhooks
{
    public class WebhookConfig
    {
        public int LocalPort { get; set; }
    }

    public class WebhookListenerConfig
    {
        public string Path { get; set; }
    }

    public class SonarrWebhookConfig : WebhookListenerConfig { }

    public class CouchPotatoWebhookConfig : WebhookListenerConfig { }
}
