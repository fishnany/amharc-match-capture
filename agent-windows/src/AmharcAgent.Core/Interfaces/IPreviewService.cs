namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Provides the browser-consumable local operator preview without exposing
/// camera credentials or the authenticated RTSP source to the UI.
/// </summary>
public interface IPreviewService
{
    string ContentType { get; }

    Task StreamMjpegAsync(
        Stream destination,
        CancellationToken ct = default);
}