using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class AxisAudioRuntimeProbeTests
{
    [Fact]
    public void DigestChallenge_ParsesAxisChallenge()
    {
        var ok =
            AxisAudioRuntimeProbe.DigestChallenge.TryParse(
                "Digest realm=\"AXIS_ACCC8EE73214\", nonce=\"abc123\", opaque=\"opaque1\"",
                out var challenge);

        ok.Should().BeTrue();
        challenge!.Value.Realm.Should().Be("AXIS_ACCC8EE73214");
        challenge.Value.Nonce.Should().Be("abc123");
    }

    [Fact]
    public void DigestAuthorization_DoesNotExposePassword()
    {
        var challenge =
            new AxisAudioRuntimeProbe.DigestChallenge(
                "AXIS_TEST",
                "nonce123",
                null);
        var secret = "synthetic-test-secret";

        var header =
            AxisAudioRuntimeProbe.DigestAuthorization.Create(
                challenge,
                new AudioCredential("synthetic-user", secret),
                "DESCRIBE",
                "rtsp://192.0.2.10/media");

        header.Should().StartWith("Digest ");
        header.Should().Contain("synthetic-user");
        header.Should().NotContain(secret);
        header.Should().Contain("response=");
    }

    [Fact]
    public void AudioSdp_ParsesObservedAxisAudioContract()
    {
        var sdp =
            "v=0\r\n" +
            "m=audio 0 RTP/AVP 97\r\n" +
            "a=rtpmap:97 mpeg4-generic/8000/1\r\n" +
            "a=fmtp:97 mode=AAC-hbr;bitrate=16000\r\n" +
            "a=control:rtsp://192.0.2.10/axis-media/media.amp/trackID=2?videocodec=off&audio=1\r\n";

        var ok =
            AxisAudioRuntimeProbe.AudioSdp.TryParse(
                sdp,
                "rtsp://192.0.2.10/axis-media/media.amp?videocodec=off&audio=1",
                out var audio);

        ok.Should().BeTrue();
        audio!.PayloadType.Should().Be(97);
        audio.Codec.Should().Be("mpeg4-generic");
        audio.SampleRateHz.Should().Be(8000);
        audio.Channels.Should().Be(1);
        audio.ControlUri.Should().Contain("trackID=2");
    }

    [Fact]
    public void AudioSdp_RejectsPresentationWithoutAudio()
    {
        var sdp =
            "v=0\r\n" +
            "m=video 0 RTP/AVP 96\r\n" +
            "a=rtpmap:96 H264/90000\r\n";

        AxisAudioRuntimeProbe.AudioSdp.TryParse(
                sdp,
                "rtsp://192.0.2.10/media",
                out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ProbeSource_UsesCanonicalProfileAndNeverCredentialBearingUri()
    {
        var source =
            File.ReadAllText(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "..", "..",
                    "src", "AmharcAgent.Infrastructure", "Audio",
                    "AxisAudioRuntimeProbe.cs"));

        source.Should().Contain("FieldNetworkProfile.Canonical");
        source.Should().Contain("client.Client.Bind");
        source.Should().NotContain("DefaultCameraUsername");
        source.Should().NotContain("DefaultCameraPassword");
        source.Should().NotContain("root:pass");
    }

    [Fact]
    public void SelectPreferredHeaderValue_PrefersDigestAcrossDuplicateWwwAuthenticateHeaders()
    {
        const string digest =
            "Digest realm=\"AXIS_TEST\", nonce=\"abc\", stale=FALSE";
        const string basic =
            "Basic realm=\"AXIS_TEST\"";

        AxisAudioRuntimeProbe.RtspResponse
            .SelectPreferredHeaderValue("WWW-Authenticate", digest, basic)
            .Should().Be(digest);

        AxisAudioRuntimeProbe.RtspResponse
            .SelectPreferredHeaderValue("WWW-Authenticate", basic, digest)
            .Should().Be(digest);

        AxisAudioRuntimeProbe.DigestChallenge
            .TryParse(digest, out var parsed)
            .Should().BeTrue();

        parsed.Should().NotBeNull();
    }

    [Fact]
    public void DigestAuthorization_IsMethodAndUriSpecific()
    {
        var challenge =
            new AxisAudioRuntimeProbe.DigestChallenge(
                "AXIS_TEST",
                "nonce-test",
                null);
        var credential = new AudioCredential("synthetic-user", "synthetic-secret");

        var describe =
            AxisAudioRuntimeProbe.DigestAuthorization.Create(
                challenge,
                credential,
                "DESCRIBE",
                "rtsp://192.168.1.136/axis-media/media.amp?videocodec=off&audio=1");

        var setup =
            AxisAudioRuntimeProbe.DigestAuthorization.Create(
                challenge,
                credential,
                "SETUP",
                "rtsp://192.168.1.136/axis-media/media.amp/trackID=2?videocodec=off&audio=1");

        var play =
            AxisAudioRuntimeProbe.DigestAuthorization.Create(
                challenge,
                credential,
                "PLAY",
                "rtsp://192.168.1.136/axis-media/media.amp?videocodec=off&audio=1");

        var teardown =
            AxisAudioRuntimeProbe.DigestAuthorization.Create(
                challenge,
                credential,
                "TEARDOWN",
                "rtsp://192.168.1.136/axis-media/media.amp?videocodec=off&audio=1");

        describe.Should().NotBe(setup);
        describe.Should().NotBe(play);
        describe.Should().NotBe(teardown);
        setup.Should().NotBe(play);
        setup.Should().NotBe(teardown);
        play.Should().NotBe(teardown);

        describe.Should().Contain("username=\"synthetic-user\"");
        describe.Should().NotContain("synthetic-secret");
        setup.Should().NotContain("synthetic-secret");
        play.Should().NotContain("synthetic-secret");
        teardown.Should().NotContain("synthetic-secret");
    }
}
