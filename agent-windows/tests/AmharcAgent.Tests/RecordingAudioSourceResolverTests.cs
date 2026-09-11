using Xunit;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Audio;

namespace AmharcAgent.Tests;

public sealed class RecordingAudioSourceResolverTests
{
    [Fact]
    public async Task ResolveAsync_AvailableCredential_ReturnsCanonicalAuthoritativeSource()
    {
        var credential = new AudioCredential("operator", "secret-value");
        var sut = new AxisRecordingAudioSourceResolver(
            new StubCredentialProvider(
                AudioCredentialResolution.Available(credential)));

        var result = await sut.ResolveAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal(RecordingAudioSourceAvailability.Available, result.Availability);
        Assert.Equal("192.168.1.136", result.Endpoint);
        Assert.Equal(554, result.Port);
        Assert.Equal(
            "/axis-media/media.amp?videocodec=off&audio=1",
            result.PresentationPath);
        Assert.Same(credential, result.Credential);
    }

    [Fact]
    public async Task ResolveAsync_MissingCredential_FailsClosedWithoutCredential()
    {
        var sut = new AxisRecordingAudioSourceResolver(
            new StubCredentialProvider(
                AudioCredentialResolution.Missing()));

        var result = await sut.ResolveAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(
            RecordingAudioSourceAvailability.MissingCredential,
            result.Availability);
        Assert.Null(result.Credential);
        Assert.Equal("192.168.1.136", result.Endpoint);
        Assert.Equal(554, result.Port);
    }

    [Fact]
    public async Task ResolveAsync_UnavailableCredentialAuthority_FailsClosed()
    {
        var sut = new AxisRecordingAudioSourceResolver(
            new StubCredentialProvider(
                AudioCredentialResolution.Unavailable()));

        var result = await sut.ResolveAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(
            RecordingAudioSourceAvailability.Unavailable,
            result.Availability);
        Assert.Null(result.Credential);
    }

    [Fact]
    public async Task Resolution_ToString_DoesNotExposeCredentialMaterial()
    {
        const string secret = "do-not-log-this-secret";
        var sut = new AxisRecordingAudioSourceResolver(
            new StubCredentialProvider(
                AudioCredentialResolution.Available(
                    new AudioCredential("operator", secret))));

        var result = await sut.ResolveAsync();
        var rendered = result.ToString();

        Assert.DoesNotContain(secret, rendered, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", rendered, StringComparison.Ordinal);
    }

    private sealed class StubCredentialProvider(
        AudioCredentialResolution resolution)
        : IAudioCredentialProvider
    {
        public ValueTask<AudioCredentialResolution> ResolveAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(resolution);
        }
    }
}