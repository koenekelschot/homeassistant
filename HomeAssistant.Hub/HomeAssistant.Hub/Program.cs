using HomeAssistant.Hub.Dsmr;
using HomeAssistant.Hub.HomeWizard;
using HomeAssistant.Hub.Models;
using HomeAssistant.Hub.Mqtt;
using HomeAssistant.Hub.Soma;
using HomeAssistant.Hub.Webhooks;
using NLog;
using SimpleDI;
using SimpleDI.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistant.Hub
{
    static class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Timer _temperatureTimer;
        private static decimal? _targetTemperature;
        private static SimpleDI.IServiceProvider ServiceProvider;
        private static IConfigurationRoot Configuration;
        private static MqttService _mqttService;
        private static SonarrWebhookService _sonarrWebhookService;
        private static CouchPotatoWebhookService _couchPotatoWebhookService;

        static void Main(string[] args)
        {
            try
            {
                logger.Info("Starting HomeAssistant.Hub");
                ConfigureEnv();
                Startup();
                logger.Info("HomeAssistant.Hub started");

                Console.WriteLine("Press [enter] to exit.");
                Console.ReadLine();

                logger.Info("Shutting down HomeAssistant.Hub");
                Shutdown();
                logger.Info("HomeAssistant.Hub shutdown");
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                logger.Error(e);
            }
            return;
        }

        private static void ConfigureEnv()
        {
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("Appsettings.json");
            Configuration = configBuilder.Build();

            IServiceCollection services = new ServiceCollection()
                .AddSingleton<MqttService>()
                .AddSingleton<MqttTopicResolveService>()
                .AddSingleton<MqttMessageParseService>()
                .AddSingleton<HomeWizardService>()
                .AddSingleton<DsmrService>()
                .AddSingleton<ShadesService>()
                .AddSingleton<WebhookService>()
                .AddSingleton<SonarrWebhookService>()
                .AddSingleton<CouchPotatoWebhookService>()
                .Configure(Configuration.GetSection<MqttClientConfig>("Mqtt:Client"))
                .Configure(Configuration.GetSection<MqttTopicConfig>("Mqtt:Topics"))
                .Configure(Configuration.GetSection<HomeWizardConfig>("HomeWizard"))
                .Configure(Configuration.GetSection<DsmrConfig>("Dsmr"))
                .Configure(Configuration.GetSection<ShadesConfig>("Soma"))
                .Configure(Configuration.GetSection<WebhookConfig>("Webhooks"))
                .Configure(Configuration.GetSection<SonarrWebhookConfig>("Webhooks:Sonarr"))
                .Configure(Configuration.GetSection<CouchPotatoWebhookConfig>("Webhooks:CouchPotato"));

            ServiceProvider = services.BuildServiceProvider();
        }

        private static void Startup()
        {
            try
            {
                ServiceProvider.GetService<DsmrService>().Start();

                InitializeMqtt();
                InitializeWebhooks();
                InitializeTemperatureTimer();
                InitializeShades();
            }
            catch (Exception e)
            {
                logger.Error(e, "Exception while starting application");
                Console.WriteLine(e);
            }
        }

        private static void Shutdown()
        {
            ServiceProvider.GetService<DsmrService>().Stop();
            _mqttService?.Stop();
            ServiceProvider.GetService<WebhookService>().Stop();
        }

        private static void InitializeMqtt()
        {
            _mqttService = ServiceProvider.GetService<MqttService>();
            _mqttService.MessageReceived += async (object sender, Message message) =>
            {
                await OnMessageReceived(message);
            };
            _mqttService.Start(new string[] {
                Configuration.Get<string>("Mqtt:Topics:SubscribeSet"),
                Configuration.Get<string>("Mqtt:Topics:SubscribeDim"),
                Configuration.Get<string>("Mqtt:Topics:SubscribeTemp"),
                Configuration.Get<string>("Mqtt:Topics:SubscribeShade"),
            });
        }

        private static void InitializeWebhooks()
        {
            _sonarrWebhookService = ServiceProvider.GetService<SonarrWebhookService>();
            _sonarrWebhookService.MessageReceived += async (object sender, Message message) =>
            {
                await OnMessageReceived(message);
            };

            _couchPotatoWebhookService = ServiceProvider.GetService<CouchPotatoWebhookService>();
            _couchPotatoWebhookService.MessageReceived += async (object sender, Message message) =>
            {
                await OnMessageReceived(message);
            };

            ServiceProvider.GetService<WebhookService>().Start();
        }

        private async static Task OnMessageReceived(Message message)
        {
            if (message == null)
            {
                return;
            }

            switch (message.Type)
            {
                case MessageType.SwitchState:
                    await OnSwitchStateMessageReceived(message as SwitchStateMessage);
                    break;
                case MessageType.DimLevel:
                    await OnDimLevelMessageReceived(message as DimLevelMessage);
                    break;
                case MessageType.Temperature:
                    await OnTemperatureMessageReceived(message as TemperatureMessage);
                    break;
                case MessageType.Shade:
                    await OnShadeMessageReceived(message as ShadeMessage);
                    break;
                case MessageType.Download:
                    await OnDownloadMessageReceived(message as DownloadMessage);
                    break;
                default:
                    return;
            }
        }

        private static async Task OnSwitchStateMessageReceived(SwitchStateMessage message)
        {
            logger.Info($"Received '{message.Data}' for switch {message.DeviceId}");
            await ServiceProvider.GetService<HomeWizardService>().ToggleSwitch(message);
            _mqttService.PublishMessage(message);
        }

        private static async Task OnDimLevelMessageReceived(DimLevelMessage message)
        {
            logger.Info($"Received '{message.Data}' for switch {message.DeviceId}");
            await ServiceProvider.GetService<HomeWizardService>().DimSwitch(message);
            _mqttService.PublishMessage(message);
        }

        private static async Task OnTemperatureMessageReceived(TemperatureMessage message)
        {
            logger.Info($"Received '{message.Data}' as temperature");
            _targetTemperature = message.Data;
            await ServiceProvider.GetService<HomeWizardService>().AdjustTemperature(message);
            _mqttService.PublishMessage(message);
        }

        private static async Task OnDownloadMessageReceived(DownloadMessage message)
        {
            logger.Info($"Received message from {message.DeviceId}");
            _mqttService.PublishMessage(message);
        }

        private static async Task OnShadeMessageReceived(ShadeMessage message)
        {
            logger.Info($"Received '{message.Data}' for shade {message.DeviceId}");
            string messageData = message.Data.ToLowerInvariant();
            switch (messageData)
            {
                case "open":
                    logger.Info("Opening shade");
                    await SetShadePosition(message.DeviceId, "0", 0);
                    break;
                case "close":
                    logger.Info("Closing shade");
                    await SetShadePosition(message.DeviceId, "100", 0);
                    break;
                case "stop":
                    logger.Info("Stopping shade");
                    await StopShade(message.DeviceId);
                    break;
                default:
                    logger.Info("Set position of shade");
                    await SetShadePosition(message.DeviceId, messageData, 0);
                    break;
            }
        }

        private static async Task SetShadePosition(string shadeName, string positionString, int retryNumber)
        {
            if (uint.TryParse(positionString, out uint newPosition))
            {
                var shadesService = ServiceProvider.GetService<ShadesService>();
                var oldPosition = await shadesService.GetPosition(shadeName);
                bool result = await shadesService.SetPosition(shadeName, newPosition);
                logger.Debug($"Setting position successfully? {result}");

                if (!result && ++retryNumber <= 5)
                {
                    int secondsDelay = (int)Math.Pow(2, retryNumber);
                    await Task.Delay(secondsDelay * 1000);
                    await SetShadePosition(shadeName, positionString, retryNumber);
                    return;
                }

                PublishShadePosition(shadeName, result ? newPosition : oldPosition.Value);
            }
        }

        private static async Task StopShade(string shadeName)
        {
            await ServiceProvider.GetService<ShadesService>().Stop(shadeName);
            await CheckShadePosition(shadeName);
        }

        private static async Task CheckShadePosition(string shadeName)
        {
            var position = await ServiceProvider.GetService<ShadesService>().GetPosition(shadeName);
            if (position.HasValue)
            {
                PublishShadePosition(shadeName, position.Value);
            }
        }

        private static void InitializeShades()
        {
            var shades = ServiceProvider.GetService<ShadesService>().GetConfiguredShades();
            var tasks = shades.Select(shade => CheckShadePosition(shade.Name));
            Task.Run(async () =>
            {
                await Task.WhenAll(tasks);
            });
        }

        private static void PublishShadePosition(string shadeName, uint position)
        {
            _mqttService.PublishMessage(new ShadeMessage() { DeviceId = shadeName, Data = position.ToString() });
            logger.Info($"Sent '{position.ToString()}' for {shadeName}");
        }

        private static void InitializeTemperatureTimer()
        {
            _temperatureTimer = SetupTimer(true, async (object target) => {
                var roomTemperature = await ServiceProvider.GetService<HomeWizardService>().GetRoomTemperature();
                PublishTemperature(roomTemperature, TemperatureType.Room);
                PublishTemperature(_targetTemperature, TemperatureType.Target);
            });
        }

        private static void PublishTemperature(decimal? temperature, TemperatureType type)
        {
            if (!temperature.HasValue)
            {
                return;
            }

            TemperatureMessage message = new TemperatureMessage
            {
                TemperatureType = type,
                Data = temperature.Value
            };

            _mqttService.PublishMessage(message);
            logger.Info($"Sent '{message.Data}' as {message.TemperatureType} temperature");
        }

        private static Timer SetupTimer(bool fireImmediately, TimerCallback callback)
        {
            TimeSpan interval = GetTimerInterval();
            TimeSpan due = fireImmediately ? TimeSpan.Zero : interval;
            return new Timer(callback, null, due, interval);
        }

        private static TimeSpan GetTimerInterval()
        {
            double timerInterval = Configuration.Get<double>("TimerInterval");
            return TimeSpan.FromMinutes(timerInterval);
        }
    }
}
