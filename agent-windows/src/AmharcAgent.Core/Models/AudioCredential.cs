namespace AmharcAgent.Core.Models;

public sealed class AudioCredential
{
    public AudioCredential(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        Username = username;
        Password = password;
    }

    public string Username { get; }

    public string Password { get; }

    public override string ToString() => "AudioCredential [REDACTED]";
}
