# AMHARC Match Capture — Product Requirements

**Version:** 0.1.0  
**Date:** July 2026  
**Status:** Draft

---

## 1. Product Vision

AMHARC Match Capture is a hybrid, local-first sports video capture, recording, live-production, PTZ control, event-tagging and streaming platform designed for Gaelic games (Gaelic football, hurling, ladies' football, and camogie).

The platform gives a single trained operator the ability to:

- Record full-length matches at broadcast quality from professional IP cameras
- Control camera pan, tilt and zoom using a joystick
- Tag match events in real time using a 15-button Elgato Stream Deck
- Maintain a match clock and scoreboard
- Generate broadcast-quality overlay graphics
- Live stream matches to RTMP destinations
- Export structured match data for later AMHARC analysis

The system must operate fully without Internet connectivity. Local recording, event tagging, match timing, and PTZ control must continue at all times regardless of network availability.

---

## 2. Users

### Primary User: Match Operator

A trained individual responsible for all capture operations at a match.

**Characteristics:**
- Operates alone
- Works outdoors, at night, and in broadcast commentary positions
- Uses a Windows 11 laptop connected by Ethernet to a PTZ camera
- Controls the system under time pressure during live matches
- Must not need programming skills to configure and run a match

**Goals:**
- Configure the system once before a match starts
- Record the full match without gaps
- Tag events accurately and quickly
- Keep the scoreboard and clock correct
- Export a complete match package after the match

### Secondary User: AMHARC Analyst

Accesses exported match data for performance analysis.

**Characteristics:**
- Works post-match
- Consumes JSON event files, CSV exports, and match manifests

---

## 3. Use Cases

| ID | Use Case |
|----|---------|
| UC-001 | Create a new match with all required metadata |
| UC-002 | Configure an IP camera for use with the system |
| UC-003 | Connect to a live camera feed and preview video |
| UC-004 | Start and stop local match recording |
| UC-005 | Control camera PTZ using a joystick |
| UC-006 | Tag match events using Stream Deck buttons |
| UC-007 | Manually tag events from the operator interface |
| UC-008 | Maintain the match clock and correct it manually |
| UC-009 | Maintain the match scoreboard |
| UC-010 | Manage match periods (halves, quarters) |
| UC-011 | Undo an incorrectly tagged event |
| UC-012 | Edit an event after it has been tagged |
| UC-013 | Review and approve events post-match |
| UC-014 | Generate broadcast overlay graphics |
| UC-015 | Live stream the match via RTMP |
| UC-016 | Monitor system health during a match |
| UC-017 | Export match data in JSON and CSV formats |
| UC-018 | Generate an AMHARC Match Capture Manifest |
| UC-019 | Recover from a camera disconnection |
| UC-020 | Operate without Internet connectivity |
| UC-021 | Configure the Stream Deck profile for a sport |
| UC-022 | Save and recall PTZ camera presets |
| UC-023 | Request a video clip from a specific event |
| UC-024 | Monitor available recording storage |

---

## 4. Functional Requirements

### 4.1 Match Creation

- The operator must be able to create a new match with all required fields (see Data Model).
- Supported sports: `gaelic-football`, `hurling`, `ladies-football`, `camogie`.
- Supported period structures: `halves`, `quarters`, `custom`.
- The system must generate a human-readable match identifier (e.g. `AMHARC-2026-000145`).

### 4.2 Camera Management

- The operator must be able to add, edit, remove, and test cameras.
- Camera credentials must be stored securely (not in plain text).
- Supported camera manufacturers: Axis, Canon, Panasonic, Sony, PTZOptics, BirdDog, AVer, Bolin, ONVIF-compatible, generic RTSP.
- Camera-specific behaviour must be isolated behind a `CameraAdapter` interface.
- The initial concrete implementation is the AXIS Q6128-E via VAPIX.

### 4.3 Live Preview

- The system must display a live preview of the camera feed with acceptable latency for joystick PTZ control.
- Preview latency must be sufficiently low for responsive pan, tilt and zoom operation.

### 4.4 Recording

- Start and stop recording on demand.
- Record to recoverable MKV segments (default: 2-minute or 5-minute segments).
- Record the native RTSP stream without transcoding where possible.
- Retain all original segments until final MP4 validation succeeds.
- Generate automatic file names and folder structure.
- Calculate checksum for final recordings.
- Support recovery after unexpected application shutdown.
- Recording must continue regardless of Internet connectivity.
- Recording must continue regardless of streaming or overlay failures.

### 4.5 PTZ Control

- Pan, tilt and zoom from a USB joystick.
- Variable speed, dead-zone configuration, axis inversion.
- Save and recall up to 8 named presets per camera.
- Support emergency wide view (single button/preset recall).
- PTZ lock to prevent accidental movement.

### 4.6 Stream Deck

- Detect a 15-button Elgato Stream Deck.
- Support configurable button layouts with labels, icons and colours.
- Preloaded profiles for Gaelic football and hurling.
- Create events with both `matchClockSeconds` and `recordingElapsedSeconds` on every button press.
- Support undo for the last event.

### 4.7 Match Clock

- Maintain two independent values: `matchClockSeconds` and `recordingElapsedSeconds`.
- These values must never be assumed identical.
- Support count-up and count-down modes.
- Manual correction with audit log entry.
- Period management: start, end, half-time, full-time.

### 4.8 Scoreboard

- Maintain goals and points separately for each team.
- Compute totals as `goals × 3 + points`.
- Support undo of score changes.
- Audit all score corrections.
- Automatically update overlay on score change.

### 4.9 Broadcast Overlays

- Standard scoreboard, compact scoreboard, lower thirds, goal graphic, point graphic, card graphic, substitution graphic, half-time graphic, full-time graphic, technical interruption, starting soon.
- Three output modes: clean feed (no overlays), programme feed (with overlays), overlay-only (transparent background for OBS browser source).
- Configurable templates (position, size, fonts, colours, logos, animation, duration).

### 4.10 Live Streaming

- RTMP output (YouTube, Vimeo, custom RTMP).
- Future SRT support.
- Secure stream key storage.
- Start, stop, reconnect.
- Monitor bandwidth and dropped frames.
- Local recording must continue during stream failure.

### 4.11 Export

- Export events as JSON (structured) and CSV.
- Generate AMHARC Match Capture Manifest (versioned JSON).
- Export technical log.
- All exports use versioned schemas.

---

## 5. Non-Functional Requirements

### 5.1 Reliability

- Record continuously for at least two hours without failure.
- Survive camera disconnection and reconnect automatically.
- Survive Internet loss without interrupting recording.
- Recover incomplete recordings after abnormal shutdown.
- Events must be persisted immediately on creation.

### 5.2 Performance

- Support 1080p50 from the AXIS Q6128-E.
- Support one 4K camera stream (future).
- PTZ controls must be responsive (low latency).
- Stream Deck event timestamps must be within 100 ms of the actual event.
- UI updates must appear in near real time during live operation.

### 5.3 Usability

- One trained operator must be able to run the system unassisted.
- Match setup must take less than five minutes for a configured system.
- Recording state must be visually unmistakable.
- Destructive actions must require explicit confirmation.
- Undo must be available for common tagging and score actions.

### 5.4 Accessibility

- Support keyboard navigation throughout the operator interface.
- Provide visible keyboard focus states.
- Meet WCAG 2.1 AA contrast requirements.
- Never use colour as the sole indicator of state.
- Provide all critical warnings in text form.

### 5.5 Security

- Camera credentials must not be stored in plain text.
- Streaming credentials (stream keys) must be encrypted at rest.
- The local API must be restricted to localhost or configured private interfaces.
- All inputs must be validated.
- FFmpeg arguments must be constructed from validated templates only.
- Audit log must record all clock corrections and score corrections.

### 5.6 Observability

- All server-side code must use structured JSON logging.
- Every log entry must include timestamp, severity, component, sessionId, and matchId where applicable.
- Log levels: trace, debug, information, warning, error, critical.

---

## 6. Constraints

- The production system must run on Windows 11 on the match laptop.
- The Replit-hosted environment is used only for development, testing, and the web operator interface prototype.
- The system must not require a permanent Internet connection during match operation.
- The local agent must remain operational offline.
- Cloud services are optional and must not be required for basic match capture.

---

## 7. Assumptions

- The AXIS Q6128-E is the initial reference camera; all camera-specific code is isolated behind an adapter interface.
- The Elgato Stream Deck 15-button model is available and connected via USB.
- A USB joystick compatible with Windows DirectInput is available.
- The operator has configured the system before arriving at the match venue.
- Recording storage is local NVMe SSD with at least 500 GB available.
- FFmpeg is installed on the local Windows machine.

---

## 8. Acceptance Criteria

See `docs/testing/MVP_ACCEPTANCE_TEST_PLAN.md` for the full MVP acceptance test plan.

The MVP is accepted when all 30 criteria in the acceptance test plan have been demonstrated successfully on the target hardware.

---

*AMHARC Match Capture — Product Requirements v0.1.0*
