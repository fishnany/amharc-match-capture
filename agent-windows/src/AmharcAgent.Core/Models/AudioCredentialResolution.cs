namespace AmharcAgent.Core.Models;

public enum AudioCredentialAvailability
{
    Available = 0,
    Missing = 1,
    Unavailable = 2
}

public sealed class AudioCredentialResolution
{
    private AudioCredentialResolution(
        AudioCredentialAvailability availability,
        AudioCredential? credential,
        string detail)
    {
        Availability = availability;
        Credential = credential;
        Detail = detail;
    }

    public AudioCredentialAvailability Availability { get; }

    public AudioCredential? Credential { get; }

    public string Detail { get; }

    public static AudioCredentialResolution Available(AudioCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return new(
            AudioCredentialAvailability.Available,
            credential,
            "Audio credential resolved from protected runtime authority.");
    }

    public static AudioCredentialResolution Missing() =>
        new(
            AudioCredentialAvailability.Missing,
            null,
            "Audio credential is not provisioned in the protected runtime authority.");

    public static AudioCredentialResolution Unavailable() =>
        new(
            AudioCredentialAvailability.Unavailable,
            null,
            "Protected audio credential authority is unavailable.");
}
