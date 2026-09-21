using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Audio;

internal sealed class AxisAudioRuntimeProbe : IAudioRuntimeProbe
{
    private const string PresentationPath =
        "/axis-media/media.amp?videocodec=off&audio=1";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MediaObservationWindow = TimeSpan.FromSeconds(5);

    public async Task<AudioRuntimeProbeResult> ProbeAsync(
        AudioCredential credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var profile = FieldNetworkProfile.Canonical;
        var remoteUri = $"rtsp://{profile.AudioAddress}:{profile.AudioRtspPort}{PresentationPath}";

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            client.Client.Bind(
                new IPEndPoint(
                    IPAddress.Parse(profile.CaptureAddress),
                    0));

            using var connectCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(ConnectTimeout);

            try
            {
                await client.ConnectAsync(
                    profile.AudioAddress,
                    profile.AudioRtspPort,
                    connectCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(
                    AudioRuntimeProbeFailure.EndpointUnreachable,
                    "Audio RTSP endpoint connection timed out.");
            }
            catch (SocketException)
            {
                return Failure(
                    AudioRuntimeProbeFailure.EndpointUnreachable,
                    "Audio RTSP endpoint is unreachable.");
            }

            await using var stream = client.GetStream();
            var cseq = 1;

            var initial =
                await SendRequestAsync(
                    stream,
                    "DESCRIBE",
                    remoteUri,
                    cseq++,
                    null,
                    null,
                    cancellationToken);

            string? authorization = null;
            DigestChallenge? digest = null;

            if (initial.StatusCode == 401)
            {
                var challenge =
                    initial.Headers.TryGetValue("WWW-Authenticate", out var value)
                        ? value
                        : null;

                if (!DigestChallenge.TryParse(challenge, out digest))
                {
                    return Failure(
                        AudioRuntimeProbeFailure.AuthenticationRejected,
                        "Audio endpoint did not provide a usable Digest challenge.",
                        endpointReachable: true);
                }

                authorization =
                    DigestAuthorization.Create(
                        digest!.Value,
                        credential,
                        "DESCRIBE",
                        remoteUri);

                initial =
                    await SendRequestAsync(
                        stream,
                        "DESCRIBE",
                        remoteUri,
                        cseq++,
                        authorization,
                        null,
                        cancellationToken);
            }

            if (initial.StatusCode == 401)
            {
                return Failure(
                    AudioRuntimeProbeFailure.AuthenticationRejected,
                    "Audio RTSP authentication was rejected.",
                    endpointReachable: true);
            }

            if (initial.StatusCode != 200)
            {
                return Failure(
                    AudioRuntimeProbeFailure.InvalidAudioPresentation,
                    $"Audio DESCRIBE returned RTSP {initial.StatusCode}.",
                    endpointReachable: true,
                    authenticated: authorization is not null);
            }

            if (authorization is null || !digest.HasValue)
            {
                return Failure(
                    AudioRuntimeProbeFailure.AuthenticationRejected,
                    "Audio RTSP endpoint did not establish Digest authentication.",
                    endpointReachable: true);
            }

            if (!AudioSdp.TryParse(initial.Body, remoteUri, out var audio))
            {
                return Failure(
                    AudioRuntimeProbeFailure.InvalidAudioPresentation,
                    "Authenticated SDP did not advertise a valid audio stream.",
                    endpointReachable: true,
                    authenticated: true);
            }

            var setup =
                await SendRequestAsync(
                    stream,
                    "SETUP",
                    audio!.ControlUri,
                    cseq++,
                    DigestAuthorization.Create(
                        digest!.Value,
                        credential,
                        "SETUP",
                        audio.ControlUri),
                    new Dictionary<string, string>
                    {
                        ["Transport"] = "RTP/AVP/TCP;unicast;interleaved=0-1"
                    },
                    cancellationToken);

            if (setup.StatusCode == 401)
            {
                return FromAudio(
                    audio,
                    AudioRuntimeProbeFailure.AuthenticationRejected,
                    "Audio SETUP authentication was rejected.",
                    authenticated: false);
            }

            if (setup.StatusCode != 200 ||
                !setup.Headers.TryGetValue("Session", out var sessionHeader))
            {
                return FromAudio(
                    audio,
                    AudioRuntimeProbeFailure.MediaSessionFailed,
                    $"Audio SETUP failed with RTSP {setup.StatusCode}.");
            }

            var sessionId = sessionHeader.Split(';', 2)[0].Trim();

            var play =
                await SendRequestAsync(
                    stream,
                    "PLAY",
                    remoteUri,
                    cseq++,
                    DigestAuthorization.Create(
                        digest!.Value,
                        credential,
                        "PLAY",
                        remoteUri),
                    new Dictionary<string, string>
                    {
                        ["Session"] = sessionId
                    },
                    cancellationToken);

            if (play.StatusCode == 401)
            {
                return FromAudio(
                    audio,
                    AudioRuntimeProbeFailure.AuthenticationRejected,
                    "Audio PLAY authentication was rejected.",
                    authenticated: false);
            }

            if (play.StatusCode != 200)
            {
                return FromAudio(
                    audio,
                    AudioRuntimeProbeFailure.MediaSessionFailed,
                    $"Audio PLAY failed with RTSP {play.StatusCode}.");
            }

            var flow =
                await ObserveInterleavedRtpAsync(
                    stream,
                    audio.PayloadType,
                    MediaObservationWindow,
                    cancellationToken);

            try
            {
                await SendRequestAsync(
                    stream,
                    "TEARDOWN",
                    remoteUri,
                    cseq,
                    DigestAuthorization.Create(
                        digest!.Value,
                        credential,
                        "TEARDOWN",
                        remoteUri),
                    new Dictionary<string, string>
                    {
                        ["Session"] = sessionId
                    },
                    CancellationToken.None);
            }
            catch
            {
                // Best effort only. Probe result is based on media evidence.
            }

            if (!flow.Observed)
            {
                return FromAudio(
                    audio,
                    AudioRuntimeProbeFailure.MediaFlowNotObserved,
                    "No qualifying RTP audio flow was observed.");
            }

            return new AudioRuntimeProbeResult(
                EndpointReachable: true,
                Authenticated: true,
                AudioAdvertised: true,
                MediaFlowObserved: true,
                Codec: audio.Codec,
                SampleRateHz: audio.SampleRateHz,
                Channels: audio.Channels,
                PayloadType: audio.PayloadType,
                Failure: AudioRuntimeProbeFailure.None,
                Detail: "Authenticated RTP audio flow observed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure(
                AudioRuntimeProbeFailure.Unexpected,
                "Unexpected audio runtime probe failure.");
        }
    }


    private static AudioRuntimeProbeResult Failure(
        AudioRuntimeProbeFailure failure,
        string detail,
        bool endpointReachable = false,
        bool authenticated = false) =>
        new(
            endpointReachable,
            authenticated,
            false,
            false,
            null,
            null,
            null,
            null,
            failure,
            detail);

    private static AudioRuntimeProbeResult FromAudio(
        AudioSdp audio,
        AudioRuntimeProbeFailure failure,
        string detail,
        bool authenticated = true) =>
        new(
            true,
            authenticated,
            true,
            false,
            audio.Codec,
            audio.SampleRateHz,
            audio.Channels,
            audio.PayloadType,
            failure,
            detail);

    internal static async Task<RtspResponse> SendRequestAsync(
        NetworkStream stream,
        string method,
        string uri,
        int cseq,
        string? authorization,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(method).Append(' ').Append(uri).Append(" RTSP/1.0\r\n");
        builder.Append("CSeq: ").Append(cseq).Append("\r\n");
        builder.Append("User-Agent: AMHARC-Capture/1.0\r\n");
        if (method == "DESCRIBE")
        {
            builder.Append("Accept: application/sdp\r\n");
        }

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            builder.Append("Authorization: ").Append(authorization).Append("\r\n");
        }

        if (headers is not null)
        {
            foreach (var pair in headers)
            {
                builder.Append(pair.Key).Append(": ").Append(pair.Value).Append("\r\n");
            }
        }

        builder.Append("\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        return await RtspResponse.ReadAsync(
            stream,
            ResponseTimeout,
            cancellationToken);
    }

    internal static async Task<RtpObservation> ObserveInterleavedRtpAsync(
        NetworkStream stream,
        int expectedPayloadType,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(window);

        ushort? firstSequence = null;
        uint? firstTimestamp = null;
        var matchingFrames = 0;
        var payloadBytes = 0;

        try
        {
            while (!timeout.IsCancellationRequested)
            {
                var prefix = await ReadExactAsync(stream, 4, timeout.Token);
                if (prefix[0] != 0x24)
                {
                    continue;
                }

                var length = (prefix[2] << 8) | prefix[3];
                var packet = await ReadExactAsync(stream, length, timeout.Token);
                if (packet.Length < 12)
                {
                    continue;
                }

                var payloadType = packet[1] & 0x7f;
                if (payloadType != expectedPayloadType)
                {
                    continue;
                }

                var sequence = (ushort)((packet[2] << 8) | packet[3]);
                var timestamp =
                    ((uint)packet[4] << 24) |
                    ((uint)packet[5] << 16) |
                    ((uint)packet[6] << 8) |
                    packet[7];

                matchingFrames++;
                payloadBytes += packet.Length - 12;

                if (firstSequence is null)
                {
                    firstSequence = sequence;
                    firstTimestamp = timestamp;
                    continue;
                }

                if (sequence != firstSequence.Value &&
                    timestamp != firstTimestamp!.Value &&
                    payloadBytes > 0)
                {
                    return new RtpObservation(true, matchingFrames, payloadBytes);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return new RtpObservation(false, matchingFrames, payloadBytes);
    }

    private static async Task<byte[]> ReadExactAsync(
        Stream stream,
        int count,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read =
                await stream.ReadAsync(
                    buffer.AsMemory(offset, count - offset),
                    cancellationToken);
            if (read == 0)
            {
                throw new IOException("RTSP stream closed unexpectedly.");
            }

            offset += read;
        }

        return buffer;
    }

    internal readonly record struct RtpObservation(
        bool Observed,
        int MatchingFrames,
        int PayloadBytes);

    internal sealed record AudioSdp(
        int PayloadType,
        string Codec,
        int SampleRateHz,
        int Channels,
        string ControlUri)
    {
        internal static bool TryParse(
            string? sdp,
            string presentationUri,
            out AudioSdp? audio)
        {
            audio = null;
            if (string.IsNullOrWhiteSpace(sdp))
            {
                return false;
            }

            var lines =
                sdp.Split(
                    new[] { "\r\n", "\n" },
                    StringSplitOptions.RemoveEmptyEntries);

            var mediaIndex =
                Array.FindIndex(
                    lines,
                    line => line.StartsWith("m=audio ", StringComparison.OrdinalIgnoreCase));
            if (mediaIndex < 0)
            {
                return false;
            }

            var mediaParts =
                lines[mediaIndex].Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);
            if (mediaParts.Length < 4 ||
                !int.TryParse(mediaParts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var payloadType) ||
                payloadType is < 0 or > 127)
            {
                return false;
            }

            string? codec = null;
            int sampleRate = 0;
            int channels = 0;
            string? control = null;

            for (var i = mediaIndex + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("m=", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var rtpPrefix = $"a=rtpmap:{payloadType} ";
                if (lines[i].StartsWith(rtpPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var map = lines[i][rtpPrefix.Length..].Split('/');
                    if (map.Length >= 2)
                    {
                        codec = map[0].Trim();
                        int.TryParse(map[1], NumberStyles.None, CultureInfo.InvariantCulture, out sampleRate);
                        channels =
                            map.Length >= 3 &&
                            int.TryParse(map[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedChannels)
                                ? parsedChannels
                                : 1;
                    }
                }

                if (lines[i].StartsWith("a=control:", StringComparison.OrdinalIgnoreCase))
                {
                    control = lines[i]["a=control:".Length..].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(codec) ||
                sampleRate <= 0 ||
                channels <= 0 ||
                string.IsNullOrWhiteSpace(control))
            {
                return false;
            }

            var controlUri =
                Uri.TryCreate(control, UriKind.Absolute, out var absolute)
                    ? absolute.ToString()
                    : new Uri(new Uri(presentationUri), control).ToString();

            audio =
                new AudioSdp(
                    payloadType,
                    codec,
                    sampleRate,
                    channels,
                    controlUri);
            return true;
        }
    }

    internal readonly record struct DigestChallenge(
        string Realm,
        string Nonce,
        string? Opaque)
    {
        internal static bool TryParse(
            string? header,
            out DigestChallenge? challenge)
        {
            challenge = null;
            if (string.IsNullOrWhiteSpace(header) ||
                !header.TrimStart().StartsWith("Digest ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var realm = Parameter(header, "realm");
            var nonce = Parameter(header, "nonce");
            if (string.IsNullOrWhiteSpace(realm) ||
                string.IsNullOrWhiteSpace(nonce))
            {
                return false;
            }

            challenge =
                new DigestChallenge(
                    realm,
                    nonce,
                    Parameter(header, "opaque"));
            return true;
        }

        private static string? Parameter(string header, string name)
        {
            var match =
                Regex.Match(
                    header,
                    $@"(?:^|,\s*|\s){Regex.Escape(name)}\s*=\s*""([^""]*)""",
                    RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    internal static class DigestAuthorization
    {
        internal static string Create(
            DigestChallenge challenge,
            AudioCredential credential,
            string method,
            string uri)
        {
            var ha1 =
                Md5Hex(
                    $"{credential.Username}:{challenge.Realm}:{credential.Password}");
            var ha2 = Md5Hex($"{method}:{uri}");
            var response =
                Md5Hex($"{ha1}:{challenge.Nonce}:{ha2}");

            var value =
                $"Digest username=\"{Escape(credential.Username)}\", " +
                $"realm=\"{Escape(challenge.Realm)}\", " +
                $"nonce=\"{Escape(challenge.Nonce)}\", " +
                $"uri=\"{Escape(uri)}\", response=\"{response}\"";

            if (!string.IsNullOrWhiteSpace(challenge.Opaque))
            {
                value += $", opaque=\"{Escape(challenge.Opaque)}\"";
            }

            return value;
        }

        private static string Md5Hex(string value)
        {
            var hash = MD5.HashData(Encoding.ASCII.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\", StringComparison.Ordinal)
                 .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    internal sealed record RtspResponse(
        int StatusCode,
        IReadOnlyDictionary<string, string> Headers,
        string Body,
        DigestChallenge? AuthenticationChallenge)
    {
        internal static async Task<RtspResponse> ReadAsync(
            NetworkStream stream,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);

            var headerBytes = new List<byte>();
            while (true)
            {
                var one = await ReadExactAsync(stream, 1, linked.Token);
                headerBytes.Add(one[0]);
                var n = headerBytes.Count;
                if (n >= 4 &&
                    headerBytes[n - 4] == 13 &&
                    headerBytes[n - 3] == 10 &&
                    headerBytes[n - 2] == 13 &&
                    headerBytes[n - 1] == 10)
                {
                    break;
                }

                if (headerBytes.Count > 65536)
                {
                    throw new InvalidDataException("RTSP response headers exceed limit.");
                }
            }

            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines =
                headerText.Split(
                    new[] { "\r\n" },
                    StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                throw new InvalidDataException("Missing RTSP status line.");
            }

            var statusParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (statusParts.Length < 2 ||
                !int.TryParse(statusParts[1], out var status))
            {
                throw new InvalidDataException("Invalid RTSP status line.");
            }

            var headers =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                {
                    var name = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();

                    if (headers.TryGetValue(name, out var existing))
                    {
                        headers[name] = SelectPreferredHeaderValue(name, existing, value);
                    }
                    else
                    {
                        headers[name] = value;
                    }
                }
            }

            var contentLength =
                headers.TryGetValue("Content-Length", out var lengthText) &&
                int.TryParse(lengthText, out var parsedLength)
                    ? parsedLength
                    : 0;

            var body =
                contentLength > 0
                    ? Encoding.ASCII.GetString(
                        await ReadExactAsync(stream, contentLength, linked.Token))
                    : string.Empty;

            DigestChallenge? challenge = null;
            if (headers.TryGetValue("WWW-Authenticate", out var auth) &&
                DigestChallenge.TryParse(auth, out var parsed))
            {
                challenge = parsed;
            }

            return new RtspResponse(status, headers, body, challenge);
        }

        internal static string SelectPreferredHeaderValue(
            string name,
            string existing,
            string candidate)
        {
            if (!name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            var existingIsDigest =
                existing.TrimStart().StartsWith("Digest ", StringComparison.OrdinalIgnoreCase);
            var candidateIsDigest =
                candidate.TrimStart().StartsWith("Digest ", StringComparison.OrdinalIgnoreCase);

            if (existingIsDigest)
            {
                return existing;
            }

            if (candidateIsDigest)
            {
                return candidate;
            }

            return candidate;
        }
    }
}
