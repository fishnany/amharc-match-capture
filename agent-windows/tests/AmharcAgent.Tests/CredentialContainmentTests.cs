namespace AmharcAgent.Tests;

using System.Text.Json;
using AmharcAgent.Api.Controllers;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;
using Xunit;

public sealed class CredentialContainmentTests
{
    [Fact]
    public void Camera_DefaultPassword_IsEmpty()
    {
        Assert.Equal(string.Empty, new Camera().Password);
    }

    [Fact]
    public void ProtectedCredential_ToString_RedactsSecret()
    {
        const string secret = "synthetic-test-secret";
        var credential = new ProtectedCredential("synthetic-user", secret);

        Assert.DoesNotContain(secret, credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-user", credential.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CameraResponse_DoesNotExposeCredentialFields()
    {
        var response = new CameraResponse(
            "camera-1", "Primary", "AXIS", "Q6128-E", "192.0.2.10",
            554, 80, CameraRole.Primary, CameraConnectionState.Disconnected,
            null, null, null, null, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, true);

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasCredentials", json, StringComparison.Ordinal);
    }

    [Fact]
    public void StreamingDestinationResponse_DoesNotExposeStreamKey()
    {
        var response = new StreamingDestinationResponse(
            "destination-1", StreamingPlatform.Custom, "Test", "rtmp://example.invalid/live",
            null, null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true);

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("\"StreamKey\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasStreamKey", json, StringComparison.Ordinal);
    }
}
