using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkStateEvaluatorTests
{
    private static readonly FieldNetworkProfile Profile =
        FieldNetworkProfile.Canonical;

    [Fact]
    public void Evaluate_ReturnsReadyArchitecture_ForCanonicalState()
    {
        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                CanonicalObservation());

        state.CaptureAdapterPresent.Should().BeTrue();
        state.CaptureAddressCorrect.Should().BeTrue();
        state.CaptureGatewayAbsent.Should().BeTrue();
        state.CaptureSubnetRoutePresent.Should().BeTrue();
        state.DefaultRouteOutsideCaptureAdapter.Should().BeTrue();
        state.LocalArchitectureReady.Should().BeTrue();

        state.Camera.Reachable.Should().BeTrue();
        state.Audio.Reachable.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RejectsCaptureAddressDrift()
    {
        var observation =
            CanonicalObservation(
                captureAddress: "192.168.1.15");

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.CaptureAdapterPresent.Should().BeTrue();
        state.ObservedCaptureAddress.Should().Be("192.168.1.15");
        state.CaptureAddressCorrect.Should().BeFalse();
        state.LocalArchitectureReady.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_RejectsCaptureGateway()
    {
        var observation =
            CanonicalObservation(
                captureGateways:
                    new[] { "192.168.1.1" });

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.CaptureGatewayAbsent.Should().BeFalse();
        state.LocalArchitectureReady.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_RejectsMissingCaptureSubnetRoute()
    {
        var observation =
            CanonicalObservation(
                includeCaptureRoute: false);

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.CaptureSubnetRoutePresent.Should().BeFalse();
        state.LocalArchitectureReady.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_RejectsDefaultRouteThroughCaptureAdapter()
    {
        var observation =
            CanonicalObservation(
                includeCaptureDefaultRoute: true);

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.DefaultRouteOutsideCaptureAdapter.Should().BeFalse();
        state.LocalArchitectureReady.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_UsesMacAsCorroboratingAdapterIdentity()
    {
        var observation =
            CanonicalObservation(
                captureAlias: "Ethernet 7");

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.CaptureAdapterPresent.Should().BeTrue();
        state.CaptureAdapterName.Should().Be("Ethernet 7");
        state.LocalArchitectureReady.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ReportsEndpointFailureWithoutInvalidatingLocalArchitecture()
    {
        var observation =
            CanonicalObservation(
                cameraReachable: false);

        var state =
            FieldNetworkStateEvaluator.Evaluate(
                Profile,
                observation);

        state.LocalArchitectureReady.Should().BeTrue();
        state.Camera.Reachable.Should().BeFalse();
        state.Audio.Reachable.Should().BeTrue();
    }

    private static WindowsFieldNetworkObservation CanonicalObservation(
        string captureAddress = "192.168.1.10",
        string captureAlias = "AMHARC Capture",
        IReadOnlyList<string>? captureGateways = null,
        bool includeCaptureRoute = true,
        bool includeCaptureDefaultRoute = false,
        bool cameraReachable = true,
        bool audioReachable = true)
    {
        var adapters =
            new[]
            {
                new WindowsNetworkAdapterObservation(
                    InterfaceIndex: 7,
                    Alias: captureAlias,
                    Description:
                        "ASIX USB to Gigabit Ethernet Family Adapter",
                    MacAddress:
                        "74-78-27-AE-5E-F8",
                    IsUp: true,
                    Addresses:
                        new[]
                        {
                            new WindowsIpv4AddressObservation(
                                captureAddress,
                                24)
                        },
                    Gateways:
                        captureGateways ??
                        Array.Empty<string>()),

                new WindowsNetworkAdapterObservation(
                    InterfaceIndex: 22,
                    Alias: "Wi-Fi",
                    Description: "Wireless",
                    MacAddress: "00-11-22-33-44-55",
                    IsUp: true,
                    Addresses:
                        new[]
                        {
                            new WindowsIpv4AddressObservation(
                                "192.168.0.67",
                                24)
                        },
                    Gateways:
                        new[] { "192.168.0.1" })
            };

        var routes =
            new List<WindowsRouteObservation>
            {
                new(
                    22,
                    "Wi-Fi",
                    "0.0.0.0/0",
                    "192.168.0.1")
            };

        if (includeCaptureRoute)
        {
            routes.Add(
                new(
                    7,
                    captureAlias,
                    "192.168.1.0/24",
                    "0.0.0.0"));
        }

        if (includeCaptureDefaultRoute)
        {
            routes.Add(
                new(
                    7,
                    captureAlias,
                    "0.0.0.0/0",
                    "192.168.1.1"));
        }

        return new WindowsFieldNetworkObservation(
            DateTimeOffset.Parse(
                "2026-09-09T16:00:00Z"),
            adapters,
            routes,
            new WindowsEndpointObservation(
                "192.168.1.135",
                554,
                cameraReachable,
                "camera"),
            new WindowsEndpointObservation(
                "192.168.1.136",
                554,
                audioReachable,
                "audio"));
    }
}