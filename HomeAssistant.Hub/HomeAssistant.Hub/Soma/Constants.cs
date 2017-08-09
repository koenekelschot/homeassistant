using System;

namespace HomeAssistant.Hub.Soma
{
    public class Constants
    {
        public static readonly Guid ShadeBatteryService = Guid.Parse("0000180f-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ShadeBatteryCharacteristic = Guid.Parse("00002a19-0000-1000-8000-00805f9b34fb"); //returns battery value in percentage format, 0-100

        public static readonly Guid ShadeMotorService = Guid.Parse("00001861-B87F-490C-92CB-11BA5EA5167C");
        public static readonly Guid ShadeMotorStateCharacteristic = Guid.Parse("00001525-B87F-490C-92CB-11BA5EA5167C"); //returns an array, the first value is the motor position in percentage
        public static readonly Guid ShadeMotorControlCharacteristic = Guid.Parse("00001530-B87F-490C-92CB-11BA5EA5167C"); //write 0x69 to move up, 0x96 to move down, 0x0 to stop
        public static readonly Guid ShadeMotorTargetCharacteristic = Guid.Parse("00001526-B87F-490C-92CB-11BA5EA5167C"); //write 0x00 - 0x69, (0 - 100 represented as % in base10)
        public static readonly Guid NotifyCharacteristic = Guid.Parse("00001531-B87F-490C-92CB-11BA5EA5167C"); //play sound?
    }
}
