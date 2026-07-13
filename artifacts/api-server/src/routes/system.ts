import { Router, type IRouter } from "express";

const router: IRouter = Router();

router.get("/system/status", async (_req, res): Promise<void> => {
  res.json({
    version: "0.1.0",
    uptime: Math.floor(process.uptime()),
    camera: "disconnected",
    recording: "idle",
    streaming: "idle",
    storage: {
      totalBytes: 500_000_000_000,
      usedBytes: 120_000_000_000,
      availableBytes: 380_000_000_000,
      availableMinutes: 420,
      recordingDirectory: "C:/Matches",
      warningLevel: "ok",
      isExternalStorage: false,
    },
    streamDeck: "disconnected",
    joystick: "disconnected",
    overlay: "inactive",
    audio: "none",
    warnings: [],
  });
});

export default router;
