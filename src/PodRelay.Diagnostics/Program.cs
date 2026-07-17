using System.Text.Json;
using System.Text.Json.Serialization;
using PodRelay.Core.Audio;
using PodRelay.Core.Connection;
using PodRelay.Core.Devices;
using PodRelay.Core.Discovery;
using Windows.Gaming.Input;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "snapshot";
var discovery = new WindowsDeviceDiscovery();

try
{
    switch (command)
    {
        case "snapshot":
            await WriteSnapshotAsync(discovery);
            return 0;
        case "list-bluetooth":
            WriteJson(await discovery.GetPairedBluetoothDevicesAsync());
            return 0;
        case "list-audio":
            WriteJson(await discovery.GetAudioRenderEndpointsAsync());
            return 0;
        case "list-bluetooth-audio":
            WriteJson(new WindowsBluetoothAudioController().GetAllEndpoints());
            return 0;
        case "connect":
            return await ConnectAsync(discovery, args.Skip(1).ToArray());
        case "status":
            return await StatusAsync(discovery, args.Skip(1).ToArray());
        case "disconnect":
            return await DisconnectAsync(discovery, args.Skip(1).ToArray());
        case "watch-airpods":
            return await WatchAirPodsAsync(args.Skip(1).ToArray());
        case "list-game-controllers":
            WriteJson(RawGameController.RawGameControllers.Select(controller => new
            {
                controller.NonRoamableId,
                controller.DisplayName,
                controller.HardwareVendorId,
                controller.HardwareProductId,
                controller.IsWireless,
                controller.ButtonCount,
                controller.AxisCount,
                controller.SwitchCount
            }));
            return 0;
        case "default-audio":
            WriteJson(new WindowsDefaultAudioController().GetRenderEndpoints());
            return 0;
        case "capture-audio":
            WriteJson(new WindowsDefaultAudioController().GetCaptureEndpoints());
            return 0;
        default:
            Console.Error.WriteLine("Usage: PodRelay.Diagnostics [snapshot|list-bluetooth|list-audio|list-bluetooth-audio|list-game-controllers|default-audio|capture-audio|status --address XX:XX:XX:XX:XX:XX|connect --address XX:XX:XX:XX:XX:XX|disconnect --address XX:XX:XX:XX:XX:XX --confirm|watch-airpods --seconds 20]");
            return 2;
    }
}

catch (Exception exception)
{
    Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
    Console.Error.WriteLine(exception.StackTrace);
    return 1;
}

static async Task<int> ConnectAsync(WindowsDeviceDiscovery discovery, string[] arguments)
{
    var target = await FindTargetAsync(discovery, arguments, "connect");
    if (target is null)
    {
        return 3;
    }

    var coordinator = new EnsureConnectedCoordinator(new WindowsConnectionPlatform());
    coordinator.StateChanged += (_, state) => Console.Error.WriteLine($"State: {state}");
    var result = await coordinator.EnsureConnectedAsync(target);
    WriteJson(result);
    return result.IsSuccess ? 0 : 6;
}

static async Task<int> StatusAsync(WindowsDeviceDiscovery discovery, string[] arguments)
{
    var target = await FindTargetAsync(discovery, arguments, "status");
    if (target is null)
    {
        return 3;
    }

    var observation = await new WindowsConnectionPlatform().ObserveAsync(target, CancellationToken.None);
    WriteJson(new { target, observation, capturedAt = DateTimeOffset.Now });
    return observation.IsFullyConnected ? 0 : 5;
}

static async Task<int> DisconnectAsync(WindowsDeviceDiscovery discovery, string[] arguments)
{
    if (!arguments.Contains("--confirm", StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("disconnect changes device state; repeat with --confirm.");
        return 2;
    }

    var addressIndex = Array.FindIndex(arguments, argument => argument.Equals("--address", StringComparison.OrdinalIgnoreCase));
    if (addressIndex < 0 || addressIndex + 1 >= arguments.Length)
    {
        Console.Error.WriteLine("disconnect requires --address XX:XX:XX:XX:XX:XX --confirm");
        return 2;
    }

    var requestedAddress = arguments[addressIndex + 1].Replace("-", ":", StringComparison.Ordinal).ToUpperInvariant();
    var device = (await discovery.GetPairedBluetoothDevicesAsync())
        .SingleOrDefault(candidate => candidate.FormattedAddress == requestedAddress);
    if (device?.ContainerId is null)
    {
        Console.Error.WriteLine($"No paired Bluetooth device with an association container has address {requestedAddress}.");
        return 3;
    }

    var results = new WindowsBluetoothAudioController().RequestDisconnect(device.ContainerId.Value);
    WriteJson(results);
    return results.Any(result => result.LastControlHResult >= 0) ? 0 : 6;
}

static async Task<int> WatchAirPodsAsync(string[] arguments)
{
    var secondsIndex = Array.FindIndex(arguments, argument => argument.Equals("--seconds", StringComparison.OrdinalIgnoreCase));
    var seconds = secondsIndex >= 0 && secondsIndex + 1 < arguments.Length &&
        int.TryParse(arguments[secondsIndex + 1], out var parsed)
            ? Math.Clamp(parsed, 1, 120)
            : 20;
    var events = new List<AirPodsAdvertisement>();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;
    try
    {
        using var watcher = new AirPodsAdvertisementWatcher();
        watcher.AirPodsSeen += (_, advertisement) =>
        {
            lock (events)
            {
                events.Add(advertisement);
            }

            Console.Error.WriteLine(
                $"AirPods frame: model {advertisement.PublicStatus.ModelCodeHex}, " +
                $"state {advertisement.PublicStatus.WearState}, raw 0x{advertisement.PublicStatus.RawStatus:X2}, " +
                $"RSSI {advertisement.SignalStrengthDbm} dBm at {advertisement.ReceivedAt:O}");
        };
        watcher.Start();
        Console.Error.WriteLine($"Watching passively for Apple AirPods proximity frames for {seconds} seconds. Open the case during this interval.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }

    AirPodsAdvertisement[] captured;
    lock (events)
    {
        captured = events.ToArray();
    }

    WriteJson(new { capturedCount = captured.Length, events = captured });
    return captured.Length > 0 ? 0 : 7;
}

static async Task<TargetDevice?> FindTargetAsync(
    WindowsDeviceDiscovery discovery,
    string[] arguments,
    string commandName)
{
    var addressIndex = Array.FindIndex(arguments, argument => argument.Equals("--address", StringComparison.OrdinalIgnoreCase));
    if (addressIndex < 0 || addressIndex + 1 >= arguments.Length)
    {
        Console.Error.WriteLine($"{commandName} requires --address XX:XX:XX:XX:XX:XX");
        return null;
    }

    var requestedAddress = arguments[addressIndex + 1].Replace("-", ":", StringComparison.Ordinal).ToUpperInvariant();
    var device = (await discovery.GetPairedBluetoothDevicesAsync())
        .SingleOrDefault(candidate => candidate.FormattedAddress == requestedAddress);
    if (device is null)
    {
        Console.Error.WriteLine($"No paired Bluetooth device has address {requestedAddress}.");
        return null;
    }

    if (device.ContainerId is null)
    {
        Console.Error.WriteLine($"Device '{device.Name}' has no association container ID.");
        return null;
    }

    return new TargetDevice(
        device.FormattedAddress,
        device.ContainerId.Value,
        device.Name,
        device.AirPodsModelCode);
}

static async Task WriteSnapshotAsync(WindowsDeviceDiscovery discovery)
{
    var bluetooth = await discovery.GetPairedBluetoothDevicesAsync();
    var audio = await discovery.GetAudioRenderEndpointsAsync();
    var radioState = await discovery.GetBluetoothRadioStateAsync();

    WriteJson(new
    {
        capturedAt = DateTimeOffset.Now,
        windows = Environment.OSVersion.VersionString,
        processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        bluetoothRadioState = radioState?.ToString() ?? "Unavailable",
        bluetoothDevices = bluetooth,
        audioRenderEndpoints = audio
    });
}

static void WriteJson<T>(T value)
{
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    }));
}
