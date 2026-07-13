/**
 * AMHARC Match Capture — Mock Interface Implementations
 *
 * These mocks are used in Replit development and unit tests where no physical
 * hardware (camera, Stream Deck, joystick) is available. All production
 * interfaces are identical to the mock interfaces.
 *
 * @version 0.1.0
 */

import type {
  ICameraAdapter,
  CameraConnectionState,
  CameraInfo,
  StreamProfile,
  CameraHealth,
  IStreamReceiver,
  StreamStats,
  IRecordingManager,
  RecordingState,
  RecordingSegmentInfo,
  RecordingOptions,
  IPtzController,
  PtzDirection,
  ZoomDirection,
  PtzPreset,
  IJoystickService,
  JoystickAxisState,
  JoystickConfig,
  IStreamDeckService,
  StreamDeckButtonConfig,
  StreamDeckProfile,
  IMatchClockService,
  ClockState,
  ClockCorrectionAuditEntry,
  ClockMode,
  IEventTaggingService,
  MatchEvent,
  CreateEventOptions,
  IOverlayService,
  OverlayState,
  OverlayOutputMode,
  IStreamingService,
  StreamingState,
  StreamingStats,
  StreamingDestinationConfig,
  IStorageMonitor,
  StorageStatus,
  IHealthMonitoringService,
  SystemHealth,
} from "./interfaces";

// ---------------------------------------------------------------------------
// MockCameraAdapter
// ---------------------------------------------------------------------------

export class MockCameraAdapter implements ICameraAdapter {
  readonly cameraId = "MOCK-CAM-001";
  readonly manufacturer = "mock";
  readonly model = "Mock PTZ Camera v1.0";
  connectionState: CameraConnectionState = "disconnected";

  private _connectionHandlers: Array<(state: CameraConnectionState) => void> = [];
  private _healthHandlers: Array<(health: CameraHealth) => void> = [];

  async connect(): Promise<void> {
    this.connectionState = "connecting";
    this._connectionHandlers.forEach((h) => h("connecting"));
    await delay(800);
    this.connectionState = "connected";
    this._connectionHandlers.forEach((h) => h("connected"));
  }

  async disconnect(): Promise<void> {
    this.connectionState = "disconnected";
    this._connectionHandlers.forEach((h) => h("disconnected"));
  }

  async getStreamUrl(): Promise<string> {
    return "rtsp://mock-camera/stream1";
  }

  async getCameraInfo(): Promise<CameraInfo> {
    return {
      model: "Mock PTZ Camera v1.0",
      serialNumber: "MOCK-0001",
      firmwareVersion: "1.0.0-mock",
      macAddress: "AA:BB:CC:DD:EE:FF",
    };
  }

  async getStreamProfiles(): Promise<StreamProfile[]> {
    return [
      { name: "Quality", resolution: "1920x1080", frameRate: 50, codec: "H.264", bitRate: 8_000_000 },
      { name: "Bandwidth", resolution: "1280x720", frameRate: 25, codec: "H.264", bitRate: 3_000_000 },
    ];
  }

  async reconnect(): Promise<void> {
    await this.connect();
  }

  onConnectionStateChanged(handler: (state: CameraConnectionState) => void): void {
    this._connectionHandlers.push(handler);
  }

  onHealthChanged(handler: (health: CameraHealth) => void): void {
    this._healthHandlers.push(handler);
  }

  /** Simulate a camera disconnection for testing. */
  simulateDisconnect(): void {
    this.connectionState = "error";
    this._connectionHandlers.forEach((h) => h("error"));
  }
}

// ---------------------------------------------------------------------------
// MockPtzController
// ---------------------------------------------------------------------------

export class MockPtzController implements IPtzController {
  private _presets: PtzPreset[] = [
    { presetId: "P1", name: "Full Pitch", isHome: false, description: null },
    { presetId: "P2", name: "Home Position", isHome: true, description: null },
    { presetId: "P3", name: "Left Goal", isHome: false, description: null },
    { presetId: "P4", name: "Right Goal", isHome: false, description: null },
  ];

  async pan(_direction: PtzDirection, _speed: number): Promise<void> { await delay(10); }
  async tilt(_direction: PtzDirection, _speed: number): Promise<void> { await delay(10); }
  async zoom(_direction: ZoomDirection, _speed: number): Promise<void> { await delay(10); }
  async moveAbsolute(_pan: number, _tilt: number, _zoom: number): Promise<void> { await delay(50); }
  async stop(): Promise<void> { await delay(10); }
  async goHome(): Promise<void> { await delay(200); }

  async recallPreset(presetId: string): Promise<void> {
    const preset = this._presets.find((p) => p.presetId === presetId);
    if (!preset) throw new Error(`Preset ${presetId} not found`);
    await delay(300);
  }

  async savePreset(presetId: string, name: string): Promise<void> {
    const existing = this._presets.findIndex((p) => p.presetId === presetId);
    const preset: PtzPreset = { presetId, name, isHome: false, description: null };
    if (existing >= 0) {
      this._presets[existing] = preset;
    } else {
      this._presets.push(preset);
    }
  }

  async emergencyWide(): Promise<void> {
    await this.recallPreset("P1");
  }

  async getPresets(): Promise<PtzPreset[]> {
    return [...this._presets];
  }
}

// ---------------------------------------------------------------------------
// MockMatchClockService
// ---------------------------------------------------------------------------

export class MockMatchClockService implements IMatchClockService {
  private _matchClockSeconds = 0;
  private _recordingElapsedSeconds = 0;
  private _isRunning = false;
  private _currentPeriod = 1;
  private _clockMode: ClockMode = "count-up";
  private _startedAt: Date | null = null;
  private _auditLog: ClockCorrectionAuditEntry[] = [];
  private _handlers: Array<(state: ClockState) => void> = [];
  private _intervalId: ReturnType<typeof setInterval> | null = null;

  get state(): ClockState {
    return {
      matchClockSeconds: this._matchClockSeconds,
      recordingElapsedSeconds: this._recordingElapsedSeconds,
      isRunning: this._isRunning,
      currentPeriod: this._currentPeriod,
      clockMode: this._clockMode,
      updatedAt: new Date(),
    };
  }

  start(): void {
    if (this._isRunning) return;
    this._isRunning = true;
    this._startedAt = new Date();
    this._intervalId = setInterval(() => {
      this._matchClockSeconds++;
      this._recordingElapsedSeconds++;
      this._handlers.forEach((h) => h(this.state));
    }, 1000);
  }

  pause(): void {
    if (!this._isRunning) return;
    this._isRunning = false;
    if (this._intervalId) clearInterval(this._intervalId);
  }

  resume(): void {
    this.start();
  }

  reset(): void {
    this.pause();
    this._matchClockSeconds = 0;
    this._recordingElapsedSeconds = 0;
    this._handlers.forEach((h) => h(this.state));
  }

  correct(matchClockSeconds: number, reason: string | null): void {
    this._auditLog.push({
      correctedAt: new Date(),
      previousMatchClockSeconds: this._matchClockSeconds,
      newMatchClockSeconds: matchClockSeconds,
      reason,
      operator: null,
    });
    this._matchClockSeconds = matchClockSeconds;
    this._handlers.forEach((h) => h(this.state));
  }

  startPeriod(period: number): void {
    this._currentPeriod = period;
    this.start();
  }

  endPeriod(_period: number): void {
    this.pause();
  }

  startHalfTime(): void {
    this.pause();
  }

  endHalfTime(): void {
    this._currentPeriod = 2;
  }

  markFullTime(): void {
    this.pause();
  }

  getAuditLog(): ClockCorrectionAuditEntry[] {
    return [...this._auditLog];
  }

  onStateChanged(handler: (state: ClockState) => void): void {
    this._handlers.push(handler);
  }
}

// ---------------------------------------------------------------------------
// MockStorageMonitor
// ---------------------------------------------------------------------------

export class MockStorageMonitor implements IStorageMonitor {
  private _warningHandlers: Array<(status: StorageStatus) => void> = [];

  readonly status: StorageStatus = {
    totalBytes: 500_000_000_000,
    usedBytes: 120_000_000_000,
    availableBytes: 380_000_000_000,
    availableMinutes: 420,
    recordingDirectory: "C:/Matches",
    warningLevel: "ok",
    isExternalStorage: false,
  };

  async check(): Promise<StorageStatus> {
    return this.status;
  }

  hasMinimumSpace(): boolean {
    return this.status.availableMinutes > 30;
  }

  onWarning(handler: (status: StorageStatus) => void): void {
    this._warningHandlers.push(handler);
  }
}

// ---------------------------------------------------------------------------
// MockHealthMonitoringService
// ---------------------------------------------------------------------------

export class MockHealthMonitoringService implements IHealthMonitoringService {
  private _warningHandlers: Array<(component: string, message: string) => void> = [];
  private _errorHandlers: Array<(component: string, message: string) => void> = [];

  readonly health: SystemHealth = {
    camera: { component: "camera", state: "unknown", message: null, checkedAt: new Date() },
    recording: { component: "recording", state: "healthy", message: null, checkedAt: new Date() },
    streaming: { component: "streaming", state: "healthy", message: null, checkedAt: new Date() },
    storage: { component: "storage", state: "healthy", message: null, checkedAt: new Date() },
    streamDeck: { component: "stream-deck", state: "unknown", message: null, checkedAt: new Date() },
    joystick: { component: "joystick", state: "unknown", message: null, checkedAt: new Date() },
    overlay: { component: "overlay", state: "healthy", message: null, checkedAt: new Date() },
    audio: { component: "audio", state: "unknown", message: null, checkedAt: new Date() },
    localApi: { component: "local-api", state: "healthy", message: null, checkedAt: new Date() },
    overallState: "degraded",
  };

  getHealth(): SystemHealth {
    return this.health;
  }

  onWarning(handler: (component: string, message: string) => void): void {
    this._warningHandlers.push(handler);
  }

  onError(handler: (component: string, message: string) => void): void {
    this._errorHandlers.push(handler);
  }
}

// ---------------------------------------------------------------------------
// Utilities
// ---------------------------------------------------------------------------

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
