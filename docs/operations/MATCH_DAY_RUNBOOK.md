# AMHARC Match Capture — Match Day Runbook

**Version:** 0.1.0  
**Date:** July 2026

---

## Pre-Requisites

Before arriving at the match venue:
- Laptop is charged and powered on.
- Camera battery / PoE source is charged and in kit bag.
- Elgato Stream Deck is packed and USB cable is present.
- USB joystick is packed.
- At least 100 GB of free space on the recording drive.
- The AMHARC Match Capture application has been tested since the last Windows update.

---

## 1. Equipment Setup

1. Mount the camera on the telescopic mast at the required height.
2. Secure the mast and ensure it will not move in wind.
3. Connect PoE+ cable from the PoE injector or switch to the camera.
4. Connect the Ethernet cable from the PoE switch to the laptop.
5. Connect the Stream Deck to the laptop via USB.
6. Connect the joystick to the laptop via USB.
7. Connect the optional commentary microphone or audio interface.
8. Power on the PoE source.
9. Wait 90 seconds for the camera to boot.

---

## 2. Camera Setup

1. Open AMHARC Match Capture on the laptop.
2. Navigate to **Camera Setup**.
3. Select or create the camera profile for this venue.
4. Enter the camera IP address if it has changed.
5. Click **Test Connection** — the test must pass before proceeding.
6. Confirm the live preview is visible in the camera card.
7. Move the PTZ to the **Full Pitch** preset using the joystick.
8. Confirm the view covers the complete playing surface.

---

## 3. Network Setup

1. Confirm the laptop is connected by Ethernet to the camera only (not to the public Internet unless live streaming is required).
2. If live streaming: connect a separate network adapter or USB dongle to the Internet access point.
3. Navigate to **System Health** and confirm all status indicators are green (camera connected, stream deck connected, joystick connected).
4. Check storage status shows at least 90 minutes remaining.

---

## 4. Match Creation

1. Navigate to **Match Setup**.
2. Complete all required fields:
   - Sport
   - Competition
   - Season, Round
   - Date
   - Venue
   - Home team, away team (full names and short codes)
   - Team colours
   - Operator name
   - Scheduled start time
   - Period structure (halves / quarters / custom)
   - Camera
   - Stream Deck profile (Gaelic football or hurling)
   - Overlay template
   - Streaming destination (if applicable)
   - Recording directory
3. Click **Create Match**.
4. Confirm the match summary is correct.
5. Navigate to **Live Capture** to confirm all status indicators are live.

---

## 5. Pre-Match Checks

Run the following checks 15 minutes before kick-off:

| Check | Expected Result |
|-------|----------------|
| Camera connected | Green indicator |
| Live preview visible | Video feed in preview area |
| PTZ responds to joystick | Camera moves correctly |
| Stream Deck detected | Green indicator, buttons lit |
| All 15 buttons labelled correctly | Correct profile visible |
| Storage remaining | At least 90 minutes |
| Overlay template selected | Template name shown |
| Match clock at 00:00 | Clock shows 00:00, not running |
| Score at 0-0 / 0-0 | Both scores are zero |

---

## 6. Recording Checks

1. Click **Start Recording**.
2. Confirm the recording indicator turns **AMHARC Green** and pulses.
3. Confirm **Elapsed Recording Time** begins incrementing.
4. Confirm **Segment Count** shows 1.
5. Confirm **Storage Remaining** begins decreasing.
6. Confirm a segment file has been created in the recording directory.

---

## 7. Stream Checks (if applicable)

1. Navigate to **Streaming Setup** and confirm the destination is configured.
2. Click **Start Stream**.
3. Confirm the stream indicator turns green.
4. Open the streaming destination in a browser or the platform app and confirm the live feed is visible.
5. Monitor bandwidth and dropped-frame counters for 60 seconds.
6. Return to **Live Capture**.

---

## 8. Match Workflow

### Kick-off

1. Press **Period 1 Start** on the Stream Deck (Button 15) or click **Start Period** in the Operator Interface.
2. Start the match clock immediately.
3. Confirm the match clock begins counting up in AMHARC Lime.

### During play

- Use the joystick to follow the ball with pan, tilt and zoom.
- Press Stream Deck buttons for each event as it happens.
- If a score is tagging incorrectly, press **Undo** (if configured) or correct it in the Event Timeline within 30 seconds.
- Monitor the storage remaining indicator. If it falls below the warning threshold, note it for the half-time check.

### Half-time

1. Press **Period 1 End** on the Stream Deck or click **End Period** in the Operator Interface.
2. Pause the match clock.
3. Confirm the half-time score is correct.
4. Navigate to **Event Timeline** and review any flagged events.
5. Check storage and streaming health.
6. Return to **Live Capture** for the second half.

### Second half

1. Press **Period 2 Start** or click **Start Period**.
2. Resume the match clock.
3. Continue tagging events.

### Full-time

1. Press **Period 2 End** or click **End Period**.
2. Stop the match clock.
3. Click **Stop Recording**.
4. Confirm the recording indicator turns off.
5. Note the final elapsed recording time.

---

## 9. Post-Match Checks

1. Confirm the final score is correct in **Match Detail**.
2. Navigate to **Event Timeline** and review all flagged events.
3. Correct any tagging errors before export.
4. Navigate to **Exports** and export:
   - JSON events
   - CSV events
   - AMHARC Match Capture Manifest
   - Technical log
5. Confirm all export files are present in the recording directory.
6. If live streaming was active, confirm the stream has stopped.

---

## 10. Export

1. Navigate to **Exports**.
2. Select all export formats (JSON, CSV, Manifest, Technical Log).
3. Click **Export Match Package**.
4. Confirm the export summary shows the expected file count.
5. Copy or upload the export package as required by AMHARC.

---

## 11. Shutdown

1. Close the AMHARC Match Capture application.
2. Disconnect the Stream Deck.
3. Disconnect the joystick.
4. Remove the Ethernet cable from the laptop.
5. Disconnect the PoE cable from the camera.
6. Allow the camera to power down.
7. Disassemble the mast and pack equipment.

---

## 12. Troubleshooting

### Camera disconnects during recording

1. Do not stop recording — recording will continue in standby mode.
2. Check the Ethernet cable connection.
3. Check the PoE source has power.
4. The agent will attempt automatic reconnect every 10 seconds.
5. When reconnected, recording resumes in a new segment.

### Recording stops unexpectedly

1. Check the storage remaining — the recording may have stopped due to insufficient space.
2. Free up space if possible and restart recording.
3. All completed segments up to the failure point are valid.

### Stream Deck not detected

1. Disconnect and reconnect the USB cable.
2. Confirm the Stream Deck driver is installed.
3. Navigate to **System Health** and click **Rescan Devices**.

### Match clock is incorrect

1. Navigate to **Live Capture** and click the clock to open the correction dialog.
2. Enter the correct time.
3. Provide a reason (required for audit log).
4. Click **Apply Correction**.

---

*AMHARC Match Capture — Match Day Runbook v0.1.0*
