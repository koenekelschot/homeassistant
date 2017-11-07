using HomeAssistant.Hub.Models;
using Newtonsoft.Json;
using NLog;
using SimpleDI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.Webhooks
{
    public sealed class SonarrWebhookService : WebhookListener
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly SonarrWebhookConfig _settings;

        public override event MessageReceivedEventHandler MessageReceived;

        public SonarrWebhookService(WebhookService webhookService, IOptions<SonarrWebhookConfig> settings) : base(webhookService)
        {
            _settings = settings.Value;
        }

        public override async Task ProcessMessage(WebhookMessage message)
        {
            if (!message.UrlSegments.Any(segment => segment.Equals(_settings.Path, StringComparison.InvariantCultureIgnoreCase))) {
                return;
            }

            logger.Info("Got message for Sonarr");

            SonarrEvent parsedMessage = JsonConvert.DeserializeObject<SonarrEvent>(message.Data);
            //logger.Info(parsedMessage.EventType);

            MessageReceived?.Invoke(this, "Sonarr message goes here");
        }

        private class SonarrEvent
        {
            public string EventType { get; set; } //Grab, Download, Rename, Test
            public SonarrSeries Series { get; set; }
            public SonarrEpisode[] Episodes { get; set; }
            public SonarrRelease Release { get; set; }
            public bool IsUpgrade { get; set; }
        }

        private class SonarrSeries
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Path { get; set; }
            public int TvdbId { get; set; }
        }

        private class SonarrEpisode
        {
            public long Id { get; set; }
            public int EpisodeNumber { get; set; }
            public int SeasonNumber { get; set; }
            public string Title { get; set; }
            public DateTime? AirDate { get; set; }
            public DateTime? AirDateUtc { get; set; }
        }

        private class SonarrRelease
        {
            public string Quality { get; set; }
            public int QualityVersion { get; set; }
            public string ReleaseGroup { get; set; }
            public string ReleaseTitle { get; set; }
            public string Indexer { get; set; }
            public long Size { get; set; }
        }
    }
}
