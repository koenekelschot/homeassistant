namespace HomeAssistant.Hub.Models
{
    public class Shade
    {
        public string Name { get; }
        public uint? LastPosition { get; set; }
        public uint? TargetPosition { get; set; }
        public uint MinPosition { get; set; } = 0;
        public uint MaxPosition { get; set; } = 100;

        public Shade(string name)
        {
            Name = name;
        }
    }
}
