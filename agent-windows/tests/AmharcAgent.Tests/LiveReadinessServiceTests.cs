using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using DomainMatch = AmharcAgent.Core.Domain.Match;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Readiness;
using FluentAssertions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class LiveReadinessServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenCoreFieldAuthoritiesAreReady_ReturnsReady()
    {
        var match =
            new DomainMatch
            {
                MatchId = "match-1",
                HomeTeam = "KILDARE",
                AwayTeam = "DUBLIN"
            };

        var matches =
            new Mock<IMatchRepository>();

        matches
            .Setup(repository =>
                repository.GetByIdAsync(
                    "match-1",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        var clock =
            new Mock<IMatchClockService>();

        clock
            .SetupGet(service => service.State)
            .Returns(
                new ClockState(
                    MatchClockSeconds: 0,
                    RecordingElapsedSeconds: 0,
                    IsRunning: false,
                    CurrentPeriod: 0,
                    PeriodStartTotalMatchElapsedSeconds: null,
                    ClockMode: "count-up",
                    UpdatedAt: DateTimeOffset.UtcNow));

        var storage =
            new Mock<IStorageMonitorService>();

        storage
            .Setup(service =>
                service.CheckAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StorageStatus(
                    TotalBytes: 1_000_000,
                    UsedBytes: 100_000,
                    AvailableBytes: 900_000,
                    AvailableMinutes: 120,
                    RecordingDirectory: @"C:\AmharcRecordings",
                    WarningLevel: StorageWarningLevel.Ok,
                    IsExternalStorage: false));

        var streamDeck =
            new Mock<IStreamDeckService>();

        streamDeck
            .SetupGet(service => service.IsConnected)
            .Returns(true);

        streamDeck
            .SetupGet(service => service.DeviceName)
            .Returns("Stream Deck");

        var ownership =
            new Mock<IStreamDeckOwnershipService>();

        ownership
            .SetupGet(service => service.State)
            .Returns(StreamDeckOwnershipState.Controlled);

        ownership
            .SetupGet(service => service.CompetingProcesses)
            .Returns(Array.Empty<string>());

        var joystick =
            new Mock<IJoystickService>();

        joystick
            .SetupGet(service => service.IsConnected)
            .Returns(true);

        joystick
            .SetupGet(service => service.DeviceName)
            .Returns("AXIS T8311");

        var service =
            new LiveReadinessService(
                matches.Object,
                clock.Object,
                storage.Object,
                streamDeck.Object,
                ownership.Object,
                joystick.Object,
                CreateConnectedCamera().Object,
                CreateReadyRecording().Object,
                CreateReadySettings());

        var state =
            await service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Degraded);
        state.Ready.Should().BeFalse();
        state.MatchId.Should().Be("match-1");
        state.Checks.Should().HaveCount(12);
        state.Findings.Should().ContainSingle(
            finding => finding.Code == "audio.unverified");
    }

    [Fact]
    public async Task EvaluateAsync_WhenMatchIsMissing_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Matches
            .Setup(repository =>
                repository.GetByIdAsync(
                    "missing",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainMatch?)null);

        var state =
            await fixture.Service.EvaluateAsync("missing");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "match.not-found" &&
                finding.Severity == LiveReadinessSeverityV1.Blocking);
    }

    [Fact]
    public async Task EvaluateAsync_WhenStorageIsWarning_ReturnsDegraded()
    {
        var fixture =
            CreateFixture();

        fixture.Storage
            .Setup(service =>
                service.CheckAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StorageStatus(
                    TotalBytes: 1_000_000,
                    UsedBytes: 900_000,
                    AvailableBytes: 100_000,
                    AvailableMinutes: 30,
                    RecordingDirectory: @"C:\AmharcRecordings",
                    WarningLevel: StorageWarningLevel.Warning,
                    IsExternalStorage: false));

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Degraded);
        state.Ready.Should().BeFalse();
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "storage.warning");
    }

    [Fact]
    public async Task EvaluateAsync_WhenStorageIsCritical_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Storage
            .Setup(service =>
                service.CheckAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StorageStatus(
                    TotalBytes: 1_000_000,
                    UsedBytes: 990_000,
                    AvailableBytes: 10_000,
                    AvailableMinutes: 5,
                    RecordingDirectory: @"C:\AmharcRecordings",
                    WarningLevel: StorageWarningLevel.Critical,
                    IsExternalStorage: false));

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "storage.critical");
    }

    [Fact]
    public async Task EvaluateAsync_WhenStreamDeckOwnershipIsConflicted_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Ownership
            .SetupGet(service => service.State)
            .Returns(StreamDeckOwnershipState.Conflicted);

        fixture.Ownership
            .SetupGet(service => service.CompetingProcesses)
            .Returns(new[] { "StreamDeck.exe" });

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "streamdeck.ownership-conflict");
    }

    [Fact]
    public async Task EvaluateAsync_WhenJoystickIsDisconnected_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Joystick
            .SetupGet(service => service.IsConnected)
            .Returns(false);

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "joystick.disconnected");
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotAcquireOwnershipOrMutateClock()
    {
        var fixture =
            CreateFixture();

        await fixture.Service.EvaluateAsync("match-1");

        fixture.Ownership.Verify(
            service =>
                service.AcquireAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);

        fixture.Ownership.Verify(
            service =>
                service.InspectAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);

        fixture.Clock.Verify(
            service => service.Start(),
            Times.Never);

        fixture.Clock.Verify(
            service => service.Pause(),
            Times.Never);

        fixture.Clock.Verify(
            service => service.Resume(),
            Times.Never);

        fixture.Clock.Verify(
            service => service.Reset(),
            Times.Never);
    }

    private static Mock<ICameraAdapter> CreateConnectedCamera()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(service => service.ConnectionState)
            .Returns(CameraConnectionState.Connected);

        camera
            .SetupGet(service => service.CameraId)
            .Returns("primary");

        camera
            .SetupGet(service => service.Model)
            .Returns("Q6128-E");

        camera
            .Setup(service =>
                service.GetStreamUrlAsync(
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync("rtsp://camera/live");

        return camera;
    }

    private static Mock<IRecordingService> CreateReadyRecording()
    {
        var recording =
            new Mock<IRecordingService>();

        recording
            .SetupGet(service => service.State)
            .Returns(RecordingState.Idle);

        recording
            .SetupGet(service => service.SegmentCount)
            .Returns(0);

        recording
            .SetupGet(service => service.ElapsedSeconds)
            .Returns(0);

        return recording;
    }

    private static AgentSettings CreateReadySettings()
    {
        var ffmpegPath =
            Path.Combine(
                Path.GetTempPath(),
                $"amharc-ffmpeg-{Guid.NewGuid():N}.exe");

        File.WriteAllBytes(
            ffmpegPath,
            [0x4D, 0x5A]);

        return new AgentSettings
        {
            FfmpegPath = ffmpegPath
        };
    }
    [Fact]
    public async Task EvaluateAsync_WhenCameraIsDisconnected_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Camera
            .SetupGet(service => service.ConnectionState)
            .Returns(CameraConnectionState.Disconnected);

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "camera.disconnected");
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "video.source-unavailable");
    }

    [Fact]
    public async Task EvaluateAsync_WhenRtspSourceCannotBeProjected_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Camera
            .Setup(service =>
                service.GetStreamUrlAsync(
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "RTSP unavailable"));

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "video.source-unavailable");
    }

    [Fact]
    public async Task EvaluateAsync_WhenResolvedFfmpegIsMissing_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Settings.FfmpegPath =
            Path.Combine(
                Path.GetTempPath(),
                $"missing-{Guid.NewGuid():N}.exe");

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "ffmpeg.unavailable");
    }

    [Fact]
    public async Task EvaluateAsync_WhenRecordingIsError_ReturnsBlocked()
    {
        var fixture =
            CreateFixture();

        fixture.Recording
            .SetupGet(service => service.State)
            .Returns(RecordingState.Error);

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Blocked);
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "recording.error");
    }

    [Fact]
    public async Task EvaluateAsync_WhenMediaAuthoritiesAreOtherwiseReady_AudioKeepsStateDegraded()
    {
        var fixture =
            CreateFixture();

        var state =
            await fixture.Service.EvaluateAsync("match-1");

        state.Status.Should().Be(LiveReadinessStatusV1.Degraded);
        state.Ready.Should().BeFalse();
        state.Findings.Should().Contain(
            finding =>
                finding.Code == "audio.unverified" &&
                finding.Severity == LiveReadinessSeverityV1.Warning);
    }
    private static Fixture CreateFixture()
    {
        var matches =
            new Mock<IMatchRepository>();

        matches
            .Setup(repository =>
                repository.GetByIdAsync(
                    "match-1",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DomainMatch
                {
                    MatchId = "match-1",
                    HomeTeam = "KILDARE",
                    AwayTeam = "DUBLIN"
                });

        var clock =
            new Mock<IMatchClockService>();

        clock
            .SetupGet(service => service.State)
            .Returns(
                new ClockState(
                    MatchClockSeconds: 0,
                    RecordingElapsedSeconds: 0,
                    IsRunning: false,
                    CurrentPeriod: 0,
                    PeriodStartTotalMatchElapsedSeconds: null,
                    ClockMode: "count-up",
                    UpdatedAt: DateTimeOffset.UtcNow));

        var storage =
            new Mock<IStorageMonitorService>();

        storage
            .Setup(service =>
                service.CheckAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new StorageStatus(
                    TotalBytes: 1_000_000,
                    UsedBytes: 100_000,
                    AvailableBytes: 900_000,
                    AvailableMinutes: 120,
                    RecordingDirectory: @"C:\AmharcRecordings",
                    WarningLevel: StorageWarningLevel.Ok,
                    IsExternalStorage: false));

        var streamDeck =
            new Mock<IStreamDeckService>();

        streamDeck
            .SetupGet(service => service.IsConnected)
            .Returns(true);

        streamDeck
            .SetupGet(service => service.DeviceName)
            .Returns("Stream Deck");

        var ownership =
            new Mock<IStreamDeckOwnershipService>();

        ownership
            .SetupGet(service => service.State)
            .Returns(StreamDeckOwnershipState.Controlled);

        ownership
            .SetupGet(service => service.CompetingProcesses)
            .Returns(Array.Empty<string>());

        var joystick =
            new Mock<IJoystickService>();

        joystick
            .SetupGet(service => service.IsConnected)
            .Returns(true);

        joystick
            .SetupGet(service => service.DeviceName)
            .Returns("AXIS T8311");

        var camera =
            CreateConnectedCamera();

        var recording =
            CreateReadyRecording();

        var settings =
            CreateReadySettings();

        var service =
            new LiveReadinessService(
                matches.Object,
                clock.Object,
                storage.Object,
                streamDeck.Object,
                ownership.Object,
                joystick.Object,
                camera.Object,
                recording.Object,
                settings);

        return new Fixture(
            service,
            matches,
            clock,
            storage,
            ownership,
            joystick,
            camera,
            recording,
            settings);
    }

    private sealed record Fixture(
        LiveReadinessService Service,
        Mock<IMatchRepository> Matches,
        Mock<IMatchClockService> Clock,
        Mock<IStorageMonitorService> Storage,
        Mock<IStreamDeckOwnershipService> Ownership,
        Mock<IJoystickService> Joystick,
        Mock<ICameraAdapter> Camera,
        Mock<IRecordingService> Recording,
        AgentSettings Settings);
}
