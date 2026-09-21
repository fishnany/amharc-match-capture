using System.Security.Principal;

namespace AmharcAgent.Infrastructure.Network;

internal interface IFieldNetworkMutationExecutionGate
{
    bool IsExplicitlyEnabled { get; }
    bool IsElevated { get; }
}

internal sealed class WindowsFieldNetworkMutationExecutionGate
    : IFieldNetworkMutationExecutionGate
{
    public bool IsExplicitlyEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "AMHARC_ENABLE_NETWORK_REMEDIATION"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public bool IsElevated
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;

            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);

            return principal.IsInRole(
                WindowsBuiltInRole.Administrator);
        }
    }
}