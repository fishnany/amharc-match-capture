using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkMutationAuthorityTests
{
    [Fact]
    public async Task ExecuteAsync_NoCommands_ReturnsNoAction()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);

        var result = await sut.ExecuteAsync(Array.Empty<FieldNetworkMutationCommand>());

        result.Status.Should().Be(FieldNetworkMutationExecutionStatus.NoAction);
        ops.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_AppliesCanonicalIpv4Command()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
            7,
            "AMHARC Capture",
            "192.168.1.10",
            24,
            null,
            null);

        var result = await sut.ExecuteAsync(new[] { command });

        result.Status.Should().Be(FieldNetworkMutationExecutionStatus.Applied);
        ops.Applied.Should().ContainSingle().Which.Should().Be(command);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsWrongAdapterAlias()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
            22,
            "Wi-Fi",
            "192.168.1.10",
            24,
            null,
            null);

        var act = () => sut.ExecuteAsync(new[] { command });

        await act.Should().ThrowAsync<InvalidOperationException>();
        ops.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_RejectsArbitraryIpv4()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
            7,
            "AMHARC Capture",
            "10.10.10.10",
            8,
            null,
            null);

        var act = () => sut.ExecuteAsync(new[] { command });

        await act.Should().ThrowAsync<InvalidOperationException>();
        ops.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_RejectsCaptureGatewayCreationShape()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute,
            7,
            "AMHARC Capture",
            null,
            null,
            "192.168.1.0/24",
            "192.168.1.1");

        var act = () => sut.ExecuteAsync(new[] { command });

        await act.Should().ThrowAsync<InvalidOperationException>();
        ops.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_StopsAfterFirstOperationFailure()
    {
        var ops = new FakeWindowsNetworkMutationOperations {
            FailOn = FieldNetworkMutationCommandKind.RemoveCaptureGateway
        };
        var sut = new FieldNetworkMutationAuthority(ops);

        var commands = new[]
        {
            new FieldNetworkMutationCommand(
                FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
                7, "AMHARC Capture", "192.168.1.10", 24, null, null),
            new FieldNetworkMutationCommand(
                FieldNetworkMutationCommandKind.RemoveCaptureGateway,
                7, "AMHARC Capture", null, null, null, null),
            new FieldNetworkMutationCommand(
                FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute,
                7, "AMHARC Capture", null, null, "0.0.0.0/0", null)
        };

        var result = await sut.ExecuteAsync(commands);

        result.Status.Should().Be(FieldNetworkMutationExecutionStatus.Failed);
        ops.Applied.Should().ContainSingle();
        ops.Applied[0].Kind.Should().Be(FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4);
        result.Actions.Should().HaveCount(2);
        result.Actions.Last().Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_HonoursCancellation()
    {
        var ops = new FakeWindowsNetworkMutationOperations();
        var sut = new FieldNetworkMutationAuthority(ops);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new FieldNetworkMutationCommand(
            FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute,
            7, "AMHARC Capture", null, null, "0.0.0.0/0", null);

        var act = () => sut.ExecuteAsync(new[] { command }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ops.Applied.Should().BeEmpty();
    }
}