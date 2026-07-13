namespace AmharcAgent.Core.Models;

public enum ComponentHealthState { Healthy, Degraded, Critical, Unknown }

public record ComponentHealth(
    string Component,
    ComponentHealthState State,
    string? Message,
    DateTimeOffset CheckedAt);

public record SystemHealth(
    ComponentHealth Camera,
    ComponentHealth Recording,
    ComponentHealth Streaming,
    ComponentHealth Storage,
    ComponentHealth StreamDeck,
    ComponentHealth Joystick,
    ComponentHealth Overlay,
    ComponentHealth Audio,
    ComponentHealth LocalApi,
    ComponentHealthState OverallState);
