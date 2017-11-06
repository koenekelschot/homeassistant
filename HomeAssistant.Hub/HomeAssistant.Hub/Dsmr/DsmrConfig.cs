using System.Net;

namespace HomeAssistant.Hub.Dsmr
{
    public class DsmrConfig
    {
        public string RemoteHost { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public int IntervalMinutes { get; set; }
    }
}
