using System;

namespace Repro.ClassLib.UsbEvent
{
    internal class UsbSerialDevice : IEquatable<UsbSerialDevice>
    {
        public string ProductName { get; internal set; } = string.Empty;

        public ushort ProductID { get; internal set; }
        public ushort VendorID { get; internal set; }

        public string SerialNumber { get; internal set; } = string.Empty;
        public string SerialPort { get; internal set; } = string.Empty;

        public override string ToString()
            => $"{VendorID:X4}:{ProductID:X4} - {ProductName} ({SerialNumber})";

        // We just use the serial port to distinct usb serial devices.
        public bool Equals(UsbSerialDevice? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return SerialPort == other.SerialPort;
        }

        // We just use the serial port to distinct usb serial devices.
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((UsbSerialDevice)obj);
        }

        public override int GetHashCode()
        {
            return SerialPort.GetHashCode();
        }
    }
}
