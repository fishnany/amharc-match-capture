import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

import type {
  BroadcastPresentationStateV1,
} from "@workspace/api-client-react";

const MATCH_HUB_PATH =
  "/hubs/match";

const JOIN_MATCH_METHOD =
  "JoinMatch";

const LEAVE_MATCH_METHOD =
  "LeaveMatch";

const PRESENTATION_UPDATED_EVENT =
  "BroadcastPresentationUpdated";

export interface BroadcastRealtimeHandlers {
  onPresentation:
    (
      presentation: BroadcastPresentationStateV1,
    ) => void;

  onReconnecting:
    (error: Error | undefined) => void;

  onReconnected:
    () => Promise<void> | void;

  onRecoveryError:
    (error: unknown) => void;

  onClosed:
    (error: Error | undefined) => void;
}

export interface BroadcastRealtimeClient {
  start:
    () => Promise<void>;

  stop:
    () => Promise<void>;
}

export function createBroadcastRealtimeClient(
  matchId: string,
  handlers: BroadcastRealtimeHandlers,
): BroadcastRealtimeClient {
  const trimmedMatchId =
    matchId.trim();

  if (!trimmedMatchId) {
    throw new Error(
      "A matchId is required to establish broadcast realtime state.",
    );
  }

  const connection =
    new HubConnectionBuilder()
      .withUrl(MATCH_HUB_PATH)
      .withAutomaticReconnect()
      .configureLogging(
        LogLevel.Warning,
      )
      .build();

  connection.on(
    PRESENTATION_UPDATED_EVENT,
    (
      presentation:
        BroadcastPresentationStateV1,
    ) => {
      if (
        presentation.matchId !==
        trimmedMatchId
      ) {
        return;
      }

      handlers.onPresentation(
        presentation,
      );
    },
  );

  connection.onreconnecting(
    (error) => {
      handlers.onReconnecting(
        error,
      );
    },
  );

  connection.onreconnected(
    async () => {
      try {
        await connection.invoke(
          JOIN_MATCH_METHOD,
          trimmedMatchId,
        );

        await handlers.onReconnected();
      }
      catch (error: unknown) {
        handlers.onRecoveryError(
          error,
        );
      }
    },
  );

  connection.onclose(
    (error) => {
      handlers.onClosed(
        error,
      );
    },
  );

  return {
    async start() {
      if (
        connection.state !==
        HubConnectionState.Disconnected
      ) {
        return;
      }

      await connection.start();

      await connection.invoke(
        JOIN_MATCH_METHOD,
        trimmedMatchId,
      );
    },

    async stop() {
      if (
        connection.state ===
        HubConnectionState.Disconnected
      ) {
        return;
      }

      try {
        if (
          connection.state ===
          HubConnectionState.Connected
        ) {
          await connection.invoke(
            LEAVE_MATCH_METHOD,
            trimmedMatchId,
          );
        }
      }
      finally {
        await connection.stop();
      }
    },
  };
}
