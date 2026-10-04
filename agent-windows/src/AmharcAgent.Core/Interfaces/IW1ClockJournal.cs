namespace AmharcAgent.Core.Interfaces;

public sealed record W1JournalState(string Subject, long Revision, string WriterVersion, string ContextJson, string ActivityId);
public sealed record W1JournalActivity(string Id, string Subject, string OperationKey, string InputSha256, string Json);
public interface IW1ClockJournal
{
    W1JournalState? Read(string subject);
    W1JournalActivity? ReadActivity(string activityId);
    W1JournalActivity? FindOperation(string subject, string operationKey);
    IReadOnlyList<W1JournalActivity> ReadActivities(string subject) => Array.Empty<W1JournalActivity>();
    void Commit(long expectedRevision, W1JournalState state, W1JournalActivity activity);
}