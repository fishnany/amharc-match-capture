using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkMutationCommandBuilderTests
{
    private static readonly FieldNetworkProfile Profile = FieldNetworkProfile.Canonical;
    private readonly FieldNetworkRemediationExecutor _executor = new();
    private readonly FieldNetworkMutationCommandBuilder _builder = new();

    [Fact]
    public void Build_ProducesCanonicalIpv4Command()
    {
        var observation=Observation(address:"192.168.1.15");
        var result=Verified(observation);
        var commands=_builder.Build(Profile,observation,result);
        commands.Should().ContainSingle();
        commands[0].Kind.Should().Be(FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4);
        commands[0].Address.Should().Be("192.168.1.10");
        commands[0].PrefixLength.Should().Be(24);
        commands[0].AdapterAlias.Should().Be("AMHARC Capture");
    }

    [Fact]
    public void Build_ProducesCanonicalGatewayRemovalOnlyForCaptureAdapter()
    {
        var observation=Observation(gateway:true);
        var commands=_builder.Build(Profile,observation,Verified(observation));
        commands.Should().ContainSingle();
        commands[0].Kind.Should().Be(FieldNetworkMutationCommandKind.RemoveCaptureGateway);
        commands[0].InterfaceIndex.Should().Be(7);
    }

    [Fact]
    public void Build_ProducesCanonicalSubnetRoute()
    {
        var observation=Observation(includeCaptureRoute:false);
        var commands=_builder.Build(Profile,observation,Verified(observation));
        commands.Should().ContainSingle();
        commands[0].DestinationPrefix.Should().Be("192.168.1.0/24");
        commands[0].NextHop.Should().Be("0.0.0.0");
    }

    [Fact]
    public void Build_ProducesCaptureDefaultRouteRemoval()
    {
        var observation=Observation(includeCaptureDefault:true);
        var commands=_builder.Build(Profile,observation,Verified(observation));
        commands.Should().ContainSingle();
        commands[0].DestinationPrefix.Should().Be("0.0.0.0/0");
        commands[0].InterfaceIndex.Should().Be(7);
    }

    [Fact]
    public void Build_DeduplicatesGatewayAndDefaultRouteToSinglePhysicalRemoval()
    {
        var observation = Observation(
            gateway: true,
            includeCaptureDefault: true);

        var result = Verified(observation);

        result.ValidatedActions.Should().Contain(
            a => a.Kind == FieldNetworkRepairActionKind.RemoveCaptureGateway);

        result.ValidatedActions.Should().Contain(
            a => a.Kind == FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute);

        var commands = _builder.Build(Profile, observation, result);

        commands.Should().ContainSingle();

        commands[0].Kind.Should().Be(
            FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute);

        commands[0].DestinationPrefix.Should().Be("0.0.0.0/0");
        commands[0].InterfaceIndex.Should().Be(7);

        commands.Should().NotContain(
            c => c.Kind == FieldNetworkMutationCommandKind.RemoveCaptureGateway);
    }

    [Fact]
    public void Build_RefusesNonVerifiedExecutionResult()
    {
        var observation=Observation(address:"192.168.1.15");
        var forged=new FieldNetworkRemediationExecutionResult(
            observation.ObservedAt,
            FieldNetworkRemediationExecutionStatus.Refused,
            "forged", "AMHARC Capture",
            new[]{new FieldNetworkRepairAction(FieldNetworkRepairActionKind.SetCaptureIpv4,"forged")});

        var act=()=>_builder.Build(Profile,observation,forged);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_RefusesChangedMacAfterDryRun()
    {
        var original=Observation(address:"192.168.1.15");
        var result=Verified(original);
        var changed=Observation(address:"192.168.1.15",mac:"AA-BB-CC-DD-EE-FF");
        var act=()=>_builder.Build(Profile,changed,result);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_RefusesChangedAliasAfterDryRun()
    {
        var original=Observation(address:"192.168.1.15");
        var result=Verified(original);
        var changed=Observation(address:"192.168.1.15",alias:"Wi-Fi");
        var act=()=>_builder.Build(Profile,changed,result);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_UsesProfileNotRepairSummaryForAddress()
    {
        var observation=Observation(address:"192.168.1.15");
        var verified=Verified(observation);
        var tampered=verified with {
            ValidatedActions=new[]{
                new FieldNetworkRepairAction(
                    FieldNetworkRepairActionKind.SetCaptureIpv4,
                    "Set arbitrary attacker-controlled address 10.10.10.10/8")
            }
        };
        var commands=_builder.Build(Profile,observation,tampered);
        commands[0].Address.Should().Be(Profile.CaptureAddress);
        commands[0].PrefixLength.Should().Be(Profile.CapturePrefixLength);
        commands[0].Address.Should().NotBe("10.10.10.10");
    }

    [Fact]
    public void Build_UsesProfileNotRepairSummaryForRoute()
    {
        var observation=Observation(includeCaptureRoute:false);
        var verified=Verified(observation);
        var tampered=verified with {
            ValidatedActions=new[]{
                new FieldNetworkRepairAction(
                    FieldNetworkRepairActionKind.EnsureCaptureSubnetRoute,
                    "route 10.0.0.0/8 via 10.0.0.1")
            }
        };
        var commands=_builder.Build(Profile,observation,tampered);
        commands[0].DestinationPrefix.Should().Be(Profile.CaptureSubnet);
        commands[0].NextHop.Should().Be("0.0.0.0");
    }

    [Fact]
    public void Build_CannotCreateInternetGateway()
    {
        var observation=Observation(includeCaptureRoute:false);
        var command=_builder.Build(Profile,observation,Verified(observation)).Single();
        command.NextHop.Should().Be("0.0.0.0");
        command.NextHop.Should().NotBe("192.168.1.1");
    }

    private FieldNetworkRemediationExecutionResult Verified(WindowsFieldNetworkObservation observation)
    {
        var plan=FieldNetworkRemediationPlanner.Plan(Profile,observation);
        var result=_executor.ExecuteDryRun(Profile,observation,plan);
        result.Status.Should().Be(FieldNetworkRemediationExecutionStatus.DryRunVerified);
        return result;
    }

    private static WindowsFieldNetworkObservation Observation(
        string address="192.168.1.10",
        string alias="AMHARC Capture",
        string mac="74-78-27-AE-5E-F8",
        bool gateway=false,
        bool includeCaptureRoute=true,
        bool includeCaptureDefault=false)
    {
        var adapters=new[]{
            new WindowsNetworkAdapterObservation(
                7,alias,"ASIX USB to Gigabit Ethernet Family Adapter",mac,true,
                new[]{new WindowsIpv4AddressObservation(address,24)},
                gateway ? new[]{"192.168.1.1"} : Array.Empty<string>()),
            new WindowsNetworkAdapterObservation(
                22,"Wi-Fi","Wireless","00-11-22-33-44-55",true,
                new[]{new WindowsIpv4AddressObservation("192.168.0.67",24)},
                new[]{"192.168.0.1"})
        };
        var routes=new List<WindowsRouteObservation>{
            new(22,"Wi-Fi","0.0.0.0/0","192.168.0.1")
        };
        if(includeCaptureRoute) routes.Add(new(7,alias,"192.168.1.0/24","0.0.0.0"));
        if(includeCaptureDefault) routes.Add(new(7,alias,"0.0.0.0/0","192.168.1.1"));
        return new(
            DateTimeOffset.Parse("2026-09-09T18:30:00Z"),adapters,routes,
            new("192.168.1.135",554,true,"camera"),
            new("192.168.1.136",554,true,"audio"));
    }
}
