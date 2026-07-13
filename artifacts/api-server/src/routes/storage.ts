import { Router, type IRouter } from "express";

const router: IRouter = Router();

router.get("/storage/status", async (_req, res): Promise<void> => {
  // Mock storage status — in production this would query the Windows filesystem
  const totalBytes = 500_000_000_000; // 500 GB
  const usedBytes = 120_000_000_000;  // 120 GB used
  const availableBytes = totalBytes - usedBytes;
  // Estimate: ~8.5 Mbps recording bitrate → ~63.75 MB/min
  const availableMinutes = Math.floor(availableBytes / (63_750_000));

  res.json({
    totalBytes,
    usedBytes,
    availableBytes,
    availableMinutes,
    recordingDirectory: "C:/Matches",
    warningLevel: availableMinutes < 30 ? "critical" : availableMinutes < 90 ? "warning" : "ok",
    isExternalStorage: false,
  });
});

export default router;
