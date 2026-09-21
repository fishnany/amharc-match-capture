import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  StartStreamingBody,
  CreateStreamingDestinationBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

let streamState: any = null;

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

function getStreamStatus(): any {
  if (!streamState || !streamState.isStreaming) {
    return {
      state: "idle",
      isStreaming: false,
      destination: null,
      uptimeSeconds: null,
      outgoingBitRate: null,
      droppedFrames: null,
      reconnectCount: 0,
      error: null,
      startedAt: null,
    };
  }

  const uptimeSeconds = Math.floor(
    (Date.now() - new Date(streamState.startedAt).getTime()) / 1000,
  );

  return {
    state: "streaming",
    isStreaming: true,
    destination: streamState.destination,
    uptimeSeconds,
    outgoingBitRate: 4_500_000,
    droppedFrames: 0,
    reconnectCount: 0,
    error: null,
    startedAt: streamState.startedAt,
  };
}

router.post("/streaming/start", async (req, res): Promise<void> => {
  const parsed = StartStreamingBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }

  const destination = destinations.get(parsed.data.destinationId);
  if (!destination) {
    res.status(400).json({ error: "Unknown streaming destination" });
    return;
  }

  streamState = {
    isStreaming: true,
    startedAt: new Date().toISOString(),
    destination: destination.name,
  };

  req.log.info(
    { destinationId: parsed.data.destinationId },
    "Streaming started",
  );
  res.json(getStreamStatus());
});

router.post("/streaming/stop", async (req, res): Promise<void> => {
  if (streamState) {
    streamState.isStreaming = false;
  }

  req.log.info("Streaming stopped");
  res.json(getStreamStatus());
});

router.get("/streaming/status", async (_req, res): Promise<void> => {
  res.json(getStreamStatus());
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

  const destinationId =
    `DEST-${randomUUID().substring(0, 6).toUpperCase()}`;

  const dest = {
    destinationId,
    ...parsed.data,
    hasStreamKey: !!parsed.data.streamKey,
    isDefault: parsed.data.isDefault ?? false,
  };

  // Do not store stream key in memory in plain text.
  delete dest.streamKey;

  destinations.set(destinationId, dest);
  res.status(201).json(dest);
});

export default router;