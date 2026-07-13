# ADR-004: Dual Clock Model (matchClockSeconds vs recordingElapsedSeconds)

**Date:** July 2026  
**Status:** Accepted

---

## Context

Gaelic match clocks are frequently paused (half-time, injuries, delays) and may be manually corrected by the operator. The recording continues during all of these periods. An event tagged at match clock time 45:00 may correspond to recording time 52:30 if there were two delays. Without maintaining both values independently, any analysis based on either time dimension would be incorrect.

## Decision

The `MatchClockService` will maintain two independent integer counters:
- `matchClockSeconds` — the official match time, subject to pause, resume, and manual correction.
- `recordingElapsedSeconds` — continuous elapsed time from the start of the recording, never paused or corrected.

Every `MatchEvent` record will include both values. The system will never assume they are identical. No code may derive one from the other.

## Consequences

- All event tagging code must explicitly read both values from the clock service.
- The clock state API (`/api/matches/{id}/clock`) must return both values.
- WebSocket clock updates must broadcast both values.
- Exporters must include both values in all output formats.
- Analysis tools must treat the two values as independent axes.
