namespace HomeAssistant.Hub.Soma
{
    public class Shade
    {
        public string Name { get; set; }
        public uint PositionUp { get; set; } = 0;
        public uint PositionDown { get; set; } = 100;

        public uint Range => PositionDown - PositionUp;
    }
}
