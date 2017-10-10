using HomeAssistant.Hub.DependencyInjection;
using HomeAssistant.Hub.Dsmr;
using HomeAssistant.Hub.HomeWizard;
using HomeAssistant.Hub.Models;
using HomeAssistant.Hub.Mqtt;
using HomeAssistant.Hub.Soma;
using NLog;
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
        private static DsmrClient _dsmrClient;
        private static ShadesService _somaShadesService;

        //static void Main(string[] args)
        //{
        //    Console.WriteLine(" Press [enter] to exit.");

        //    string[] subscriptionTopics = {
        //        AppSettings["mqtt_subscribe_set"],
        //        AppSettings["mqtt_subscribe_dim"],
        //        AppSettings["mqtt_subscribe_temp"],
        //        AppSettings["mqtt_subscribe_shade"]
        //    };

        //    _dsmrClient = DsmrClient.Instance;
        //    _homeWizardClient = HomeWizardClient.Instance;
        //    _mqttClient = MqttPubSubClient.Instance;
        //    _somaShadesService = ShadesService.Instance;

        //    _mqttClient.MessageReceived += async (object sender, Message message) => { await OnMqttMessageReceived(message); };
        //    _mqttClient.Subscribe(subscriptionTopics);

        //    InitializeTemperatureTimer();
        //    InitializeShades();

        //    _dsmrClient.Start();
        //    Console.ReadLine();
        //    _dsmrClient.Stop();
        //}

        private static DependencyInjection.IServiceProvider ServiceProvider;

        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            //services
            //    .AddSingleton<TestSingleton>()
            //    .AddSingleton<TestSingleton2>(new TestSingleton2() { Random = 42 })
            //    .AddTransient<TestTransient>()
            //    .AddSingleton<ITestSingleton, TestSingleton3>()
            //    .AddSingleton<ITestSingleton, TestSingleton4>()
            //    .AddTransient<Outer>().AddTransient<Inner>();
            services
                .AddTransient<Circular1>()
                .AddTransient<Circular2>()
                .AddTransient<Circular3>();
            ServiceProvider = services.BuildServiceProvider();

            //var builder = new ConfigurationBuilder()
            //    .SetBasePath(env.ContentRootPath)
            //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            //Configuration = builder.Build(); --> IConfigurationRoot
            //services.Configure<MySubOptions>(Configuration.GetSection("subsection"));

            Test();
        }

        private static void Test()
        {
            //var test1 = ServiceProvider.GetService<TestSingleton>();
            //var test2 = ServiceProvider.GetService<TestSingleton>();
            //bool areInstancesEqual = test1.Instance.Equals(test2.Instance);

            //var test3 = ServiceProvider.GetService<TestSingleton2>();
            //bool isCustomInstance = test3.Random == 42;

            //var test4 = ServiceProvider.GetService<TestTransient>();
            //var test5 = ServiceProvider.GetService<TestTransient>();
            //bool areNewInstances = !test4.Instance.Equals(test5.Instance);

            //var test6 = ServiceProvider.GetService<ITestSingleton>();

            //var test7 = ServiceProvider.GetService<Outer>();
            var circularTest = ServiceProvider.GetService<Circular1>();
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

    public class TestSingleton
    {
        public readonly Guid Instance;

        public TestSingleton()
        {
            Instance = Guid.NewGuid();
        }
    }

    public class TestSingleton2
    {
        public readonly Guid Instance;
        public int Random { get; set; }

        public TestSingleton2()
        {
            Instance = Guid.NewGuid();
        }
    }

    public class TestSingleton3 : ITestSingleton
    {
        public readonly Guid Instance;
        public int Random { get; set; }

        public TestSingleton3()
        {
            Instance = Guid.NewGuid();
        }
    }

    public class TestSingleton4 : ITestSingleton
    {
        public readonly Guid Instance;
        public int Random { get; set; }

        public TestSingleton4()
        {
            Instance = Guid.NewGuid();
        }
    }

    public interface ITestSingleton
    {
        int Random { get; set; }
    }

    public class TestTransient
    {
        public readonly Guid Instance;

        public TestTransient()
        {
            Instance = Guid.NewGuid();
        }
    }

    public class Outer
    {
        public readonly Inner inner;
        public readonly string test;

        public Outer(Inner inner, string test = "42")
        {
            this.inner = inner;
            this.test = test;
        }
    }

    public class Inner
    {
        public Inner() { }
    }

    public class Circular1
    {
        public readonly Circular2 _circularRef;

        public Circular1(Circular2 circularRef)
        {
            _circularRef = circularRef;
        }
    }

    public class Circular2
    {
        public readonly Circular3 _circularRef;

        public Circular2(Circular3 circularRef)
        {
            _circularRef = circularRef;
        }
    }

    public class Circular3
    {
        public readonly Circular1 _circularRef;

        public Circular3(Circular1 circularRef)
        {
            _circularRef = circularRef;
        }
    }
}
