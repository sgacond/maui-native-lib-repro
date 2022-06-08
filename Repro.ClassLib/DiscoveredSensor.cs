namespace Repro.ClassLib
{
    public record DiscoveredSensor(
        string Name,
        string SerialNumber,
        string SerialPort)
    {
        public override string ToString()
            => $"{Name} ({SerialNumber}) on {SerialPort}";
    }
}
