namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Identifies the runtime application instance acting as canonical clock authority.
/// </summary>
public sealed record ClockAuthorityV1(
    string SourceApplication,
    string InstanceId);
