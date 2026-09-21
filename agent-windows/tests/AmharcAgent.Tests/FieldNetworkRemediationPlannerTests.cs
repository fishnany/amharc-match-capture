using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkRemediationPlannerTests
{
    private static readonly FieldNetworkProfile Profile = FieldNetworkProfile.Canonical;

    [Fact]
    public void Plan_ReturnsNoAction_ForCanonicalState()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(Profile, Observation());
        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.NoAction);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RepairsWrongCaptureAddress()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(address: "192.168.1.15"));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.RepairAvailable);
        plan.Actions.Select(a => a.Kind)
            .Should().Contain(FieldNetworkRepairActionKind.SetCaptureIpv4);
    }

    [Fact]
    public void Plan_RepairsGatewayOnCaptureAdapter()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(gateways: new[] { "192.168.1.1" }));

        plan.Actions.Select(a => a.Kind)
            .Should().Contain(FieldNetworkRepairActionKind.RemoveCaptureGateway);
    }

    [Fact]
    public void Plan_RepairsMissingCaptureSubnetRoute()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(includeCaptureRoute: false));

        plan.Actions.Select(a => a.Kind)
            .Should().Contain(FieldNetworkRepairActionKind.EnsureCaptureSubnetRoute);
    }

    [Fact]
    public void Plan_RepairsDefaultRouteOnCaptureAdapter()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(includeCaptureDefault: true));

        plan.Actions.Select(a => a.Kind)
            .Should().Contain(FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute);
    }

    [Fact]
    public void Plan_RefusesAliasOnlyIdentity()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(mac: "AA-BB-CC-DD-EE-FF"));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.Refused);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RefusesMacOnlyIdentity()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(alias: "Ethernet 7"));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.Refused);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RefusesWhenCaptureAdapterMissing()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(includeCaptureAdapter: false));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.Refused);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_RefusesWhenExternalDefaultRouteMissing()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(includeExternalDefault: false, address: "192.168.1.15"));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.Refused);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_DoesNotTreatEndpointFailureAsNetworkConfigurationRepair()
    {
        var plan = FieldNetworkRemediationPlanner.Plan(
            Profile, Observation(cameraReachable: false, audioReachable: false));

        plan.Disposition.Should().Be(FieldNetworkRemediationDisposition.NoAction);
        plan.Actions.Should().BeEmpty();
    }

    private static WindowsFieldNetworkObservation Observation(
        string address = "192.168.1.10",
        string alias = "AMHARC Capture",
        string mac = "74-78-27-AE-5E-F8",
        IReadOnlyList<string>? gateways = null,
        bool includeCaptureAdapter = true,
        bool includeCaptureRoute = true,
        bool includeCaptureDefault = false,
        bool includeExternalDefault = true,
        bool cameraReachable = true,
        bool audioReachable = true)
    {
        var adapters = new List<WindowsNetworkAdapterObservation>();

        if (includeCaptureAdapter)
        {
            adapters.Add(new(
                7,
                alias,
                "ASIX USB to Gigabit Ethernet Family Adapter",
                mac,
                true,
                new[] { new WindowsIpv4AddressObservation(address, 24) },
                gateways ?? Array.Empty<string>()));
        }

        adapters.Add(new(
            22,
            "Wi-Fi",
            "Wireless",
            "00-11-22-33-44-55",
            true,
            new[] { new WindowsIpv4AddressObservation("192.168.0.67", 24) },
            new[] { "192.168.0.1" }));

        var routes = new List<WindowsRouteObservation>();

        if (includeExternalDefault)
            routes.Add(new(22, "Wi-Fi", "0.0.0.0/0", "192.168.0.1"));

        if (includeCaptureRoute && includeCaptureAdapter)
            routes.Add(new(7, alias, "192.168.1.0/24", "0.0.0.0"));

        if (includeCaptureDefault && includeCaptureAdapter)
            routes.Add(new(7, alias, "0.0.0.0/0", "192.168.1.1"));

        return new(
            DateTimeOffset.Parse("2026-09-09T17:30:00Z"),
            adapters,
            routes,
            new("192.168.1.135", 554, cameraReachable, "camera"),
            new("192.168.1.136", 554, audioReachable, "audio"));
    }
}