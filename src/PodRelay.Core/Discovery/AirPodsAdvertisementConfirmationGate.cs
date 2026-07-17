namespace PodRelay.Core.Discovery;

public enum AirPodsAdvertisementDecision
{
    Accept,
    RejectInvalidSignal,
    AwaitStateConfirmation
}

public sealed class AirPodsAdvertisementConfirmationGate
{
    public const short UnknownSignalStrengthDbm = -127;

    private readonly TimeSpan confirmationWindow;
    private readonly int requiredSamples;
    private readonly Dictionary<ushort, DeviceState> states = [];

    public AirPodsAdvertisementConfirmationGate(
        TimeSpan? confirmationWindow = null,
        int requiredSamples = 2)
    {
        this.confirmationWindow = confirmationWindow ?? TimeSpan.FromSeconds(2);
        if (this.confirmationWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmationWindow),
                "The confirmation window must be positive.");
        }

        if (requiredSamples < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredSamples),
                "At least two samples are required for state confirmation.");
        }

        this.requiredSamples = requiredSamples;
    }

    public AirPodsAdvertisementDecision Evaluate(
        ushort modelCode,
        AirPodsWearState wearState,
        short signalStrengthDbm,
        DateTimeOffset observedAt)
    {
        if (signalStrengthDbm == UnknownSignalStrengthDbm)
        {
            return AirPodsAdvertisementDecision.RejectInvalidSignal;
        }

        if (!states.TryGetValue(modelCode, out var state))
        {
            states[modelCode] = new DeviceState(wearState);
            return AirPodsAdvertisementDecision.Accept;
        }

        if (wearState is AirPodsWearState.InCase or AirPodsWearState.Unknown)
        {
            state.Accept(wearState);
            return AirPodsAdvertisementDecision.Accept;
        }

        if (state.LastAcceptedWearState != AirPodsWearState.InCase)
        {
            state.Accept(wearState);
            return AirPodsAdvertisementDecision.Accept;
        }

        if (state.PendingWearState != wearState ||
            observedAt < state.PendingSince ||
            observedAt - state.PendingSince > confirmationWindow)
        {
            state.BeginConfirmation(wearState, observedAt);
            return AirPodsAdvertisementDecision.AwaitStateConfirmation;
        }

        state.PendingSamples++;
        if (state.PendingSamples < requiredSamples)
        {
            return AirPodsAdvertisementDecision.AwaitStateConfirmation;
        }

        state.Accept(wearState);
        return AirPodsAdvertisementDecision.Accept;
    }

    private sealed class DeviceState(AirPodsWearState initialWearState)
    {
        public AirPodsWearState LastAcceptedWearState { get; private set; } = initialWearState;
        public AirPodsWearState PendingWearState { get; private set; } = AirPodsWearState.Unknown;
        public DateTimeOffset PendingSince { get; private set; } = DateTimeOffset.MinValue;
        public int PendingSamples { get; set; }

        public void BeginConfirmation(AirPodsWearState wearState, DateTimeOffset observedAt)
        {
            PendingWearState = wearState;
            PendingSince = observedAt;
            PendingSamples = 1;
        }

        public void Accept(AirPodsWearState wearState)
        {
            LastAcceptedWearState = wearState;
            PendingWearState = AirPodsWearState.Unknown;
            PendingSince = DateTimeOffset.MinValue;
            PendingSamples = 0;
        }
    }
}
