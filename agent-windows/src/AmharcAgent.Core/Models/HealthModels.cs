namespace AmharcAgent.Core.Models;

/// <summary>Health state for an individual component.</summary>
public enum ComponentHealthState { Healthy, Degraded, Critical, Unknown }

/// <summary>Health snapshot for a single component.</summary>
public record ComponentHealth(
    string Component,
    ComponentHealthState State,
    string? Message,
    DateTimeOffset CheckedAt);

/// <summary>Aggregated health snapshot for the whole system.</summary>
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
