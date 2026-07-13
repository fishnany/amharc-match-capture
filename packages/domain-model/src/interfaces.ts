/**
 * AMHARC Match Capture — Core Domain Interfaces
 *
 * These interfaces define the contracts between the local agent components.
 * Concrete implementations live in the adapters/ and infrastructure/ directories.
 *
 * @version 0.1.0
 */

// ---------------------------------------------------------------------------
// Camera
// ---------------------------------------------------------------------------

export type CameraConnectionState =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "error";

export interface CameraInfo {
  model: string | null;
  serialNumber: string | null;
  firmwareVersion: string | null;
  macAddress: string | null;
}

export interface StreamProfile {
  name: string;
  resolution: string | null;
  frameRate: number | null;
  codec: string | null;
  bitRate: number | null;
}

export interface ICameraAdapter {
  readonly cameraId: string;
  readonly manufacturer: string;
  readonly model: string | null;
  readonly connectionState: CameraConnectionState;

  connect(): Promise<void>;
  disconnect(): Promise<void>;
  getStreamUrl(profileName?: string): Promise<string>;
  getCameraInfo(): Promise<CameraInfo>;
  getStreamProfiles(): Promise<StreamProfile[]>;
  reconnect(): Promise<void>;

  onConnectionStateChanged(handler: (state: CameraConnectionState) => void): void;
  onHealthChanged(handler: (health: CameraHealth) => void): void;
}

export interface CameraHealth {
  bitRate: number | null;
  frameRate: number | null;
  droppedFrames: number | null;
  timestamp: Date;
}

// ---------------------------------------------------------------------------
// Stream Receiver
// ---------------------------------------------------------------------------

export interface StreamStats {
  bitRate: number;
  frameRate: number;
  droppedFrames: number;
  resolution: string;
}

export interface IStreamReceiver {
  readonly isReceiving: boolean;
  readonly stats: StreamStats | null;

  start(rtspUrl: string): Promise<void>;
  stop(): Promise<void>;
  getStats(): StreamStats | null;
  onInterrupted(handler: () => void): void;
  onResumed(handler: () => void): void;
}

// ---------------------------------------------------------------------------
// Recording Manager
// ---------------------------------------------------------------------------

export type RecordingState =
  | "idle"
  | "starting"
  | "recording"
  | "rotating"
  | "stopping"
  | "remuxing"
  | "complete"
  | "error"
  | "recovering";

export interface RecordingSegmentInfo {
  segmentNumber: number;
  filePath: string;
  startTimestamp: Date;
  endTimestamp: Date | null;
  isComplete: boolean;
  durationSeconds: number | null;
  fileSizeBytes: number | null;
}

export interface IRecordingManager {
  readonly state: RecordingState;
  readonly elapsedSeconds: number;
  readonly segmentCount: number;
  readonly recordingDirectory: string | null;

  startRecording(options: RecordingOptions): Promise<void>;
  stopRecording(): Promise<void>;
  getSegments(): RecordingSegmentInfo[];
  remuxToMp4(): Promise<string>;
  recover(): Promise<void>;
  getChecksum(filePath: string): Promise<string>;
}

export interface RecordingOptions {
  matchId: string;
  cameraId: string;
  rtspUrl: string;
  outputDirectory: string;
  segmentDurationSeconds: number;
  includeAudio: boolean;
}

// ---------------------------------------------------------------------------
// PTZ Controller
// ---------------------------------------------------------------------------

export type PtzDirection = "left" | "right" | "up" | "down";
export type ZoomDirection = "in" | "out";

export interface PtzPreset {
  presetId: string;
  name: string;
  isHome: boolean;
  description: string | null;
}

export interface IPtzController {
  pan(direction: PtzDirection, speed: number): Promise<void>;
  tilt(direction: PtzDirection, speed: number): Promise<void>;
  zoom(direction: ZoomDirection, speed: number): Promise<void>;
  moveAbsolute(pan: number, tilt: number, zoom: number): Promise<void>;
  stop(): Promise<void>;
  goHome(): Promise<void>;
  recallPreset(presetId: string): Promise<void>;
  savePreset(presetId: string, name: string): Promise<void>;
  emergencyWide(): Promise<void>;
  getPresets(): Promise<PtzPreset[]>;
}

// ---------------------------------------------------------------------------
// Joystick Service
// ---------------------------------------------------------------------------

export interface JoystickAxisState {
  pan: number;   // -1.0 to 1.0
  tilt: number;  // -1.0 to 1.0
  zoom: number;  // -1.0 to 1.0
}

export interface JoystickConfig {
  deadZone: number;
  panSensitivity: number;
  tiltSensitivity: number;
  zoomSensitivity: number;
  invertPan: boolean;
  invertTilt: boolean;
  invertZoom: boolean;
}

export interface IJoystickService {
  readonly isConnected: boolean;
  readonly deviceName: string | null;
  readonly config: JoystickConfig;

  start(): Promise<void>;
  stop(): Promise<void>;
  updateConfig(config: Partial<JoystickConfig>): void;
  onAxisChanged(handler: (axes: JoystickAxisState) => void): void;
  onButtonPressed(handler: (button: number) => void): void;
  onConnected(handler: (deviceName: string) => void): void;
  onDisconnected(handler: () => void): void;
}

// ---------------------------------------------------------------------------
// Stream Deck Service
// ---------------------------------------------------------------------------

export interface StreamDeckButtonConfig {
  buttonNumber: number;
  label: string;
  icon: string | null;
  colour: string | null;
  eventType: string;
  team: "home" | "away" | null;
  scoreEffect: string | null;
  overlayEffect: string | null;
  clipRequest: boolean;
  enabled: boolean;
}

export interface StreamDeckProfile {
  profileId: string;
  name: string;
  sport: string;
  buttons: StreamDeckButtonConfig[];
}

export interface IStreamDeckService {
  readonly isConnected: boolean;
  readonly deviceName: string | null;
  readonly activeProfileId: string | null;

  start(): Promise<void>;
  stop(): Promise<void>;
  loadProfile(profile: StreamDeckProfile): Promise<void>;
  setButtonState(buttonNumber: number, active: boolean): Promise<void>;
  setButtonLabel(buttonNumber: number, label: string): Promise<void>;
  onButtonPressed(handler: (buttonNumber: number, config: StreamDeckButtonConfig) => void): void;
  onConnected(handler: (deviceName: string) => void): void;
  onDisconnected(handler: () => void): void;
}

// ---------------------------------------------------------------------------
// Match Clock Service
// ---------------------------------------------------------------------------

export type ClockMode = "count-up" | "count-down";

export interface ClockState {
  matchClockSeconds: number;
  recordingElapsedSeconds: number;
  isRunning: boolean;
  currentPeriod: number;
  clockMode: ClockMode;
  updatedAt: Date;
}

export interface ClockCorrectionAuditEntry {
  correctedAt: Date;
  previousMatchClockSeconds: number;
  newMatchClockSeconds: number;
  reason: string | null;
  operator: string | null;
}

export interface IMatchClockService {
  readonly state: ClockState;

  start(): void;
  pause(): void;
  resume(): void;
  reset(): void;
  correct(matchClockSeconds: number, reason: string | null): void;
  startPeriod(period: number): void;
  endPeriod(period: number): void;
  startHalfTime(): void;
  endHalfTime(): void;
  markFullTime(): void;

  getAuditLog(): ClockCorrectionAuditEntry[];
  onStateChanged(handler: (state: ClockState) => void): void;
}

// ---------------------------------------------------------------------------
// Event Tagging Service
// ---------------------------------------------------------------------------

export type EventSource =
  | "operator-ui"
  | "stream-deck"
  | "joystick"
  | "system"
  | "imported"
  | "api"
  | "automatic";

export type ReviewStatus =
  | "unreviewed"
  | "reviewed"
  | "corrected"
  | "rejected"
  | "flagged";

export interface MatchEvent {
  eventId: string;
  matchId: string;
  eventType: string;
  team: "home" | "away" | null;
  playerId: string | null;
  playerNumber: number | null;
  period: number;
  matchClockSeconds: number;
  recordingElapsedSeconds: number;
  systemTimestamp: Date;
  source: EventSource;
  operator: string | null;
  note: string | null;
  scoreBefore: string | null;
  scoreAfter: string | null;
  clipRequested: boolean;
  reviewStatus: ReviewStatus;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateEventOptions {
  matchId: string;
  eventType: string;
  team?: "home" | "away";
  playerNumber?: number;
  period: number;
  matchClockSeconds: number;
  recordingElapsedSeconds: number;
  source: EventSource;
  note?: string;
  clipRequested?: boolean;
}

export interface IEventTaggingService {
  createEvent(options: CreateEventOptions): Promise<MatchEvent>;
  updateEvent(eventId: string, updates: Partial<MatchEvent>): Promise<MatchEvent>;
  deleteEvent(eventId: string): Promise<void>;
  undoLastEvent(matchId: string): Promise<MatchEvent | null>;
  getEvents(matchId: string): Promise<MatchEvent[]>;
  exportEventsJson(matchId: string): Promise<string>;
  exportEventsCsv(matchId: string): Promise<string>;
}

// ---------------------------------------------------------------------------
// Overlay Service
// ---------------------------------------------------------------------------

export type OverlayOutputMode =
  | "clean"
  | "programme"
  | "overlay-only"
  | "operator-preview";

export interface OverlayState {
  activeTemplateId: string | null;
  isVisible: boolean;
  outputMode: OverlayOutputMode;
  currentGraphic: string | null;
  graphicVisible: boolean;
}

export interface IOverlayService {
  readonly state: OverlayState;

  showScoreboard(): void;
  hideScoreboard(): void;
  showGraphic(graphicType: string, durationMs?: number): void;
  hideGraphic(): void;
  setOutputMode(mode: OverlayOutputMode): void;
  setTemplate(templateId: string): void;
  updateScore(homeGoals: number, homePoints: number, awayGoals: number, awayPoints: number): void;
  updateClock(matchClockSeconds: number, period: number): void;
}

// ---------------------------------------------------------------------------
// Streaming Service
// ---------------------------------------------------------------------------

export type StreamingState =
  | "idle"
  | "connecting"
  | "streaming"
  | "reconnecting"
  | "stopping"
  | "error";

export interface StreamingStats {
  uptimeSeconds: number;
  outgoingBitRate: number;
  droppedFrames: number;
  reconnectCount: number;
}

export interface StreamingDestinationConfig {
  destinationId: string;
  platform: string;
  serverUrl: string;
  streamKey: string;
  resolution: string | null;
  frameRate: number | null;
  bitRate: number | null;
}

export interface IStreamingService {
  readonly state: StreamingState;
  readonly stats: StreamingStats | null;

  start(destination: StreamingDestinationConfig): Promise<void>;
  stop(): Promise<void>;
  getStats(): StreamingStats | null;
  onStateChanged(handler: (state: StreamingState) => void): void;
  onError(handler: (error: Error) => void): void;
}

// ---------------------------------------------------------------------------
// Storage Monitor
// ---------------------------------------------------------------------------

export type StorageWarningLevel = "ok" | "warning" | "critical";

export interface StorageStatus {
  totalBytes: number;
  usedBytes: number;
  availableBytes: number;
  availableMinutes: number;
  recordingDirectory: string;
  warningLevel: StorageWarningLevel;
  isExternalStorage: boolean;
}

export interface IStorageMonitor {
  readonly status: StorageStatus;

  check(): Promise<StorageStatus>;
  hasMinimumSpace(): boolean;
  onWarning(handler: (status: StorageStatus) => void): void;
}

// ---------------------------------------------------------------------------
// Export Service
// ---------------------------------------------------------------------------

export interface ExportManifest {
  format: "amharc-match-capture";
  formatVersion: number;
  application: {
    name: "AMHARC Match Capture";
    version: string;
  };
  match: {
    matchId: string;
    sport: string;
    competition: string;
    season: string;
    round: string | null;
    date: string;
    venue: string | null;
    homeTeam: string;
    awayTeam: string;
    periodStructure: string;
  };
  recordings: Array<{
    recordingId: string;
    cameraId: string;
    cameraRole: string;
    file: string;
    startTimestamp: string;
    durationSeconds: number;
    checksum: string;
  }>;
  eventFile: string;
  scoreFile: string;
  technicalLog: string;
}

export interface IExportService {
  exportEventsJson(matchId: string): Promise<string>;
  exportEventsCsv(matchId: string): Promise<string>;
  generateManifest(matchId: string): Promise<ExportManifest>;
  writeManifest(matchId: string, outputPath: string): Promise<void>;
  writeTechnicalLog(matchId: string, outputPath: string): Promise<void>;
}

// ---------------------------------------------------------------------------
// Health Monitoring Service
// ---------------------------------------------------------------------------

export type ComponentHealthState = "healthy" | "degraded" | "critical" | "unknown";

export interface ComponentHealth {
  component: string;
  state: ComponentHealthState;
  message: string | null;
  checkedAt: Date;
}

export interface SystemHealth {
  camera: ComponentHealth;
  recording: ComponentHealth;
  streaming: ComponentHealth;
  storage: ComponentHealth;
  streamDeck: ComponentHealth;
  joystick: ComponentHealth;
  overlay: ComponentHealth;
  audio: ComponentHealth;
  localApi: ComponentHealth;
  overallState: ComponentHealthState;
}

export interface IHealthMonitoringService {
  readonly health: SystemHealth;

  getHealth(): SystemHealth;
  onWarning(handler: (component: string, message: string) => void): void;
  onError(handler: (component: string, message: string) => void): void;
}
