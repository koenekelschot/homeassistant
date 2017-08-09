namespace HomeAssistant.Hub.HomeWizard.Models
{
    public class HeatLink : Device
    {
        public string Code { get; set; }
        public string Pump { get; set; }
        public string Heating { get; set; }
        public string Dwh { get; set; }
        public double Rte { get; set; }
        public double Rsp { get; set; }
        public double Tte { get; set; }
        public string Ttm { get; set; } //something nullable
        public double Wp { get; set; }
        public double Wte { get; set; }
        public int Ofc { get; set; }
        public int Odc { get; set; }
    }
}
