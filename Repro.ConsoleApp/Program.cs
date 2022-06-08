using Repro.ClassLib;

var test = new SensorDiscovery();

await test.InitializeUSBSupport();

test.SensorConnected += (s, e) => Console.WriteLine($"USB sensor connected: {e.Name}.");
test.SensorDisconnected += (s, e) => Console.WriteLine($"USB sensor disconnected: {e.Name}.");

Console.WriteLine("Waiting for connect / disconnect events. Press enter to stop.");
Console.ReadLine();