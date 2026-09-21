import {
  useEffect,
  useState,
} from "react";

import type {
  BroadcastPresentationStateV1,
} from "@workspace/api-client-react";

import {
  initialBroadcastRendererState,
  type BroadcastRendererState,
} from "./broadcastState";

import {
  loadBroadcastPresentation,
} from "./broadcastClient";

import {
  createBroadcastRealtimeClient,
} from "./broadcastRealtimeClient";

import {
  resolveMatchId,
} from "./matchId";

const STALE_AFTER_MS =
  15_000;

function createPresentationState(
  matchId: string,
  presentation: BroadcastPresentationStateV1,
): BroadcastRendererState {
  const receivedAt =
    Date.now();

  const observedAt =
    Date.parse(
      presentation.clock.observedAtUtc,
    );

  const stale =
    Number.isFinite(observedAt) &&
    receivedAt - observedAt >
      STALE_AFTER_MS;

  return {
    status: stale
      ? "stale"
      : "live",
    matchId,
    presentation,
    lastReceivedAt: receivedAt,
    errorMessage: null,
  };
}

function describeError(
  error: unknown,
  fallback: string,
): string {
  return error instanceof Error
    ? error.message
    : fallback;
}

export function useBroadcastPresentation():
  BroadcastRendererState {
  const [
    state,
    setState,
  ] =
    useState<BroadcastRendererState>(
      initialBroadcastRendererState,
    );

  useEffect(() => {
    let cancelled =
      false;

    let recovering =
      false;

    let queuedPresentation:
      BroadcastPresentationStateV1 | null =
        null;

    const matchId =
      resolveMatchId();

    if (!matchId) {
      setState({
        status: "error",
        matchId: null,
        presentation: null,
        lastReceivedAt: null,
        errorMessage:
          "Renderer URL does not contain a matchId.",
      });

      return;
    }

    const applyPresentation =
      (
        presentation:
          BroadcastPresentationStateV1,
      ) => {
        if (cancelled) {
          return;
        }

        if (
          presentation.matchId !==
          matchId
        ) {
          return;
        }

        setState(
          createPresentationState(
            matchId,
            presentation,
          ),
        );
      };

    const receiveRealtimePresentation =
      (
        presentation:
          BroadcastPresentationStateV1,
      ) => {
        if (cancelled) {
          return;
        }

        if (
          presentation.matchId !==
          matchId
        ) {
          return;
        }

        if (recovering) {
          queuedPresentation =
            presentation;

          return;
        }

        applyPresentation(
          presentation,
        );
      };

    const markStale =
      (
        message: string | null =
          null,
      ) => {
        if (cancelled) {
          return;
        }

        setState(
          (current) => ({
            ...current,
            status:
              current.presentation
                ? "stale"
                : "error",
            errorMessage:
              message,
          }),
        );
      };

    const recoverCanonicalState =
      async () => {
        recovering =
          true;

        try {
          const presentation =
            await loadBroadcastPresentation(
              matchId,
            );

          if (cancelled) {
            return;
          }

          applyPresentation(
            presentation,
          );

          const queued =
            queuedPresentation;

          queuedPresentation =
            null;

          if (queued) {
            applyPresentation(
              queued,
            );
          }
        }
        finally {
          recovering =
            false;
        }
      };

    const realtime =
      createBroadcastRealtimeClient(
        matchId,
        {
          onPresentation:
            receiveRealtimePresentation,

          onReconnecting:
            (error) => {
              recovering =
                true;

              queuedPresentation =
                null;

              markStale(
                error?.message ??
                  "Broadcast realtime connection interrupted.",
              );
            },

          onReconnected:
            async () => {
              try {
                await recoverCanonicalState();
              }
              catch (error: unknown) {
                markStale(
                  describeError(
                    error,
                    "Unable to recover canonical broadcast state after realtime reconnection.",
                  ),
                );
              }
            },

          onRecoveryError:
            (error) => {
              recovering =
                false;

              markStale(
                describeError(
                  error,
                  "Unable to rejoin canonical broadcast realtime state.",
                ),
              );
            },

          onClosed:
            (error) => {
              recovering =
                false;

              queuedPresentation =
                null;

              markStale(
                error?.message ??
                  "Broadcast realtime connection closed.",
              );
            },
        },
      );

    setState({
      status: "loading",
      matchId,
      presentation: null,
      lastReceivedAt: null,
      errorMessage: null,
    });

    void (
      async () => {
        try {
          /*
           * REST establishes the initial canonical presentation.
           * The renderer never derives score or advances match time.
           */
          const bootstrapPresentation =
            await loadBroadcastPresentation(
              matchId,
            );

          if (cancelled) {
            return;
          }

          applyPresentation(
            bootstrapPresentation,
          );

          /*
           * Gate realtime messages until the post-join recovery
           * snapshot has closed the REST-to-SignalR publication gap.
           */
          recovering =
            true;

          queuedPresentation =
            null;

          await realtime.start();

          if (cancelled) {
            await realtime.stop();

            return;
          }

          await recoverCanonicalState();
        }
        catch (error: unknown) {
          if (cancelled) {
            return;
          }

          recovering =
            false;

          markStale(
            describeError(
              error,
              "Unable to establish canonical broadcast presentation state.",
            ),
          );
        }
      }
    )();

    return () => {
      cancelled =
        true;

      recovering =
        false;

      queuedPresentation =
        null;

      void realtime.stop();
    };
  }, []);

  return state;
}
