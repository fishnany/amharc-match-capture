using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Commands;

/// <summary>
/// Dispatches semantic AMHARC commands to domain services.
/// Input devices such as Stream Deck, operator UI, tablets and future
/// voice controls should issue commands through this layer rather than
/// invoking match-domain services directly.
/// </summary>
public class AmharcCommandDispatcher(
    IMatchRepository matches,
    IEventTaggingService events,
    IMatchClockService clock,
    ILogger<AmharcCommandDispatcher> logger)
    : IAmharcCommandDispatcher
{
    public async Task DispatchAsync(
        AmharcCommand command,
        CancellationToken ct = default)
    {
        switch (command.CommandId)
        {
            case AmharcCommandIds.ScoreHomeGoal:
                await CreateScoreEventAsync(
                    command,
                    "goal",
                    EventTeam.Home,
                    ct);
                break;

            case AmharcCommandIds.ScoreHomeTwoPoint:
                await CreateScoreEventAsync(
                    command,
                    "two-point-score",
                    EventTeam.Home,
                    ct);
                break;

            case AmharcCommandIds.ScoreHomePoint:
                await CreateScoreEventAsync(
                    command,
                    "point",
                    EventTeam.Home,
                    ct);
                break;

            case AmharcCommandIds.ScoreAwayGoal:
                await CreateScoreEventAsync(
                    command,
                    "goal",
                    EventTeam.Away,
                    ct);
                break;

            case AmharcCommandIds.ScoreAwayTwoPoint:
                await CreateScoreEventAsync(
                    command,
                    "two-point-score",
                    EventTeam.Away,
                    ct);
                break;

            case AmharcCommandIds.ScoreAwayPoint:
                await CreateScoreEventAsync(
                    command,
                    "point",
                    EventTeam.Away,
                    ct);
                break;

            case AmharcCommandIds.MatchClockStart:
                clock.Start();
                LogCommand(command);
                break;

            case AmharcCommandIds.MatchClockPause:
                clock.Pause();
                LogCommand(command);
                break;

            case AmharcCommandIds.MatchClockResume:
                clock.Resume();
                LogCommand(command);
                break;

            case AmharcCommandIds.EventUndo:
                {
                    var matchId =
                        await ResolveMatchIdAsync(command, ct);

                    await events.UndoLastEventAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            default:
                throw new NotSupportedException(
                    $"AMHARC command '{command.CommandId}' is not supported.");
        }
    }

    private async Task CreateScoreEventAsync(
        AmharcCommand command,
        string eventType,
        EventTeam team,
        CancellationToken ct)
    {
        var matchId =
            await ResolveMatchIdAsync(command, ct);

        var state = clock.State;

        var options = new CreateEventOptions(
            MatchId: matchId,
            EventType: eventType,
            Team: team,
            PlayerNumber: null,
            Period: state.CurrentPeriod,
            MatchClockSeconds: state.MatchClockSeconds,
            RecordingElapsedSeconds: state.RecordingElapsedSeconds,
            Source: command.Source,
            Note: null,
            ClipRequested: false,
            Operator: command.Operator);

        await events.CreateEventAsync(
            options,
            ct);

        LogCommand(command);
    }

    private async Task<string> ResolveMatchIdAsync(
        AmharcCommand command,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(command.MatchId))
            return command.MatchId;

        var activeMatch =
            await matches.GetActiveMatchAsync(ct);

        if (activeMatch is null)
        {
            throw new InvalidOperationException(
                "No active AMHARC match is available for this command.");
        }

        return activeMatch.MatchId;
    }

    private void LogCommand(
        AmharcCommand command)
    {
        logger.LogInformation(
            "AMHARC command dispatched: {CommandId} from {Source}",
            command.CommandId,
            command.Source);
    }
}
