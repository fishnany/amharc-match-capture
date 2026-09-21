namespace AmharcAgent.Core.Models;

/// <summary>
/// Canonical AMHARC field-network profile.
///
/// The interface index is intentionally not persisted because Windows may
/// allocate a different index after restart, driver changes, or re-enumeration.
/// </summary>
public sealed record FieldNetworkProfile(
    string CaptureAdapterAlias,
    string CaptureAdapterDescription,
    string CaptureAdapterMacAddress,
    string CaptureSubnet,
    string CaptureAddress,
    int CapturePrefixLength,
    string CameraAddress,
    int CameraRtspPort,
    string AudioAddress,
    int AudioRtspPort)
{
    public static FieldNetworkProfile Canonical { get; } =
        new(
            CaptureAdapterAlias: "AMHARC Capture",
            CaptureAdapterDescription: "ASIX USB to Gigabit Ethernet Family Adapter",
            CaptureAdapterMacAddress: "74-78-27-AE-5E-F8",
            CaptureSubnet: "192.168.1.0/24",
            CaptureAddress: "192.168.1.10",
            CapturePrefixLength: 24,
            CameraAddress: "192.168.1.135",
            CameraRtspPort: 554,
            AudioAddress: "192.168.1.136",
            AudioRtspPort: 554);
}