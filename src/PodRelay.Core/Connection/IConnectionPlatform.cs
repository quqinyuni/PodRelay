namespace PodRelay.Core.Connection;

public interface IConnectionPlatform
{
    Task<ConnectionObservation> ObserveAsync(TargetDevice target, CancellationToken cancellationToken);

    Task<ReconnectRequestResult> RequestReconnectAsync(TargetDevice target, CancellationToken cancellationToken);

    Task SetDefaultOutputAsync(string endpointId, CancellationToken cancellationToken);
}

