import "./renderer.css";

import {
  StandardScoreboard,
} from "./StandardScoreboard";

import {
  resolveBroadcastTemplate,
} from "./broadcastTemplate";

import {
  useBroadcastPresentation,
} from "./useBroadcastPresentation";

export function App() {
  const state =
    useBroadcastPresentation();

  const presentation =
    state.presentation;

  const templateResolution =
    resolveBroadcastTemplate(
      presentation?.presentation
        .activeTemplateId,
    );

  const scoreboardVisible =
    presentation?.presentation
      .scoreboardVisible === true;

  const renderStandardScoreboard =
    presentation !== null &&
    scoreboardVisible &&
    templateResolution.template ===
      "standard-scoreboard";

  return (
    <main
      className="broadcast-canvas"
      data-renderer="amharc-broadcast-renderer"
      data-connection-status={state.status}
      data-match-id={
        presentation?.matchId ??
        state.matchId ??
        ""
      }
      data-contract-version={
        presentation?.contractVersion ??
        ""
      }
      data-scoreboard-visible={
        scoreboardVisible
          ? "true"
          : "false"
      }
      data-requested-template={
        templateResolution
          .requestedTemplateId ??
        ""
      }
      data-resolved-template={
        templateResolution.template
      }
      data-output-mode={
        presentation?.presentation
          .outputMode ??
        ""
      }
    >
      <div className="safe-area">
        {renderStandardScoreboard && (
          <StandardScoreboard
            presentation={presentation}
          />
        )}

        {!presentation &&
          state.status === "loading" && (
            <div
              className="renderer-diagnostic"
              role="status"
              aria-live="polite"
            >
              Loading canonical broadcast state
            </div>
          )}

        {!presentation &&
          state.status === "error" && (
            <div
              className="renderer-diagnostic renderer-diagnostic--error"
              role="status"
            >
              {state.errorMessage ??
                "Broadcast state unavailable"}
            </div>
          )}
      </div>
    </main>
  );
}
