using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Exceptions;
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
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status is MatchStatus.Complete or MatchStatus.Abandoned)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot be started from terminal state {match.Status}.");
                    }

                    var liveMatch =
                        await matches.GetActiveMatchAsync(ct);

                    if (liveMatch is not null &&
                        liveMatch.MatchId != matchId)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Cannot start match {matchId} because match {liveMatch.MatchId} " +
                            $"is already operationally live with status {liveMatch.Status}.");
                    }

                    clock.Start();

                    match.Status =
                        MatchStatus.Active;

                    if (match.CurrentPeriod <= 0)
                    {
                        match.CurrentPeriod =
                            1;
                    }

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockPause:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status is MatchStatus.Complete or MatchStatus.Abandoned)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot be paused from terminal state {match.Status}.");
                    }

                    clock.Pause();

                    match.Status =
                        MatchStatus.Paused;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockResume:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status is MatchStatus.Complete or MatchStatus.Abandoned)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot be resumed from terminal state {match.Status}.");
                    }

                    var liveMatch =
                        await matches.GetActiveMatchAsync(ct);

                    if (liveMatch is not null &&
                        liveMatch.MatchId != matchId)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Cannot resume match {matchId} because match {liveMatch.MatchId} " +
                            $"is already operationally live with status {liveMatch.Status}.");
                    }

                    clock.Resume();

                    match.Status =
                        MatchStatus.Active;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockHalfTimeStart:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.Active)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot enter half-time from state {match.Status}.");
                    }

                    if (match.PeriodStructure is not
                        (PeriodStructure.TwoPeriods or PeriodStructure.ExtraTime))
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot use regulation half-time lifecycle with period structure {match.PeriodStructure}.");
                    }

                    if (match.CurrentPeriod != 1)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot enter half-time from period {match.CurrentPeriod}.");
                    }

                    clock.EndPeriod(
                        match.CurrentPeriod);

                    clock.StartHalfTime();

                    match.Status =
                        MatchStatus.HalfTime;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockHalfTimeEnd:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.HalfTime)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot end half-time from state {match.Status}.");
                    }

                    if (match.PeriodStructure is not
                        (PeriodStructure.TwoPeriods or PeriodStructure.ExtraTime))
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot use regulation half-time lifecycle with period structure {match.PeriodStructure}.");
                    }

                    if (match.CurrentPeriod != 1)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot end half-time from period {match.CurrentPeriod}.");
                    }

                    const int nextPeriod =
                        2;

                    clock.EndHalfTime();

                    clock.StartPeriod(
                        nextPeriod);

                    clock.Resume();

                    match.CurrentPeriod =
                        nextPeriod;

                    match.Status =
                        MatchStatus.Active;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockExtraTimeEnter:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.Active ||
                        match.PeriodStructure != PeriodStructure.ExtraTime ||
                        match.CurrentPeriod != 2)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot enter the extra-time interval from state {match.Status}, period {match.CurrentPeriod}, structure {match.PeriodStructure}.");
                    }

                    clock.EndPeriod(2);
                    clock.Pause();

                    match.Status =
                        MatchStatus.ExtraTimeInterval;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockExtraTimeStart:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.ExtraTimeInterval ||
                        match.PeriodStructure != PeriodStructure.ExtraTime ||
                        match.CurrentPeriod != 2)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot start ET1 from state {match.Status}, period {match.CurrentPeriod}, structure {match.PeriodStructure}.");
                    }

                    const int et1Period = 3;

                    clock.StartPeriod(et1Period);
                    clock.Resume();

                    match.CurrentPeriod =
                        et1Period;

                    match.Status =
                        MatchStatus.Active;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockExtraTimeHalfTimeStart:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.Active ||
                        match.PeriodStructure != PeriodStructure.ExtraTime ||
                        match.CurrentPeriod != 3)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot enter the ET half-time interval from state {match.Status}, period {match.CurrentPeriod}, structure {match.PeriodStructure}.");
                    }

                    clock.EndPeriod(3);
                    clock.Pause();

                    match.Status =
                        MatchStatus.ExtraTimeInterval;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockExtraTimeHalfTimeEnd:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status != MatchStatus.ExtraTimeInterval ||
                        match.PeriodStructure != PeriodStructure.ExtraTime ||
                        match.CurrentPeriod != 3)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot start ET2 from state {match.Status}, period {match.CurrentPeriod}, structure {match.PeriodStructure}.");
                    }

                    const int et2Period = 4;

                    clock.StartPeriod(et2Period);
                    clock.Resume();

                    match.CurrentPeriod =
                        et2Period;

                    match.Status =
                        MatchStatus.Active;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }


            case AmharcCommandIds.MatchAbandon:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);

                    if (match.Status is MatchStatus.Setup or
                        MatchStatus.Complete or
                        MatchStatus.Abandoned)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot be abandoned from state {match.Status}.");
                    }

                    clock.Pause();

                    match.Status =
                        MatchStatus.Abandoned;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }


            case AmharcCommandIds.MatchClockFullTime:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    var match =
                        await RequireMatchAsync(
                            matchId,
                            ct);


                    if (match.Status is MatchStatus.Setup or
                        MatchStatus.Ready or
                        MatchStatus.Complete or
                        MatchStatus.Abandoned)
                    {
                        throw new MatchLifecycleConflictException(
                            $"Match {matchId} cannot reach full-time from state {match.Status}.");
                    }

                    clock.MarkFullTime();

                    match.Status =
                        MatchStatus.Complete;

                    await matches.UpdateAsync(
                        match,
                        ct);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

            case AmharcCommandIds.MatchClockCorrect:
                {
                    var matchId =
                        await ResolveMatchIdAsync(
                            command,
                            ct);

                    if (command.Parameters is null ||
                        !command.Parameters.TryGetValue(
                            "matchClockSeconds",
                            out var secondsValue) ||
                        !int.TryParse(
                            secondsValue,
                            out var matchClockSeconds))
                    {
                        throw new ArgumentException(
                            "Clock correction requires a valid 'matchClockSeconds' parameter.");
                    }

                    command.Parameters.TryGetValue(
                        "reason",
                        out var reason);

                    clock.Correct(
                        matchClockSeconds,
                        reason);

                    await clock.SaveRuntimeStateAsync(
                        matchId,
                        ct);

                    LogCommand(command);
                    break;
                }

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

    private async Task<Match> RequireMatchAsync(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await matches.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
        {
            throw new InvalidOperationException(
                $"Match {matchId} could not be found.");
        }

        return match;
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
