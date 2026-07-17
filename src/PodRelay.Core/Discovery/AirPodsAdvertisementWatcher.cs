using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace PodRelay.Core.Discovery;

public sealed class AirPodsAdvertisementWatcher : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher watcher;
    private readonly AdvertisementDuplicateGate duplicateGate;
    private readonly AirPodsAdvertisementConfirmationGate confirmationGate;
    private bool disposed;

    public AirPodsAdvertisementWatcher(TimeSpan? duplicateWindow = null)
    {
        duplicateGate = new AdvertisementDuplicateGate(duplicateWindow ?? TimeSpan.FromSeconds(10));
        confirmationGate = new AirPodsAdvertisementConfirmationGate();
        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Passive
        };
        watcher.Received += OnReceived;
    }

    public event EventHandler<AirPodsAdvertisement>? AirPodsSeen;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (watcher.Status is BluetoothLEAdvertisementWatcherStatus.Created or BluetoothLEAdvertisementWatcherStatus.Stopped)
        {
            watcher.Start();
        }
    }

    public void Stop()
    {
        if (!disposed && watcher.Status is (BluetoothLEAdvertisementWatcherStatus.Started or BluetoothLEAdvertisementWatcherStatus.Aborted))
        {
            watcher.Stop();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        watcher.Received -= OnReceived;
        disposed = true;
    }

    private void OnReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var manufacturerData in args.Advertisement.ManufacturerData)
        {
            var payload = ReadBytes(manufacturerData.Data);
            if (!AirPodsAdvertisementClassifier.TryDecodePublicStatus(
                manufacturerData.CompanyId,
                payload,
                out var publicStatus) || publicStatus is null)
            {
                continue;
            }

            var now = DateTimeOffset.Now;
            var decision = confirmationGate.Evaluate(
                publicStatus.ModelCode,
                publicStatus.WearState,
                args.RawSignalStrengthInDBm,
                now);
            if (decision != AirPodsAdvertisementDecision.Accept)
            {
                return;
            }

            var stateKey = $"{args.BluetoothAddress:X12}:{publicStatus.ModelCode:X4}:{publicStatus.RawStatus:X2}";
            if (!duplicateGate.TryAccept(now, stateKey))
            {
                return;
            }

            AirPodsSeen?.Invoke(this, new AirPodsAdvertisement(
                args.BluetoothAddress,
                args.RawSignalStrengthInDBm,
                payload,
                publicStatus,
                now));
            return;
        }
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
