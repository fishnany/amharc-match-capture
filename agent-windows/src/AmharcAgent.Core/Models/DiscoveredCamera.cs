namespace AmharcAgent.Core.Models;

public record DiscoveredCamera(
    string IpAddress,
    string? MacAddress,
    string? Model,
    string? SerialNumber,
    string? FirmwareVersion);
