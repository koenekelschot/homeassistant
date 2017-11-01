using System.Net;

namespace HomeAssistant.Hub.Config
{
    public class DsmrConfig
    {
        public IPAddress RemoteHost { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public int IntervalMinutes { get; set; }
    }
}
