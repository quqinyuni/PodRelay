using Windows.Gaming.Input;

namespace PodRelay.Core.Devices;

public sealed class WindowsGameControllerWatcher : IDisposable
{
    private bool started;
    private bool disposed;

    public event EventHandler<GameControllerSnapshot>? ControllerConnected;
    public event EventHandler<GameControllerSnapshot>? ControllerDisconnected;

    public IReadOnlyList<GameControllerSnapshot> GetConnectedControllers() =>
        RawGameController.RawGameControllers
            .Select(ToSnapshot)
            .GroupBy(controller => controller.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(controller => controller.DisplayName)
            .ToArray();

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started)
        {
            return;
        }

        RawGameController.RawGameControllerAdded += OnControllerAdded;
        RawGameController.RawGameControllerRemoved += OnControllerRemoved;
        started = true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (started)
        {
            RawGameController.RawGameControllerAdded -= OnControllerAdded;
            RawGameController.RawGameControllerRemoved -= OnControllerRemoved;
            started = false;
        }

        disposed = true;
    }

    private void OnControllerAdded(object? sender, RawGameController controller) =>
        ControllerConnected?.Invoke(this, ToSnapshot(controller));

    private void OnControllerRemoved(object? sender, RawGameController controller) =>
        ControllerDisconnected?.Invoke(this, ToSnapshot(controller));

    private static GameControllerSnapshot ToSnapshot(RawGameController controller) =>
        new(
            controller.NonRoamableId,
            string.IsNullOrWhiteSpace(controller.DisplayName) ? "游戏控制器" : controller.DisplayName,
            controller.HardwareVendorId,
            controller.HardwareProductId,
            controller.IsWireless);
}
