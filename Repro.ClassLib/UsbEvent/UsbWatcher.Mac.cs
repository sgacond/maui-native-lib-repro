using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Repro.ClassLib.UsbEvent
{
	[SupportedOSPlatform("macOS")]
    [SupportedOSPlatform("MacCatalyst15.4")]
    internal class UsbWatcherMac : IUsbWatcher
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct NativeHelperUsbDeviceData
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string Product;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string ProductDescription;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string ProductID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string SerialNumber;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string Vendor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string VendorDescription;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string VendorID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string SerialPort;
        }


        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void UsbDeviceCallback(NativeHelperUsbDeviceData usbDevice);


#if MACCATALYST
        [DllImport("libUsbEventWatcher.MacCatalyst-arm64.dylib", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("libUsbEventWatcher.Mac-arm64.dylib", CallingConvention = CallingConvention.Cdecl)]
#endif
        static extern void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

#if MACCATALYST
        [DllImport("libUsbEventWatcher.MacCatalyst-arm64.dylib", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("libUsbEventWatcher.Mac-arm64.dylib", CallingConvention = CallingConvention.Cdecl)]
#endif
        static extern void StopMacWatcher();
        
        public event EventHandler<UsbSerialDevice>? UsbDeviceAdded;
        public event EventHandler<UsbSerialDevice>? UsbDeviceRemoved;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ILogger<UsbWatcherMac> _log;
        private bool _initialEnumerationDone;

        public IEnumerable<UsbSerialDevice> SensorUsbDevices => _sensorUsbDevices;

        private List<UsbSerialDevice> _sensorUsbDevices;
        private Dictionary<string, string> _lastKnownPortBySerialNumber = new();
        
        public UsbWatcherMac(ILogger<UsbWatcherMac> log)
        {
            _log = log;
            _sensorUsbDevices = new List<UsbSerialDevice>();
            _initialEnumerationDone = false;
        }

        public async Task Startup()
        {
            _ = Task.Factory.StartNew(
                () => StartMacWatcher(InsertedCallback, RemovedCallback),
                _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            // Wait: We only have a blocking startup method. Collection should be available after "startup".
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            _initialEnumerationDone = true;
            
            _log.LogInformation("Mac USB event watcher started.");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            StopMacWatcher();
        }

        private void InsertedCallback(NativeHelperUsbDeviceData nativeDeviceData)
        {
            var device = ConvertUsbDeviceFromNativeStruct(nativeDeviceData);
            if (device.VendorID != SensorConstants.S1Vid || device.ProductID != SensorConstants.S1Pid)
            {
                _log.LogDebug("Ignoring connected event for device: {device}", device);
                return;
            }

            if(_sensorUsbDevices.Contains(device))
            {
                _log.LogDebug("Ignoring connected event for already connected device: {device}", device);
                return;
            }

            if (string.IsNullOrEmpty(device.SerialPort))
            {
                if (_lastKnownPortBySerialNumber.ContainsKey(device.SerialNumber))
                {
                    _log.LogWarning("Serial port not set! This can happen on re-plug! Restore with remembered.");
                    device.SerialPort = _lastKnownPortBySerialNumber[device.SerialNumber];
                }
                else
                {
                    _log.LogWarning("Serial port not set! This can happen on re-plug! Restore by serial number.");
                    device.SerialPort = $"/dev/tty.usbmodem{device.SerialNumber.Replace("-", "_")}";
                }
            }
            else
            {
                if (_lastKnownPortBySerialNumber.ContainsKey(device.SerialNumber))
                    _lastKnownPortBySerialNumber[device.SerialNumber] = device.SerialPort;
                else
                    _lastKnownPortBySerialNumber.Add(device.SerialNumber, device.SerialPort);
            }
            
            _sensorUsbDevices.Add(device);

            if (_initialEnumerationDone)
            {
                _log.LogInformation("Add event received for id: {usbDevice}", nativeDeviceData);
                UsbDeviceAdded?.Invoke(this, device);
            }
        }

        private void RemovedCallback(NativeHelperUsbDeviceData nativeDeviceData)
        {
            var device = ConvertUsbDeviceFromNativeStruct(nativeDeviceData);
            if (!SensorUsbDevices.Contains(device))
            {
                _log.LogDebug("Ignore deletion event for device: {device}.", device);
                return;
            }

            _log.LogInformation("Deletion event received for id: {device}. Remove from SensorUsbDevices list.", device);
            _sensorUsbDevices.Remove(device);
            UsbDeviceRemoved?.Invoke(this, device);
        }
        
        private static UsbSerialDevice ConvertUsbDeviceFromNativeStruct(NativeHelperUsbDeviceData usbDeviceData)
        {
            ushort vid, pid;

            ushort.TryParse(usbDeviceData.ProductID, NumberStyles.Any, CultureInfo.InvariantCulture, out pid);
            ushort.TryParse(usbDeviceData.VendorID, NumberStyles.Any, CultureInfo.InvariantCulture, out vid);

            return new UsbSerialDevice
            {
                ProductName = usbDeviceData.Product,
                SerialNumber = usbDeviceData.SerialNumber,
                SerialPort = usbDeviceData.SerialPort,
                ProductID = pid,
                VendorID = vid
            };
        }
    }
}