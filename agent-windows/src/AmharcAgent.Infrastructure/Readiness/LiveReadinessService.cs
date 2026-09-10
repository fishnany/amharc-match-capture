using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;

namespace AmharcAgent.Infrastructure.Readiness;

/// <summary>
/// Composes an operator-facing live-readiness projection from authoritative
/// Capture subsystem state.
///
/// This service is read-only with respect to those subsystems. It does not
/// connect devices, acquire ownership, start or stop recording, mutate the
/// match, or advance/correct the canonical clock.
/// </summary>
public sealed class LiveReadinessService(
    IMatchRepository matches,
    IMatchClockService clock,
    IStorageMonitorService storage,
    IStreamDeckService streamDeck,
    IStreamDeckOwnershipService streamDeckOwnership,
    IJoystickService joystick,
    ICameraAdapter camera,
    IRecordingService recording,
    IAudioRuntimeHealthService audioRuntimeHealth,
    AgentSettings settings)
    : ILiveReadinessService
{
    public async Task<LiveReadinessStateV1> EvaluateAsync(
        string? matchId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var checks =
            new List<LiveReadinessCheckV1>();

        var findings =
            new List<LiveReadinessFindingV1>();

        checks.Add(
            new(
                LiveReadinessDimensionV1.Agent,
                LiveReadinessStatusV1.Ready,
                Required: true,
                Summary: "Capture Agent is running."));

        var match =
            string.IsNullOrWhiteSpace(matchId)
                ? null
                : await matches.GetByIdAsync(matchId.Trim(), ct);

        if (string.IsNullOrWhiteSpace(matchId))
        {
            checks.Add(
                new(
                    LiveReadinessDimensionV1.Match,
                    LiveReadinessStatusV1.Blocked,
                    Required: true,
                    Summary: "No match selected."));

            findings.Add(
                new(
                    LiveReadinessDimensionV1.Match,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "match.required",
                    Message: "Select a match before entering live operation."));
        }
        else if (match is null)
        {
            checks.Add(
                new(
                    LiveReadinessDimensionV1.Match,
                    LiveReadinessStatusV1.Blocked,
                    Required: true,
                    Summary: "Selected match does not exist.",
                    Detail: matchId.Trim()));

            findings.Add(
                new(
                    LiveReadinessDimensionV1.Match,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "match.not-found",
                    Message: $"Match '{matchId.Trim()}' does not exist."));
        }
        else
        {
            checks.Add(
                new(
                    LiveReadinessDimensionV1.Match,
                    LiveReadinessStatusV1.Ready,
                    Required: true,
                    Summary: $"{match.HomeTeam} v {match.AwayTeam}",
                    Detail: match.MatchId));
        }

        var clockState =
            clock.State;

        var clockStatus =
            clockState.CurrentPeriod < 0 ||
            clockState.MatchClockSeconds < 0 ||
            clockState.RecordingElapsedSeconds < 0
                ? LiveReadinessStatusV1.Blocked
                : LiveReadinessStatusV1.Ready;

        checks.Add(
            new(
                LiveReadinessDimensionV1.Clock,
                clockStatus,
                Required: true,
                Summary:
                    clockStatus == LiveReadinessStatusV1.Ready
                        ? "Canonical match clock is available."
                        : "Canonical match clock state is invalid.",
                Detail:
                    $"period={clockState.CurrentPeriod}; " +
                    $"matchSeconds={clockState.MatchClockSeconds}; " +
                    $"recordingSeconds={clockState.RecordingElapsedSeconds}; " +
                    $"running={clockState.IsRunning}"));

        if (clockStatus == LiveReadinessStatusV1.Blocked)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Clock,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "clock.invalid-state",
                    Message: "Canonical match clock contains invalid negative values."));
        }

        var cameraState =
            camera.ConnectionState;

        var cameraReadiness =
            cameraState switch
            {
                CameraConnectionState.Connected =>
                    LiveReadinessStatusV1.Ready,

                CameraConnectionState.Connecting or
                CameraConnectionState.Reconnecting =>
                    LiveReadinessStatusV1.Degraded,

                _ =>
                    LiveReadinessStatusV1.Blocked
            };

        checks.Add(
            new(
                LiveReadinessDimensionV1.Camera,
                cameraReadiness,
                Required: true,
                Summary:
                    cameraReadiness == LiveReadinessStatusV1.Ready
                        ? "Primary camera is connected."
                        : $"Primary camera is {cameraState}.",
                Detail:
                    $"cameraId={camera.CameraId}; model={camera.Model ?? "unknown"}"));

        if (cameraReadiness == LiveReadinessStatusV1.Degraded)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Camera,
                    LiveReadinessSeverityV1.Warning,
                    Code: "camera.transitioning",
                    Message: $"Primary camera is currently {cameraState}."));
        }
        else if (cameraReadiness == LiveReadinessStatusV1.Blocked)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Camera,
                    LiveReadinessSeverityV1.Blocking,
                    Code:
                        cameraState == CameraConnectionState.Error
                            ? "camera.error"
                            : "camera.disconnected",
                    Message:
                        cameraState == CameraConnectionState.Error
                            ? "Primary camera is in an error state."
                            : "Primary camera is not connected."));
        }

        string? streamUrl =
            null;

        if (cameraState == CameraConnectionState.Connected)
        {
            try
            {
                streamUrl =
                    await camera.GetStreamUrlAsync(
                        ct: ct);
            }
            catch (Exception)
            {
                streamUrl =
                    null;
            }
        }

        var videoReady =
            cameraState == CameraConnectionState.Connected &&
            !string.IsNullOrWhiteSpace(streamUrl);

        checks.Add(
            new(
                LiveReadinessDimensionV1.Video,
                videoReady
                    ? LiveReadinessStatusV1.Ready
                    : LiveReadinessStatusV1.Blocked,
                Required: true,
                Summary:
                    videoReady
                        ? "RTSP video source is available."
                        : "RTSP video source is unavailable.",
                Detail:
                    videoReady
                        ? "Configured camera can provide an RTSP source."
                        : null));

        if (!videoReady)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Video,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "video.source-unavailable",
                    Message: "A usable RTSP video source is required for Capture."));
        }

        var ffmpegPath =
            settings.FfmpegPath;

        var ffmpegReady =
            !string.IsNullOrWhiteSpace(ffmpegPath) &&
            File.Exists(ffmpegPath);

        checks.Add(
            new(
                LiveReadinessDimensionV1.Ffmpeg,
                ffmpegReady
                    ? LiveReadinessStatusV1.Ready
                    : LiveReadinessStatusV1.Blocked,
                Required: true,
                Summary:
                    ffmpegReady
                        ? "Resolved FFmpeg runtime is available."
                        : "Resolved FFmpeg runtime is unavailable.",
                Detail:
                    string.IsNullOrWhiteSpace(ffmpegPath)
                        ? null
                        : ffmpegPath));

        if (!ffmpegReady)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Ffmpeg,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "ffmpeg.unavailable",
                    Message: "The resolved FFmpeg executable is unavailable."));
        }

        var recordingState =
            recording.State;

        var recordingReadiness =
            recordingState switch
            {
                RecordingState.Error =>
                    LiveReadinessStatusV1.Blocked,

                RecordingState.Starting or
                RecordingState.Rotating or
                RecordingState.Stopping or
                RecordingState.Remuxing or
                RecordingState.Recovering =>
                    LiveReadinessStatusV1.Degraded,

                _ =>
                    LiveReadinessStatusV1.Ready
            };

        checks.Add(
            new(
                LiveReadinessDimensionV1.Recording,
                recordingReadiness,
                Required: true,
                Summary:
                    recordingReadiness == LiveReadinessStatusV1.Ready
                        ? $"Recording pipeline is operational ({recordingState})."
                        : $"Recording pipeline is {recordingState}.",
                Detail:
                    $"segments={recording.SegmentCount}; " +
                    $"elapsedSeconds={recording.ElapsedSeconds:F0}"));

        if (recordingReadiness == LiveReadinessStatusV1.Blocked)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Recording,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "recording.error",
                    Message: "Recording pipeline is in an error state."));
        }
        else if (recordingReadiness == LiveReadinessStatusV1.Degraded)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Recording,
                    LiveReadinessSeverityV1.Warning,
                    Code: "recording.transitioning",
                    Message:
                        $"Recording pipeline is transitioning through {recordingState}."));
        }

        var audioState =
            audioRuntimeHealth.Current;

        var audioReadiness =
            audioState.Status switch
            {
                AudioRuntimeHealthStatus.Ready =>
                    LiveReadinessStatusV1.Ready,
                AudioRuntimeHealthStatus.Blocked =>
                    LiveReadinessStatusV1.Blocked,
                AudioRuntimeHealthStatus.Unknown or
                AudioRuntimeHealthStatus.Degraded =>
                    LiveReadinessStatusV1.Degraded,
                _ =>
                    LiveReadinessStatusV1.Degraded
            };

        var audioSummary =
            audioState.Status switch
            {
                AudioRuntimeHealthStatus.Ready =>
                    "Audio runtime is ready.",
                AudioRuntimeHealthStatus.Blocked =>
                    "Audio runtime is blocked.",
                AudioRuntimeHealthStatus.Degraded =>
                    "Audio runtime health is degraded.",
                _ =>
                    "Audio runtime health has not yet been observed."
            };

        checks.Add(
            new(
                LiveReadinessDimensionV1.Audio,
                audioReadiness,
                Required: true,
                Summary: audioSummary,
                Detail: audioState.Detail));

        if (audioState.Status != AudioRuntimeHealthStatus.Ready)
        {
            var audioFinding =
                audioState.Status switch
                {
                    AudioRuntimeHealthStatus.Blocked =>
                        (
                            LiveReadinessSeverityV1.Blocking,
                            "audio.blocked",
                            "Authoritative audio runtime health is blocked."),
                    AudioRuntimeHealthStatus.Degraded =>
                        (
                            LiveReadinessSeverityV1.Warning,
                            "audio.degraded",
                            "Authoritative audio runtime health is degraded."),
                    _ =>
                        (
                            LiveReadinessSeverityV1.Warning,
                            "audio.unknown",
                            "Authoritative audio runtime health has not yet been observed.")
                };

            findings.Add(
                new(
                    LiveReadinessDimensionV1.Audio,
                    audioFinding.Item1,
                    Code: audioFinding.Item2,
                    Message:
                        string.IsNullOrWhiteSpace(audioState.Detail)
                            ? audioFinding.Item3
                            : audioState.Detail));
        }

        var storageStatus =
            await storage.CheckAsync(ct);

        var storageReadiness =
            storageStatus.WarningLevel switch
            {
                StorageWarningLevel.Ok =>
                    LiveReadinessStatusV1.Ready,

                StorageWarningLevel.Warning =>
                    LiveReadinessStatusV1.Degraded,

                _ =>
                    LiveReadinessStatusV1.Blocked
            };

        checks.Add(
            new(
                LiveReadinessDimensionV1.Storage,
                storageReadiness,
                Required: true,
                Summary:
                    $"{storageStatus.AvailableMinutes:F0} minutes recording capacity available.",
                Detail:
                    $"{storageStatus.AvailableBytes} bytes free at " +
                    storageStatus.RecordingDirectory));

        if (storageStatus.WarningLevel == StorageWarningLevel.Warning)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Storage,
                    LiveReadinessSeverityV1.Warning,
                    Code: "storage.warning",
                    Message:
                        $"Recording storage is reduced to " +
                        $"{storageStatus.AvailableMinutes:F0} minutes."));
        }
        else if (storageStatus.WarningLevel == StorageWarningLevel.Critical)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Storage,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "storage.critical",
                    Message:
                        $"Recording storage is critically low at " +
                        $"{storageStatus.AvailableMinutes:F0} minutes."));
        }

        checks.Add(
            new(
                LiveReadinessDimensionV1.StreamDeck,
                streamDeck.IsConnected
                    ? LiveReadinessStatusV1.Ready
                    : LiveReadinessStatusV1.Blocked,
                Required: true,
                Summary:
                    streamDeck.IsConnected
                        ? "Stream Deck is connected."
                        : "Stream Deck is not connected.",
                Detail: streamDeck.DeviceName));

        if (!streamDeck.IsConnected)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.StreamDeck,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "streamdeck.disconnected",
                    Message: "Stream Deck is required for the current field profile."));
        }

        var ownershipState =
            streamDeckOwnership.State;

        var ownershipReady =
            ownershipState == StreamDeckOwnershipState.Controlled;

        checks.Add(
            new(
                LiveReadinessDimensionV1.StreamDeckOwnership,
                ownershipReady
                    ? LiveReadinessStatusV1.Ready
                    : LiveReadinessStatusV1.Blocked,
                Required: true,
                Summary:
                    ownershipReady
                        ? "AMHARC has exclusive Stream Deck control."
                        : $"Stream Deck ownership is {ownershipState}.",
                Detail:
                    streamDeckOwnership.CompetingProcesses.Count == 0
                        ? null
                        : string.Join(
                            ", ",
                            streamDeckOwnership.CompetingProcesses)));

        if (!ownershipReady)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.StreamDeckOwnership,
                    LiveReadinessSeverityV1.Blocking,
                    Code:
                        ownershipState == StreamDeckOwnershipState.Conflicted
                            ? "streamdeck.ownership-conflict"
                            : "streamdeck.ownership-unavailable",
                    Message:
                        ownershipState == StreamDeckOwnershipState.Conflicted
                            ? "Competing software currently conflicts with AMHARC Stream Deck ownership."
                            : $"AMHARC Stream Deck ownership is {ownershipState}."));
        }

        checks.Add(
            new(
                LiveReadinessDimensionV1.Joystick,
                joystick.IsConnected
                    ? LiveReadinessStatusV1.Ready
                    : LiveReadinessStatusV1.Blocked,
                Required: true,
                Summary:
                    joystick.IsConnected
                        ? "PTZ joystick is connected."
                        : "PTZ joystick is not connected.",
                Detail: joystick.DeviceName));

        if (!joystick.IsConnected)
        {
            findings.Add(
                new(
                    LiveReadinessDimensionV1.Joystick,
                    LiveReadinessSeverityV1.Blocking,
                    Code: "joystick.disconnected",
                    Message: "PTZ joystick is required for the current mast field profile."));
        }

        return LiveReadinessStateV1.Create(
            match?.MatchId ?? matchId?.Trim(),
            checks,
            findings);
    }
}