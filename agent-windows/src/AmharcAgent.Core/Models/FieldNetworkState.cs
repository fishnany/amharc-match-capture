namespace AmharcAgent.Core.Models;

/// <summary>
/// Read-only observation of the canonical AMHARC field-network state.
///
/// This model describes observed state only. It does not imply that
/// remediation has been attempted or authorised.
/// </summary>
public sealed record FieldNetworkState(
    DateTimeOffset ObservedAt,
    string CaptureSubnet,
    string ExpectedCaptureAddress,
    string? CaptureAdapterName,
    string? ObservedCaptureAddress,
    int? ObservedPrefixLength,
    bool CaptureAdapterPresent,
    bool CaptureAddressCorrect,
    bool CaptureGatewayAbsent,
    bool CaptureSubnetRoutePresent,
    bool DefaultRouteOutsideCaptureAdapter,
    FieldNetworkEndpointState Camera,
    FieldNetworkEndpointState Audio)
{
    /// <summary>
    /// True only when the local Capture network satisfies the canonical
    /// AMHARC addressing and routing invariants.
    ///
    /// Endpoint reachability is deliberately reported separately.
    /// </summary>
    public bool LocalArchitectureReady =>
        CaptureAdapterPresent &&
        CaptureAddressCorrect &&
        CaptureGatewayAbsent &&
        CaptureSubnetRoutePresent &&
        DefaultRouteOutsideCaptureAdapter;
}

/// <summary>
/// Observation of a fixed endpoint on the AMHARC Capture network.
/// </summary>
public sealed record FieldNetworkEndpointState(
    string Name,
    string Address,
    bool Reachable,
    string? Detail = null);
