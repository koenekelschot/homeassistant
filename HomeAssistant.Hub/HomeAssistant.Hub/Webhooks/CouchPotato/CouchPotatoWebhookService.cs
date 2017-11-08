using HomeAssistant.Hub.Models;
using NLog;
using SimpleDI;
using System;
using System.Linq;
using System.Net;
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

            logger.Info($"Got message for CouchPotato");
            //message=Snatched+%22{releaseName}+%28{year}%29++{group}++{quality}++{encoding}++{seeders}+seeders%22%3A+{movieTitle}+%28{year}%29+in+{quality}+from+{indexer}&imdb_id={imdbId} | 
            var postData = message.Data.Split('&');
            foreach (string dataItem in postData)
            {
                var splitted = dataItem.Split('=');
                if (splitted[0].Equals("message", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Snatched "{releaseName} ({year})  {group}  {quality}  {encoding}  {seeders} seeders": {movieTitle} ({year}) in {quality} from {indexer}
                    //Downloaded {movieTitle} ({quality})
                    var value = WebUtility.UrlDecode(splitted[1]);
                    if (value.StartsWith("downloaded", StringComparison.InvariantCultureIgnoreCase))
                    {
                        MessageReceived?.Invoke(this, new DownloadMessage { DeviceId = "CouchPotato", Data = value });
                    }
                    if (value.StartsWith("snatched", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var movieInfo = value.Split(':')[1];
                        movieInfo = movieInfo.Substring(0, movieInfo.LastIndexOf("from"));
                        movieInfo = movieInfo.Replace("(0)", string.Empty);

                        MessageReceived?.Invoke(this, new DownloadMessage { DeviceId = "CouchPotato", Data = $"Snatched {movieInfo}".Trim() });
                    }
                }
            }
        }
    }
}
