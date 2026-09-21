using AmharcAgent.Api.Controllers;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class RecordingControllerTests
{
    private static RecordingController CreateController(
        Mock<IAmharcCommandDispatcher> dispatcher,
        Mock<IRecordingService> recording) =>
        new(
            dispatcher.Object,
            recording.Object,
            NullLogger<RecordingController>.Instance);

    [Fact]
    public async Task StartRecording_DispatchesSemanticRecordingStart()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        recording
            .SetupGet(r => r.OutputDirectory)
            .Returns(@"C:\recordings\match-1");

        var controller =
            CreateController(dispatcher, recording);

        var request =
            new StartRecordingRequest(
                "match-1",
                "camera-1",
                @"C:\custom");

        var result =
            await controller.StartRecording(
                request,
                CancellationToken.None);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(c =>
                    c.CommandId ==
                        AmharcCommandIds.RecordingStart),
                CancellationToken.None),
            Times.Once);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task StartRecording_DoesNotCallRecordingServiceDirectly()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        var controller =
            CreateController(dispatcher, recording);

        await controller.StartRecording(
            new StartRecordingRequest(
                "match-1",
                null,
                null),
            CancellationToken.None);

        recording.Verify(
            r => r.StartRecordingAsync(
                It.IsAny<RecordingOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StopRecording_DispatchesSemanticRecordingStop()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        var controller =
            CreateController(dispatcher, recording);

        var result =
            await controller.StopRecording(
                CancellationToken.None);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(c =>
                    c.CommandId ==
                        AmharcCommandIds.RecordingStop),
                CancellationToken.None),
            Times.Once);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task StopRecording_DoesNotCallRecordingServiceDirectly()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        var controller =
            CreateController(dispatcher, recording);

        await controller.StopRecording(
            CancellationToken.None);

        recording.Verify(
            r => r.StopRecordingAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GetStatus_DoesNotDispatchCommand()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        var controller =
            CreateController(dispatcher, recording);

        var result = controller.GetStatus();

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task StartRecording_DispatchFailureReturnsBadRequest()
    {
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var recording = new Mock<IRecordingService>();

        dispatcher
            .Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "recording start failed"));

        var controller =
            CreateController(dispatcher, recording);

        var result =
            await controller.StartRecording(
                new StartRecordingRequest(
                    "match-1",
                    null,
                    null),
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartRecording_PreservesCompleteSemanticCommandEnvelope()
    {
        var dispatcher =
            new Mock<IAmharcCommandDispatcher>();

        var recording =
            new Mock<IRecordingService>();

        AmharcCommand? captured =
            null;

        dispatcher
            .Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<AmharcCommand, CancellationToken>(
                (command, _) =>
                    captured = command)
            .Returns(Task.CompletedTask);

        var controller =
            CreateController(
                dispatcher,
                recording);

        var request =
            new StartRecordingRequest(
                "match-42",
                "CAM-42",
                @"D:\AMHARC\match-42");

        var result =
            await controller.StartRecording(
                request,
                CancellationToken.None);

        Assert.IsType<OkObjectResult>(
            result);

        Assert.NotNull(
            captured);

        Assert.Equal(
            AmharcCommandIds.RecordingStart,
            captured!.CommandId);

        Assert.Equal(
            EventSource.Api,
            captured.Source);

        Assert.Equal(
            "match-42",
            captured.MatchId);

        Assert.NotNull(
            captured.Parameters);

        Assert.True(
            captured.Parameters!
                .TryGetValue(
                    "cameraId",
                    out var cameraId));

        Assert.Equal(
            "CAM-42",
            cameraId);

        Assert.True(
            captured.Parameters
                .TryGetValue(
                    "outputDirectory",
                    out var outputDirectory));

        Assert.Equal(
            @"D:\AMHARC\match-42",
            outputDirectory);

        Assert.False(
            captured.Parameters
                .ContainsKey(
                    "matchId"));
    }


    [Fact]
    public async Task StopRecording_UsesApiSemanticCommandEnvelope()
    {
        var dispatcher =
            new Mock<IAmharcCommandDispatcher>();

        var recording =
            new Mock<IRecordingService>();

        AmharcCommand? captured =
            null;

        dispatcher
            .Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<AmharcCommand, CancellationToken>(
                (command, _) =>
                    captured = command)
            .Returns(Task.CompletedTask);

        var controller =
            CreateController(
                dispatcher,
                recording);

        var result =
            await controller.StopRecording(
                CancellationToken.None);

        Assert.IsType<OkObjectResult>(
            result);

        Assert.NotNull(
            captured);

        Assert.Equal(
            AmharcCommandIds.RecordingStop,
            captured!.CommandId);

        Assert.Equal(
            EventSource.Api,
            captured.Source);

        Assert.Null(
            captured.MatchId);

        Assert.True(
            captured.Parameters is null ||
            captured.Parameters.Count == 0);
    }
}
