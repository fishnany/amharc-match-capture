namespace AmharcAgent.Core.Models;

public enum RecordingAudioSourceAvailability
{
    Available = 0,
    MissingCredential = 1,
    Unavailable = 2
}

public sealed class RecordingAudioSourceResolution
{
    private RecordingAudioSourceResolution(
        RecordingAudioSourceAvailability availability,
        string endpoint,
        int port,
        string presentationPath,
        AudioCredential? credential,
        string detail)
    {
        Availability = availability;
        Endpoint = endpoint;
        Port = port;
        PresentationPath = presentationPath;
        Credential = credential;
        Detail = detail;
    }

    public RecordingAudioSourceAvailability Availability { get; }

    public string Endpoint { get; }

    public int Port { get; }

    public string PresentationPath { get; }

    public AudioCredential? Credential { get; }

    public string Detail { get; }

    public bool IsAvailable =>
        Availability == RecordingAudioSourceAvailability.Available &&
        Credential is not null;

    public static RecordingAudioSourceResolution Available(
        string endpoint,
        int port,
        string presentationPath,
        AudioCredential credential) =>
        new(
            RecordingAudioSourceAvailability.Available,
            endpoint,
            port,
            presentationPath,
            credential ?? throw new ArgumentNullException(nameof(credential)),
            "Authoritative recording audio source is available.");

    public static RecordingAudioSourceResolution MissingCredential(
        string endpoint,
        int port,
        string presentationPath) =>
        new(
            RecordingAudioSourceAvailability.MissingCredential,
            endpoint,
            port,
            presentationPath,
            null,
            "Protected credential for authoritative recording audio source is missing.");

    public static RecordingAudioSourceResolution Unavailable(
        string endpoint,
        int port,
        string presentationPath) =>
        new(
            RecordingAudioSourceAvailability.Unavailable,
            endpoint,
            port,
            presentationPath,
            null,
            "Authoritative recording audio source cannot be resolved.");

    public override string ToString() =>
        $"RecordingAudioSourceResolution {{ Availability={Availability}, Endpoint={Endpoint}, Port={Port}, PresentationPath={PresentationPath}, Credential=[REDACTED] }}";
}