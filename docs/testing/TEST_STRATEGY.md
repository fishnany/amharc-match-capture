# AMHARC Match Capture — Test Strategy

**Version:** 0.1.0  
**Date:** July 2026

---

## 1. Testing Levels

### 1.1 Unit Tests

**Scope:** Individual classes and functions tested in isolation with mocked dependencies.

**Coverage targets:**
- All domain logic in `packages/domain-model/`
- All event schema validation in `packages/event-schema/`
- Match clock time calculations (count-up, count-down, correction)
- Score computation (goals × 3 + points)
- File naming convention generation
- Export schema serialisation

**Tools:** xUnit (C#), Vitest (TypeScript)

**Examples:**
- `MatchClockService.Pause()` correctly stops elapsed time accumulation
- `MatchClockService.Correct()` records a correction audit entry
- Score `homeTotal` is recalculated correctly after a goal correction
- `matchClockSeconds` and `recordingElapsedSeconds` are never set equal to each other automatically

### 1.2 Integration Tests

**Scope:** Multiple components working together with real or in-memory dependencies.

**Coverage targets:**
- Camera adapter connecting to a mock RTSP server
- Recording manager creating, rotating, and closing MKV segments
- Event creation via the local API, verified in SQLite
- Score update reflected in overlay state
- Stream Deck button press creating an event with correct timestamps
- Export service producing valid JSON and CSV from test events

**Tools:** xUnit, TestContainers (for SQLite in-memory), local mock RTSP source

### 1.3 Hardware Integration Tests

**Scope:** Real hardware must be available. Requires the AXIS Q6128-E and a Windows 11 machine.

**Tests:**
- Camera connects via RTSP and authenticates successfully
- Live preview displays without noticeable latency
- PTZ pan/tilt/zoom responds to joystick axes
- Presets save and recall correctly
- Recording starts and a valid MKV segment is created
- Recording continues after a simulated cable pull and camera reconnect

**Note:** These tests cannot run in Replit.

### 1.4 Full-Match Endurance Tests

**Scope:** System must record without failure for at least two continuous hours.

**Procedure:**
1. Configure an AXIS Q6128-E with a test RTSP source.
2. Create a test match and start recording.
3. Tag 50+ events over 70 minutes of simulated match time.
4. Monitor recording status, segment rotation, and storage consumption.
5. Stop recording after 70 minutes.
6. Validate final MP4 plays correctly in VLC.
7. Verify all 50+ events are present in the export.

**Pass criteria:** No recording gaps, no lost events, valid final MP4.

### 1.5 Recovery Tests

**Scenario A — Application crash during recording:**
1. Start recording a match.
2. Force-kill the local agent process.
3. Restart the local agent.
4. Verify that all completed MKV segments play correctly.
5. Verify that the final (open) segment is recoverable.

**Scenario B — Camera disconnection:**
1. Start recording a match.
2. Disconnect the camera Ethernet cable.
3. Verify that the agent enters the `Reconnecting` state.
4. Reconnect the cable.
5. Verify that the agent reconnects and resumes recording in a new segment.
6. Verify that all segments before the disconnection are valid.

**Scenario C — Power loss:**
1. Start recording a match.
2. Forcibly cut power to the laptop.
3. Boot and restart the agent.
4. Verify that segments before the power loss are recoverable.

### 1.6 Network Interruption Tests

**Scope:** Confirm that recording and event tagging continue when Internet connectivity is lost.

**Procedure:**
1. Configure live streaming to a test RTMP destination.
2. Start recording and streaming.
3. Disable the Internet interface (keep the camera Ethernet active).
4. Verify that recording continues.
5. Verify that event tagging continues.
6. Verify that the clock continues running.
7. Restore the Internet connection.
8. Verify that streaming reconnects automatically.

**Pass criteria:** Recording, clock, and events unaffected by Internet loss.

### 1.7 Storage Tests

**Scope:** Storage monitor detects low-space conditions and prevents new segments.

**Tests:**
- Warning emitted at the 90-minute-remaining threshold.
- Warning emitted at the 30-minute-remaining threshold.
- Recording refuses to start a new segment when below minimum threshold.
- Existing segment is not abandoned; it closes cleanly.

### 1.8 Stream Deck Tests

**Scope:** Requires physical Stream Deck hardware on Windows.

**Tests:**
- Stream Deck detected on USB connection.
- Gaelic football profile loads correctly.
- Each of the 15 buttons creates a correctly timestamped event.
- Undo removes the last event from the event log.
- Button colours and labels update to reflect active period state.

### 1.9 Joystick Tests

**Scope:** Requires USB joystick hardware on Windows.

**Tests:**
- Joystick detected on USB connection.
- Pan axis maps to pan-left/pan-right commands.
- Tilt axis maps to tilt-up/tilt-down commands.
- Zoom axis maps to zoom-in/zoom-out commands.
- Dead zone configuration prevents drift.
- Axis inversion toggle reverses movement direction.

### 1.10 Streaming Tests

**Tests:**
- RTMP connection established to test destination.
- Bandwidth and dropped-frame counters are live.
- Stream disconnects gracefully when `StopStreaming()` is called.
- Stream reconnects automatically after a simulated network drop.
- Local recording continues during stream failure.

---

## 2. Test Data

Sample test data is provided in `samples/` for all tests that do not require hardware:

- `samples/sample-matches/gaelic-football-match.json` — Gaelic football match fixture
- `samples/sample-matches/hurling-match.json` — Hurling match fixture
- `samples/sample-events/gaelic-football-events.json` — 50 Gaelic football events
- `samples/sample-events/hurling-events.json` — 50 hurling events
- `samples/mock-streams/` — Test RTSP stream configuration using FFmpeg

---

## 3. Acceptance Test Plan

See `docs/testing/MVP_ACCEPTANCE_TEST_PLAN.md` for the 30-criteria MVP acceptance test plan.

---

*AMHARC Match Capture — Test Strategy v0.1.0*
