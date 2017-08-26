using HomeAssistant.Hub.Dsmr;
using HomeAssistant.Hub.HomeWizard;
using HomeAssistant.Hub.Models;
using HomeAssistant.Hub.Mqtt;
using HomeAssistant.Hub.Soma;
using NLog;
using System;
using System.Collections.Generic;
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
        private static Timer _shadesTimer;
        private static volatile bool _shadesTimerCallbackRunning = false;
        private static bool _shadesMoving = false;
        private static List<Shade> _shades = new List<Shade>();

        private static HomeWizardClient _homeWizardClient;
        private static MqttPubSubClient _mqttClient;
        private static DsmrClient _dsmrClient;
        private static ShadesService _somaShadesService;

        static void Main(string[] args)
        {
            Console.WriteLine(" Press [enter] to exit.");

            string[] subscriptionTopics = {
                AppSettings["mqtt_subscribe_set"],
                AppSettings["mqtt_subscribe_dim"],
                AppSettings["mqtt_subscribe_temp"],
                AppSettings["mqtt_subscribe_shade"]
            };

            _dsmrClient = DsmrClient.Instance;
            _homeWizardClient = HomeWizardClient.Instance;
            _mqttClient = MqttPubSubClient.Instance;
            _somaShadesService = ShadesService.Instance;

            _mqttClient.MessageReceived += async (object sender, Message message) => { await OnMqttMessageReceived(message); };
            _mqttClient.Subscribe(subscriptionTopics);

            InitializeTemperatureTimer();
            InitializeShades();

            _dsmrClient.Start();
            Console.ReadLine();
            _dsmrClient.Stop();
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
            switch(messageData)
            {
                case "open":
                    logger.Info("Opening shade");
                    await SetShadePosition(message.DeviceId, "0");
                    break;
                case "close":
                    logger.Info("Closing shade");
                    await SetShadePosition(message.DeviceId, "100");
                    break;
                case "stop":
                    logger.Info("Stopping shade");
                    await StopShade(message.DeviceId);
                    break;
                default:
                    logger.Info("Set position of shade");
                    await SetShadePosition(message.DeviceId, messageData);
                    break;
            }
        }

        private static async Task SetShadePosition(string shadeName, string positionString)
        {
            if (uint.TryParse(positionString, out uint position))
            {
                await _somaShadesService.SetPosition(shadeName, position);
                UpdateShadeTargetPosition(shadeName, position);
                await CheckShades();
            }
        }

        private static async Task StopShade(string shadeName)
        {
            await _somaShadesService.Stop(shadeName);
            var position = await _somaShadesService.GetPosition(shadeName);
            logger.Info($"Stopped at position {position}");
            UpdateShadeTargetPosition(shadeName, position);
            await CheckShades();
        }

        private static void UpdateShadeTargetPosition(string shadeName, uint? position)
        {
            if (position.HasValue)
            {
                var shade = _shades.First(s => s.Name.Equals(shadeName, StringComparison.InvariantCultureIgnoreCase));
                shade.TargetPosition = position.Value;
            }
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

        private static void InitializeShades()
        {
            var shadeNames = AppSettings["soma_shades"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct();
            _shades.AddRange(shadeNames.Select(name => new Shade(name)));
            _shadesTimer = SetupTimer(true, async (object target) =>
            {
                await CheckShades();
            });
        }

        private static async Task CheckShades()
        {
			if (_shadesTimerCallbackRunning)
            {
                return;
            }
            _shadesTimerCallbackRunning = true;

            logger.Info("Checking shades");
            var movingShades = _shades.Where(shade => shade.TargetPosition.HasValue);
            logger.Info($"Found {movingShades.Count()} moving shades");
            var tasks = _shades.Select(shade => CheckShadePosition(shade));
            await Task.WhenAll(tasks);

            if (movingShades.Any() && !_shadesMoving)
            {
                _shadesMoving = true;
                ChangeShadesTimerInterval();
            }
            if (!movingShades.Any() && _shadesMoving)
            {
                _shadesMoving = false;
                ChangeShadesTimerInterval();
            }

            _shadesTimerCallbackRunning = false;
        }

        private static async Task CheckShadePosition(Shade shade)
        {
			logger.Info("Checking position");
			var position = await _somaShadesService.GetPosition(shade.Name);
            if (position.HasValue)
            {
                if (!shade.LastPosition.HasValue)
                {
					shade.LastPosition = position.Value;
                    PublishShadePosition(shade);
                }

                if (shade.TargetPosition.HasValue)
                {
					logger.Info($"Target: {shade.TargetPosition.Value} Current: {position.Value}");
					var delta = 3;
                    if (position.Value > shade.TargetPosition.Value - delta && position.Value < shade.TargetPosition.Value + delta)
                    {
                        shade.TargetPosition = null;
                        shade.LastPosition = position.Value;
                        PublishShadePosition(shade);
                    }
                }
            }
			logger.Info("Finished checking position");
        }

        private static void PublishShadePosition(Shade shade)
        {
            _mqttClient.PublishMessage(new ShadeMessage() { DeviceId = shade.Name, Data = shade.LastPosition.Value.ToString() });
            logger.Info($"Sent '{shade.LastPosition.Value}' for {shade.Name}");
        }

        private static void ChangeShadesTimerInterval()
        {
            TimeSpan interval = _shadesMoving ? TimeSpan.FromSeconds(3) : GetTimerInterval();
            _shadesTimer.Change(TimeSpan.Zero, interval);
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
