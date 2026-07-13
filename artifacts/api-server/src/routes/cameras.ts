import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  CreateCameraBody,
  UpdateCameraBody,
  GetCameraParams,
  UpdateCameraParams,
  DeleteCameraParams,
  ConnectCameraParams,
  DisconnectCameraParams,
  TestCameraParams,
  SendPtzCommandParams,
  SendPtzCommandBody,
  GetCameraPresetsParams,
  SaveCameraPresetParams,
  SaveCameraPresetBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

// In-memory mock store for development
const cameras = new Map<string, any>([
  [
    "CAM-AXIS-001",
    {
      cameraId: "CAM-AXIS-001",
      name: "Primary PTZ",
      manufacturer: "axis",
      adapter: "AxisVapixAdapter",
      model: "AXIS Q6128-E",
      ipAddress: "192.168.1.100",
      rtspUrl: "rtsp://192.168.1.100/axis-media/media.amp",
      username: "admin",
      streamProfile: "Quality",
      codec: "H.264",
      resolution: "1920x1080",
      frameRate: 50,
      connectionState: "disconnected",
      healthState: null,
      firmwareVersion: "10.12.1",
      serialNumber: "ACCC8E000001",
      bitRate: null,
      droppedFrames: null,
      createdAt: new Date().toISOString(),
      updatedAt: null,
    },
  ],
]);

const presets = new Map<string, any[]>([
  [
    "CAM-AXIS-001",
    [
      { presetId: "P1", name: "Full Pitch", cameraId: "CAM-AXIS-001", description: "Wide view of the full playing surface", isDefault: true },
      { presetId: "P2", name: "Midfield", cameraId: "CAM-AXIS-001", description: "Centre circle area", isDefault: false },
      { presetId: "P3", name: "Left Goal", cameraId: "CAM-AXIS-001", description: "Home team goal area", isDefault: false },
      { presetId: "P4", name: "Right Goal", cameraId: "CAM-AXIS-001", description: "Away team goal area", isDefault: false },
      { presetId: "P5", name: "Home Position", cameraId: "CAM-AXIS-001", description: "Default home position", isDefault: false },
    ],
  ],
]);

router.get("/cameras", async (_req, res): Promise<void> => {
  res.json(Array.from(cameras.values()));
});

router.post("/cameras", async (req, res): Promise<void> => {
  const parsed = CreateCameraBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const cameraId = `CAM-${randomUUID().substring(0, 8).toUpperCase()}`;
  const camera = {
    cameraId,
    ...parsed.data,
    connectionState: "disconnected",
    healthState: null,
    firmwareVersion: null,
    serialNumber: null,
    bitRate: null,
    droppedFrames: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
  };
  cameras.set(cameraId, camera);
  presets.set(cameraId, []);
  res.status(201).json(camera);
});

router.get("/cameras/:cameraId", async (req, res): Promise<void> => {
  const params = GetCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const camera = cameras.get(params.data.cameraId);
  if (!camera) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  res.json(camera);
});

router.put("/cameras/:cameraId", async (req, res): Promise<void> => {
  const params = UpdateCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const camera = cameras.get(params.data.cameraId);
  if (!camera) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  const parsed = UpdateCameraBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const updated = { ...camera, ...parsed.data, updatedAt: new Date().toISOString() };
  cameras.set(params.data.cameraId, updated);
  res.json(updated);
});

router.delete("/cameras/:cameraId", async (req, res): Promise<void> => {
  const params = DeleteCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  if (!cameras.has(params.data.cameraId)) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  cameras.delete(params.data.cameraId);
  presets.delete(params.data.cameraId);
  res.sendStatus(204);
});

router.post("/cameras/:cameraId/connect", async (req, res): Promise<void> => {
  const params = ConnectCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const camera = cameras.get(params.data.cameraId);
  if (!camera) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  // Mock: simulate a connection attempt
  camera.connectionState = "connected";
  camera.bitRate = 8_500_000;
  camera.droppedFrames = 0;
  cameras.set(params.data.cameraId, camera);
  res.json({ success: true, connectionState: "connected", cameraId: params.data.cameraId, message: null });
});

router.post("/cameras/:cameraId/disconnect", async (req, res): Promise<void> => {
  const params = DisconnectCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const camera = cameras.get(params.data.cameraId);
  if (!camera) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  camera.connectionState = "disconnected";
  camera.bitRate = null;
  cameras.set(params.data.cameraId, camera);
  res.json({ success: true, message: "Camera disconnected" });
});

router.post("/cameras/:cameraId/test", async (req, res): Promise<void> => {
  const params = TestCameraParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const camera = cameras.get(params.data.cameraId);
  if (!camera) {
    res.status(404).json({ error: "Camera not found" });
    return;
  }
  // Mock test result
  res.json({
    success: true,
    latencyMs: 18,
    resolution: camera.resolution ?? "1920x1080",
    frameRate: camera.frameRate ?? 50,
    bitRate: 8_500_000,
    message: null,
  });
});

router.post("/cameras/:cameraId/ptz", async (req, res): Promise<void> => {
  const params = SendPtzCommandParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = SendPtzCommandBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  req.log.info({ cameraId: params.data.cameraId, action: parsed.data.action }, "PTZ command received");
  res.json({ success: true, message: `PTZ command '${parsed.data.action}' sent` });
});

router.get("/cameras/:cameraId/presets", async (req, res): Promise<void> => {
  const params = GetCameraPresetsParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const cameraPresets = presets.get(params.data.cameraId) ?? [];
  res.json(cameraPresets);
});

router.post("/cameras/:cameraId/presets", async (req, res): Promise<void> => {
  const params = SaveCameraPresetParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = SaveCameraPresetBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const presetId = `P${Date.now()}`;
  const preset = {
    presetId,
    cameraId: params.data.cameraId,
    ...parsed.data,
    isDefault: parsed.data.isDefault ?? false,
  };
  const cameraPresets = presets.get(params.data.cameraId) ?? [];
  cameraPresets.push(preset);
  presets.set(params.data.cameraId, cameraPresets);
  res.status(201).json(preset);
});

export default router;
