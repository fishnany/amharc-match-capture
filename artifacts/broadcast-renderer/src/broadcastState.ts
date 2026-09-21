import type {
  BroadcastPresentationStateV1,
} from "@workspace/api-client-react";

export type BroadcastConnectionStatus =
  | "loading"
  | "live"
  | "stale"
  | "error";

export interface BroadcastRendererState {
  status: BroadcastConnectionStatus;
  matchId: string | null;
  presentation: BroadcastPresentationStateV1 | null;
  lastReceivedAt: number | null;
  errorMessage: string | null;
}

export const initialBroadcastRendererState: BroadcastRendererState = {
  status: "loading",
  matchId: null,
  presentation: null,
  lastReceivedAt: null,
  errorMessage: null,
};
