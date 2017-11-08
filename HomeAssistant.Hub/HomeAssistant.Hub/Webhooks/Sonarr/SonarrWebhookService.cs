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
            if (parsedMessage == null)
            {
                logger.Warn("Failed to parse message from Sonarr");
            }

            var eventType = parsedMessage.EventType.ToLower();
            if (eventType.Equals("grab") || eventType.Equals("download"))
            {
                string action = eventType.Equals("download")
                    ? parsedMessage.IsUpgrade ? "upgraded" : "downloaded"
                    : "grabbed";

                for (int i = 0; i < parsedMessage.Episodes.Length; i++)
                {
                    var episodeMessage = $"{action} {ProcessEpisode(parsedMessage, i)}".Trim();
                    episodeMessage = Char.ToUpper(episodeMessage[0]) + episodeMessage.Substring(1);

                    MessageReceived?.Invoke(this, new DownloadMessage { DeviceId = "Sonarr", Data = episodeMessage });
                }
            }
        }

        private string ProcessEpisode(SonarrEvent sonarrEvent, int episodeIndex)
        {
            string name = $"{sonarrEvent.Series.Title} {sonarrEvent.Episodes[episodeIndex].EpisodeString}";
            string quality = !string.IsNullOrEmpty(sonarrEvent.Episodes[episodeIndex].Quality)
                ? sonarrEvent.Episodes[episodeIndex].Quality
                : sonarrEvent.Release?.Quality;

            return string.IsNullOrEmpty(quality)
                ? name
                : $"{name} ({quality})";
        }

        private class SonarrEvent
        {
            public string EventType { get; set; } //Grab, Download, Rename, Test
            public SonarrSeries Series { get; set; }
            public SonarrEpisode[] Episodes { get; set; }
            public SonarrRelease Release { get; set; }
            public bool IsUpgrade { get; set; } //Download only
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
            public string Quality { get; set; }
            public int QualityVersion { get; set; }
            public string EpisodeString => $"S{SeasonNumber.ToString().PadLeft(2, '0')}E{EpisodeNumber.ToString().PadLeft(2, '0')}";
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
