import {
  getMatchBroadcastPresentation,
} from "@workspace/api-client-react";

import type {
  BroadcastPresentationStateV1,
} from "@workspace/api-client-react";

export async function loadBroadcastPresentation(
  matchId: string,
): Promise<BroadcastPresentationStateV1> {
  if (!matchId.trim()) {
    throw new Error(
      "A matchId is required to load broadcast presentation state.",
    );
  }

  return getMatchBroadcastPresentation(matchId);
}
