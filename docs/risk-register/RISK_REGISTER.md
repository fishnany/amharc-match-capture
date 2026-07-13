# AMHARC Match Capture — Risk Register

**Version:** 0.1.0  
**Date:** July 2026

| ID | Risk | Likelihood | Impact | Mitigation |
|----|------|------------|--------|-----------|
| R-001 | RTSP preview latency too high for responsive PTZ control | Medium | High | Use LibVLC or FFmpeg with low-latency flags; test at ground before match |
| R-002 | VAPIX PTZ commands delayed by network congestion | Low | Medium | Use isolated Ethernet segment; monitor round-trip time |
| R-003 | Camera not compatible with RTSP or ONVIF | Low | High | Test in advance; maintain GenericRtspAdapter as fallback |
| R-004 | Elgato Stream Deck SDK licensing restrictions | Medium | High | Evaluate SDK licence; alternative: raw HID via Windows API |
| R-005 | USB joystick not recognised by Windows DirectInput | Low | Medium | Test in advance; provide manual PTZ control in operator UI as fallback |
| R-006 | FFmpeg distribution licensing requirements | Medium | Medium | FFmpeg may be LGPL or GPL depending on build; document licence in NOTICE.md |
| R-007 | H.265/HEVC codec licensing | Medium | Medium | Use H.264 by default; document codec options and licence implications |
| R-008 | Windows Defender Firewall blocking local API port | Medium | High | Document firewall rule configuration in setup guide |
| R-009 | Camera credentials accidentally logged | Low | Critical | Structured logger with redaction rules; CI lint for credential field names |
| R-010 | PoE injector fails during match | Low | Critical | Carry spare PoE injector; document contingency in runbook |
| R-011 | Ethernet cable damaged during match | Low | High | Carry spare cables; agent reconnects automatically when cable restored |
| R-012 | Laptop thermal throttling under sustained 4K recording | Medium | Medium | Test recording at full bitrate for 2+ hours; monitor CPU temperature |
| R-013 | NVMe SSD capacity exhausted mid-match | Low | Critical | Pre-match storage check; warning at 90 and 30 minutes remaining |
| R-014 | External SSD disconnects during recording | Low | High | MKV segments ensure prior footage is recoverable; warning on device removal |
| R-015 | External SSD write speed insufficient for 4K bitrate | Medium | High | Benchmark SSD before match; NVMe recommended |
| R-016 | Internet bandwidth insufficient for 1080p RTMP stream | Medium | Medium | Bandwidth test before match; fallback to 720p or disable streaming |
| R-017 | YouTube or Vimeo stream key rejected | Low | Medium | Validate stream key in Streaming Setup before match |
| R-018 | Overlay rendering degrades recording performance | Medium | Medium | Overlay runs as a separate process; clean recording is independent |
| R-019 | Multi-camera synchronisation offset exceeds 1 second | Medium | High | Phase 7 only; use PTP or NTP time synchronisation between cameras |
| R-020 | Audio latency exceeds acceptable threshold for commentary | Medium | Medium | Configure audio delay offset in settings; test before going live |
| R-021 | Application crash during recording | Low | High | MKV segments ensure partial recovery; recovery flow documented |
| R-022 | Power loss during recording | Very Low | High | Segments before power loss are recoverable; UPS recommended for commentary position |
| R-023 | MKV file corruption due to simultaneous read/write | Very Low | Medium | FFmpeg writes to a single segment at a time; reads only on completed segments |
| R-024 | Wind or mast vibration degrading video quality | Medium | Medium | Use emergency wide view (lower zoom) in windy conditions |
| R-025 | Rain on camera optics | Low | Medium | AXIS Q6128-E is rated IP66; clean lens before match |
| R-026 | Operator presses wrong Stream Deck button | Medium | Low | Undo available; correct in Event Timeline post-match |
| R-027 | Score correction error not detected until post-match | Medium | Medium | Score changes require confirmation dialog; audit log tracks all corrections |
| R-028 | Match clock correction not logged | Very Low | Medium | All corrections written to audit log; ADR-004 enforces dual clock model |
| R-029 | Long-term schema incompatibility in AMHARC export format | Medium | Medium | Use versioned export schema (`formatVersion`); maintain migration support |
| R-030 | Replit-hosted development environment used in production | Very Low | Critical | Clear documentation that Replit is for development only; ADR-001 enforces local-first |
