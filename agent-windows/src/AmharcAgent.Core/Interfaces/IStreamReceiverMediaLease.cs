namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Credential-free lease over canonical live-video media.
/// Disposing a lease detaches only that consumer; it does not stop canonical ingress.
/// </summary>
public interface IStreamReceiverMediaLease : IAsyncDisposable
{
    /// <summary>
    /// Video-only MPEG-TS stream produced by canonical media ingress.
    /// The stream contains no source URI or credential material.
    /// </summary>
    Stream Stream { get; }
}