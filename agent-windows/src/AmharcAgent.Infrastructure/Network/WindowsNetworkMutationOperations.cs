using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Real Windows network mutation provider.
///
/// Default deny:
///  - explicit AMHARC_ENABLE_NETWORK_REMEDIATION=true required;
///  - process elevation required;
///  - canonical adapter alias + MAC + interface index revalidated immediately
///    before every operation;
///  - only typed canonical commands are accepted.
///
/// This provider contains no caller-supplied shell text.
/// </summary>
internal sealed class WindowsNetworkMutationOperations(
    IFieldNetworkMutationExecutionGate gate,
    IWindowsNetworkAdapterIdentityResolver identityResolver,
    IWindowsNetworkCommandRunner runner)
    : IWindowsNetworkMutationOperations
{
    public async Task ApplyAsync(
        FieldNetworkMutationCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "AMHARC network remediation is supported on Windows only.");

        if (!gate.IsExplicitlyEnabled)
            throw new InvalidOperationException(
                "AMHARC network remediation is disabled by default. " +
                "Explicit enablement is required.");

        if (!gate.IsElevated)
            throw new InvalidOperationException(
                "AMHARC network remediation requires an elevated process.");

        var profile =
            FieldNetworkProfile.Canonical;

        var identity =
            identityResolver.ResolveByInterfaceIndex(
                command.InterfaceIndex);

        if (identity is null ||
            !identity.IsUp ||
            !EqualsIgnoreCase(
                identity.Alias,
                profile.CaptureAdapterAlias) ||
            !EqualsIgnoreCase(
                NormaliseMac(identity.MacAddress),
                NormaliseMac(profile.CaptureAdapterMacAddress)) ||
            command.InterfaceIndex != identity.InterfaceIndex ||
            !EqualsIgnoreCase(
                command.AdapterAlias,
                profile.CaptureAdapterAlias))
        {
            throw new InvalidOperationException(
                "Fresh canonical Capture adapter identity verification failed.");
        }

        ValidateCanonical(profile, command);

        var script =
            BuildCanonicalScript(
                profile,
                command);

        await runner.RunPowerShellAsync(
            script,
            ct);
    }

    private static string BuildCanonicalScript(
        FieldNetworkProfile profile,
        FieldNetworkMutationCommand command)
    {
        var index =
            command.InterfaceIndex.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        return command.Kind switch
        {
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4 =>
                "$ErrorActionPreference='Stop'; " +
                $"$i={index}; " +
                "Get-NetIPAddress -InterfaceIndex $i -AddressFamily IPv4 " +
                "-ErrorAction SilentlyContinue | " +
                $"Where-Object {{ $_.IPAddress -ne '{profile.CaptureAddress}' " +
                $" -or $_.PrefixLength -ne {profile.CapturePrefixLength} }} | " +
                "Remove-NetIPAddress -Confirm:$false -ErrorAction Stop; " +
                $"if (-not (Get-NetIPAddress -InterfaceIndex $i " +
                $"-IPAddress '{profile.CaptureAddress}' " +
                "-AddressFamily IPv4 -ErrorAction SilentlyContinue)) { " +
                $"New-NetIPAddress -InterfaceIndex $i " +
                $"-IPAddress '{profile.CaptureAddress}' " +
                $"-PrefixLength {profile.CapturePrefixLength} " +
                "-ErrorAction Stop | Out-Null }",

            FieldNetworkMutationCommandKind.RemoveCaptureGateway =>
                "$ErrorActionPreference='Stop'; " +
                $"$i={index}; " +
                "Get-NetRoute -InterfaceIndex $i -AddressFamily IPv4 " +
                "-DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | " +
                "Remove-NetRoute -Confirm:$false -ErrorAction Stop",

            FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute =>
                "$ErrorActionPreference='Stop'; " +
                $"$i={index}; " +
                $"if (-not (Get-NetRoute -InterfaceIndex $i " +
                $"-DestinationPrefix '{profile.CaptureSubnet}' " +
                "-AddressFamily IPv4 -ErrorAction SilentlyContinue)) { " +
                $"New-NetRoute -InterfaceIndex $i " +
                $"-DestinationPrefix '{profile.CaptureSubnet}' " +
                "-NextHop '0.0.0.0' -ErrorAction Stop | Out-Null }",

            FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute =>
                "$ErrorActionPreference='Stop'; " +
                $"$i={index}; " +
                "Get-NetRoute -InterfaceIndex $i -AddressFamily IPv4 " +
                "-DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | " +
                "Remove-NetRoute -Confirm:$false -ErrorAction Stop",

            _ =>
                throw new InvalidOperationException(
                    "Unsupported Windows network mutation command.")
        };
    }

    private static void ValidateCanonical(
        FieldNetworkProfile profile,
        FieldNetworkMutationCommand command)
    {
        switch (command.Kind)
        {
            case FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4:
                if (command.Address != profile.CaptureAddress ||
                    command.PrefixLength != profile.CapturePrefixLength ||
                    command.DestinationPrefix is not null ||
                    command.NextHop is not null)
                    throw new InvalidOperationException(
                        "Non-canonical IPv4 mutation rejected.");
                break;

            case FieldNetworkMutationCommandKind.RemoveCaptureGateway:
                if (command.Address is not null ||
                    command.PrefixLength is not null ||
                    command.DestinationPrefix is not null ||
                    command.NextHop is not null)
                    throw new InvalidOperationException(
                        "Invalid gateway-removal shape rejected.");
                break;

            case FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute:
                if (command.DestinationPrefix != profile.CaptureSubnet ||
                    command.NextHop != "0.0.0.0" ||
                    command.Address is not null ||
                    command.PrefixLength is not null)
                    throw new InvalidOperationException(
                        "Non-canonical subnet-route mutation rejected.");
                break;

            case FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute:
                if (command.DestinationPrefix != "0.0.0.0/0" ||
                    command.Address is not null ||
                    command.PrefixLength is not null ||
                    command.NextHop is not null)
                    throw new InvalidOperationException(
                        "Non-canonical default-route mutation rejected.");
                break;

            default:
                throw new InvalidOperationException(
                    "Unknown mutation command rejected.");
        }
    }

    private static bool EqualsIgnoreCase(
        string? left,
        string? right) =>
        string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);

    private static string NormaliseMac(
        string? value) =>
        (value ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
}