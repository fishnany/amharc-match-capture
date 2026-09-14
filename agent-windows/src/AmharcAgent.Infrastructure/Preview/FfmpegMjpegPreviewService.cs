using System.Diagnostics;
using System.Text.RegularExpressions;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Preview;

/// <summary>
/// Isolated local operator-preview pipeline.
///
/// Preview deliberately owns a separate FFmpeg process from recording. A
/// preview failure or browser disconnect must not stop or mutate the clean
/// recording or authoritative-audio pipeline.
/// </summary>
public sealed class FfmpegMjpegPreviewService(
    ICameraAdapter camera,
    ILogger<FfmpegMjpegPreviewService> logger,
    string ffmpegPath) : IPreviewService
{
    private const string Boundary = "amharcframe";
    private readonly SemaphoreSlim _sessionGate = new(1, 1);

    public string ContentType =>
        $"multipart/x-mixed-replace; boundary={Boundary}";

    public async Task StreamMjpegAsync(
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await _sessionGate.WaitAsync(ct);

        Process? process = null;
        Task<string>? stderrTask = null;

        try
        {
            if (camera.ConnectionState !=
                CameraConnectionState.Connected)
            {
                await camera.ConnectAsync(ct);
            }

            var runtimeRtspUrl =
                await camera.GetAuthenticatedStreamUrlAsync(
                    null,
                    ct);

            var arguments =
                BuildPreviewArguments(runtimeRtspUrl);

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                },
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Unable to start FFmpeg operator preview process.");
            }

            // Never log the complete argument string: it contains the
            // authenticated runtime camera URI.
            logger.LogInformation(
                "AMHARC operator preview started using managed FFmpeg.");

            stderrTask =
                process.StandardError.ReadToEndAsync(ct);

            try
            {
                await process.StandardOutput.BaseStream.CopyToAsync(
                    destination,
                    64 * 1024,
                    ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                // Normal browser disconnect/navigation.
            }
            catch (IOException)
                when (ct.IsCancellationRequested)
            {
                // Normal HTTP response-stream closure during cancellation.
            }

            if (!ct.IsCancellationRequested &&
                process.HasExited &&
                process.ExitCode != 0)
            {
                var stderr =
                    stderrTask is null
                        ? string.Empty
                        : await stderrTask;

                var redacted =
                    RedactRtspCredentials(stderr);

                if (!string.IsNullOrWhiteSpace(redacted))
                {
                    logger.LogWarning(
                        "Operator preview FFmpeg exited with code {ExitCode}. FFmpeg stderr:{NewLine}{Stderr}",
                        process.ExitCode,
                        Environment.NewLine,
                        redacted);
                }

                throw new InvalidOperationException(
                    $"Operator preview FFmpeg exited with code {process.ExitCode}.");
            }
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(
                            CancellationToken.None);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process already exited between checks.
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Unable to terminate operator preview FFmpeg cleanly.");
                }

                process.Dispose();
            }

            _sessionGate.Release();

            logger.LogInformation(
                "AMHARC operator preview stopped.");
        }
    }

    private static string BuildPreviewArguments(
        string authenticatedRtspUrl)
    {
        if (string.IsNullOrWhiteSpace(authenticatedRtspUrl))
        {
            throw new ArgumentException(
                "Authenticated RTSP URL is required.",
                nameof(authenticatedRtspUrl));
        }

        return string.Join(
            " ",
            "-hide_banner",
            "-loglevel warning",
            "-rtsp_transport tcp",
            "-fflags nobuffer",
            "-flags low_delay",
            "-probesize 32768",
            "-analyzeduration 0",
            $"-i \"{authenticatedRtspUrl}\"",
            "-map 0:v:0",
            "-an",
            "-vf \"scale=1280:-2,fps=15\"",
            "-q:v 5",
            "-f mpjpeg",
            $"-boundary_tag {Boundary}",
            "pipe:1");
    }

    private static string RedactRtspCredentials(
        string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Regex.Replace(
            value,
            @"rtsp://[^/@\s]+:[^/@\s]+@",
            "rtsp://***:***@",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
    }
}