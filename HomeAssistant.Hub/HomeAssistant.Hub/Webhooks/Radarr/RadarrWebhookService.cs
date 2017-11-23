using HomeAssistant.Hub.Models;
using Newtonsoft.Json;
using NLog;
using SimpleDI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.Webhooks
{
    public sealed class RadarrWebhookService : WebhookListener
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly RadarrWebhookConfig _settings;

        public override event MessageReceivedEventHandler MessageReceived;

        public RadarrWebhookService(WebhookService webhookService, IOptions<RadarrWebhookConfig> settings) : base(webhookService)
        {
            _settings = settings.Value;
        }

        public override async Task ProcessMessage(WebhookMessage message)
        {
            if (!message.UrlSegments.Any(segment => segment.Equals(_settings.Path, StringComparison.InvariantCultureIgnoreCase))) {
                return;
            }

            logger.Info("Got message for Radarr");

            RadarrEvent parsedMessage = JsonConvert.DeserializeObject<RadarrEvent>(message.Data);
            if (parsedMessage == null)
            {
                logger.Warn("Failed to parse message from Radarr");
            }

            var eventType = parsedMessage.EventType.ToLower();
            if (eventType.Equals("grab") || eventType.Equals("download"))
            {
                string action = eventType.Equals("download")
                    ? parsedMessage.IsUpgrade ? "upgraded" : "downloaded"
                    : "grabbed";

                var movie = parsedMessage.RemoteMovie.Title + " " + parsedMessage.RemoteMovie.Year;
                var data = action + " " + movie;

                MessageReceived?.Invoke(this, new DownloadMessage { DeviceId = "Radarr", Data = data });
            }
        }

        private class RadarrEvent
        {
            public string EventType { get; set; } //Grab, Download, Rename, Test
            public RadarrMovie Movie { get; set; }
            public RemoteMovie RemoteMovie { get; set; }
            public MovieFile MovieFile { get; set; } //Download only
            public RadarrRelease Release { get; set; }
            public bool IsUpgrade { get; set; } //Download only
        }

        private class RadarrMovie
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string ReleaseDate { get; set; } //YYYY-MM-DD
        }

        private class RemoteMovie
        {
            public long TmdbId { get; set; }
            public string ImdbId { get; set; }
            public string Title { get; set; }
            public int Year { get; set; }
        }

        private class RadarrRelease
        {
            public string Quality { get; set; }
            public int QualityVersion { get; set; }
            public string ReleaseGroup { get; set; }
            public string ReleaseTitle { get; set; }
            public string Indexer { get; set; }
            public long Size { get; set; }
        }

        private class MovieFile
        {
            public int Id { get; set; }
            public string RelativePath { get; set; }
            public string Path { get; set; }
            public string Quality { get; set; }
            public int QualityVersion { get; set; }
            public string ReleaseGroup { get; set; }
            public string SceneName { get; set; }
        }
    }
}
