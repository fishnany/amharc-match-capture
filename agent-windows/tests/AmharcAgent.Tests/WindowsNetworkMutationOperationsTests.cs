using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class WindowsNetworkMutationOperationsTests
{
    [Fact]
    public async Task ApplyAsync_RefusesWhenExplicitEnablementIsOff()
    {
        var runner = new FakeRunner();
        var sut = Create(
            enabled: false,
            elevated: true,
            runner: runner);

        var act = () => sut.ApplyAsync(CanonicalIpv4());

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_RefusesWhenNotElevated()
    {
        var runner = new FakeRunner();
        var sut = Create(
            enabled: true,
            elevated: false,
            runner: runner);

        var act = () => sut.ApplyAsync(CanonicalIpv4());

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_RefusesWrongHardwareMac()
    {
        var runner = new FakeRunner();
        var sut = Create(
            enabled: true,
            elevated: true,
            runner: runner,
            mac: "AA-BB-CC-DD-EE-FF");

        var act = () => sut.ApplyAsync(CanonicalIpv4());

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_RefusesWrongAdapterAlias()
    {
        var runner = new FakeRunner();
        var sut = Create(
            enabled: true,
            elevated: true,
            runner: runner,
            alias: "Wi-Fi");

        var act = () => sut.ApplyAsync(CanonicalIpv4());

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_RefusesWrongInterfaceIndex()
    {
        var runner = new FakeRunner();
        var sut = Create(
            enabled: true,
            elevated: true,
            runner: runner,
            interfaceIndex: 22);

        var act = () => sut.ApplyAsync(CanonicalIpv4());

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_RefusesArbitraryIpv4()
    {
        var runner = new FakeRunner();
        var sut = Create(true, true, runner);

        var forged = CanonicalIpv4() with {
            Address = "10.10.10.10",
            PrefixLength = 8
        };

        var act = () => sut.ApplyAsync(forged);

        await act.Should().ThrowAsync<InvalidOperationException>();
        runner.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_BuildsCanonicalIpv4ScriptUsingTypedValues()
    {
        var runner = new FakeRunner();
        var sut = Create(true, true, runner);

        await sut.ApplyAsync(CanonicalIpv4());

        runner.Scripts.Should().ContainSingle();
        runner.Scripts[0].Should().Contain("192.168.1.10");
        runner.Scripts[0].Should().Contain("PrefixLength 24");
        runner.Scripts[0].Should().Contain("InterfaceIndex $i");
        runner.Scripts[0].Should().NotContain("10.10.10.10");
    }

    [Fact]
    public async Task ApplyAsync_BuildsCanonicalSubnetRouteOnly()
    {
        var runner = new FakeRunner();
        var sut = Create(true, true, runner);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute,
            7, "AMHARC Capture",
            null, null,
            "192.168.1.0/24",
            "0.0.0.0");

        await sut.ApplyAsync(command);

        runner.Scripts.Should().ContainSingle();
        runner.Scripts[0].Should().Contain("192.168.1.0/24");
        runner.Scripts[0].Should().Contain("0.0.0.0");
        runner.Scripts[0].Should().NotContain("192.168.1.1");
    }

    [Fact]
    public async Task ApplyAsync_BuildsDefaultRouteRemovalWithoutCreatingGateway()
    {
        var runner = new FakeRunner();
        var sut = Create(true, true, runner);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute,
            7, "AMHARC Capture",
            null, null,
            "0.0.0.0/0",
            null);

        await sut.ApplyAsync(command);

        runner.Scripts.Should().ContainSingle();
        runner.Scripts[0].Should().Contain("Remove-NetRoute");
        runner.Scripts[0].Should().NotContain("New-NetIPAddress");
        runner.Scripts[0].Should().NotContain("192.168.1.1");
    }

    private static WindowsNetworkMutationOperations Create(
        bool enabled,
        bool elevated,
        FakeRunner runner,
        string alias = "AMHARC Capture",
        string mac = "74-78-27-AE-5E-F8",
        int interfaceIndex = 7)
    {
        return new(
            new FakeGate(enabled, elevated),
            new FakeIdentityResolver(
                new(
                    interfaceIndex,
                    alias,
                    "ASIX USB to Gigabit Ethernet Family Adapter",
                    mac,
                    true)),
            runner);
    }

    private static FieldNetworkMutationCommand CanonicalIpv4() =>
        new(
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
            7,
            "AMHARC Capture",
            "192.168.1.10",
            24,
            null,
            null);

    private sealed class FakeGate(
        bool enabled,
        bool elevated)
        : IFieldNetworkMutationExecutionGate
    {
        public bool IsExplicitlyEnabled => enabled;
        public bool IsElevated => elevated;
    }

    private sealed class FakeIdentityResolver(
        WindowsNetworkAdapterIdentity? identity)
        : IWindowsNetworkAdapterIdentityResolver
    {
        public WindowsNetworkAdapterIdentity? ResolveByInterfaceIndex(
            int interfaceIndex) =>
            identity?.InterfaceIndex == interfaceIndex
                ? identity
                : null;
    }

    private sealed class FakeRunner
        : IWindowsNetworkCommandRunner
    {
        public List<string> Scripts { get; } = new();

        public Task RunPowerShellAsync(
            string script,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Scripts.Add(script);
            return Task.CompletedTask;
        }
    }
}