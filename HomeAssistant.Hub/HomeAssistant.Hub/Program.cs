using HomeAssistant.Hub.Config;
using HomeAssistant.Hub.HomeWizard;
using HomeAssistant.Hub.Models;
using HomeAssistant.Hub.Mqtt;
using HomeAssistant.Hub.Services;
using HomeAssistant.Hub.Soma;
using NLog;
using SimpleDI;
using SimpleDI.Configuration;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistant.Hub
{
    static class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly NameValueCollection AppSettings = ConfigurationManager.AppSettings;
        private static Timer _temperatureTimer;
        private static double? _targetTemperature;

        private static HomeWizardClient _homeWizardClient;
        private static MqttPubSubClient _mqttClient;
        private static ShadesService _somaShadesService;

        private static SimpleDI.IServiceProvider ServiceProvider;
        private static IConfigurationRoot Configuration;

        static void Main(string[] args)
        {
            ConfigureEnv();
            Console.WriteLine(" Press [enter] to exit.");
            Startup();
                                    
            string[] subscriptionTopics = {
                AppSettings["mqtt_subscribe_set"],
                AppSettings["mqtt_subscribe_dim"],
                AppSettings["mqtt_subscribe_temp"],
                AppSettings["mqtt_subscribe_shade"]
            };

            _homeWizardClient = HomeWizardClient.Instance;
            _mqttClient = MqttPubSubClient.Instance;
            _somaShadesService = ShadesService.Instance;

            _mqttClient.MessageReceived += async (object sender, Message message) => { await OnMqttMessageReceived(message); };
            _mqttClient.Subscribe(subscriptionTopics);

            InitializeTemperatureTimer();
            InitializeShades();

            Console.ReadLine();
            Shutdown();
        }

        private static void ConfigureEnv()
        {
            var configBuilder = new ConfigurationBuilder();
            Configuration = configBuilder.Build();

            IServiceCollection services = new ServiceCollection()
                .AddSingleton<DsmrService>()
                .Configure<DsmrConfig>(Configuration.GetSection<DsmrConfig>(""));
            ServiceProvider = services.BuildServiceProvider();

            //var services = new ServiceCollection()
            //    .AddSingleton<TestSingleton>()
            //    .AddTransient<TestTransient>();
            //ServiceProvider = services.BuildServiceProvider();
            //var builder = new ConfigurationBuilder()
            //    .SetBasePath("json")
            //    .AddJsonFile("config1.json", optional: true)
            //    .Build();
        }

        private static void Startup()
        {
            ServiceProvider.GetService<DsmrService>().Start();
        }

        private static void Shutdown()
        {
            ServiceProvider.GetService<DsmrService>().Stop();
        }

        private async static Task OnMqttMessageReceived(Message message)
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
                default:
                    return;
            }
        }

        private static async Task OnSwitchStateMessageReceived(SwitchStateMessage message)
        {
            logger.Info($"Received '{message.Data}' for switch {message.DeviceId}");
            await _homeWizardClient.Update(message);
            _mqttClient.PublishMessage(message);
        }

        private static async Task OnDimLevelMessageReceived(DimLevelMessage message)
        {
            logger.Info($"Received '{message.Data}' for switch {message.DeviceId}");
            await _homeWizardClient.Update(message);
            _mqttClient.PublishMessage(message);
        }

        private static async Task OnTemperatureMessageReceived(TemperatureMessage message)
        {
            logger.Info($"Received '{message.Data}' as temperature");
            _targetTemperature = message.Data;
            await _homeWizardClient.Update(message);
            _mqttClient.PublishMessage(message);
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
            if (uint.TryParse(positionString, out uint position))
            {
                bool result = await _somaShadesService.SetPosition(shadeName, position);
                logger.Debug($"Setting position successfully? {result}");
                if (!result && ++retryNumber <= 5)
                {
                    int secondsDelay = (int)Math.Pow(2, retryNumber);
                    await Task.Delay(secondsDelay * 1000);
                    await SetShadePosition(shadeName, positionString, retryNumber);
                    return;
                }
                await CheckShadePosition(shadeName);
            }
        }

        private static async Task StopShade(string shadeName)
        {
            await _somaShadesService.Stop(shadeName);
            await CheckShadePosition(shadeName);
        }

        private static async Task CheckShadePosition(string shade)
        {
            var position = await _somaShadesService.GetPosition(shade);
            if (position.HasValue)
            {
                PublishShadePosition(shade, position.Value);
            }
        }

        private static void InitializeShades()
        {
            var shades = AppSettings["soma_shades"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct();
            var tasks = shades.Select(shade => CheckShadePosition(shade));
            Task.Run(async () =>
            {
                await Task.WhenAll(tasks);
            }).Wait();
        }

        private static void PublishShadePosition(string shadeName, uint position)
        {
            _mqttClient.PublishMessage(new ShadeMessage() { DeviceId = shadeName, Data = position.ToString() });
            logger.Info($"Sent '{position.ToString()}' for {shadeName}");
        }

        private static void InitializeTemperatureTimer()
        {
            _temperatureTimer = SetupTimer(true, async (object target) => {
                var roomTemperature = await _homeWizardClient.GetRoomTemperature();
                PublishTemperature(roomTemperature, TemperatureType.Room);
                PublishTemperature(_targetTemperature, TemperatureType.Target);
            });
        }

        private static void PublishTemperature(double? temperature, TemperatureType type)
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

            _mqttClient.PublishMessage(message);
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
            double timerInterval = double.Parse(AppSettings["timer_interval"]);
            return TimeSpan.FromMinutes(timerInterval);
        }
    }
}
