import { Router, type IRouter } from "express";
import {
  StartRecordingParams,
  StopRecordingParams,
  GetRecordingStatusParams,
} from "@workspace/api-zod";

const router: IRouter = Router();

// In-memory recording state
const recordingState = new Map<string, any>();

function getStatus(matchId: string): any {
  const state = recordingState.get(matchId) ?? {
    isRecording: false,
    startedAt: null,
    segmentCount: 0,
    recordingDirectory: null,
    stoppedAt: null,
  };
  let elapsedSeconds = 0;
  if (state.isRecording && state.startedAt) {
    elapsedSeconds = Math.floor((Date.now() - new Date(state.startedAt).getTime()) / 1000);
  } else if (state.stoppedAt && state.startedAt) {
    elapsedSeconds = Math.floor((new Date(state.stoppedAt).getTime() - new Date(state.startedAt).getTime()) / 1000);
  }
  return {
    isRecording: state.isRecording,
    elapsedSeconds,
    segmentCount: state.segmentCount,
    currentSegmentFile: state.isRecording ? `segment_${String(state.segmentCount).padStart(4, "0")}.mkv` : null,
    recordingDirectory: state.recordingDirectory,
    bitRate: state.isRecording ? 8_500_000 : null,
    droppedFrames: state.isRecording ? 0 : null,
    startedAt: state.startedAt,
    stoppedAt: state.stoppedAt,
  };
}

router.post("/matches/:matchId/recording/start", async (req, res): Promise<void> => {
  const params = StartRecordingParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const existing = recordingState.get(params.data.matchId);
  if (existing?.isRecording) {
    res.json(getStatus(params.data.matchId));
    return;
  }
  recordingState.set(params.data.matchId, {
    isRecording: true,
    startedAt: new Date().toISOString(),
    segmentCount: 1,
    recordingDirectory: `C:/Matches/${new Date().getFullYear()}/${params.data.matchId}`,
    stoppedAt: null,
  });
  req.log.info({ matchId: params.data.matchId }, "Recording started");
  res.json(getStatus(params.data.matchId));
});

router.post("/matches/:matchId/recording/stop", async (req, res): Promise<void> => {
  const params = StopRecordingParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const state = recordingState.get(params.data.matchId);
  if (state) {
    state.isRecording = false;
    state.stoppedAt = new Date().toISOString();
    recordingState.set(params.data.matchId, state);
  }
  req.log.info({ matchId: params.data.matchId }, "Recording stopped");
  res.json(getStatus(params.data.matchId));
});

router.get("/matches/:matchId/recording/status", async (req, res): Promise<void> => {
  const params = GetRecordingStatusParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  res.json(getStatus(params.data.matchId));
});

export default router;
