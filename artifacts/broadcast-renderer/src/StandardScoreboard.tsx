import type {
  BroadcastPresentationStateV1,
} from "@workspace/api-client-react";

export interface StandardScoreboardProps {
  presentation: BroadcastPresentationStateV1;
}

function formatClock(
  seconds: number,
): string {
  const wholeSeconds =
    Math.max(
      0,
      Math.trunc(seconds),
    );

  const minutes =
    Math.floor(
      wholeSeconds / 60,
    );

  const remainingSeconds =
    wholeSeconds % 60;

  return `${minutes}:${remainingSeconds
    .toString()
    .padStart(2, "0")}`;
}

function formatPeriod(
  period: number,
): string {
  if (period <= 0) {
    return "PRE";
  }

  return `P${period}`;
}

export function StandardScoreboard({
  presentation,
}: StandardScoreboardProps) {
  const {
    match,
    score,
    clock,
  } =
    presentation;

  const competitionContext =
    match.round
      ? `${match.competition} · ${match.round}`
      : match.competition;

  return (
    <section
      className="standard-scoreboard"
      aria-label="Match scoreboard"
      data-template="standard-scoreboard"
      data-match-id={presentation.matchId}
      data-contract-version={
        presentation.contractVersion
      }
      data-output-mode={
        presentation.presentation.outputMode
      }
      data-active-template={
        presentation.presentation.activeTemplateId ??
        ""
      }
      data-home-display={
        score.homeDisplay
      }
      data-away-display={
        score.awayDisplay
      }
      data-clock-sequence={
        clock.sequence
      }
      data-clock-running={
        clock.isRunning
          ? "true"
          : "false"
      }
    >
      <div className="standard-scoreboard__context">
        <span className="standard-scoreboard__brand">
          AMHARC
        </span>

        <span className="standard-scoreboard__competition">
          {competitionContext}
        </span>
      </div>

      <div className="standard-scoreboard__body">
        <div className="standard-scoreboard__teams">
          <div className="standard-scoreboard__team">
            <span className="standard-scoreboard__team-name">
              {match.homeTeam}
            </span>

            <span
              className="standard-scoreboard__score"
              aria-label={`${match.homeTeam} ${score.homeDisplay}`}
            >
              {score.homeDisplay}
            </span>
          </div>

          <div className="standard-scoreboard__divider" />

          <div className="standard-scoreboard__team">
            <span className="standard-scoreboard__team-name">
              {match.awayTeam}
            </span>

            <span
              className="standard-scoreboard__score"
              aria-label={`${match.awayTeam} ${score.awayDisplay}`}
            >
              {score.awayDisplay}
            </span>
          </div>
        </div>

        <div className="standard-scoreboard__clock">
          <span className="standard-scoreboard__period">
            {formatPeriod(
              clock.period,
            )}
          </span>

          <span
            className="standard-scoreboard__clock-value"
            data-period-clock-seconds={
              clock.periodClockSeconds
            }
          >
            {formatClock(
              clock.periodClockSeconds,
            )}
          </span>
        </div>
      </div>
    </section>
  );
}
