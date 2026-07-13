---
name: AMHARC Phase 0 Architecture
description: Key architectural constraints and decisions for AMHARC Match Capture that must persist across sessions
---

## Core Constraints

**Production target is Windows 11, not Replit.** The Replit environment hosts the operator interface prototype and mock API only. The real system runs C#/ASP.NET Core on a Windows 11 laptop.

**Local-first is an absolute constraint.** Recording, event tagging, PTZ, match clock, scoreboard, and local SQLite persistence must work without Internet. Streaming, cloud sync, and remote admin are optional.

**Dual clock model — never violate this.** Every MatchEvent must carry both `matchClockSeconds` (official match time, can be paused/corrected) and `recordingElapsedSeconds` (continuous recording time, never paused). These must never be assumed equal. See ADR-004.

**Why:** Match clocks are paused at half-time and corrected manually. If only one time value is stored, post-match analysis on either axis is corrupted.

**How to apply:** Any API endpoint, event creation code, or export that handles timing must include both fields. Never derive one from the other.

## Camera Adapter Pattern

All camera-specific code lives in adapter classes implementing `ICameraAdapter` and `IPtzController`. Production has AxisCameraAdapter (VAPIX). Development/Replit uses MockCameraAdapter. Never reference camera-specific APIs outside adapters.

## Score Format

Gaelic games score = goals-points (e.g. "1-12"). Total = goals × 3 + points. HomeTotal and AwayTotal are always recomputed from goals and points.

## Brand

Black `#000000`, AMHARC Green `#1C8551`, AMHARC Lime `#B6DC46`, White `#FFFFFF`. No other colours without functional justification. Lime = live/active; Green = connected/recording; Red/amber = errors only.
