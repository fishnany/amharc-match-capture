namespace AmharcAgent.Tests;

using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Audio;
using Xunit;

public sealed class AudioCredentialProviderTests
{
    [Fact]
    public async Task AvailableCredential_IsReturnedWithoutSecretDisclosure()
    {
        const string secret = "synthetic-test-secret";

        var provider = new WindowsCredentialManagerAudioCredentialProvider(
            new FakeReader(
                WindowsCredentialReadResult.Success(
                    "audio-user",
                    secret)));

        var result = await provider.ResolveAsync();

        Assert.Equal(AudioCredentialAvailability.Available, result.Availability);
        Assert.NotNull(result.Credential);
        Assert.Equal("audio-user", result.Credential!.Username);
        Assert.Equal(secret, result.Credential.Password);
        Assert.DoesNotContain(secret, result.Credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingCredential_IsDistinct()
    {
        var provider = new WindowsCredentialManagerAudioCredentialProvider(
            new FakeReader(WindowsCredentialReadResult.NotFound()));

        var result = await provider.ResolveAsync();

        Assert.Equal(AudioCredentialAvailability.Missing, result.Availability);
        Assert.Null(result.Credential);
    }

    [Fact]
    public async Task AuthorityFailure_IsUnavailable()
    {
        var provider = new WindowsCredentialManagerAudioCredentialProvider(
            new FakeReader(WindowsCredentialReadResult.Error()));

        var result = await provider.ResolveAsync();

        Assert.Equal(AudioCredentialAvailability.Unavailable, result.Availability);
        Assert.Null(result.Credential);
    }

    [Fact]
    public async Task BlankUsername_IsUnavailable()
    {
        var provider = new WindowsCredentialManagerAudioCredentialProvider(
            new FakeReader(
                WindowsCredentialReadResult.Success(
                    " ",
                    "synthetic-test-secret")));

        var result = await provider.ResolveAsync();

        Assert.Equal(AudioCredentialAvailability.Unavailable, result.Availability);
        Assert.Null(result.Credential);
    }

    [Fact]
    public void Target_IsCanonicalNonSecretIdentifier()
    {
        Assert.Equal(
            "AMHARC/Audio/192.168.1.136",
            WindowsCredentialManagerAudioCredentialProvider.CredentialTargetName);
    }

    private sealed class FakeReader : IWindowsCredentialReader
    {
        private readonly WindowsCredentialReadResult _result;

        public FakeReader(WindowsCredentialReadResult result)
        {
            _result = result;
        }

        public WindowsCredentialReadResult ReadGeneric(string targetName)
        {
            Assert.Equal(
                WindowsCredentialManagerAudioCredentialProvider.CredentialTargetName,
                targetName);

            return _result;
        }
    }
}
