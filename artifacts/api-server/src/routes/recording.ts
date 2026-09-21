import { Router, type IRouter } from "express";
import { StartRecordingBody } from "@workspace/api-zod";

const router: IRouter = Router();

// In-memory recording state
let activeMatchId: string | null = null;
const recordingState = new Map<string, any>();

function getStatus(matchId: string | null): any {
  const state = matchId ? recordingState.get(matchId) : undefined;
  if (!state) {
    return {
      state: "idle",
      elapsedSeconds: 0,
      segmentCount: 0,
      outputDirectory: null,
      segments: [],
    };
  }

  let elapsedSeconds = 0;
  if (state.isRecording && state.startedAt) {
    elapsedSeconds = Math.floor(
      (Date.now() - new Date(state.startedAt).getTime()) / 1000,
    );
  } else if (state.stoppedAt && state.startedAt) {
    elapsedSeconds = Math.floor(
      (new Date(state.stoppedAt).getTime() -
        new Date(state.startedAt).getTime()) /
        1000,
    );
  }

  return {
    state: state.isRecording ? "recording" : "idle",
    elapsedSeconds,
    segmentCount: state.segmentCount,
    outputDirectory: state.recordingDirectory,
    segments: [],
  };
}

router.post("/recording/start", async (req, res): Promise<void> => {
  const parsed = StartRecordingBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }

  const { matchId, outputDirectory } = parsed.data;

  if (activeMatchId && activeMatchId !== matchId) {
    const activeState = recordingState.get(activeMatchId);
    if (activeState?.isRecording) {
      res.status(409).json({
        error: `Recording already active for match '${activeMatchId}'`,
      });
      return;
    }
  }

  const existing = recordingState.get(matchId);
  if (existing?.isRecording) {
    activeMatchId = matchId;
    res.json(getStatus(matchId));
    return;
  }

  recordingState.set(matchId, {
    isRecording: true,
    startedAt: new Date().toISOString(),
    segmentCount: 1,
    recordingDirectory:
      outputDirectory ??
      `C:/Matches/${new Date().getFullYear()}/${matchId}`,
    stoppedAt: null,
  });
  activeMatchId = matchId;

  req.log.info({ matchId }, "Recording started");
  res.json(getStatus(matchId));
});

router.post("/recording/stop", async (req, res): Promise<void> => {
  const matchId = activeMatchId;
  if (matchId) {
    const state = recordingState.get(matchId);
    if (state) {
      state.isRecording = false;
      state.stoppedAt = new Date().toISOString();
      recordingState.set(matchId, state);
    }
    req.log.info({ matchId }, "Recording stopped");
  }

  res.json(getStatus(matchId));
});

router.get("/recording/status", async (_req, res): Promise<void> => {
  res.json(getStatus(activeMatchId));
});

export default router;