import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  StartStreamingParams,
  StopStreamingParams,
  GetStreamingStatusParams,
  CreateStreamingDestinationBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

const streamState = new Map<string, any>();

const destinations = new Map<string, any>([
  [
    "DEST-001",
    {
      destinationId: "DEST-001",
      name: "YouTube Live",
      platform: "youtube",
      serverUrl: "rtmp://a.rtmp.youtube.com/live2",
      hasStreamKey: true,
      resolution: "1920x1080",
      frameRate: 50,
      bitRate: 4_500_000,
      isDefault: true,
    },
  ],
]);

function getStreamStatus(matchId: string): any {
  const state = streamState.get(matchId);
  if (!state || !state.isStreaming) {
    return { isStreaming: false, destination: null, uptimeSeconds: null, outgoingBitRate: null, droppedFrames: null, reconnectCount: 0, error: null, startedAt: null };
  }
  const uptimeSeconds = Math.floor((Date.now() - new Date(state.startedAt).getTime()) / 1000);
  return {
    isStreaming: true,
    destination: state.destination,
    uptimeSeconds,
    outgoingBitRate: 4_500_000,
    droppedFrames: 0,
    reconnectCount: 0,
    error: null,
    startedAt: state.startedAt,
  };
}

router.post("/matches/:matchId/streaming/start", async (req, res): Promise<void> => {
  const params = StartStreamingParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  streamState.set(params.data.matchId, { isStreaming: true, startedAt: new Date().toISOString(), destination: "YouTube Live" });
  req.log.info({ matchId: params.data.matchId }, "Streaming started");
  res.json(getStreamStatus(params.data.matchId));
});

router.post("/matches/:matchId/streaming/stop", async (req, res): Promise<void> => {
  const params = StopStreamingParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const state = streamState.get(params.data.matchId);
  if (state) {
    state.isStreaming = false;
    streamState.set(params.data.matchId, state);
  }
  req.log.info({ matchId: params.data.matchId }, "Streaming stopped");
  res.json(getStreamStatus(params.data.matchId));
});

router.get("/matches/:matchId/streaming/status", async (req, res): Promise<void> => {
  const params = GetStreamingStatusParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  res.json(getStreamStatus(params.data.matchId));
});

router.get("/streaming/destinations", async (_req, res): Promise<void> => {
  res.json(Array.from(destinations.values()));
});

router.post("/streaming/destinations", async (req, res): Promise<void> => {
  const parsed = CreateStreamingDestinationBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const destinationId = `DEST-${randomUUID().substring(0, 6).toUpperCase()}`;
  const dest = {
    destinationId,
    ...parsed.data,
    hasStreamKey: !!parsed.data.streamKey,
    isDefault: parsed.data.isDefault ?? false,
  };
  // Do not store stream key in memory in plain text
  delete dest.streamKey;
  destinations.set(destinationId, dest);
  res.status(201).json(dest);
});

export default router;
