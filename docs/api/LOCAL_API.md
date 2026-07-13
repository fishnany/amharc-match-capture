# AMHARC Match Capture — Local API Reference

**Version:** 0.1.0  
**Base URL:** `http://localhost:5000/api`  
**Transport:** HTTP/1.1 + WebSocket

---

## 1. Authentication

In the initial version, the local API is accessible only from `127.0.0.1` and does not require authentication. Remote access (if enabled in settings) requires a bearer token.

---

## 2. REST Endpoints

### Health

| Method | Path | Description |
|--------|------|-------------|
| GET | `/healthz` | Health check |
| GET | `/system/status` | Full system status |

### Cameras

| Method | Path | Description |
|--------|------|-------------|
| GET | `/cameras` | List all cameras |
| POST | `/cameras` | Add a camera |
| GET | `/cameras/{cameraId}` | Get camera details |
| PUT | `/cameras/{cameraId}` | Update camera configuration |
| DELETE | `/cameras/{cameraId}` | Remove camera |
| POST | `/cameras/{cameraId}/connect` | Connect to camera |
| POST | `/cameras/{cameraId}/disconnect` | Disconnect from camera |
| POST | `/cameras/{cameraId}/test` | Test camera connection |
| POST | `/cameras/{cameraId}/ptz` | Send PTZ command |
| GET | `/cameras/{cameraId}/presets` | List PTZ presets |
| POST | `/cameras/{cameraId}/presets` | Save PTZ preset |

### Matches

| Method | Path | Description |
|--------|------|-------------|
| GET | `/matches` | List all matches |
| POST | `/matches` | Create a new match |
| GET | `/matches/{matchId}` | Get match details |
| PUT | `/matches/{matchId}` | Update match |
| POST | `/matches/{matchId}/start` | Start match |
| POST | `/matches/{matchId}/stop` | Stop match (full-time) |
| GET | `/matches/{matchId}/clock` | Get clock state |
| POST | `/matches/{matchId}/clock/start` | Start match clock |
| POST | `/matches/{matchId}/clock/pause` | Pause clock |
| POST | `/matches/{matchId}/clock/resume` | Resume clock |
| POST | `/matches/{matchId}/clock/correct` | Correct clock manually |
| GET | `/matches/{matchId}/score` | Get current score |
| PUT | `/matches/{matchId}/score` | Update score |

### Events

| Method | Path | Description |
|--------|------|-------------|
| GET | `/matches/{matchId}/events` | List match events |
| POST | `/matches/{matchId}/events` | Create an event |
| PUT | `/matches/{matchId}/events/{eventId}` | Update an event |
| DELETE | `/matches/{matchId}/events/{eventId}` | Delete an event |
| POST | `/matches/{matchId}/events/undo` | Undo last event |

### Recording

| Method | Path | Description |
|--------|------|-------------|
| POST | `/matches/{matchId}/recording/start` | Start recording |
| POST | `/matches/{matchId}/recording/stop` | Stop recording |
| GET | `/matches/{matchId}/recording/status` | Get recording status |

### Streaming

| Method | Path | Description |
|--------|------|-------------|
| POST | `/matches/{matchId}/streaming/start` | Start live stream |
| POST | `/matches/{matchId}/streaming/stop` | Stop live stream |
| GET | `/matches/{matchId}/streaming/status` | Get streaming status |
| GET | `/streaming/destinations` | List streaming destinations |
| POST | `/streaming/destinations` | Add streaming destination |

### Storage

| Method | Path | Description |
|--------|------|-------------|
| GET | `/storage/status` | Get storage status |

### Devices

| Method | Path | Description |
|--------|------|-------------|
| GET | `/devices` | List all devices |
| GET | `/devices/stream-deck` | Stream Deck status |
| GET | `/devices/joystick` | Joystick status |
| GET | `/stream-deck/profiles` | List Stream Deck profiles |
| POST | `/stream-deck/profiles` | Create profile |

### Overlays

| Method | Path | Description |
|--------|------|-------------|
| GET | `/overlays/templates` | List overlay templates |
| GET | `/overlays/state` | Get overlay state |

### Exports

| Method | Path | Description |
|--------|------|-------------|
| POST | `/matches/{matchId}/export` | Export match data |

---

## 3. WebSocket Messages

The local agent publishes real-time state updates over WebSocket at `ws://localhost:5000/ws`.

### Connection

```
ws://localhost:5000/ws
```

### Message Format

All messages follow this envelope:

```json
{
  "type": "<message-type>",
  "timestamp": "2026-07-18T14:32:00.000Z",
  "payload": { ... }
}
```

### Published Message Types

| Type | Trigger | Payload |
|------|---------|---------|
| `camera-status` | Camera connection state change | `{ cameraId, connectionState }` |
| `camera-health` | Camera health metrics update | `{ cameraId, bitRate, frameRate, droppedFrames }` |
| `recording-status` | Recording start/stop/segment rotation | `{ isRecording, elapsedSeconds, segmentCount }` |
| `recording-progress` | Every 5 seconds whilst recording | `{ elapsedSeconds, segmentCount, bitRate }` |
| `storage-status` | Storage change or warning | `{ availableBytes, availableMinutes, warningLevel }` |
| `match-clock` | Every 500 ms when clock is running | `{ matchClockSeconds, recordingElapsedSeconds, isRunning }` |
| `score-update` | Score change | `{ homeGoals, homePoints, awayGoals, awayPoints, homeTotal, awayTotal }` |
| `event-created` | New event created | Full event object |
| `event-updated` | Event updated | Full event object |
| `event-deleted` | Event deleted | `{ eventId, matchId }` |
| `stream-deck-status` | Stream Deck connected/disconnected | `{ connected, deviceName, activeProfileId }` |
| `joystick-status` | Joystick connected/disconnected | `{ connected, deviceName }` |
| `ptz-status` | PTZ state change | `{ cameraId, pan, tilt, zoom, isMoving }` |
| `overlay-status` | Overlay state change | `{ isVisible, outputMode, currentGraphic }` |
| `streaming-status` | Streaming state change | `{ isStreaming, destination, bitRate, droppedFrames }` |
| `audio-status` | Audio level update | `{ level, isMuted, isClipping }` |
| `system-warning` | Warning generated | `{ component, message, severity }` |
| `system-error` | Error generated | `{ component, message, code }` |

---

## 4. PTZ Command Reference

`POST /cameras/{cameraId}/ptz`

| `action` | Description |
|---------|-------------|
| `pan-left` | Continuous pan left at `panSpeed` |
| `pan-right` | Continuous pan right at `panSpeed` |
| `tilt-up` | Continuous tilt up at `tiltSpeed` |
| `tilt-down` | Continuous tilt down at `tiltSpeed` |
| `zoom-in` | Continuous zoom in at `zoomSpeed` |
| `zoom-out` | Continuous zoom out at `zoomSpeed` |
| `stop` | Stop all movement |
| `home` | Return to home position |
| `preset-recall` | Recall preset `presetId` |
| `preset-save` | Save current position as `presetId` with `presetName` |
| `emergency-wide` | Recall the emergency wide preset |

---

## 5. Error Responses

All endpoints return errors in the following format:

```json
{ "error": "Human-readable error message" }
```

Standard HTTP status codes:
- `400` — validation failure
- `404` — resource not found
- `409` — conflict (e.g. recording already active)
- `500` — internal server error

---

*AMHARC Match Capture — Local API Reference v0.1.0*
