namespace AmharcAgent.Core.Models;

public enum FieldNetworkMutationCommandKind
{
    SetCanonicalCaptureIpv4,
    RemoveCaptureGateway,
    EnsureCanonicalCaptureSubnetRoute,
    RemoveCaptureDefaultRoute
}

/// <summary>
/// Declarative command specification only. It is not a shell command and
/// contains no executable text supplied by an API caller.
/// </summary>
public sealed record FieldNetworkMutationCommand(
    FieldNetworkMutationCommandKind Kind,
    int InterfaceIndex,
    string AdapterAlias,
    string? Address,
    int? PrefixLength,
    string? DestinationPrefix,
    string? NextHop);