using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.StreamDeck;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class StreamDeckOwnershipServiceTests
{
    private static AgentSettings MakeSettings(
        bool exclusiveOwnership = true,
        bool closeCompetingSoftware = true)
    {
        return new AgentSettings
        {
            StreamDeck = new StreamDeckConfig
            {
                ExclusiveOwnership = exclusiveOwnership,
                CloseCompetingSoftwareOnStartup = closeCompetingSoftware
            }
        };
    }

    [Fact]
    public async Task InspectAsync_NoCompetingProcess_ReturnsControlled()
    {
        var processManager = new Mock<IStreamDeckProcessManager>();

        processManager
            .Setup(p => p.FindCompetingProcesses())
            .Returns(Array.Empty<StreamDeckProcessInfo>());

        var sut = new StreamDeckOwnershipService(
            MakeSettings(),
            processManager.Object,
            NullLogger<StreamDeckOwnershipService>.Instance);

        var state = await sut.InspectAsync();

        state.Should().Be(StreamDeckOwnershipState.Controlled);
        sut.CompetingProcesses.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CompetingProcess_ReturnsConflicted()
    {
        var processManager = new Mock<IStreamDeckProcessManager>();

        processManager
            .Setup(p => p.FindCompetingProcesses())
            .Returns(
            [
                new StreamDeckProcessInfo(
                    1234,
                    "StreamDeck")
            ]);

        var sut = new StreamDeckOwnershipService(
            MakeSettings(),
            processManager.Object,
            NullLogger<StreamDeckOwnershipService>.Instance);

        var state = await sut.InspectAsync();

        state.Should().Be(StreamDeckOwnershipState.Conflicted);

        sut.CompetingProcesses.Should()
            .ContainSingle()
            .Which.Should()
            .Be("StreamDeck (1234)");
    }

    [Fact]
    public async Task AcquireAsync_AutoCloseDisabled_DoesNotCloseProcess()
    {
        var processManager = new Mock<IStreamDeckProcessManager>();

        processManager
            .Setup(p => p.FindCompetingProcesses())
            .Returns(
            [
                new StreamDeckProcessInfo(
                    1234,
                    "StreamDeck")
            ]);

        var sut = new StreamDeckOwnershipService(
            MakeSettings(
                exclusiveOwnership: true,
                closeCompetingSoftware: false),
            processManager.Object,
            NullLogger<StreamDeckOwnershipService>.Instance);

        var state = await sut.AcquireAsync();

        state.Should().Be(StreamDeckOwnershipState.Conflicted);

        processManager.Verify(
            p => p.CloseProcessAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcquireAsync_AutoCloseEnabled_ClosesProcessAndReturnsControlled()
    {
        var processManager = new Mock<IStreamDeckProcessManager>();

        processManager
            .SetupSequence(p => p.FindCompetingProcesses())
            .Returns(
            [
                new StreamDeckProcessInfo(
                    1234,
                    "StreamDeck")
            ])
            .Returns(
            [
                new StreamDeckProcessInfo(
                    1234,
                    "StreamDeck")
            ])
            .Returns(Array.Empty<StreamDeckProcessInfo>());

        processManager
            .Setup(p => p.CloseProcessAsync(
                1234,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new StreamDeckOwnershipService(
            MakeSettings(),
            processManager.Object,
            NullLogger<StreamDeckOwnershipService>.Instance);

        var state = await sut.AcquireAsync();

        state.Should().Be(StreamDeckOwnershipState.Controlled);

        processManager.Verify(
            p => p.CloseProcessAsync(
                1234,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
