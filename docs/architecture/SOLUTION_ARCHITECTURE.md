# AMHARC Match Capture — Solution Architecture

**Version:** 0.1.0  
**Date:** July 2026

---

## 1. System Context

AMHARC Match Capture operates as a hybrid system with three runtime environments.

```mermaid
graph TB
    subgraph MatchSite["Match Site (No Internet Required)"]
        Camera["AXIS Q6128-E\nPTZ Camera\n(RTSP/VAPIX/ONVIF)"]
        PoE["PoE+ Switch\nor Injector"]
        Laptop["Windows 11 Laptop\n(Local Agent + Operator UI)"]
        StreamDeck["Elgato Stream Deck\n15 Buttons"]
        Joystick["USB Joystick\nor Axis Controller"]
        SSD["Local NVMe SSD\n(Recording Storage)"]
    end

    subgraph LocalAgent["Local Windows Capture Agent"]
        CameraAdapter["Camera Adapter\n(RTSP / VAPIX)"]
        RecordingMgr["Recording Manager\n(FFmpeg / MKV)"]
        PtzCtrl["PTZ Controller\n(VAPIX)"]
        MatchClock["Match Clock Service"]
        EventTagging["Event Tagging Service"]
        OverlayService["Overlay Service"]
        StreamingService["Streaming Service\n(RTMP)"]
        StreamDeckSvc["Stream Deck Service\n(HID)"]
        JoystickSvc["Joystick Service\n(DirectInput)"]
        HealthMonitor["Health Monitor"]
        LocalDB["SQLite Database"]
        LocalAPI["Local HTTP + WebSocket API\n(ASP.NET Core)"]
    end

    subgraph OperatorUI["Web Operator Interface\n(Browser on Laptop)"]
        Dashboard["Dashboard"]
        LiveCapture["Live Capture"]
        EventTimeline["Event Timeline"]
        MatchSetup["Match Setup"]
    end

    subgraph CloudOptional["Cloud Services (Optional)"]
        CloudSync["Metadata Sync"]
        RemoteAdmin["Remote Administration"]
    end

    Camera -- "RTSP + VAPIX" --> CameraAdapter
    PoE --> Camera
    StreamDeck -- "USB HID" --> StreamDeckSvc
    Joystick -- "USB DirectInput" --> JoystickSvc
    CameraAdapter --> RecordingMgr
    CameraAdapter --> OverlayService
    RecordingMgr --> SSD
    LocalAPI <-- "HTTP / WebSocket\nlocalhost" --> OperatorUI
    LocalAgent --> CloudOptional
    OperatorUI --> Laptop
```

---

## 2. Component Architecture

The local agent is structured as a set of loosely coupled services communicating through well-defined interfaces.

```mermaid
graph LR
    subgraph Adapters
        AxisAdapter["AxisCameraAdapter\n(VAPIX)"]
        OnvifAdapter["OnvifAdapter\n(Future)"]
        GenericRtsp["GenericRtspAdapter\n(Future)"]
    end

    subgraph Core
        CameraIface["ICameraAdapter"]
        PtzIface["IPtzController"]
        StreamReceiver["IStreamReceiver"]
        RecordingMgr["IRecordingManager"]
        MatchClock["IMatchClockService"]
        EventTagging["IEventTaggingService"]
        OverlaySvc["IOverlayService"]
        StreamingSvc["IStreamingService"]
        StorageMonitor["IStorageMonitor"]
        ExportSvc["IExportService"]
        HealthMonitor["IHealthMonitoringService"]
        StreamDeckSvc["IStreamDeckService"]
        JoystickSvc["IJoystickService"]
    end

    AxisAdapter -.->|implements| CameraIface
    AxisAdapter -.->|implements| PtzIface
    OnvifAdapter -.->|implements| CameraIface
    OnvifAdapter -.->|implements| PtzIface
    GenericRtsp -.->|implements| CameraIface

    CameraIface --> StreamReceiver
    StreamReceiver --> RecordingMgr
    StreamReceiver --> OverlaySvc
    StreamingSvc --> OverlaySvc
    StreamDeckSvc --> EventTagging
    JoystickSvc --> PtzIface
    MatchClock --> EventTagging
    ExportSvc --> EventTagging
    ExportSvc --> RecordingMgr
    HealthMonitor --> CameraIface
    HealthMonitor --> RecordingMgr
    HealthMonitor --> StreamingSvc
    HealthMonitor --> StorageMonitor
```

---

## 3. Runtime Architecture

The system runs four main processes on the Windows laptop.

| Process | Technology | Port | Role |
|---------|------------|------|------|
| Local Agent | C# / ASP.NET Core | 5000 (localhost) | Core hardware integration, recording, events, API |
| Operator Interface | React / Vite | Browser | Operator control surface |
| Overlay Renderer | React / Vite | 5001 (localhost) | OBS browser source for broadcast overlays |
| SQLite DB | SQLite | File | Persistent local data store |

---

## 4. Media Flow

```mermaid
graph LR
    Cam["Camera\n(RTSP)"]
    SR["Stream Receiver\n(FFmpeg / LibVLC)"]
    CR["Clean Recording\n(MKV segments)"]
    Preview["Live Preview\n(Low-latency)"]
    Overlay["Overlay Service\n(HTML/Canvas)"]
    PR["Programme Recording\n(MKV segments)"]
    RTMP["Streaming Service\n(RTMP output)"]
    SSD["Local SSD"]

    Cam --> SR
    SR --> CR
    SR --> Preview
    SR --> Overlay
    Overlay --> PR
    PR --> RTMP
    CR --> SSD
    PR --> SSD
```

**Key principles:**
- The clean recording uses stream copy (no re-encoding) to preserve the native bitstream.
- The programme recording adds overlay graphics; hardware encoding is used where supported.
- Recording is independent of streaming. Streaming failure never stops recording.

---

## 5. Data Flow

```mermaid
sequenceDiagram
    participant Op as Operator
    participant SD as Stream Deck
    participant UI as Operator Interface
    participant API as Local API
    participant ES as Event Tagging Service
    participant DB as SQLite

    Op->>SD: Presses button (e.g. Goal)
    SD->>API: POST /api/matches/{id}/events
    API->>ES: CreateEvent(matchId, eventType, clockState)
    ES->>DB: INSERT event (matchClockSeconds + recordingElapsedSeconds)
    DB-->>ES: OK
    ES-->>API: Event created
    API-->>UI: WebSocket push (event-created)
    UI-->>Op: Event appears in timeline

    Op->>UI: Reviews event in timeline
    Op->>UI: Edits player number
    UI->>API: PUT /api/matches/{id}/events/{eventId}
    API->>ES: UpdateEvent
    ES->>DB: UPDATE event
    DB-->>ES: OK
    API-->>UI: Updated event
```

---

## 6. Failure Behaviour

| Failure | Recording | Streaming | Clock | Events |
|---------|-----------|-----------|-------|--------|
| Internet loss | ✅ Continues | ❌ Stops (reconnects) | ✅ Continues | ✅ Continues |
| Streaming failure | ✅ Continues | ❌ Stops (reconnects) | ✅ Continues | ✅ Continues |
| Overlay failure | ✅ Continues | ⚠️ Clean feed only | ✅ Continues | ✅ Continues |
| Application crash | ⚠️ Segments preserved | ❌ Stops | — | ⚠️ Last events may be lost |
| Camera disconnect | ⚠️ Segment closes | ❌ Stops | ✅ Continues | ✅ Continues |
| Storage full | ❌ Cannot start new segment | — | ✅ Continues | ✅ Continues |
| Power loss | ⚠️ MKV recoverable | ❌ Stops | — | ⚠️ Committed events preserved |

---

## 7. Security Boundaries

- The local API listens only on `127.0.0.1:5000` by default.
- Camera credentials are stored using Windows Credential Manager (DPAPI encryption).
- Stream keys are encrypted using AES-256 before storage in SQLite.
- No camera credentials are ever written to logs.
- The overlay renderer is served locally; it is not exposed externally.
- Remote administration (if enabled) requires authenticated HTTPS with JWT.

---

## 8. Local-First Design

The system treats Internet connectivity as entirely optional.

**Core match operations that must work without Internet:**
- Camera connection and RTSP stream
- PTZ control (VAPIX over local Ethernet)
- Recording to local SSD
- Match clock and scoreboard
- Stream Deck event tagging
- Broadcast overlays
- SQLite event persistence
- Export to local filesystem

**Operations that require Internet (optional):**
- RTMP live streaming
- Cloud metadata synchronisation
- Remote administration

---

*AMHARC Match Capture — Solution Architecture v0.1.0*
