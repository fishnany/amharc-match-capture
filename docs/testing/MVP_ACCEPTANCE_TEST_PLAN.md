# AMHARC Match Capture — MVP Acceptance Test Plan

**Version:** 0.1.0  
**Date:** July 2026

The MVP is accepted when all 30 criteria below are demonstrated on the target hardware.

| # | Criterion | Pass Condition |
|---|-----------|---------------|
| 1 | AXIS Q6128-E connects over Ethernet | Camera connection state shows "Connected" |
| 2 | Camera authenticates successfully | No authentication error; camera info retrieved |
| 3 | Live video is displayed | Live preview visible in the operator interface |
| 4 | Operator can start recording | Recording indicator becomes active; segment file created |
| 5 | Operator can stop recording | Recording indicator deactivates; elapsed time stops |
| 6 | System records for at least two continuous hours | Recording active, no gaps in segment timeline after 120 minutes |
| 7 | Resulting video plays in standard software | VLC plays the final MP4 without errors |
| 8 | Recording uses recoverable segments | MKV segments playable individually before final remux |
| 9 | Forced application closure does not invalidate all footage | All completed segments play after agent restart |
| 10 | System reconnects after temporary camera interruption | Reconnects automatically within 30 seconds of cable reconnection |
| 11 | System warns when disk space is insufficient | Warning visible in UI and System Health page |
| 12 | Local recording continues without Internet access | Recording active after Internet adapter disabled |
| 13 | Joystick can control pan, tilt and zoom | Camera moves in all three axes in response to joystick input |
| 14 | Stream Deck can create timestamped events | Event appears in Event Timeline with correct clock values |
| 15 | Every event includes match time and recording time | `matchClockSeconds` and `recordingElapsedSeconds` both present on all events |
| 16 | Score changes update the scoreboard | Score updates immediately in the UI and on the overlay |
| 17 | Period controls work | Period 1 and Period 2 events appear in the Event Timeline |
| 18 | Events can be edited | Team, player number, note, and review status can be updated |
| 19 | Events can be undone | Last event removed from timeline after undo |
| 20 | Events export to JSON | Valid JSON file produced containing all events |
| 21 | Events export to CSV | Valid CSV file produced with all event fields |
| 22 | System produces AMHARC Match Capture Manifest | JSON manifest file present with `format: "amharc-match-capture"` |
| 23 | A clean recording can be produced | Recording file without overlay graphics |
| 24 | A programme recording can be produced | Recording file with broadcast overlays composited |
| 25 | Broadcast overlay can display score and clock | Score and clock visible in overlay preview mode |
| 26 | RTMP streaming can operate while local recording continues | Stream active; recording active simultaneously |
| 27 | Stream failure does not stop local recording | Recording continues after RTMP disconnect |
| 28 | Camera credentials are not stored in plain text | No credential plain text in SQLite file or log output |
| 29 | System and technical logs are generated | Structured JSON log file present after match |
| 30 | Windows 11 setup guide is provided | `docs/deployment/WINDOWS_SETUP_GUIDE.md` exists and is complete |
