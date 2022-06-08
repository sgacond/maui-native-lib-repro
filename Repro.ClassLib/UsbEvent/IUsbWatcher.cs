using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repro.ClassLib.UsbEvent
{
    internal interface IUsbWatcher : IDisposable
    {
        Task Startup();

        IEnumerable<UsbSerialDevice> SensorUsbDevices { get; }

        event EventHandler<UsbSerialDevice>? UsbDeviceAdded;
        event EventHandler<UsbSerialDevice>? UsbDeviceRemoved;
    }
}