using System.Collections.Generic;

namespace HomeAssistant.Hub.HomeWizard.Models
{
    public class Sensors
    {
        public int Preset { get; set; }
        public string Time { get; set; }
        //public IList<Switch> Switches { get; set; }
        //public IList<UvMeter> UvMeters { get; set; }
        //public IList<WindMeter> WindMeters { get; set; }
        //public IList<RainMeter> RainMeters { get; set; }
        //public IList<ThermoMeter> ThermoMeters { get; set; }
        //public IList<WeatherDisplay> WeatherDisplays { get; set; }
        //public IList<EnergyMeter> EnergyMeters { get; set; }
        //public IList<EnergyLink> EnergyLinks { get; set; }
        public List<HeatLink> HeatLinks { get; set; }
        //public IList<HueBridge> Hues { get; set; }
        //public IList<Scene> Scenes { get; set; }
        //public IList<KakuSensor> KakuSensors { get; set; }
        //public IList<Camera> Cameras { get; set; }
    }
}
