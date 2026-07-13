# AMHARC Match Capture — Windows Agent

The local agent that runs on your match-day Windows 11 laptop and integrates with your hardware:
- **AXIS Q6128-E** PTZ camera via VAPIX
- **Elgato Stream Deck** button controller
- **PTZ joystick** via DirectInput
- **FFmpeg** for MKV segment recording and RTMP streaming
- **SQLite** for local match/event persistence

The agent hosts the operator interface at **http://localhost:5000** — open it in any browser on the same machine.

---

## Prerequisites

| Tool | Where to get it |
|------|----------------|
| .NET 8 SDK | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Visual Studio 2022 (or VS Code + C# Dev Kit) | https://visualstudio.microsoft.com |
| Node.js 20 + pnpm | https://nodejs.org · `npm i -g pnpm` |
| FFmpeg (Windows build) | https://ffmpeg.org/download.html → `ffmpeg-release-essentials.zip` |
| Inno Setup 6 (for building the installer) | https://jrsoftware.org/isdl.php |

---

## Quick start (development)

### 1. Clone the repo

```powershell
git clone https://github.com/fishnany/amharc-match-capture.git
cd amharc-match-capture
```

### 2. Build the operator UI

```powershell
pnpm install
pnpm --filter @workspace/operator-ui run build
# Copy output to the API wwwroot so it's served at localhost:5000
New-Item -ItemType Directory -Force agent-windows\src\AmharcAgent.Api\wwwroot | Out-Null
Copy-Item -Recurse -Force artifacts\operator-ui\dist\* agent-windows\src\AmharcAgent.Api\wwwroot\
```

### 3. Place FFmpeg

Extract `ffmpeg.exe` from the downloaded zip and copy it to:

```
agent-windows\src\AmharcAgent.Api\ffmpeg.exe
```

Or install FFmpeg system-wide and add it to your `PATH` — the agent will find it automatically.

### 4. Open the solution

```powershell
cd agent-windows
start AmharcAgent.sln
```

Or open VS Code:

```powershell
code .
```

### 5. Run the agent

**Visual Studio:** Press **F5** (Debug) or **Ctrl+F5** (without debugger).

**Command line:**

```powershell
cd agent-windows
dotnet run --project src\AmharcAgent.Api
```

### 6. Open the operator interface

Navigate to **http://localhost:5000** in Chrome, Edge, or Firefox.

---

## Configuration

Edit `agent-windows\src\AmharcAgent.Api\appsettings.json`:

```json
{
  "AmharcAgent": {
    "RecordingDirectory": "C:\\AmharcRecordings",
    "FfmpegPath": "ffmpeg.exe",
    "SegmentDurationSeconds": 300,
    "DefaultCameraUsername": "root",
    "DefaultCameraPassword": "pass",
    "AutoDiscoverCameras": true,
    "OperatorName": "Your Name",
    "StreamDeckEnabled": true,
    "JoystickEnabled": true
  }
}
```

| Setting | Default | Notes |
|---------|---------|-------|
| `RecordingDirectory` | `C:\AmharcRecordings` | Where MKV segments and final MP4s are written |
| `FfmpegPath` | `ffmpeg.exe` | Full path if not on system PATH |
| `SegmentDurationSeconds` | `300` | 5-minute MKV segments — recoverable if power fails |
| `DefaultCameraUsername` | `root` | AXIS factory default |
| `DefaultCameraPassword` | `pass` | AXIS factory default |
| `AutoDiscoverCameras` | `true` | Scans local subnets on startup for AXIS cameras |
| `OperatorName` | `Operator` | Shown in the UI and stamped on events |

---

## Camera setup

### Finding your AXIS Q6128-E IP address

The camera uses DHCP, so its IP address may change. To find it:

**Option A — AXIS IP Utility (recommended)**
Download from https://www.axis.com/support/tools/axis-ip-utility. It lists all AXIS cameras on your network with their IP addresses.

**Option B — Camera discovery in the UI**
Go to **Cameras** → **Discover** in the operator interface. The agent scans your local subnet and lists any responding AXIS cameras.

**Option C — Router DHCP table**
Log in to your router and look for devices with AXIS or MAC addresses starting with `00:40:8C`.

### Adding the camera

1. In the operator interface, go to **Cameras** → **Add Camera**
2. Enter the IP address found above
3. Leave username/password as `root`/`pass` (factory defaults)
4. Click **Connect** — the camera model and serial number should appear

### Setting up PTZ presets

1. Use the PTZ joystick or the UI joystick controls to position the camera
2. Go to **Cameras** → **Presets** → **Save Preset**
3. Name it (e.g. "Kick-out End", "Sideline", "Home Goals")
4. These presets are stored on the camera itself via VAPIX

---

## Stream Deck setup

1. Plug in the Elgato Stream Deck **before** starting the agent
2. The agent auto-detects it on startup — no Elgato software required
3. Go to **Stream Deck** in the operator interface to select or create a button profile
4. Profiles are pre-loaded for Gaelic Football and Hurling with all 15 buttons mapped to event types

---

## Joystick setup

1. Plug in the PTZ joystick **before** starting the agent
2. It's detected via DirectInput on startup — any HID joystick works
3. Axes: X = pan, Y = tilt, Z (or Rz) = zoom
4. Dead zone and sensitivity are configurable in **Settings**

---

## Recording

Recordings are stored as 5-minute MKV segments in:

```
C:\AmharcRecordings\{matchId}\{date}\
```

After the match, use **Exports** → **Export Match** to:
- Remux all segments into a single MP4
- Generate an events JSON and CSV
- Write the match manifest and technical log

---

## Building the installer

See [`installer/BUILD.md`](installer/BUILD.md) for full instructions.  
The installer bundles the agent, FFmpeg, and the operator UI into a single `amharc-match-capture-setup.exe`.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| **"ffmpeg.exe not found"** | Place `ffmpeg.exe` in the application directory or add FFmpeg to your system PATH |
| **Camera not reachable** | Check the IP address (use AXIS IP Utility), confirm camera and laptop are on same network/switch |
| **Camera shows "401 Unauthorized"** | Check username/password in appsettings.json — factory default is `root`/`pass` |
| **Stream Deck not detected** | Unplug and re-plug the Stream Deck, then restart the agent; ensure no other software (Elgato Stream Deck app) has it locked |
| **Joystick not working** | Confirm the joystick appears in Windows → Settings → Bluetooth & devices → Other devices; try a different USB port |
| **Recording won't start** | Confirm camera is connected first (green status in UI), then check `RecordingDirectory` has enough space |
| **Port 5000 in use** | Change the port in appsettings.json: `"Urls": "http://localhost:5001"` |
| **SQLite locked error** | Only one instance of the agent should run at a time — check Task Manager for duplicate processes |

---

## Architecture

```
Browser (Operator Interface)
    │ http://localhost:5000
    ▼
AmharcAgent.Api (ASP.NET Core 8)
    │ serves static files (operator UI) + REST API + SignalR WebSocket
    ├── AmharcAgent.Infrastructure
    │     ├── AxisCameraAdapter ──► AXIS Q6128-E (VAPIX HTTP + RTSP)
    │     ├── FfmpegRecordingService ──► ffmpeg.exe (MKV segments)
    │     ├── RtmpStreamingService ──► ffmpeg.exe (RTMP re-stream)
    │     ├── StreamDeckService ──► Elgato Stream Deck (HID)
    │     ├── JoystickService ──► PTZ Joystick (DirectInput)
    │     ├── MatchClockService (dual clock — match + recording, always independent)
    │     ├── EventTaggingService ──► SQLite (AmharcAgent.Data)
    │     ├── StorageMonitorService
    │     ├── HealthMonitoringService
    │     └── ExportService
    └── AmharcAgent.Data (EF Core 8 + SQLite)
          └── amharc.db
```

---

## Dual clock model

Every match event is stamped with **two independent time values**:

| Value | Description |
|-------|-------------|
| `matchClockSeconds` | Official match time — pauses during half-time, correctable by operator |
| `recordingElapsedSeconds` | Continuous recording time — never pauses, not affected by corrections |

These values diverge the moment the match clock is paused or corrected. Both are always stored — never derived from each other. This lets you seek the recording to any event precisely regardless of stoppages.
