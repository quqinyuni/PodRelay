namespace PodRelay.Core.Discovery;

public static class AirPodsAdvertisementClassifier
{
    public const ushort AppleCompanyId = 0x004c;

    public static bool IsAirPodsFrame(ushort companyId, IReadOnlyList<byte> payload) =>
        companyId == AppleCompanyId &&
        payload.Count >= 11 &&
        payload[0] == 0x07 &&
        payload[1] is 0x19 or 0x13 &&
        payload[2] == 0x01;

    public static bool TryDecodePublicStatus(
        ushort companyId,
        IReadOnlyList<byte> payload,
        out AirPodsPublicStatus? status)
    {
        status = null;
        if (!IsAirPodsFrame(companyId, payload))
        {
            return false;
        }

        var modelCode = (ushort)((payload[3] << 8) | payload[4]);
        var rawStatus = payload[5];
        var anyPodInEar = (rawStatus & ((1 << 1) | (1 << 3))) != 0;
        var hasCaseContext = (rawStatus & ((1 << 2) | (1 << 4) | (1 << 6))) != 0;
        var wearState = anyPodInEar
            ? AirPodsWearState.InEar
            : hasCaseContext
                ? AirPodsWearState.InCase
                : AirPodsWearState.OutOfCase;

        status = new AirPodsPublicStatus(
            modelCode,
            rawStatus,
            anyPodInEar,
            hasCaseContext,
            wearState);
        return true;
    }

    public static bool IsLikelyTargetModel(
        string targetDisplayName,
        ushort modelCode,
        ushort? targetModelCode = null)
    {
        if (targetModelCode is not null)
        {
            return modelCode == targetModelCode;
        }

        if (targetDisplayName.Contains("Pro2", StringComparison.OrdinalIgnoreCase) ||
            targetDisplayName.Contains("Pro 2", StringComparison.OrdinalIgnoreCase))
        {
            return modelCode is 0x1420 or 0x2420;
        }

        if (targetDisplayName.Contains("AirPods Pro", StringComparison.OrdinalIgnoreCase))
        {
            return modelCode == 0x0E20;
        }

        return true;
    }
}
