using AmharcAgent.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace AmharcAgent.Data.Repositories;

/// <summary>Explicit development/test store. Never opens AgentSettings' database
/// or migrates an existing Match database implicitly.</summary>
public sealed class W1SqliteClockJournal : IW1ClockJournal
{
    private readonly string _connectionString;
    private readonly Action? _beforeCommitFault;
    public W1SqliteClockJournal(string scratchDatabasePath, Action? beforeCommitFault = null)
    {
        if (string.IsNullOrWhiteSpace(scratchDatabasePath))
            throw new ArgumentException("An explicit scratch database is required.");
        _connectionString = new SqliteConnectionStringBuilder {
            DataSource = scratchDatabasePath, DefaultTimeout = 30
        }.ToString();
        _beforeCommitFault = beforeCommitFault;
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS W1ClockContexts(
              Subject TEXT PRIMARY KEY, Revision INTEGER NOT NULL,
              WriterVersion TEXT NOT NULL, ContextJson TEXT NOT NULL, ActivityId TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS W1ClockActivities(
              Id TEXT PRIMARY KEY, Subject TEXT NOT NULL, OperationKey TEXT NOT NULL,
              InputSha256 TEXT NOT NULL, Json TEXT NOT NULL, UNIQUE(Subject, OperationKey));
            CREATE TRIGGER IF NOT EXISTS W1ClockActivities_no_update BEFORE UPDATE ON W1ClockActivities
              BEGIN SELECT RAISE(ABORT,'W1_HISTORY_IMMUTABLE'); END;
            CREATE TRIGGER IF NOT EXISTS W1ClockActivities_no_delete BEFORE DELETE ON W1ClockActivities
              BEGIN SELECT RAISE(ABORT,'W1_HISTORY_IMMUTABLE'); END;
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return c;
    }

    public W1JournalState? Read(string subject)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Subject,Revision,WriterVersion,ContextJson,ActivityId FROM W1ClockContexts WHERE Subject=$s";
        cmd.Parameters.AddWithValue("$s", subject);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new(r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4)) : null;
    }

    public W1JournalActivity? FindOperation(string subject, string operationKey)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,Subject,OperationKey,InputSha256,Json FROM W1ClockActivities WHERE Subject=$s AND OperationKey=$o";
        cmd.Parameters.AddWithValue("$s", subject);
        cmd.Parameters.AddWithValue("$o", operationKey);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)) : null;
    }

    public W1JournalActivity? ReadActivity(string activityId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,Subject,OperationKey,InputSha256,Json FROM W1ClockActivities WHERE Id=$i";
        cmd.Parameters.AddWithValue("$i", activityId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)) : null;
    }

    public void Commit(long expectedRevision, W1JournalState state, W1JournalActivity activity)
    {
        if (state.WriterVersion != "1.0.0" || state.Subject != activity.Subject ||
            state.ActivityId != activity.Id || state.Revision != expectedRevision + 1)
            throw new InvalidOperationException("W1_OLD_OR_INCOHERENT_WRITER_REFUSAL");
        using var c = Open();
        using var tx = c.BeginTransaction(deferred: false);
        using var check = c.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT Revision,WriterVersion FROM W1ClockContexts WHERE Subject=$s";
        check.Parameters.AddWithValue("$s", state.Subject);
        using (var r = check.ExecuteReader())
        {
            if (r.Read())
            {
                if (r.GetString(1) != "1.0.0") throw new InvalidOperationException("W1_OLD_WRITER_REFUSAL");
                if (r.GetInt64(0) != expectedRevision) throw new InvalidOperationException("W1_CHECKPOINT_CONFLICT");
            }
            else if (expectedRevision != 0) throw new InvalidOperationException("W1_CHECKPOINT_CONFLICT");
        }
        using var history = c.CreateCommand();
        history.Transaction = tx;
        history.CommandText = "INSERT INTO W1ClockActivities VALUES($i,$s,$o,$h,$j)";
        history.Parameters.AddWithValue("$i", activity.Id);
        history.Parameters.AddWithValue("$s", activity.Subject);
        history.Parameters.AddWithValue("$o", activity.OperationKey);
        history.Parameters.AddWithValue("$h", activity.InputSha256);
        history.Parameters.AddWithValue("$j", activity.Json);
        history.ExecuteNonQuery();
        using var context = c.CreateCommand();
        context.Transaction = tx;
        context.CommandText = """
            INSERT INTO W1ClockContexts VALUES($s,$r,$v,$j,$i)
            ON CONFLICT(Subject) DO UPDATE SET Revision=$r,WriterVersion=$v,ContextJson=$j,ActivityId=$i
            """;
        context.Parameters.AddWithValue("$s", state.Subject);
        context.Parameters.AddWithValue("$r", state.Revision);
        context.Parameters.AddWithValue("$v", state.WriterVersion);
        context.Parameters.AddWithValue("$j", state.ContextJson);
        context.Parameters.AddWithValue("$i", state.ActivityId);
        context.ExecuteNonQuery();
        _beforeCommitFault?.Invoke();
        tx.Commit();
    }

    public IReadOnlyList<W1JournalActivity> ReadActivities(string subject)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,Subject,OperationKey,InputSha256,Json FROM W1ClockActivities WHERE Subject=$s";
        cmd.Parameters.AddWithValue("$s", subject);
        using var r = cmd.ExecuteReader();
        var result = new List<W1JournalActivity>();
        while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        return result;
    }
}