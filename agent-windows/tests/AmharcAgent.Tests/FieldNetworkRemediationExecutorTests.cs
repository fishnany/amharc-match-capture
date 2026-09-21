using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkRemediationExecutorTests
{
    private static readonly FieldNetworkProfile Profile = FieldNetworkProfile.Canonical;
    private readonly FieldNetworkRemediationExecutor _sut = new();

    [Fact]
    public void DryRun_VerifiesExactFreshRepairPlan()
    {
        var observation = Observation(address: "192.168.1.15");
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, observation);

        var result = _sut.ExecuteDryRun(Profile, observation, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.DryRunVerified);
        result.ValidatedActions.Select(x => x.Kind)
            .Should().ContainSingle()
            .Which.Should().Be(FieldNetworkRepairActionKind.SetCaptureIpv4);
    }

    [Fact]
    public void DryRun_ReturnsNoActionForCanonicalState()
    {
        var observation = Observation();
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, observation);

        var result = _sut.ExecuteDryRun(Profile, observation, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.NoAction);
        result.ValidatedActions.Should().BeEmpty();
    }

    [Fact]
    public void DryRun_RefusesChangedHardwareMac()
    {
        var original = Observation(address: "192.168.1.15");
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, original);
        var fresh = Observation(address: "192.168.1.15", mac: "AA-BB-CC-DD-EE-FF");

        var result = _sut.ExecuteDryRun(Profile, fresh, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_RefusesChangedAdapterAlias()
    {
        var original = Observation(address: "192.168.1.15");
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, original);
        var fresh = Observation(address: "192.168.1.15", alias: "Ethernet 7");

        var result = _sut.ExecuteDryRun(Profile, fresh, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_RefusesStalePlanWhenMachineStateChanged()
    {
        var original = Observation(address: "192.168.1.15");
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, original);
        var fresh = Observation();

        var result = _sut.ExecuteDryRun(Profile, fresh, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_RefusesWhenExternalDefaultRouteDisappears()
    {
        var original = Observation(address: "192.168.1.15");
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, original);
        var fresh = Observation(address: "192.168.1.15", includeExternalDefault: false);

        var result = _sut.ExecuteDryRun(Profile, fresh, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_RefusesPlanBoundToAnotherAdapter()
    {
        var observation = Observation(address: "192.168.1.15");
        var valid = FieldNetworkRemediationPlanner.Plan(Profile, observation);
        var forged = valid with { CaptureAdapterName = "Wi-Fi" };

        var result = _sut.ExecuteDryRun(Profile, observation, forged);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_RefusesExtraActionNotRequiredByFreshState()
    {
        var observation = Observation(address: "192.168.1.15");
        var valid = FieldNetworkRemediationPlanner.Plan(Profile, observation);
        var forged = valid with
        {
            Actions = valid.Actions
                .Append(new(
                    FieldNetworkRepairActionKind.RemoveCaptureGateway,
                    "Forged extra action"))
                .ToArray()
        };

        var result = _sut.ExecuteDryRun(Profile, observation, forged);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.Refused);
    }

    [Fact]
    public void DryRun_DoesNotTreatEndpointOutageAsPermissionToMutateNetwork()
    {
        var observation = Observation(cameraReachable: false, audioReachable: false);
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, observation);

        var result = _sut.ExecuteDryRun(Profile, observation, plan);

        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.NoAction);
    }

    private static WindowsFieldNetworkObservation Observation(
        string address = "192.168.1.10",
        string alias = "AMHARC Capture",
        string mac = "74-78-27-AE-5E-F8",
        bool includeExternalDefault = true,
        bool cameraReachable = true,
        bool audioReachable = true)
    {
        var adapters = new[]
        {
            new WindowsNetworkAdapterObservation(
                7, alias, "ASIX USB to Gigabit Ethernet Family Adapter",
                mac, true,
                new[] { new WindowsIpv4AddressObservation(address, 24) },
                Array.Empty<string>()),
            new WindowsNetworkAdapterObservation(
                22, "Wi-Fi", "Wireless",
                "00-11-22-33-44-55", true,
                new[] { new WindowsIpv4AddressObservation("192.168.0.67", 24) },
                new[] { "192.168.0.1" })
        };

        var routes = new List<WindowsRouteObservation>
        {
            new(7, alias, "192.168.1.0/24", "0.0.0.0")
        };

        if (includeExternalDefault)
            routes.Add(new(22, "Wi-Fi", "0.0.0.0/0", "192.168.0.1"));

        return new(
            DateTimeOffset.Parse("2026-09-09T18:00:00Z"),
            adapters,
            routes,
            new("192.168.1.135", 554, cameraReachable, "camera"),
            new("192.168.1.136", 554, audioReachable, "audio"));
    }
}