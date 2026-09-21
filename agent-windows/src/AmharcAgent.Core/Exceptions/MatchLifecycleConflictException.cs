namespace AmharcAgent.Core.Exceptions;

/// <summary>
/// Represents an expected conflict between the requested match lifecycle
/// transition and the current operational match state.
/// </summary>
public sealed class MatchLifecycleConflictException : InvalidOperationException
{
    public MatchLifecycleConflictException(string message)
        : base(message)
    {
    }
}