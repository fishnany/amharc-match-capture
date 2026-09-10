using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class AudioRuntimeHealthServiceTests
{
    [Fact]
    public void Current_BeforeObservation_IsUnknown()
    {
        var service = new AudioRuntimeHealthService();
        service.Current.Status.Should().Be(AudioRuntimeHealthStatus.Unknown);
        service.Current.IsReady.Should().BeFalse();
    }

    [Fact]
    public void FullLiveEvidence_IsReady()
    {
        var state = CreateState(true, true, true, true);
        state.Status.Should().Be(AudioRuntimeHealthStatus.Ready);
        state.IsReady.Should().BeTrue();
        state.Codec.Should().Be("mpeg4-generic");
        state.SampleRateHz.Should().Be(8000);
        state.Channels.Should().Be(1);
        state.PayloadType.Should().Be(97);
    }

    [Fact]
    public void MissingMediaFlow_IsDegraded()
    {
        var state = CreateState(true, true, true, false);
        state.Status.Should().Be(AudioRuntimeHealthStatus.Degraded);
        state.IsReady.Should().BeFalse();
    }

    [Fact]
    public void UnreachableEndpoint_IsBlocked()
    {
        var state = CreateState(false, false, false, false);
        state.Status.Should().Be(AudioRuntimeHealthStatus.Blocked);
    }

    [Fact]
    public void OlderObservation_DoesNotReplaceNewerEvidence()
    {
        var service = new AudioRuntimeHealthService();
        var newer = CreateState(
            true, true, true, true,
            new DateTimeOffset(2026, 9, 10, 17, 0, 5, TimeSpan.Zero));
        var older = CreateState(
            false, false, false, false,
            new DateTimeOffset(2026, 9, 10, 17, 0, 0, TimeSpan.Zero));

        service.Publish(newer);
        service.Publish(older);

        service.Current.Should().BeSameAs(newer);
    }

    private static AudioRuntimeHealthState CreateState(
        bool endpointReachable,
        bool authenticated,
        bool audioAdvertised,
        bool mediaFlowObserved,
        DateTimeOffset? observedAtUtc = null) =>
        AudioRuntimeHealthState.Create(
            "192.168.1.136", 554,
            endpointReachable, authenticated,
            audioAdvertised, mediaFlowObserved,
            "mpeg4-generic", 8000, 1, 97,
            observedAtUtc);
}
