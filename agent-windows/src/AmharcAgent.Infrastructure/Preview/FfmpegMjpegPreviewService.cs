using System.Diagnostics;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Preview;

/// <summary>
/// Local operator-preview consumer of canonical media ingress.
///
/// Preview no longer acquires camera credentials or RTSP directly. It consumes
/// a credential-free video-only MPEG-TS lease from canonical ingress and owns
/// only the downstream MJPEG transform required by the browser.
/// </summary>
public sealed class FfmpegMjpegPreviewService(
    IStreamReceiverMediaSource mediaSource,
    ILogger<FfmpegMjpegPreviewService> logger,
    string ffmpegPath) : IPreviewService
{
    private const string Boundary =
        "amharcframe";

    private readonly SemaphoreSlim _sessionGate =
        new(1, 1);

    public string ContentType =>
        $"multipart/x-mixed-replace; boundary={Boundary}";

    public async Task StreamMjpegAsync(
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(
            destination);

        await _sessionGate.WaitAsync(ct);

        Process? process = null;
        Task<string>? stderrTask = null;
        Task? inputPumpTask = null;
        IStreamReceiverMediaLease? lease = null;

        using var previewCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(ct);

        try
        {
            lease =
                await mediaSource.AcquireAsync(
                    previewCts.Token);

            var arguments =
                BuildPreviewArguments();

            process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName =
                                ffmpegPath,
                            Arguments =
                                arguments,
                            UseShellExecute =
                                false,
                            CreateNoWindow =
                                true,
                            RedirectStandardOutput =
                                true,
                            RedirectStandardError =
                                true,
                            RedirectStandardInput =
                                true
                        },
                    EnableRaisingEvents = true
                };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Unable to start FFmpeg operator preview process.");
            }

            logger.LogInformation(
                "AMHARC operator preview started from canonical media ingress using managed FFmpeg.");

            stderrTask =
                process.StandardError
                    .ReadToEndAsync();

            inputPumpTask =
                PumpCanonicalInputAsync(
                    lease.Stream,
                    process.StandardInput
                        .BaseStream,
                    previewCts.Token);

            try
            {
                await process.StandardOutput
                    .BaseStream
                    .CopyToAsync(
                        destination,
                        64 * 1024,
                        previewCts.Token);
            }
            catch (OperationCanceledException)
                when (previewCts
                    .IsCancellationRequested)
            {
                // Normal browser disconnect/navigation.
            }
            catch (IOException)
                when (previewCts
                    .IsCancellationRequested)
            {
                // Normal HTTP response-stream closure.
            }

            if (!previewCts
                    .IsCancellationRequested &&
                process.HasExited &&
                process.ExitCode != 0)
            {
                var stderr =
                    stderrTask is null
                        ? string.Empty
                        : await stderrTask;

                if (!string.IsNullOrWhiteSpace(
                        stderr))
                {
                    logger.LogWarning(
                        "Operator preview FFmpeg exited with code {ExitCode}. FFmpeg stderr:{NewLine}{Stderr}",
                        process.ExitCode,
                        Environment.NewLine,
                        stderr);
                }

                throw new InvalidOperationException(
                    $"Operator preview FFmpeg exited with code {process.ExitCode}.");
            }
        }
        finally
        {
            previewCts.Cancel();

            if (process is not null)
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                    // Best-effort input closure.
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(
                            entireProcessTree:
                                true);

                        await process
                            .WaitForExitAsync(
                                CancellationToken.None);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process already exited.
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Unable to terminate operator preview FFmpeg cleanly.");
                }

                process.Dispose();
            }

            if (inputPumpTask is not null)
            {
                try
                {
                    await inputPumpTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected on browser disconnect.
                }
                catch (IOException)
                {
                    // Expected when FFmpeg stdin closes.
                }
            }

            if (lease is not null)
            {
                await lease.DisposeAsync();
            }

            _sessionGate.Release();

            logger.LogInformation(
                "AMHARC operator preview stopped.");
        }
    }

    private static async Task PumpCanonicalInputAsync(
        Stream canonicalMedia,
        Stream ffmpegInput,
        CancellationToken ct)
    {
        try
        {
            await canonicalMedia.CopyToAsync(
                ffmpegInput,
                64 * 1024,
                ct);

            await ffmpegInput.FlushAsync(ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Normal preview shutdown.
        }
        catch (IOException)
            when (ct.IsCancellationRequested)
        {
            // FFmpeg input closed during shutdown.
        }
    }

    private static string BuildPreviewArguments()
    {
        return string.Join(
            " ",
            "-hide_banner",
            "-loglevel warning",
            "-fflags nobuffer",
            "-flags low_delay",
            "-probesize 32768",
            "-analyzeduration 0",
            "-f mpegts",
            "-i pipe:0",
            "-map 0:v:0",
            "-an",
            "-vf \"scale=1280:-2,fps=15\"",
            "-q:v 5",
            "-f mpjpeg",
            $"-boundary_tag {Boundary}",
            "pipe:1");
    }
}