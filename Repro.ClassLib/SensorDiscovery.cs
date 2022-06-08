using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Repro.ClassLib.UsbEvent;

namespace Repro.ClassLib
{
    public class SensorDiscovery
    {
        public event EventHandler<DiscoveredSensor>? SensorConnected;
        public event EventHandler<DiscoveredSensor>? SensorDisconnected;

        private readonly IUsbWatcher _usbEventWatcher;
        private readonly ILogger<SensorDiscovery> _log;
        private readonly ILoggerFactory _loggerFactory;

        public SensorDiscovery() : this(NullLoggerFactory.Instance)
        { }

        public SensorDiscovery(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<SensorDiscovery>();
            _loggerFactory = loggerFactory;

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
                _usbEventWatcher = new UsbWatcherMac(loggerFactory.CreateLogger<UsbWatcherMac>());
            //else if (OperatingSystem.IsWindows())
            //    _usbEventWatcher = new UsbWatcherWindows(loggerFactory.CreateLogger<UsbWatcherWindows>());
            else
                throw new NotImplementedException();

            _usbEventWatcher.UsbDeviceAdded += DeviceConnected; 
            _usbEventWatcher.UsbDeviceRemoved += DeviceRemoved;
        }

        public Task InitializeUSBSupport() => _usbEventWatcher.Startup();

        public void Dispose()
        {
            _usbEventWatcher?.Dispose();
        }

        private void DeviceConnected(object? sender, UsbSerialDevice serialDevice)
            => SensorConnected?.Invoke(this, DiscoveredSensorFromUsbDevice(serialDevice));

        private void DeviceRemoved(object? sender, UsbSerialDevice serialDevice)
            => SensorDisconnected?.Invoke(this, DiscoveredSensorFromUsbDevice(serialDevice));

        private DiscoveredSensor DiscoveredSensorFromUsbDevice(UsbSerialDevice d)
            => new DiscoveredSensor(d.ProductName, d.SerialNumber, d.SerialPort);
    }
}
