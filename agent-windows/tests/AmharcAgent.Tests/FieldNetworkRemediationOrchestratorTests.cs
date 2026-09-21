using Xunit;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;
using FluentAssertions;

namespace AmharcAgent.Tests;

public sealed class FieldNetworkRemediationOrchestratorTests
{
    private static readonly FieldNetworkProfile Profile = FieldNetworkProfile.Canonical;

    [Fact]
    public async Task RemediateAsync_HealthyState_IsNoActionAndDoesNotMutate()
    {
        var healthy = Observation();
        var observer = new SequenceObserver(healthy);
        var authority = new RecordingAuthority();
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.NoAction);
        result.FinalState.LocalArchitectureReady.Should().BeTrue();
        authority.Calls.Should().Be(0);
        observer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RemediateAsync_RefusedPlan_DoesNotMutate()
    {
        var unsafeObservation = Observation(includeExternalDefault: false);
        var observer = new SequenceObserver(unsafeObservation);
        var authority = new RecordingAuthority();
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.Refused);
        authority.Calls.Should().Be(0);
        observer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RemediateAsync_StateChangesBeforeDryRun_IsRefusedAndDoesNotMutate()
    {
        var faulty = Observation(extraAddress: true);
        var changed = Observation(includeExternalDefault: false);
        var observer = new SequenceObserver(faulty, changed);
        var authority = new RecordingAuthority();
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.Refused);
        authority.Calls.Should().Be(0);
        observer.Calls.Should().Be(2);
    }

    [Fact]
    public async Task RemediateAsync_BoundedIpv4Fault_AppliesAndReobservesRecovery()
    {
        var faulty = Observation(extraAddress: true);
        var recovered = Observation();
        var observer = new SequenceObserver(faulty, faulty, recovered);
        var authority = new RecordingAuthority(
            FieldNetworkMutationExecutionStatus.Applied);
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.Applied);
        result.DryRun!.Status.Should().Be(FieldNetworkRemediationExecutionStatus.DryRunVerified);
        result.Commands.Should().ContainSingle();
        result.Commands[0].Kind.Should().Be(
            FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4);
        result.Mutation!.Status.Should().Be(FieldNetworkMutationExecutionStatus.Applied);
        result.FinalState.LocalArchitectureReady.Should().BeTrue();
        authority.Calls.Should().Be(1);
        observer.Calls.Should().Be(3);
    }

    [Fact]
    public async Task RemediateAsync_AuthorityFailure_ReobservesAndReturnsFailed()
    {
        var faulty = Observation(extraAddress: true);
        var observer = new SequenceObserver(faulty, faulty, faulty);
        var authority = new RecordingAuthority(
            FieldNetworkMutationExecutionStatus.Failed);
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.Failed);
        result.FinalState.LocalArchitectureReady.Should().BeTrue();
        // Extra-address uniqueness is a remediation-planner invariant, not a
        // FieldNetworkState.LocalArchitectureReady invariant.
        authority.Calls.Should().Be(1);
        observer.Calls.Should().Be(3);
    }

    [Fact]
    public async Task RemediateAsync_AppliedButStillFaulty_FailsPostVerification()
    {
        var faulty = Observation(extraAddress: true);
        var observer = new SequenceObserver(faulty, faulty, faulty);
        var authority = new RecordingAuthority(
            FieldNetworkMutationExecutionStatus.Applied);
        var sut = Create(observer, authority);

        var result = await sut.RemediateAsync();

        result.Status.Should().Be(FieldNetworkRemediationOrchestrationStatus.Failed);
        result.Reason.Should().Contain("not verified");
        result.FinalState.LocalArchitectureReady.Should().BeTrue();
        // Extra-address uniqueness is a remediation-planner invariant, not a
        // FieldNetworkState.LocalArchitectureReady invariant.
    }

    private static FieldNetworkRemediationOrchestrator Create(
        IWindowsFieldNetworkObserver observer,
        IFieldNetworkMutationAuthority authority) =>
        new(
            observer,
            new FieldNetworkRemediationExecutor(),
            new FieldNetworkMutationCommandBuilder(),
            authority);

    private static WindowsFieldNetworkObservation Observation(
        bool extraAddress = false,
        bool includeExternalDefault = true)
    {
        var addresses = new List<WindowsIpv4AddressObservation>
        {
            new(Profile.CaptureAddress, Profile.CapturePrefixLength)
        };
        if (extraAddress)
            addresses.Add(new("192.168.1.11", 24));

        var adapter = new WindowsNetworkAdapterObservation(
            7,
            Profile.CaptureAdapterAlias,
            Profile.CaptureAdapterDescription,
            Profile.CaptureAdapterMacAddress,
            true,
            addresses,
            Array.Empty<string>());

        var routes = new List<WindowsRouteObservation>
        {
            new(7, Profile.CaptureAdapterAlias, Profile.CaptureSubnet, "0.0.0.0")
        };
        if (includeExternalDefault)
            routes.Add(new(22, "Wi-Fi", "0.0.0.0/0", "192.168.0.1"));

        return new(
            DateTimeOffset.UtcNow,
            new[] { adapter },
            routes,
            new(Profile.CameraAddress, Profile.CameraRtspPort, true, "reachable"),
            new(Profile.AudioAddress, Profile.AudioRtspPort, true, "reachable"));
    }

    private sealed class SequenceObserver(
        params WindowsFieldNetworkObservation[] observations)
        : IWindowsFieldNetworkObserver
    {
        private int _index;
        public int Calls { get; private set; }

        public Task<WindowsFieldNetworkObservation> ObserveAsync(
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            var index = Math.Min(_index, observations.Length - 1);
            _index++;
            return Task.FromResult(observations[index]);
        }
    }

    private sealed class RecordingAuthority(
        FieldNetworkMutationExecutionStatus status =
            FieldNetworkMutationExecutionStatus.Applied)
        : IFieldNetworkMutationAuthority
    {
        public int Calls { get; private set; }

        public Task<FieldNetworkMutationExecutionResult> ExecuteAsync(
            IReadOnlyList<FieldNetworkMutationCommand> commands,
            CancellationToken ct = default)
        {
            Calls++;
            var actions = commands.Select(c =>
                new FieldNetworkMutationActionResult(
                    c.Kind,
                    status == FieldNetworkMutationExecutionStatus.Applied,
                    "test")).ToArray();

            return Task.FromResult(new FieldNetworkMutationExecutionResult(
                DateTimeOffset.UtcNow,
                status,
                status.ToString(),
                actions));
        }
    }
}